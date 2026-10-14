using System.Collections.Concurrent;
using System.Text.Json;
using AiChat.Api.Domain;

namespace AiChat.Api.Infrastructure;

/// <summary>
/// Єдиний клас, який торкається диска. Розмова — один JSON-файл data/chats/&lt;id&gt;.json,
/// фото — data/uploads/&lt;guid&gt;.&lt;ext&gt;. Зареєстрований як singleton, тому блокування спільні для всіх запитів.
/// </summary>
public class ChatStorage
{
    private readonly string _chatsDirectory;
    private readonly ILogger<ChatStorage> _logger;

    // Один семафор на розмову: читання-зміна-запис однієї розмови йдуть по черзі,
    // а різні розмови не заважають одна одній.
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

        // Storage:DataPath — щоб у Docker можна було змонтувати volume, напр. /app/data
        var dataPath = configuration["Storage:DataPath"] ?? "data";
        var baseDir = Path.GetFullPath(dataPath, environment.ContentRootPath);

        _chatsDirectory = Path.Combine(baseDir, "chats");
        UploadsDirectory = Path.Combine(baseDir, "uploads");

        Directory.CreateDirectory(_chatsDirectory);
        Directory.CreateDirectory(UploadsDirectory);
    }

    public string UploadsDirectory { get; }

    /// <summary>
    /// id розмови приходить з URL і стає частиною шляху до файлу. Приймаємо лише 32 hex-символи
    /// (Guid у форматі "N"), інакше "..\..\щось" могло б вказати за межі data/chats.
    /// </summary>
    public static bool IsValidId(string? id) =>
        id is { Length: 32 } && Guid.TryParseExact(id, "N", out _);

    public async Task<ChatSession?> GetChatAsync(string id)
    {
        if (!IsValidId(id)) return null;
        return await ReadChatFileAsync(ChatPath(id));
    }

    public async Task<List<ChatSession>> GetAllChatsAsync()
    {
        var chats = new List<ChatSession>();

        foreach (var file in Directory.EnumerateFiles(_chatsDirectory, "*.json"))
        {
            // Один пошкоджений файл не повинен ламати весь список (раніше — 500 на GET /api/chats)
            var chat = await ReadChatFileAsync(file);
            if (chat != null) chats.Add(chat);
        }

        return chats.OrderByDescending(c => c.UpdatedAt).ToList();
    }

    public async Task SaveChatAsync(ChatSession chat)
    {
        if (!IsValidId(chat.Id)) throw new ArgumentException($"Invalid chat id '{chat.Id}'", nameof(chat));

        var gate = GetLock(chat.Id);
        await gate.WaitAsync();
        try
        {
            await WriteChatFileAsync(chat);
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
    /// Повертає null, якщо розмови немає.
    /// </summary>
    public async Task<ChatSession?> UpdateChatAsync(string id, Action<ChatSession> mutate)
    {
        if (!IsValidId(id)) return null;

        var gate = GetLock(id);
        await gate.WaitAsync();
        try
        {
            var chat = await ReadChatFileAsync(ChatPath(id));
            if (chat == null) return null;

            mutate(chat);
            await WriteChatFileAsync(chat);
            return chat;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Видаляє розмову і всі її фото. false — розмови не було.</summary>
    public async Task<bool> DeleteChatAsync(string id)
    {
        if (!IsValidId(id)) return false;

        var gate = GetLock(id);
        await gate.WaitAsync();
        try
        {
            var path = ChatPath(id);
            var chat = await ReadChatFileAsync(path);
            if (chat == null && !File.Exists(path)) return false;

            File.Delete(path);

            if (chat != null)
            {
                DeleteImages(chat.Messages.SelectMany(m => m.Images));
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

    /// <summary>Зберігає вже перевірені фото (див. ImageValidator) під випадковими іменами.</summary>
    public async Task<List<string>> SaveImagesAsync(IEnumerable<ValidatedImage> images, CancellationToken ct = default)
    {
        var imageUrls = new List<string>();

        try
        {
            foreach (var image in images)
            {
                // Розширення обираємо самі за вмістом файлу, а не беремо з імені від клієнта:
                // інакше x.html збережеться як .html і /uploads віддасть його як сторінку нашого сайту.
                var fileName = $"{Guid.NewGuid():N}{image.Extension}";
                var filePath = Path.Combine(UploadsDirectory, fileName);

                await using var stream = new FileStream(filePath, FileMode.CreateNew);
                await image.File.CopyToAsync(stream, ct);

                imageUrls.Add($"/uploads/{fileName}");
            }
        }
        catch
        {
            // Не лишаємо на диску половину фото з невдалого запиту
            DeleteImages(imageUrls);
            throw;
        }

        return imageUrls;
    }

    public void DeleteImages(IEnumerable<string> urls)
    {
        foreach (var url in urls)
        {
            if (!TryGetUploadPath(url, out var path)) continue;

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
    /// Перетворює URL фото ("/uploads/abc.png") на шлях на диску. Знадобиться й AI-частині,
    /// щоб прочитати фото і передати моделі (Base64).
    /// </summary>
    public bool TryGetUploadPath(string url, out string path)
    {
        path = string.Empty;
        const string prefix = "/uploads/";
        if (!url.StartsWith(prefix, StringComparison.Ordinal)) return false;

        var fileName = url[prefix.Length..];
        // Лише ім'я файлу, без жодних каталогів — захист від "/uploads/../chats/x.json"
        if (fileName.Length == 0 || fileName != Path.GetFileName(fileName) || fileName.Contains('\\')) return false;

        path = Path.Combine(UploadsDirectory, fileName);
        return true;
    }

    private string ChatPath(string id) => Path.Combine(_chatsDirectory, $"{id}.json");

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
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Chat file {Path} is damaged and was skipped", path);
            return null;
        }
    }

    private async Task WriteChatFileAsync(ChatSession chat)
    {
        // Спершу пишемо у тимчасовий файл, потім підміняємо: якщо процес впаде посеред запису,
        // старий файл лишиться цілим, а не обрізаним JSON
        var path = ChatPath(chat.Id);
        var tempPath = path + ".tmp";

        await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None,
                         bufferSize: 4096, useAsync: true))
        {
            await JsonSerializer.SerializeAsync(stream, chat, _jsonOptions);
        }

        File.Move(tempPath, path, overwrite: true);
    }
}
