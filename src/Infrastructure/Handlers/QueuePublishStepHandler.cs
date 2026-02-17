using Application;

namespace Infrastructure.Handlers;

public sealed class QueuePublishStepHandler(IQueuePublisher queuePublisher) : IStepHandler
{
    public string StepType => "queue_publish";

    public async Task<StepResult> HandleAsync(StepContext ctx, CancellationToken ct)
    {
        var s = (QueuePublishStepDsl)ctx.Step;
        await queuePublisher.PublishAsync(s.Topic, s.RoutingKey, s.PayloadTemplate, ct);
        return new StepResult.Succeeded(new { queued = true }, s.Next);
    }
}
