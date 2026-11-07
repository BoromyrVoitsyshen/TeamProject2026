using Microsoft.AspNetCore.Identity;

namespace AiChat.Api.Domain;

/// <summary>
/// Користувач застосунку. IdentityUser вже має Id, Email, PasswordHash, лічильник невдалих входів тощо.
/// Свій клас (а не IdentityUser напряму) — щоб пізніше додавати поля, наприклад ім'я чи авто користувача.
/// </summary>
public class AppUser : IdentityUser
{
}
