using Microsoft.EntityFrameworkCore;

namespace LearnForge.Api.Services.Attempts;

public sealed class ExpiryWorker(IServiceScopeFactory scopes, ILogger<ExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDb>();
                var service = scope.ServiceProvider.GetRequiredService<AttemptService>();
                var expired = await db.Attempts.Where(a => a.Status == AttemptStatus.InProgress && a.Mode == AssessmentMode.Mock && a.Deadline <= service.Now).OrderBy(a => a.Deadline).Take(100).ToListAsync(stoppingToken);
                foreach (var attempt in expired) await service.Finish(attempt, true);
                await db.SaveChangesAsync(stoppingToken);
            }
            catch (DbUpdateConcurrencyException) { /* A submit/save won the race. Retry on the next sweep. */ }
            catch (Exception e) when (e is not OperationCanceledException) { logger.LogError("Expiry sweep failed: {Type}", e.GetType().Name); }
        }
    }
}
