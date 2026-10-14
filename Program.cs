using AiChat.Api.Agent;
using AiChat.Api.Domain;
using AiChat.Api.Endpoints;
using AiChat.Api.Infrastructure;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Ліміт розміру запиту: 10 фото по 10 МБ. Типовий ліміт Kestrel (~30 МБ) менший, ніж дозволяє сторінка.
// Більший запит Kestrel обірве з 413 — сторінка покаже «Файли завеликі для сервера».
builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = ChatLimits.MaxRequestBytes);
builder.Services.Configure<FormOptions>(form => form.MultipartBodyLengthLimit = ChatLimits.MaxRequestBytes);

builder.Services.AddSingleton<ChatStorage>();
builder.Services.AddSingleton<ChatBusyTracker>();

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.SectionName));
// Справжня модель: замініть StubChatAgent на свій клас, що реалізує IChatAgent
builder.Services.AddSingleton<IChatAgent, StubChatAgent>();
builder.Services.AddSingleton<AgentReplyRunner>();

builder.Services.AddEndpoints(typeof(Program).Assembly);

builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields = HttpLoggingFields.RequestMethod |
                            HttpLoggingFields.RequestPath |
                            HttpLoggingFields.ResponseStatusCode;
});

var app = builder.Build();

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
