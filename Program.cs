using AiChat.Api.Infrastructure;
using AiChat.Api.Endpoints;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ChatStorage>();
builder.Services.AddEndpoints(typeof(Program).Assembly);

builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields = HttpLoggingFields.RequestMethod |
                                HttpLoggingFields.RequestPath |
                                HttpLoggingFields.ResponseStatusCode;
});

var app = builder.Build();

app.UseHttpLogging();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapEndpoints();

var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "data", "uploads");
if (!Directory.Exists(uploadsPath)) Directory.CreateDirectory(uploadsPath);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

app.Run();