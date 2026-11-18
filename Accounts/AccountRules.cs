using System.Net.Mail;

namespace AiChat.Api.Accounts;

/// <summary>Правила для полів акаунта. Сторінка login.html перевіряє те саме для зручності, сервер — для безпеки.</summary>
public static class AccountRules
{
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 128;
    public const int MaxNameLength = 80;
    public const int MaxPhoneLength = 30;

    public static string NormalizeEmail(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    public static bool IsValidEmail(string email)
    {
        if (email.Length is < 3 or > 254) return false;
        if (!MailAddress.TryCreate(email, out var address)) return false;
        // MailAddress приймає і "Ім'я <a@b>"; нам потрібна лише сама адреса, і домен із крапкою
        return address.Address == email && address.Host.Contains('.');
    }

    public static string? PasswordError(string? password) => password switch
    {
        null or "" => "Вкажіть пароль.",
        { Length: < MinPasswordLength } => $"Пароль має бути не коротшим за {MinPasswordLength} символів.",
        { Length: > MaxPasswordLength } => $"Пароль має бути не довшим за {MaxPasswordLength} символів.",
        _ => null
    };

    /// <summary>Телефон: необов'язковий; цифри, пробіли, +, -, дужки. null — порожній.</summary>
    public static (string? Phone, string? Error) NormalizePhone(string? phone)
    {
        var value = (phone ?? string.Empty).Trim();
        if (value.Length == 0) return (null, null);
        if (value.Length > MaxPhoneLength || !value.All(c => char.IsAsciiDigit(c) || c is '+' or ' ' or '-' or '(' or ')'))
            return (null, "Телефон може містити лише цифри, пробіли, +, - і дужки.");
        if (value.Count(char.IsAsciiDigit) < 9)
            return (null, "У телефоні має бути щонайменше 9 цифр.");
        return (value, null);
    }

    public static string NormalizeText(string? text) =>
        string.Join(' ', (text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
