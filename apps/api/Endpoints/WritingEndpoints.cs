using System.Security.Claims;
using static LearnForge.Api.Services.Identity.AccountIdentity;

namespace LearnForge.Api.Endpoints;

public static class WritingEndpoints
{
    extension(IEndpointRouteBuilder app)
    {
        public void MapWriting()
        {
            var group = app.MapGroup("/api/me/courses/{id}/writing").RequireAuthorization();
            group.MapGet("/", (string id, ClaimsPrincipal user, WritingService service) => service.Summary(UserId(user), id));
            group.MapPost("/sessions", (string id, WritingStartRequest request, ClaimsPrincipal user, WritingService service) => service.Start(UserId(user), id, request));
            group.MapGet("/sessions/{sessionId}", (string id, string sessionId, ClaimsPrincipal user, WritingService service) => service.Get(UserId(user), id, sessionId));
            group.MapPost("/sessions/{sessionId}/result", (string id, string sessionId, WritingResultRequest request, ClaimsPrincipal user, WritingService service) => service.Finish(UserId(user), id, sessionId, request));
        }
    }
}
