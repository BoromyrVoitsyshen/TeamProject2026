using AiChat.Api.Accounts;
using AiChat.Api.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace AiChat.Api.Persistence;

/// <summary>
/// Перелік послуг (потрібен завжди) і демо-дані, щоб каталог не був порожнім:
/// 7 майстрів (6 опубліковані), 3 клієнти, записи в минулому й майбутньому, відгуки.
/// Усі демо-акаунти мають пароль <see cref="Password"/>: master1@example.com … master7@example.com,
/// client1@example.com … client3@example.com. Імена та адреси вигадані.
/// </summary>
public static class DemoData
{
    public const string Password = "demo12345";

    private const string Diagnostics = "Комп'ютерна діагностика";
    private const string Oil = "Заміна масла і фільтрів";
    private const string Suspension = "Ремонт ходової частини";
    private const string Brakes = "Гальмівна система";
    private const string Tyres = "Шиномонтаж";
    private const string Alignment = "Розвал-сходження";
    private const string Engine = "Ремонт двигуна";
    private const string Electrics = "Автоелектрика";
    private const string AirCon = "Заправка кондиціонера";
    private const string Body = "Кузовний ремонт і фарбування";

    public static List<ServiceType> ServiceCatalogue() =>
    [
        new() { Name = Diagnostics, Category = "Діагностика", SortOrder = 1 },
        new() { Name = Oil, Category = "Технічне обслуговування", SortOrder = 2 },
        new() { Name = Suspension, Category = "Ремонт", SortOrder = 3 },
        new() { Name = Brakes, Category = "Ремонт", SortOrder = 4 },
        new() { Name = Tyres, Category = "Шини", SortOrder = 5 },
        new() { Name = Alignment, Category = "Шини", SortOrder = 6 },
        new() { Name = Engine, Category = "Ремонт", SortOrder = 7 },
        new() { Name = Electrics, Category = "Електрика", SortOrder = 8 },
        new() { Name = AirCon, Category = "Клімат", SortOrder = 9 },
        new() { Name = Body, Category = "Кузов", SortOrder = 10 }
    ];

    private sealed record DemoMaster(
        string Name,
        string City,
        string Address,
        string Phone,
        int Experience,
        string Description,
        int Step,
        bool Published,
        (string Service, int Price, int Minutes)[] Services,
        (int[] Weekdays, string Start, string End)[] Hours);

    private static readonly DemoMaster[] Masters =
    [
        new("Олег Ковальчук", "Київ", "вул. Борщагівська, 154", "+380 67 111 22 33", 12,
            "СТО на Борщагівці. Діагностика, ходова і гальма; найчастіше працюю з японськими та корейськими авто. Перед ремонтом показую, що саме зношено, і погоджую ціну.",
            30, true,
            [(Diagnostics, 500, 45), (Suspension, 800, 90), (Brakes, 700, 60), (Oil, 400, 30)],
            [([1, 2, 3, 4, 5], "09:00", "18:00")]),
        new("Ірина Мельник", "Київ", "просп. Перемоги, 67", "+380 50 222 33 44", 8,
            "Автоелектрика: проводка, стартер, генератор, сигналізації. Заправка й ремонт кондиціонерів.",
            30, true,
            [(Diagnostics, 600, 60), (Electrics, 900, 90), (AirCon, 1200, 60)],
            [([2, 3, 4], "10:00", "19:00"), ([6], "10:00", "15:00")]),
        new("Андрій Бойко", "Львів", "вул. Городоцька, 220", "+380 63 333 44 55", 15,
            "Ремонт бензинових і дизельних двигунів, капітальний ремонт. Дефектовка з фото і поясненням.",
            60, true,
            [(Engine, 3500, 240), (Diagnostics, 450, 45), (Oil, 350, 30)],
            [([3, 4, 5], "09:00", "18:00")]),
        new("Сергій Ткаченко", "Харків", "вул. Академіка Павлова, 44", "+380 95 444 55 66", 5,
            "Шиномонтаж легкових і кросоверів до R20, розвал-сходження на 3D-стенді, дрібний ремонт ходової.",
            15, true,
            [(Tyres, 600, 45), (Alignment, 700, 60), (Suspension, 750, 90)],
            [([1, 2, 3, 4, 5, 6], "08:00", "20:00")]),
        new("Наталія Шевчук", "Одеса", "вул. Генуезька, 5", "+380 66 555 66 77", 10,
            "Кузовний ремонт, локальне фарбування, полірування. Підбір фарби за кодом.",
            60, true,
            [(Body, 2000, 180), (Diagnostics, 500, 60)],
            [([1, 2, 3, 4, 5], "09:00", "17:00")]),
        new("Віктор Лисенко", "Дніпро", "просп. Слобожанський, 31", "+380 93 666 77 88", 3,
            "Швидке ТО: масло, фільтри, колодки, сезонна заміна шин. Працюю без попереднього запису лише в суботу.",
            30, true,
            [(Oil, 300, 30), (Brakes, 600, 60), (Tyres, 500, 45), (AirCon, 1000, 60)],
            [([1, 3, 5], "09:00", "18:00"), ([6], "09:00", "14:00")]),
        new("Тарас Гнатюк", "Київ", "вул. Січових Стрільців, 10", "+380 97 777 88 99", 2,
            "Профіль ще не заповнено.",
            30, false,
            [(Oil, 350, 30)],
            [([1, 2, 3, 4, 5], "09:00", "18:00")])
    ];

    private static readonly (string Name, string Phone)[] Clients =
    [
        ("Олена Іваненко", "+380 67 123 45 67"),
        ("Максим Петренко", "+380 50 765 43 21"),
        ("Марія Савчук", "+380 63 555 12 34")
    ];

    // Відгуки: (майстер, клієнт, днів тому, оцінка, текст). Під кожен відгук — минулий запис.
    private static readonly (int Master, int Client, int DaysAgo, int Rating, string Text)[] Reviews =
    [
        (0, 0, 20, 5, "Знайшов причину стуку за пів години, показав зношену втулку. Ціна як у прайсі."),
        (0, 1, 12, 5, "Швидко замінили колодки, все пояснили. Рекомендую."),
        (0, 2, 6, 4, "Добре зробили ходову, але довелося трохи почекати."),
        (1, 0, 15, 5, "Кондиціонер знову холодить. Перевірили систему на витік, усе чесно."),
        (1, 2, 9, 4, "Розібралися з генератором, хоча шукали довго."),
        (2, 1, 25, 5, "Капремонт двигуна зробили якісно, надіслали фото дефектовки."),
        (2, 0, 18, 5, "Андрій — справжній профі, мотор працює рівно."),
        (2, 2, 8, 5, "Чесна діагностика, нічого зайвого не нав'язували."),
        (3, 1, 10, 4, "Шиномонтаж швидко, без черги за записом."),
        (3, 0, 4, 3, "Розвал зробили, але кермо трохи криве — довелося повернутися."),
        (4, 2, 14, 5, "Подряпину на дверях не видно взагалі, колір підібрали ідеально.")
    ];

    public static async Task SeedAsync(AppDbContext db, DateOnly today)
    {
        var services = await db.ServiceTypes.ToDictionaryAsync(s => s.Name, s => s.Id);

        // ── Клієнти ──
        var clientUsers = Clients.Select((c, i) => new User
        {
            Name = c.Name,
            Email = $"client{i + 1}@example.com",
            Phone = c.Phone,
            PasswordHash = PasswordHasher.Hash(Password),
            Role = UserRole.Client
        }).ToList();
        db.Users.AddRange(clientUsers);
        await db.SaveChangesAsync();

        // ── Майстри: акаунт, профіль, послуги, розклад ──
        var profiles = new List<MasterProfile>();
        var schedules = new List<List<WorkingDay>>();

        for (var i = 0; i < Masters.Length; i++)
        {
            var demo = Masters[i];
            var user = new User
            {
                Name = demo.Name,
                Email = $"master{i + 1}@example.com",
                Phone = demo.Phone,
                PasswordHash = PasswordHasher.Hash(Password),
                Role = UserRole.Master
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var profile = new MasterProfile
            {
                UserId = user.Id,
                DisplayName = demo.Name,
                Description = demo.Description,
                City = demo.City,
                Address = demo.Address,
                Phone = demo.Phone,
                ExperienceYears = demo.Experience,
                SlotStepMinutes = demo.Step,
                BookingHorizonDays = MasterProfile.DefaultBookingHorizonDays,
                IsPublished = demo.Published
            };
            db.Masters.Add(profile);
            await db.SaveChangesAsync();
            profiles.Add(profile);

            db.MasterServices.AddRange(demo.Services.Select(s => new MasterService
            {
                MasterId = profile.Id,
                ServiceTypeId = services[s.Service],
                Price = s.Price,
                DurationMinutes = s.Minutes
            }));

            var days = demo.Hours
                .SelectMany(h => h.Weekdays.Select(weekday => new WorkingDay
                {
                    MasterId = profile.Id,
                    Weekday = weekday,
                    Start = ParseTime(h.Start),
                    End = ParseTime(h.End)
                }))
                .ToList();
            db.WorkingDays.AddRange(days);
            schedules.Add(days);
        }

        await db.SaveChangesAsync();

        // ── Минулі записи з відгуками ──
        foreach (var review in Reviews)
        {
            var master = Masters[review.Master];
            var offer = master.Services[0];
            var date = WorkingDateOnOrBefore(schedules[review.Master], today.AddDays(-review.DaysAgo));
            var booking = NewBooking(profiles[review.Master], clientUsers[review.Client], services[offer.Service], offer, date, schedules[review.Master], 0);
            db.Bookings.Add(booking);

            db.Reviews.Add(new Review
            {
                MasterId = profiles[review.Master].Id,
                ClientId = clientUsers[review.Client].Id,
                Rating = review.Rating,
                Text = review.Text,
                CreatedAt = DateTime.SpecifyKind(booking.EndsAt.AddHours(3), DateTimeKind.Utc)
            });
        }

        // Минулий візит без відгуку: Олена може оцінити Віктора Лисенка (у нього ще немає відгуків)
        var lysenko = Masters[5];
        db.Bookings.Add(NewBooking(profiles[5], clientUsers[0], services[lysenko.Services[0].Service], lysenko.Services[0],
            WorkingDateOnOrBefore(schedules[5], today.AddDays(-3)), schedules[5], 1));

        // ── Майбутні записи: видно в графіку майстрів і в кабінетах клієнтів ──
        var kovalchuk = Masters[0];
        db.Bookings.Add(NewBooking(profiles[0], clientUsers[0], services[kovalchuk.Services[0].Service], kovalchuk.Services[0],
            WorkingDateAfter(schedules[0], today), schedules[0], 2, "Стукає спереду справа на ямах."));
        db.Bookings.Add(NewBooking(profiles[0], clientUsers[1], services[kovalchuk.Services[2].Service], kovalchuk.Services[2],
            WorkingDateAfter(schedules[0], today), schedules[0], 6));

        var boyko = Masters[2];
        db.Bookings.Add(NewBooking(profiles[2], clientUsers[1], services[boyko.Services[1].Service], boyko.Services[1],
            WorkingDateAfter(schedules[2], today), schedules[2], 0, "Горить check engine, машина смикається."));

        await db.SaveChangesAsync();
    }

    /// <summary>Запис на <paramref name="slot"/>-й крок робочого дня майстра (0 — перший час дня).</summary>
    private static Booking NewBooking(
        MasterProfile master, User client, int serviceTypeId, (string Service, int Price, int Minutes) offer,
        DateOnly date, List<WorkingDay> days, int slot, string? comment = null)
    {
        var hours = days.First(d => d.Weekday == Weekdays.Of(date));
        var start = date.ToDateTime(hours.Start).AddMinutes(slot * master.SlotStepMinutes);

        return new Booking
        {
            MasterId = master.Id,
            ClientId = client.Id,
            ServiceTypeId = serviceTypeId,
            ServiceName = offer.Service,
            Price = offer.Price,
            StartsAt = start,
            EndsAt = start.AddMinutes(offer.Minutes),
            Comment = comment,
            Status = BookingStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static TimeOnly ParseTime(string value) =>
        Times.TryParseTime(value, out var time) ? time : throw new FormatException($"Bad demo time '{value}'");

    private static DateOnly WorkingDateOnOrBefore(List<WorkingDay> days, DateOnly date)
    {
        while (days.All(d => d.Weekday != Weekdays.Of(date))) date = date.AddDays(-1);
        return date;
    }

    private static DateOnly WorkingDateAfter(List<WorkingDay> days, DateOnly date)
    {
        do date = date.AddDays(1);
        while (days.All(d => d.Weekday != Weekdays.Of(date)));
        return date;
    }
}
