using System.Collections.Concurrent;
using System.Text.Json;
using AiChat.Api.Domain;

namespace AiChat.Api.Infrastructure;

/// <summary>
/// Єдиний клас, який торкається диска. Дані кожного користувача лежать в окремій папці:
/// розмова — data/chats/&lt;userId&gt;/&lt;chatId&gt;.json, фото — data/uploads/&lt;userId&gt;/&lt;guid&gt;.&lt;ext&gt;.
/// Тому кожен метод приймає userId: прочитати чи змінити чужу розмову неможливо в принципі —
/// шлях до файлу будується з id того, хто увійшов, а не з того, що прийшло в запиті.
/// Зареєстрований як singleton, тому блокування спільні для всіх запитів.
/// </summary>
public class ChatStorage
{
    private readonly string _chatsDirectory;
    private readonly string _uploadsDirectory;
    private readonly ILogger<ChatStorage> _logger;

    // Один семафор на розмову: читання-зміна-запис однієї розмови йдуть по черзі,
    // а різні розмови не заважають одна одній. id розмов — Guid, тож між користувачами вони не перетинаються.
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public ChatStorage(IConfiguration configuration, IHostEnvironment environment, ILogger<ChatStorage> logger)
    {
        _logger = logger;

        var dataDir = DataPaths.GetDataDirectory(configuration, environment);
        _chatsDirectory = Path.Combine(dataDir, "chats");
        _uploadsDirectory = Path.Combine(dataDir, "uploads");

        Directory.CreateDirectory(_chatsDirectory);
        Directory.CreateDirectory(_uploadsDirectory);
    }

    /// <summary>
    /// id розмови приходить з URL і стає частиною шляху до файлу. Приймаємо лише 32 hex-символи
    /// (Guid у форматі "N"), інакше "..\..\щось" могло б вказати за межі data/chats.
    /// </summary>
    public static bool IsValidId(string? id) =>
        id is { Length: 32 } && Guid.TryParseExact(id, "N", out _);

    /// <summary>id користувача від ASP.NET Identity — Guid у форматі "D" (з дефісами). Теж стає частиною шляху.</summary>
    public static bool IsValidUserId(string? userId) =>
        userId is { Length: 36 } && Guid.TryParseExact(userId, "D", out _);

    public async Task<ChatSession?> GetChatAsync(string userId, string id)
    {
        if (!IsValidUserId(userId) || !IsValidId(id)) return null;
        return await ReadChatFileAsync(ChatPath(userId, id));
    }

    public async Task<List<ChatSession>> GetAllChatsAsync(string userId)
    {
        var chats = new List<ChatSession>();
        var directory = UserChatsDirectory(userId);
        if (!Directory.Exists(directory)) return chats;

        foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
        {
            // Один пошкоджений файл не повинен ламати весь список (раніше — 500 на GET /api/chats)
            var chat = await ReadChatFileAsync(file);
            if (chat != null) chats.Add(chat);
        }

        return chats.OrderByDescending(c => c.UpdatedAt).ToList();
    }

    /// <summary>Зберігає нову розмову. Власник — <paramref name="userId"/>, незалежно від того, що записано в chat.</summary>
    public async Task SaveChatAsync(string userId, ChatSession chat)
    {
        if (!IsValidUserId(userId)) throw new ArgumentException($"Invalid user id '{userId}'", nameof(userId));
        if (!IsValidId(chat.Id)) throw new ArgumentException($"Invalid chat id '{chat.Id}'", nameof(chat));

        chat.OwnerId = userId;

        var gate = GetLock(chat.Id);
        await gate.WaitAsync();
        try
        {
            await WriteChatFileAsync(userId, chat);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Атомарно змінює розмову: під замком читає свіжу версію з диска, викликає <paramref name="mutate"/>
    /// і записує результат. Так два запити до однієї розмови (повідомлення + перейменування,
    /// відповідь агента + видалення) не перезапишуть зміни один одного.
    /// Повертає null, якщо в цього користувача такої розмови немає.
    /// </summary>
    public async Task<ChatSession?> UpdateChatAsync(string userId, string id, Action<ChatSession> mutate)
    {
        if (!IsValidUserId(userId) || !IsValidId(id)) return null;

        var gate = GetLock(id);
        await gate.WaitAsync();
        try
        {
            var chat = await ReadChatFileAsync(ChatPath(userId, id));
            if (chat == null) return null;

            mutate(chat);
            await WriteChatFileAsync(userId, chat);
            return chat;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Видаляє розмову і всі її фото. false — у цього користувача такої розмови не було.</summary>
    public async Task<bool> DeleteChatAsync(string userId, string id)
    {
        if (!IsValidUserId(userId) || !IsValidId(id)) return false;

        var gate = GetLock(id);
        await gate.WaitAsync();
        try
        {
            var path = ChatPath(userId, id);
            var chat = await ReadChatFileAsync(path);
            if (chat == null && !File.Exists(path)) return false;

            File.Delete(path);

            if (chat != null)
            {
                DeleteImages(userId, chat.Messages.SelectMany(m => m.Images));
            }

            return true;
        }
        finally
        {
            gate.Release();
            // Семафор більше не потрібен — прибираємо, щоб словник не ріс вічно
            _locks.TryRemove(id, out _);
        }
    }

    /// <summary>Зберігає вже перевірені фото (див. ImageValidator) під випадковими іменами в папку користувача.</summary>
    public async Task<List<string>> SaveImagesAsync(string userId, IEnumerable<ValidatedImage> images,
        CancellationToken ct = default)
    {
        if (!IsValidUserId(userId)) throw new ArgumentException($"Invalid user id '{userId}'", nameof(userId));

        var imageUrls = new List<string>();
        var directory = Directory.CreateDirectory(UserUploadsDirectory(userId)).FullName;

        try
        {
            foreach (var image in images)
            {
                // Розширення обираємо самі за вмістом файлу, а не беремо з імені від клієнта:
                // інакше x.html збережеться як .html і /uploads віддасть його як сторінку нашого сайту.
                var fileName = $"{Guid.NewGuid():N}{image.Extension}";
                var filePath = Path.Combine(directory, fileName);

                await using var stream = new FileStream(filePath, FileMode.CreateNew);
                await image.File.CopyToAsync(stream, ct);

                // URL без userId: адреса фото не розкриває, чиє воно. Власника визначає cookie того, хто просить.
                imageUrls.Add($"/uploads/{fileName}");
            }
        }
        catch
        {
            // Не лишаємо на диску половину фото з невдалого запиту
            DeleteImages(userId, imageUrls);
            throw;
        }

        return imageUrls;
    }

    public void DeleteImages(string userId, IEnumerable<string> urls)
    {
        foreach (var url in urls)
        {
            if (!TryGetUploadPath(userId, url, out var path)) continue;

            try
            {
                File.Delete(path);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Could not delete upload {Path}", path);
            }
        }
    }

    /// <summary>
    /// Перетворює URL фото ("/uploads/abc.png") на шлях у папці цього користувача.
    /// Чуже фото так не знайти: воно лежить в іншій папці. Знадобиться й AI-частині (Base64 для моделі).
    /// </summary>
    public bool TryGetUploadPath(string userId, string url, out string path)
    {
        path = string.Empty;
        if (!IsValidUserId(userId)) return false;

        const string prefix = "/uploads/";
        if (!url.StartsWith(prefix, StringComparison.Ordinal)) return false;

        var fileName = url[prefix.Length..];
        // Лише ім'я файлу, без жодних каталогів — захист від "/uploads/../chats/x.json"
        if (fileName.Length == 0 || fileName != Path.GetFileName(fileName) || fileName.Contains('\\')) return false;

        path = Path.Combine(UserUploadsDirectory(userId), fileName);
        return true;
    }

    private string UserChatsDirectory(string userId) => Path.Combine(_chatsDirectory, userId);

    private string UserUploadsDirectory(string userId) => Path.Combine(_uploadsDirectory, userId);

    private string ChatPath(string userId, string id) => Path.Combine(UserChatsDirectory(userId), $"{id}.json");

    private SemaphoreSlim GetLock(string id) => _locks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));

    private async Task<ChatSession?> ReadChatFileAsync(string path)
    {
        try
        {
            // FileShare.ReadWrite | Delete: на Windows інакше паралельний запис (File.Move) упав би,
            // поки хтось читає файл
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, bufferSize: 4096, useAsync: true);
            return await JsonSerializer.DeserializeAsync<ChatSession>(stream, _jsonOptions);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            // DirectoryNotFound — користувач ще не створив жодної розмови
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Chat file {Path} is damaged and was skipped", path);
            return null;
        }
    }

    private async Task WriteChatFileAsync(string userId, ChatSession chat)
    {
        Directory.CreateDirectory(UserChatsDirectory(userId));

        // Спершу пишемо у тимчасовий файл, потім підміняємо: якщо процес впаде посеред запису,
        // старий файл лишиться цілим, а не обрізаним JSON
        var path = ChatPath(userId, chat.Id);
        var tempPath = path + ".tmp";

        await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None,
                         bufferSize: 4096, useAsync: true))
        {
            await JsonSerializer.SerializeAsync(stream, chat, _jsonOptions);
        }

        File.Move(tempPath, path, overwrite: true);
    }
}
