using Application;
using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Workers;

public sealed class SchedulerWorker(IServiceProvider provider, IInternalWorkflowQueue queue, IClock clock, ILogger<SchedulerWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = provider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var due = await db.WorkflowInstances
                    .Where(x => x.Status == WorkflowInstanceStatus.Waiting && x.NextRunAtUtc <= clock.UtcNow)
                    .Select(x => x.Id)
                    .Take(100)
                    .ToListAsync(stoppingToken);
                foreach (var id in due) await queue.EnqueueAsync(id, stoppingToken);

                var heartbeatTimeout = clock.UtcNow.AddMinutes(-5);
                var stuck = await db.WorkflowInstances
                    .Where(x => x.Status == WorkflowInstanceStatus.Running && x.StartedAtUtc < heartbeatTimeout)
                    .Select(x => x.Id)
                    .Take(100)
                    .ToListAsync(stoppingToken);
                foreach (var id in stuck) await queue.EnqueueAsync(id, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "SchedulerWorker failed"); }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
