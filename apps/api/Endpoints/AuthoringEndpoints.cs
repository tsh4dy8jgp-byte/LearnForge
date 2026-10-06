using System.Security.Claims;
using System.Text.Json;
using System.Text;
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
            authoring.MapGet("/starters/{profile}", (string profile) =>
            {
                if (!Enum.TryParse<ProductProfile>(profile, true, out var parsed) || !Enum.IsDefined(parsed))
                    throw new DomainError(400, "Unknown starter profile.");
                return PackStarter.Create(parsed);
            });
            authoring.MapGet("/drafts", async (AppDb db, ClaimsPrincipal user) =>
                await db.Drafts.AsNoTracking().Where(d => d.OwnerId == UserId(user)).OrderByDescending(d => d.UpdatedAt)
                    .Select(d => new DraftSummaryDto(d.Id, d.Title, d.Revision, d.UpdatedAt)).ToArrayAsync());
            authoring.MapGet("/drafts/{id}", async (string id, AppDb db, ClaimsPrincipal user) =>
                DraftDto.From(await FindDraft(id, db, UserId(user))));
            authoring.MapPost("/drafts", async (DraftSaveRequest request, AppDb db, ClaimsPrincipal user, TimeProvider clock) =>
            {
                CheckDraft(request);
                if (request.Revision != 0) throw new DomainError(400, "New drafts must start at revision zero.");
                var draft = new AuthoringDraft { OwnerId = UserId(user), Title = request.Title.Trim(), Source = request.Source,
                    UpdatedAt = clock.GetUtcNow().UtcDateTime };
                db.Drafts.Add(draft);
                await db.SaveChangesAsync();
                return DraftDto.From(draft);
            });
            authoring.MapPut("/drafts/{id}", async (string id, DraftSaveRequest request, AppDb db, ClaimsPrincipal user, TimeProvider clock) =>
            {
                CheckDraft(request);
                var draft = await FindDraft(id, db, UserId(user));
                if (draft.Revision != request.Revision) throw new DomainError(409, "This draft changed in another window. Reload it or save your work as a copy.");
                draft.Title = request.Title.Trim(); draft.Source = request.Source;
                draft.Revision++; draft.UpdatedAt = clock.GetUtcNow().UtcDateTime;
                await db.SaveChangesAsync();
                return DraftDto.From(draft);
            });
            authoring.MapPost("/preview", (JsonElement source) =>
            {
                var result = ContentEngine.Compile(source.GetRawText());
                var pack = result.Success ? result.Pack : null;
                return new AuthoringPreviewDto(result.Success, result.Hash, result.Diagnostics,
                    pack is null ? null : CourseCatalogDto.From(pack),
                    pack?.Questions.Select(DeliveryQuestion.From).ToArray() ?? [], pack?.Scenarios ?? [],
                    pack is null ? [] : ContentLinter.Lint(pack));
            });
            authoring.MapPost("/validate", (JsonElement source) =>
            {
                var result = ContentEngine.Compile(source.GetRawText());
                return new ValidationResponseDto(result.Success, result.Hash, result.Diagnostics, result.Pack?.Questions.Length, result.Pack?.Lessons.Length,
                    result.Success ? ContentLinter.Lint(result.Pack!) : []);
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

    private static async Task<AuthoringDraft> FindDraft(string id, AppDb db, string owner) =>
        await db.Drafts.SingleOrDefaultAsync(d => d.Id == id && d.OwnerId == owner)
            ?? throw new DomainError(404, "Draft not found.");

    private static void CheckDraft(DraftSaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title)) throw new DomainError(400, "Give the draft a title.");
        if (Encoding.UTF8.GetByteCount(request.Source) > ContentEngine.MaxSourceBytes)
            throw new DomainError(400, "Draft source exceeds 2 MB.");
    }
}
