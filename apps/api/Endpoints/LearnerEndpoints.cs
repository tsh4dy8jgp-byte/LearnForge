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
            me.MapPut("/courses/{id}/lessons/{lessonId}", async (string id, string lessonId, ClaimsPrincipal user, LearningRecordService learning) =>
            {
                await learning.CompleteLesson(UserId(user), id, lessonId);
                return Results.NoContent();
            });
            me.MapGet("/courses/{id}", (string id, ClaimsPrincipal user, LearningRecordService learning) => learning.CourseProgress(UserId(user), id));
            me.MapPut("/enrollments/{id}", async (string id, EnrollmentRequest request, ClaimsPrincipal user, LearningRecordService learning) =>
            {
                await learning.SetEnrollment(UserId(user), id, request.Status);
                return Results.NoContent();
            });
            me.MapGet("/dashboard", async (ClaimsPrincipal user, AppDb db, AttemptService service, LearningRecordService learning) =>
            {
                foreach (var a in await db.Attempts.Where(a => a.UserId == UserId(user) && a.Status == AttemptStatus.InProgress).ToListAsync()) await service.Expire(a);
                return await learning.Dashboard(UserId(user));
            });
            me.MapGet("/export", async (ClaimsPrincipal user, AppDb db, AttemptService service, LearningRecordService learning) =>
            {
                var attempts = await db.Attempts.Where(a => a.UserId == UserId(user)).ToListAsync();
                var export = new LearnerExportDto(service.Now, await learning.Dashboard(UserId(user)), attempts.Select(service.View).ToArray());
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
