using Microsoft.AspNetCore.Identity;

namespace AiChat.Api.Infrastructure.Auth;

/// <summary>
/// Тексти помилок Identity українською — їх побачить користувач на сторінці входу.
/// Перевизначені лише ті, що можливі за наших налаштувань (див. AddAppAuth); решта лишаються англійською.
/// </summary>
public class UkrainianIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DuplicateEmail(string email) =>
        new() { Code = nameof(DuplicateEmail), Description = "Акаунт з таким email уже існує." };

    public override IdentityError DuplicateUserName(string userName) =>
        new() { Code = nameof(DuplicateUserName), Description = "Акаунт з таким email уже існує." };

    public override IdentityError InvalidEmail(string? email) =>
        new() { Code = nameof(InvalidEmail), Description = "Перевірте email — схоже, в ньому помилка." };

    public override IdentityError InvalidUserName(string? userName) =>
        new() { Code = nameof(InvalidUserName), Description = "Email містить недозволені символи." };

    public override IdentityError PasswordTooShort(int length) =>
        new() { Code = nameof(PasswordTooShort), Description = $"Пароль має містити щонайменше {length} символів." };

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        new() { Code = nameof(PasswordRequiresUniqueChars), Description = $"У паролі має бути щонайменше {uniqueChars} різних символів." };
}
