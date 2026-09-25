using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using static LearnForge.Api.Services.Identity.AccountIdentity;

namespace LearnForge.Api.Endpoints;

public static class AttemptEndpoints
{
    extension(IEndpointRouteBuilder app)
    {
        public void MapAttempts()
        {
            var attempts = app.MapGroup("/api/me/attempts").RequireAuthorization();
            attempts.MapGet("", async (ClaimsPrincipal user, AppDb db) => (await db.Attempts.Where(a => a.UserId == UserId(user)).OrderByDescending(a => a.StartedAt).ToListAsync()).Select(Analytics.Summary));
            attempts.MapPost("", async (StartRequest request, ClaimsPrincipal user, AttemptService service) => service.View(await service.Start(UserId(user), request)));
            attempts.MapGet("/{id}", async (string id, ClaimsPrincipal user, AttemptService service) => service.View(await service.Find(UserId(user), id)));
            attempts.MapPut("/{id}/responses", async (string id, ResponseRequest request, ClaimsPrincipal user, AttemptService service) =>
            {
                var a = await service.Find(UserId(user), id); await service.Save(a, request); return service.View(a);
            });
            attempts.MapPost("/{id}/submit", async (string id, TransitionRequest request, ClaimsPrincipal user, AttemptService service) =>
            {
                var a = await service.Find(UserId(user), id); await service.Submit(a, request.Revision); return service.View(a);
            });
            attempts.MapPost("/{id}/section", async (string id, TransitionRequest request, ClaimsPrincipal user, AttemptService service) =>
            {
                var a = await service.Find(UserId(user), id); await service.NextSection(a, request.Revision); return service.View(a);
            });
        }
    }
}
