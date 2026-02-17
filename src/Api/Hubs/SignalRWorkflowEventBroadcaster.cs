using Application;
using Domain;
using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs;

public sealed class SignalRWorkflowEventBroadcaster(IHubContext<WorkflowHub> hubContext) : IWorkflowEventBroadcaster
{
    public Task PublishInstanceUpdateAsync(WorkflowInstance instance, string eventName, CancellationToken ct)
        => hubContext.Clients.All.SendAsync(eventName, new
        {
            instanceId = instance.Id,
            instance.Status,
            instance.CurrentStepId,
            instance.CorrelationId,
            instance.StartedAtUtc,
            instance.CompletedAtUtc,
            instance.LastError
        }, ct);

    public Task PublishStepUpdateAsync(Guid instanceId, StepExecution step, string eventName, CancellationToken ct)
        => hubContext.Clients.All.SendAsync(eventName, new
        {
            instanceId,
            step.StepId,
            step.Status,
            step.Attempt,
            step.StartedAtUtc,
            step.EndedAtUtc,
            step.Error,
            step.NextRunAtUtc
        }, ct);
}
