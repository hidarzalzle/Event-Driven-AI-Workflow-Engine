using Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application;

public class StartWorkflowHandler(IApplicationDbContext db, IIdempotencyStore idempotency, IInternalWorkflowQueue queue, IClock clock) : IRequestHandler<StartWorkflowCommand, Guid>
{
    public async Task<Guid> Handle(StartWorkflowCommand request, CancellationToken ct)
    {
        var dedupeKey = $"{request.TriggerKey}:{request.IdempotencyKey}";
        var firstSeen = await idempotency.TryBeginAsync(dedupeKey, TimeSpan.FromHours(6), ct);

        var existing = await db.WorkflowInstances.FirstOrDefaultAsync(
            x => x.TriggerKey == request.TriggerKey && x.IdempotencyKey == request.IdempotencyKey,
            ct);

        if (existing is not null)
        {
            if (existing.Status is WorkflowInstanceStatus.Pending or WorkflowInstanceStatus.Waiting or WorkflowInstanceStatus.Running)
                await queue.EnqueueAsync(existing.Id, ct);
            return existing.Id;
        }

        if (!firstSeen)
            throw new InvalidOperationException("Duplicate idempotency key detected.");

        var definition = await db.WorkflowDefinitions.FirstAsync(x => x.Id == request.WorkflowDefinitionId, ct);
        var instance = new WorkflowInstance
        {
            WorkflowDefinitionId = definition.Id,
            WorkflowVersionNumber = definition.CurrentVersion,
            TriggerType = TriggerType.Webhook,
            TriggerKey = request.TriggerKey,
            IdempotencyKey = request.IdempotencyKey,
            CorrelationId = request.CorrelationId,
            ContextJson = request.ContextJson
        };

        instance.Start(clock.UtcNow);
        db.WorkflowInstances.Add(instance);
        await db.SaveChangesAsync(ct);
        await queue.EnqueueAsync(instance.Id, ct);
        return instance.Id;
    }
}
