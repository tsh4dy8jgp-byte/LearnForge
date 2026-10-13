using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static LearnForge.Api.Services.Identity.AccountIdentity;

namespace LearnForge.Api.Endpoints;

public static class FlashcardEndpoints
{
    extension(IEndpointRouteBuilder app)
    {
        public void MapFlashcards()
        {
            var cards = app.MapGroup("/api/me/courses/{id}/flashcards").RequireAuthorization();
            cards.MapGet("/", (string id, ClaimsPrincipal user, FlashcardService service) => service.Summary(UserId(user), id));
            cards.MapPost("/sessions", (string id, FlashcardStartRequest request, ClaimsPrincipal user, FlashcardService service) => service.Start(UserId(user), id, request));
            cards.MapGet("/sessions/{sessionId}", (string id, string sessionId, ClaimsPrincipal user, FlashcardService service) => service.Get(UserId(user), id, sessionId));
            cards.MapPost("/sessions/{sessionId}/reveal", (string id, string sessionId, FlashcardMutationRequest request, ClaimsPrincipal user, FlashcardService service) => service.Mutate(UserId(user), id, sessionId, "reveal", request));
            cards.MapPost("/sessions/{sessionId}/rate", (string id, string sessionId, FlashcardMutationRequest request, ClaimsPrincipal user, FlashcardService service) => service.Mutate(UserId(user), id, sessionId, "rate", request));
            cards.MapPost("/sessions/{sessionId}/finish", (string id, string sessionId, FlashcardMutationRequest request, ClaimsPrincipal user, FlashcardService service) => service.Mutate(UserId(user), id, sessionId, "finish", request));
            cards.MapPut("/suspension", (string id, FlashcardSuspendRequest request, ClaimsPrincipal user, FlashcardService service) => service.Suspend(UserId(user), id, request));
            app.MapGet("/api/catalog/{id}/flashcards/exports", async (string id, string? version, [AsParameters] FlashcardExportQuery query,
                AppDb db, ReleaseCache releases, IConfiguration config, CancellationToken cancellation) =>
            {
                ReleaseView? release;
                if (version is null) release = await releases.Latest(db, id);
                else
                {
                    var releaseId = await db.Packs.Where(p => p.PackId == id && p.Version == version).Select(p => p.Id).FirstOrDefaultAsync(cancellation);
                    release = releaseId is null ? null : await releases.Get(releaseId);
                }
                if (release is null) throw new DomainError(404, "Subject release not found.");
                return await Export(release.Pack, query.Options(), config, cancellation);
            });
            app.MapPost("/api/authoring/flashcards/exports", async (JsonElement source, [AsParameters] FlashcardExportQuery query,
                IConfiguration config, CancellationToken cancellation) =>
            {
                var compiled = ContentEngine.Compile(source.GetRawText());
                if (!compiled.Success) throw new DomainError(400, "Validate and repair the source before exporting flashcards.");
                return await Export(compiled.Pack!, query.Options(), config, cancellation);
            }).RequireAuthorization(policy => policy.RequireRole("Publisher"));
        }
    }

    private static async Task<IResult> Export(Pack pack, FlashcardExportOptions options, IConfiguration config, CancellationToken cancellation)
    {
        try
        {
            var artifact = await FlashcardExporter.Export(pack, options, config["Flashcards:Python"], cancellation);
            return Results.File(artifact.Bytes, artifact.ContentType, artifact.FileName);
        }
        catch (FlashcardExportException error) { throw new DomainError(error.Busy ? 429 : 422, error.Message); }
    }
}
