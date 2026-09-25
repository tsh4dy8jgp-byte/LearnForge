using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using static LearnForge.Api.Services.Identity.AccountIdentity;

namespace LearnForge.Api.Endpoints;

public static class CatalogEndpoints
{
    extension(IEndpointRouteBuilder app)
    {
        public void MapCatalog()
        {
            app.MapGet("/api/catalog", async (AppDb db) => (await db.Packs.OrderByDescending(p => p.PublishedAt).ToListAsync()).DistinctBy(p => p.PackId).Select(release =>
            {
                var p = Json.Read<Pack>(release.ContentJson);
                return new CatalogSummaryDto(p.Id, p.Title, p.Description, p.Version, p.Lessons.Length, p.Questions.Length, p.Objectives.Length);
            }).ToArray());
            app.MapGet("/api/catalog/{id}", async (string id, AppDb db, ClaimsPrincipal user) =>
            {
                var release = await db.Packs.Where(p => p.PackId == id).OrderByDescending(p => p.PublishedAt).FirstOrDefaultAsync();
                if (release is null) return Results.NotFound();
                var p = Json.Read<Pack>(release.ContentJson);
                var completed = user.Identity?.IsAuthenticated == true ? await db.Completions.Where(c => c.UserId == UserId(user) && c.ReleaseId == release.Id).Select(c => c.LessonId).ToArrayAsync() : [];
                return Results.Ok(new CourseCatalogDto(p.Id, p.Title, p.Description, p.Version, p.License, p.Objectives,
                    p.Lessons, p.Blueprints, p.Sources, p.Readiness ?? new(), completed, p.Questions.Length));
            });
        }
    }
}
