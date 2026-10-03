using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

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
                    courses.Add(new(p.Id, p.Title, p.Description, p.Version, p.Lessons.Length, p.Questions.Length, p.Objectives.Length, p.Profile, p.Features));
                }
                return courses.ToArray();
            });
            app.MapGet("/api/catalog/{id}", async Task<Results<Ok<CourseCatalogDto>, NotFound>> (string id, AppDb db, ReleaseCache releases) =>
            {
                var release = await releases.Latest(db, id);
                if (release is null) return TypedResults.NotFound();
                var p = release.Pack;
                return TypedResults.Ok(CourseCatalogDto.From(p));
            });
        }
    }
}
