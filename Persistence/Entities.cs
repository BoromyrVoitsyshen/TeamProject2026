namespace AiChat.Api.Persistence;

// Таблиці бази даних (SQLite через Entity Framework Core).
// Навігаційних властивостей свідомо немає: зв'язки — це лише id (MasterId, ClientId…),
// а запити збирають дані окремими простими запитами. Так кожен запит легко прочитати
// і він гарантовано перекладається в SQL.

public enum UserRole
{
    Client = 1,
    Master = 2
}

/// <summary>Акаунт. Клієнт записується до майстрів, майстер веде свій профіль і розклад.</summary>
public class User
{
    public int Id { get; set; }

    /// <summary>Email у нижньому регістрі — за ним входять, він унікальний.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>PBKDF2-хеш, див. Accounts/PasswordHasher. Сам пароль ніде не зберігається.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Профіль майстра — те, що бачать клієнти в каталозі. Один на акаунт майстра.</summary>
public class MasterProfile
{
    public const int DefaultSlotStepMinutes = 30;
    public const int DefaultBookingHorizonDays = 14;

    public int Id { get; set; }
    public int UserId { get; set; }

    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public int ExperienceYears { get; set; }

    /// <summary>З яким кроком генерувати час запису: 09:00, 09:30, 10:00… при кроці 30 хв.</summary>
    public int SlotStepMinutes { get; set; } = DefaultSlotStepMinutes;

    /// <summary>На скільки днів уперед можна записатися (сьогодні — перший день).</summary>
    public int BookingHorizonDays { get; set; } = DefaultBookingHorizonDays;

    /// <summary>Показувати в каталозі. Опублікувати можна лише заповнений профіль (місто, адреса, послуги, розклад).</summary>
    public bool IsPublished { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Послуга із загального переліку (діагностика, заміна масла…). Перелік спільний для всіх майстрів.</summary>
public class ServiceType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

/// <summary>Послуга, яку надає конкретний майстер: його ціна і тривалість.</summary>
public class MasterService
{
    public int Id { get; set; }
    public int MasterId { get; set; }
    public int ServiceTypeId { get; set; }

    /// <summary>Ціна в гривнях, ціле число.</summary>
    public int Price { get; set; }

    public int DurationMinutes { get; set; }
}

/// <summary>Робочий день тижня майстра, напр. середа 09:00–18:00. З цих рядків генерується графік.</summary>
public class WorkingDay
{
    public int Id { get; set; }
    public int MasterId { get; set; }

    /// <summary>1 = понеділок … 7 = неділя (як у календарі, а не DayOfWeek, де неділя — 0).</summary>
    public int Weekday { get; set; }

    public TimeOnly Start { get; set; }
    public TimeOnly End { get; set; }
}

/// <summary>Окрема дата, коли майстер не працює (відпустка, свято), хоча за розкладом мав би.</summary>
public class DayOff
{
    public int Id { get; set; }
    public int MasterId { get; set; }
    public DateOnly Date { get; set; }
}

public enum BookingStatus
{
    Active = 1,
    Cancelled = 2
}

/// <summary>
/// Запис клієнта до майстра. Час — місцевий час майстерні (див. Booking/BookingClock).
/// Назву і ціну послуги копіюємо в запис: якщо майстер потім змінить ціну, у записі лишиться та, за якою записались.
/// </summary>
public class Booking
{
    public int Id { get; set; }
    public int MasterId { get; set; }
    public int ClientId { get; set; }
    public int ServiceTypeId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public int Price { get; set; }

    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }

    /// <summary>Що сталося з авто — пише клієнт під час запису (необов'язково).</summary>
    public string? Comment { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CancelledAt { get; set; }

    /// <summary>"client" або "master" — хто скасував.</summary>
    public string? CancelledBy { get; set; }
}

/// <summary>Відгук клієнта про майстра: 1–5 зірок і текст. Один відгук від клієнта на майстра.</summary>
public class Review
{
    public int Id { get; set; }
    public int MasterId { get; set; }
    public int ClientId { get; set; }
    public int Rating { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
