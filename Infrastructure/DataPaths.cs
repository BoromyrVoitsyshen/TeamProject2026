namespace AiChat.Api.Infrastructure;

/// <summary>
/// Де лежать дані застосунку: чати, фото, база користувачів, ключі шифрування cookie.
/// Storage:DataPath — щоб у Docker можна було змонтувати один volume, напр. /app/data.
/// </summary>
public static class DataPaths
{
    public static string GetDataDirectory(IConfiguration configuration, IHostEnvironment environment)
    {
        var dataPath = configuration["Storage:DataPath"] ?? "data";
        var fullPath = Path.GetFullPath(dataPath, environment.ContentRootPath);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }
}
