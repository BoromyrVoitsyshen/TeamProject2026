using System.Security.Claims;
using AiChat.Api.Accounts;
using AiChat.Api.Agent;
using AiChat.Api.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiChat.Api.Endpoints.ChatForm.FillForm;

/// <summary>
/// Модель заповнює підсумок консультації за розмовою (MECH-86). Перезаписує попередню форму,
/// зокрема виправлену користувачем, — сторінка питає підтвердження перед цим.
/// </summary>
public class Endpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/chats/{id}/form", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(
        string id,
        ClaimsPrincipal user,
        ChatStorage storage,
        ChatBusyTracker busy,
        ConsultationFormService forms)
    {
        var userId = user.RequireUserId();
        if (await storage.GetChatAsync(userId, id) == null) return ApiResults.ChatNotFound();

        // Окремий ключ "form:": заповнення форми не повинно показувати в чаті «агент друкує» (busy розмови),
        // але двічі одночасно заповнювати одну форму (подвійний клік, дві вкладки) теж не треба
        var busyKey = $"form:{id}";
        if (!busy.TryBegin(busyKey))
            return ApiResults.Error(StatusCodes.Status409Conflict, "Форма вже заповнюється.");

        try
        {
            var result = await forms.FillAsync(userId, id);
            return result.Status switch
            {
                FormFillStatus.Ok => Results.Ok(result.Form),
                FormFillStatus.EmptyChat => ApiResults.Error(StatusCodes.Status400BadRequest,
                    "Спершу опишіть проблему асистенту — підсумовувати поки нічого."),
                FormFillStatus.Failed => ApiResults.Error(StatusCodes.Status502BadGateway,
                    "ШІ не зміг заповнити форму. Спробуйте ще раз трохи пізніше."),
                _ => ApiResults.ChatNotFound()
            };
        }
        finally
        {
            busy.End(busyKey);
        }
    }
}
