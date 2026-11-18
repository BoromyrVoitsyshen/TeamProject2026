using System.Net.Http.Headers;
using AiChat.Api.Accounts;
using AiChat.Api.Agent;
using AiChat.Api.Agent.OpenRouter;
using AiChat.Api.Catalog;
using AiChat.Api.Persistence;
using AiChat.Api.Domain;
using AiChat.Api.Endpoints;
using AiChat.Api.Infrastructure;
using AiChat.Api.Scheduling;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Ліміт розміру запиту: 10 фото по 10 МБ. Типовий ліміт Kestrel (~30 МБ) менший, ніж дозволяє сторінка.
// Більший запит Kestrel обірве з 413 — сторінка покаже «Файли завеликі для сервера».
builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = ChatLimits.MaxRequestBytes);
builder.Services.Configure<FormOptions>(form => form.MultipartBodyLengthLimit = ChatLimits.MaxRequestBytes);

builder.Services.AddSingleton<ChatStorage>();
builder.Services.AddSingleton<ChatBusyTracker>();

// Каталог майстрів, записи, відгуки й акаунти — база SQLite (data/app.db), див. папку Persistence/.
// Розмови з AI-помічником поки лишаються в JSON-файлах (ChatStorage).
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={DatabaseSetup.DatabasePath(builder.Configuration, builder.Environment)}"));
builder.Services.AddScoped<MasterCatalog>();
builder.Services.AddSingleton<BookingClock>();
builder.Services.AddSingleton<BookingLocks>();
builder.Services.AddAccounts();

// Cookie входу зашифрована ключами Data Protection. За замовчуванням вони живуть у профілі користувача ОС,
// а в Docker — усередині контейнера: після перезапуску контейнера всіх би «викинуло» з акаунтів.
// Тримаємо ключі поруч із даними (data/keys), у тому ж volume, що й база
builder.Services.AddDataProtection()
    .SetApplicationName("Mechs")
    .PersistKeysToFileSystem(new DirectoryInfo(
        Path.Combine(DataPaths.GetDataDirectory(builder.Configuration, builder.Environment), "keys")));

// Налаштування агента: прив'язка до секції "Agent" + перевірка при старті.
// ValidateOnStart — без нього валідатор спрацював би лише при першому повідомленні в чаті,
// і помилка конфігурації знову сховалася б за 502.
builder.Services.AddOptions<AgentOptions>()
    .Bind(builder.Configuration.GetSection(AgentOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<AgentOptions>, AgentOptionsValidator>();

// Яку реалізацію IChatAgent зареєструвати, треба вирішити ще до Build(), тому тут читаємо лише ключ.
// Усі інші налаштування беремо нижче з IOptions — уже перевіреними.
var useOpenRouter = !string.IsNullOrWhiteSpace(builder.Configuration[$"{AgentOptions.SectionName}:ApiKey"]);
if (useOpenRouter)
{
    // Typed client: IHttpClientFactory сам керує з'єднаннями (не вичерпує сокети, оновлює DNS).
    // Через це OpenRouterClient — transient, тож і все, що його отримує (агент чату, заповнення форми,
    // AgentReplyRunner), не singleton.
    builder.Services.AddHttpClient<OpenRouterClient>((services, http) =>
    {
        var agent = services.GetRequiredService<IOptions<AgentOptions>>().Value;

        http.BaseAddress = new Uri(agent.BaseUrl.TrimEnd('/') + "/");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", agent.ApiKey);
        if (!string.IsNullOrWhiteSpace(agent.SiteUrl))
            http.DefaultRequestHeaders.Add("HTTP-Referer", agent.SiteUrl);
        http.DefaultRequestHeaders.Add("X-OpenRouter-Title", agent.AppTitle);
        // Типовий таймаут HttpClient — 100 с, менше за наші 120 с. Час обмежує AgentReplyRunner,
        // а тут лише запасна межа, щоб запит не висів вічно
        http.Timeout = TimeSpan.FromSeconds(agent.TimeoutSeconds + 30);
    });
    builder.Services.AddTransient<IChatAgent, OpenRouterChatAgent>();
    builder.Services.AddTransient<IConsultationFormFiller, OpenRouterFormFiller>();

    // Перевірка Agent:Models при старті (лише попередження в лог, див. ModelAvailabilityCheck)
    builder.Services.AddHttpClient(ModelAvailabilityCheck.HttpClientName, (services, http) =>
    {
        var agent = services.GetRequiredService<IOptions<AgentOptions>>().Value;
        http.BaseAddress = new Uri(agent.BaseUrl.TrimEnd('/') + "/");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", agent.ApiKey);
        http.Timeout = TimeSpan.FromSeconds(15);
    });
    builder.Services.AddHostedService<ModelAvailabilityCheck>();
}
else
{
    // Без ключа (наприклад, у когось із команди немає свого) сайт працює із заглушкою
    builder.Services.AddSingleton<IChatAgent, StubChatAgent>();
    builder.Services.AddSingleton<IConsultationFormFiller, StubFormFiller>();
}

builder.Services.AddScoped<AgentReplyRunner>();
builder.Services.AddScoped<ConsultationFormService>();

builder.Services.AddEndpoints(typeof(Program).Assembly);

builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields = HttpLoggingFields.RequestMethod |
                            HttpLoggingFields.RequestPath |
                            HttpLoggingFields.ResponseStatusCode;
});

var app = builder.Build();

// Створює базу при першому запуску, заповнює перелік послуг і (якщо ввімкнено) демо-дані
await app.InitializeDatabaseAsync();

// Пишемо в лог, коли застосунок уже стартував, тобто ПІСЛЯ перевірки ValidateOnStart
app.Lifetime.ApplicationStarted.Register(() =>
{
    var agent = app.Services.GetRequiredService<IOptions<AgentOptions>>().Value;
    app.Logger.LogInformation("Chat agent: {Agent}", useOpenRouter
        ? $"OpenRouter, models: {string.Join(", ", agent.Models)}"
        : "stub (Agent:ApiKey is not set)");
});

app.UseHttpLogging();

// Браузер не повинен «вгадувати» тип файлу за вмістом: фото завжди лишається фото
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapEndpoints();

// Фото з розмов (/uploads) віддає ендпоінт Endpoints/GetUpload — під авторизацією, лише власні фото,
// а не статичні файли, які побачив би будь-хто зі знанням адреси

app.Run();

// Потрібно для інтеграційних тестів через WebApplicationFactory<Program>
public partial class Program;
