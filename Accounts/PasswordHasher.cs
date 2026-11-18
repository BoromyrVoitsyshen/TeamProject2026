using System.Security.Cryptography;

namespace AiChat.Api.Accounts;

/// <summary>
/// Хешування паролів: PBKDF2-SHA256, 100 000 ітерацій, своя «сіль» для кожного пароля.
/// Формат рядка: pbkdf2-sha256$ітерації$сіль$хеш (base64) — щоб згодом можна було підняти кількість ітерацій.
/// </summary>
public static class PasswordHasher
{
    private const string Scheme = "pbkdf2-sha256";
    private const int Iterations = 100_000;
    private const int SaltBytes = 16;
    private const int KeyBytes = 32;

    // Хеш для неіснуючого користувача: вхід з чужим email триває стільки ж, скільки з правильним,
    // і за часом відповіді не можна дізнатися, які email зареєстровані
    private static readonly string Dummy = Hash(Guid.NewGuid().ToString());

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeyBytes);
        return $"{Scheme}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public static bool Verify(string password, string? stored)
    {
        var parts = (stored ?? Dummy).Split('$');
        if (parts.Length != 4 || parts[0] != Scheme || !int.TryParse(parts[1], out var iterations)) return false;

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected) && stored != null;
    }
}
