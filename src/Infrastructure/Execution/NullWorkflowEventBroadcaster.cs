using Application;
using Domain;

namespace Infrastructure.Execution;

public sealed class NullWorkflowEventBroadcaster : IWorkflowEventBroadcaster
{
    public Task PublishInstanceUpdateAsync(WorkflowInstance instance, string eventName, CancellationToken ct) => Task.CompletedTask;
    public Task PublishStepUpdateAsync(Guid instanceId, StepExecution step, string eventName, CancellationToken ct) => Task.CompletedTask;
}
