using System.Net.Http.Headers;
using AiChat.Api.Agent;
using AiChat.Api.Agent.OpenRouter;
using AiChat.Api.Domain;
using AiChat.Api.Endpoints;
using AiChat.Api.Infrastructure;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Ліміт розміру запиту: 10 фото по 10 МБ. Типовий ліміт Kestrel (~30 МБ) менший, ніж дозволяє сторінка.
// Більший запит Kestrel обірве з 413 — сторінка покаже «Файли завеликі для сервера».
builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = ChatLimits.MaxRequestBytes);
builder.Services.Configure<FormOptions>(form => form.MultipartBodyLengthLimit = ChatLimits.MaxRequestBytes);

builder.Services.AddSingleton<ChatStorage>();
builder.Services.AddSingleton<ChatBusyTracker>();

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
    // Через це OpenRouterChatAgent — transient, тож AgentReplyRunner, який його отримує, теж не singleton.
    builder.Services.AddHttpClient<IChatAgent, OpenRouterChatAgent>((services, http) =>
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
}

builder.Services.AddScoped<AgentReplyRunner>();

builder.Services.AddEndpoints(typeof(Program).Assembly);

builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields = HttpLoggingFields.RequestMethod |
                            HttpLoggingFields.RequestPath |
                            HttpLoggingFields.ResponseStatusCode;
});

var app = builder.Build();

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
app.MapEndpoints();

// /uploads віддає лише фото — навіть якщо на диску якось опиниться .html чи .svg, браузер його не отримає
var imageTypes = new FileExtensionContentTypeProvider(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    [".jpg"] = "image/jpeg",
    [".jpeg"] = "image/jpeg",
    [".png"] = "image/png",
    [".gif"] = "image/gif",
    [".webp"] = "image/webp"
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(app.Services.GetRequiredService<ChatStorage>().UploadsDirectory),
    RequestPath = "/uploads",
    ContentTypeProvider = imageTypes
});

app.Run();

// Потрібно для інтеграційних тестів через WebApplicationFactory<Program>
public partial class Program;
