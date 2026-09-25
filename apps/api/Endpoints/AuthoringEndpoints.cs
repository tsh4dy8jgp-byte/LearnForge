using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using static LearnForge.Api.Services.Identity.AccountIdentity;

namespace LearnForge.Api.Endpoints;

public static class AuthoringEndpoints
{
    extension(IEndpointRouteBuilder app)
    {
        public void MapAuthoring()
        {
            var authoring = app.MapGroup("/api/authoring").RequireAuthorization(policy => policy.RequireRole("Publisher"));
            authoring.MapPost("/validate", (JsonElement source) =>
            {
                var result = ContentEngine.Compile(source.GetRawText());
                return new ValidationResponseDto(result.Success, result.Hash, result.Diagnostics, result.Pack?.Questions.Length, result.Pack?.Lessons.Length);
            });
            authoring.MapPost("/publish", async (JsonElement source, AppDb db, ClaimsPrincipal user) =>
            {
                var result = ContentEngine.Compile(source.GetRawText());
                if (!result.Success) return Results.BadRequest(new ApiErrorResponse("Content validation failed."));
                var p = result.Pack!;
                if (await db.Packs.AnyAsync(r => r.PackId == p.Id && r.Version == p.Version)) return Results.Conflict(new ApiErrorResponse("This release already exists. Publish a new version."));
                var release = new PackRelease { PackId = p.Id, Version = p.Version, ContentJson = Json.Write(p), Hash = result.Hash };
                db.Packs.Add(release); db.Audit.Add(new() { ActorId = UserId(user), Action = "pack.published", ResourceId = release.Id }); await db.SaveChangesAsync();
                return Results.Ok(new PublishResponseDto(p.Id, p.Version));
            });
        }
    }
}
