using System.Security.Claims;
using System.Text.Json;
using AiChat.Api.Agent;
using AiChat.Api.Domain;
using AiChat.Api.Infrastructure.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiChat.Api.Endpoints.ChatForm.SaveForm;

/// <summary>Зберігає підсумок, який виправив або доповнив користувач (MECH-86).</summary>
public class Endpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/chats/{id}/form", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(
        string id,
        HttpRequest request,
        ClaimsPrincipal user,
        ConsultationFormService forms)
    {
        // Читаємо тіло самі, а не через параметр ConsultationForm: тоді на зламаний JSON
        // (наприклад, "urgency": "panic") відповідаємо звичним { "error" }, а не загальною сторінкою 400
        ConsultationForm? form;
        try
        {
            form = await request.ReadFromJsonAsync<ConsultationForm>();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return ApiResults.Error(StatusCodes.Status400BadRequest, "Форма заповнена в неправильному форматі.");
        }

        if (form == null) return ApiResults.Error(StatusCodes.Status400BadRequest, "Порожня форма.");

        // Довжини, рік, пробіг перевіряє ConsultationForm.Normalize — ті самі правила, що для відповіді моделі
        var saved = await forms.SaveEditedAsync(user.GetUserId(), id, form);
        return saved == null ? ApiResults.ChatNotFound() : Results.Ok(saved);
    }
}
