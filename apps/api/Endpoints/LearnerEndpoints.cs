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
                var id = UserId(user);
                var attempts = await db.Attempts.Where(a => a.UserId == id).ToListAsync();
                var enrollments = await db.Enrollments.Where(e => e.UserId == id)
                    .Select(e => new EnrollmentExportDto(e.PackId, e.Status, e.EnrolledAt, e.LastActivityAt)).ToArrayAsync();
                var lessons = await db.LessonProgress.Where(p => p.UserId == id)
                    .Select(p => new LessonProgressExportDto(p.PackId, p.LessonId, p.CompletedAt, p.ContentHash)).ToArrayAsync();
                var evidence = await db.Evidence.Where(e => e.UserId == id).OrderBy(e => e.Id)
                    .Select(e => new EvidenceExportDto(e.PackId, e.ReleaseId, e.AttemptId, e.QuestionId, e.FamilyId, e.ObjectiveIds,
                        e.Source, e.Answered, e.FullyCorrect, e.Earned, e.Possible, e.At)).ToArrayAsync();
                var export = new LearnerExportDto(service.Now, await learning.Dashboard(id), attempts.Select(service.View).ToArray(), enrollments, lessons, evidence);
                return Results.File(System.Text.Encoding.UTF8.GetBytes(Json.Write(export)), "application/json", "learnforge-history.json");
            });
            me.MapDelete("/account", async ([FromBody] DeleteAccountRequest request, ClaimsPrincipal principal, UserManager<User> users, SignInManager<User> signIn, AppDb db) =>
            {
                var user = (await users.GetUserAsync(principal))!;
                if (!await users.CheckPasswordAsync(user, request.Password)) return Results.Unauthorized();
                var audit = await db.Audit.Where(a => a.ActorId == user.Id).ToListAsync(); db.Audit.RemoveRange(audit);
                await db.SaveChangesAsync();
                var deleted = await users.DeleteAsync(user);
                if (!deleted.Succeeded) return Results.Problem("Account deletion failed.");
                await signIn.SignOutAsync(); return Results.NoContent();
            });
        }
    }
}
