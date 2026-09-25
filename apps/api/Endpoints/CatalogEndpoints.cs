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
            app.MapGet("/api/catalog", async (AppDb db, ReleaseCache releases) =>
            {
                var courses = new List<CatalogSummaryDto>();
                foreach (var releaseId in await releases.LatestIds(db))
                {
                    var p = (await releases.Get(releaseId)).Pack;
                    courses.Add(new(p.Id, p.Title, p.Description, p.Version, p.Lessons.Length, p.Questions.Length, p.Objectives.Length));
                }
                return courses.ToArray();
            });
            app.MapGet("/api/catalog/{id}", async (string id, AppDb db, ClaimsPrincipal user, ReleaseCache releases) =>
            {
                var release = await releases.Latest(db, id);
                if (release is null) return Results.NotFound();
                var p = release.Pack;
                var completed = user.Identity?.IsAuthenticated == true
                    ? (await db.LessonProgress.Where(c => c.UserId == UserId(user) && c.PackId == id).Select(c => c.LessonId).ToArrayAsync()).Where(release.LessonHashes.ContainsKey).ToArray()
                    : [];
                return Results.Ok(new CourseCatalogDto(p.Id, p.Title, p.Description, p.Version, p.License, p.Objectives,
                    p.Lessons, p.Blueprints, p.Sources, p.Readiness ?? new(), completed, p.Questions.Length));
            });
        }
    }
}
