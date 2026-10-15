namespace AiChat.Api.Infrastructure;

/// <summary>Фото, яке пройшло перевірку; Extension визначено за вмістом файлу.</summary>
public record ValidatedImage(IFormFile File, string Extension);

/// <summary>
/// Перевіряє фото за «магічними байтами» на початку файлу. Ні імені файлу, ні Content-Type
/// від браузера не довіряємо: їх легко підробити.
/// </summary>
public static class ImageValidator
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Gif87 = "GIF87a"u8.ToArray();
    private static readonly byte[] Gif89 = "GIF89a"u8.ToArray();
    private static readonly byte[] Riff = "RIFF"u8.ToArray();
    private static readonly byte[] Webp = "WEBP"u8.ToArray();

    /// <summary>Повертає розширення (".jpg", ".png", ".gif", ".webp") або null, якщо це не дозволене фото.</summary>
    public static async Task<string?> DetectExtensionAsync(IFormFile file, CancellationToken ct = default)
    {
        var header = new byte[12];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        var span = header.AsSpan(0, read);

        if (span.StartsWith(Jpeg)) return ".jpg";
        if (span.StartsWith(Png)) return ".png";
        if (span.StartsWith(Gif87) || span.StartsWith(Gif89)) return ".gif";
        if (span.Length >= 12 && span.StartsWith(Riff) && span[8..12].SequenceEqual(Webp)) return ".webp";

        return null;
    }
}
