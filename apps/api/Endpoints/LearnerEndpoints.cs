using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static LearnForge.Api.Services.Identity.AccountIdentity;

namespace LearnForge.Api.Endpoints;

public static class LearnerEndpoints
{
    extension(IEndpointRouteBuilder app)
    {
        public void MapLearner()
        {
            var me = app.MapGroup("/api/me").RequireAuthorization();
            me.MapPut("/courses/{id}/lessons/{lessonId}", async (string id, string lessonId, ClaimsPrincipal user, AppDb db, ReleaseCache releases) =>
            {
                var release = await releases.Latest(db, id);
                if (release is null || !release.LessonHashes.TryGetValue(lessonId, out var hash)) return Results.NotFound();
                var progress = await db.LessonProgress.FindAsync(UserId(user), id, lessonId);
                if (progress is null) db.LessonProgress.Add(new() { UserId = UserId(user), PackId = id, LessonId = lessonId, ContentHash = hash });
                else progress.ContentHash = hash;
                await db.SaveChangesAsync();
                return Results.NoContent();
            });
            me.MapGet("/dashboard", async (ClaimsPrincipal user, AppDb db, AttemptService service) =>
            {
                foreach (var a in await db.Attempts.Where(a => a.UserId == UserId(user) && a.Status == AttemptStatus.InProgress).ToListAsync()) await service.Expire(a);
                return await Analytics.Dashboard(db, UserId(user), service.Now);
            });
            me.MapGet("/export", async (ClaimsPrincipal user, AppDb db, AttemptService service) =>
            {
                var attempts = await db.Attempts.Where(a => a.UserId == UserId(user)).ToListAsync();
                var export = new LearnerExportDto(service.Now, await Analytics.Dashboard(db, UserId(user), service.Now), attempts.Select(service.View).ToArray());
                return Results.File(System.Text.Encoding.UTF8.GetBytes(Json.Write(export)), "application/json", "learnforge-history.json");
            });
            me.MapDelete("/account", async ([FromBody] DeleteAccountRequest request, ClaimsPrincipal principal, UserManager<User> users, SignInManager<User> signIn, AppDb db) =>
            {
                var user = (await users.GetUserAsync(principal))!;
                if (request.Password.Length > 128 || !await users.CheckPasswordAsync(user, request.Password)) return Results.Unauthorized();
                var audit = await db.Audit.Where(a => a.ActorId == user.Id).ToListAsync(); db.Audit.RemoveRange(audit);
                await db.SaveChangesAsync();
                var deleted = await users.DeleteAsync(user);
                if (!deleted.Succeeded) return Results.Problem("Account deletion failed.");
                await signIn.SignOutAsync(); return Results.NoContent();
            });
        }
    }
}
