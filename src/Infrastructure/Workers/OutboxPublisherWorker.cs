using Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Workers;

public sealed class OutboxPublisherWorker(IServiceProvider provider, IQueuePublisher publisher, ILogger<OutboxPublisherWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = provider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var pending = await db.OutboxMessages
                    .Where(x => x.PublishedAtUtc == null && x.Attempts < 10)
                    .OrderBy(x => x.OccurredAtUtc)
                    .Take(100)
                    .ToListAsync(stoppingToken);

                foreach (var m in pending)
                {
                    try
                    {
                        await publisher.PublishAsync("workflow.outbox", m.Type, m.Payload, stoppingToken);
                        m.PublishedAtUtc = DateTime.UtcNow;
                        m.LastError = null;
                    }
                    catch (Exception ex)
                    {
                        m.LastError = ex.Message;
                        m.Attempts += 1;
                        await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(30000, 500 * Math.Pow(2, m.Attempts))), stoppingToken);
                    }
                }

                await db.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Outbox worker error"); }

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }
}
