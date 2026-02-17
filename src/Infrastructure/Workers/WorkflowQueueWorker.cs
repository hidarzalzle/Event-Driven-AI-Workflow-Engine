using Application;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Workers;

public sealed class WorkflowQueueWorker(IInternalWorkflowQueue queue, IWorkflowExecutor executor, ILogger<WorkflowQueueWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var id = await queue.DequeueAsync(stoppingToken);
                await executor.ExecuteAsync(id, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "WorkflowQueueWorker failed"); }
        }
    }
}
