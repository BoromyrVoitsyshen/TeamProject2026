using System.Text.Json;
using AiChat.Api.Domain;

namespace AiChat.Api.Infrastructure;

public class ChatStorage
{
    private readonly string _chatsDirectory;
    private readonly string _uploadsDirectory;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public ChatStorage()
    {
        var baseDir = Directory.GetCurrentDirectory();
        _chatsDirectory = Path.Combine(baseDir, "data", "chats");
        _uploadsDirectory = Path.Combine(baseDir, "data", "uploads");

        if (!Directory.Exists(_chatsDirectory)) Directory.CreateDirectory(_chatsDirectory);
        if (!Directory.Exists(_uploadsDirectory)) Directory.CreateDirectory(_uploadsDirectory);
    }

    public async Task<List<string>> SaveImagesAsync(IFormFileCollection? files)
    {
        var imageUrls = new List<string>();
        if (files == null || files.Count == 0) return imageUrls;

        foreach (var file in files)
        {
            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrEmpty(ext)) ext = ".jpg";

            var newFileName = $"{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(_uploadsDirectory, newFileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            imageUrls.Add($"/uploads/{newFileName}");
        }

        return imageUrls;
    }

    public async Task SaveChatAsync(ChatSession chat)
    {
        var filePath = Path.Combine(_chatsDirectory, $"{chat.Id}.json");
        var json = JsonSerializer.Serialize(chat, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<List<ChatSession>> GetAllChatsAsync()
    {
        var chats = new List<ChatSession>();
        var files = Directory.GetFiles(_chatsDirectory, "*.json");

        foreach (var file in files)
        {
            var json = await File.ReadAllTextAsync(file);
            var chat = JsonSerializer.Deserialize<ChatSession>(json, _jsonOptions);
            if (chat != null)
            {
                chats.Add(chat);
            }
        }

        return chats.OrderByDescending(c => c.CreatedAt).ToList();
    }

    public async Task<ChatSession?> GetChatAsync(string id)
    {
        var filePath = Path.Combine(_chatsDirectory, $"{id}.json");
        if (!File.Exists(filePath)) return null;

        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<ChatSession>(json, _jsonOptions);
    }

    public Task DeleteChatAsync(string id)
    {
        var filePath = Path.Combine(_chatsDirectory, $"{id}.json");

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        // TODO: Пізніше тут додамо логіку видалення фотографій з data/uploads/
        return Task.CompletedTask;
    }
}