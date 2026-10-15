namespace AiChat.Api.Domain;

/// <summary>
/// Ліміти повідомлення. Ті самі числа — константи MAX_* на початку wwwroot/chat.js:
/// сторінка перевіряє їх для зручності, сервер — для безпеки. Змінюєте тут — змініть і там.
/// </summary>
public static class ChatLimits
{
    public const int MaxTextLength = 10_000;
    public const int MaxImages = 10;
    public const long MaxImageBytes = 10L * 1024 * 1024;

    /// <summary>Максимальний розмір усього запиту: 10 фото по 10 МБ + текст і службові поля multipart.</summary>
    public const long MaxRequestBytes = MaxImages * MaxImageBytes + 1024 * 1024;
}
