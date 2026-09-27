using Microsoft.AspNetCore.Routing;

namespace AiChat.Api;

public interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder app);
}