using Domain;
using Microsoft.EntityFrameworkCore;

namespace Application;

public interface IApplicationDbContext
{
    DbSet<WorkflowDefinition> WorkflowDefinitions { get; }
    DbSet<WorkflowVersion> WorkflowVersions { get; }
    DbSet<WorkflowInstance> WorkflowInstances { get; }
    DbSet<StepExecution> StepExecutions { get; }
    DbSet<DeadLetterMessage> DeadLetterMessages { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface IClock { DateTime UtcNow { get; } }
public interface ILockManager { Task<IAsyncDisposable?> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken ct); }
public interface IIdempotencyStore { Task<bool> TryBeginAsync(string key, TimeSpan ttl, CancellationToken ct); }
public interface IQueuePublisher { Task PublishAsync(string topic, string routingKey, string payload, CancellationToken ct); }
public interface IAiClient { Task<string> CompleteAsync(string provider, string model, string prompt, CancellationToken ct); }
public interface IInternalWorkflowQueue { ValueTask EnqueueAsync(Guid instanceId, CancellationToken ct); ValueTask<Guid> DequeueAsync(CancellationToken ct); }
public interface IWorkflowExecutor { Task ExecuteAsync(Guid workflowInstanceId, CancellationToken ct); }
public interface IConditionEvaluator { bool Evaluate(string expression, IDictionary<string, object?> data); }
public interface IWorkflowEventBroadcaster
{
    Task PublishInstanceUpdateAsync(WorkflowInstance instance, string eventName, CancellationToken ct);
    Task PublishStepUpdateAsync(Guid instanceId, StepExecution step, string eventName, CancellationToken ct);
}
public interface IOutboxWriter { void Add(string type, object payload); }

public interface IStepHandler
{
    string StepType { get; }
    Task<StepResult> HandleAsync(StepContext ctx, CancellationToken ct);
}

public record StepContext(WorkflowInstance Instance, WorkflowDsl Workflow, StepDsl Step, IDictionary<string, object?> Data, IClock Clock);

public abstract record StepResult
{
    public sealed record Succeeded(object? Output, string? NextStepId) : StepResult;
    public sealed record Waiting(DateTime NextRunAtUtc) : StepResult;
    public sealed record Failed(string Error, bool Retryable) : StepResult;
    public sealed record Skipped(string? NextStepId) : StepResult;
}

public static class RetryCalculator
{
    private static readonly Random Rng = new();

    public static DateTime Next(RetryPolicyDsl policy, int attempt, DateTime now)
    {
        var backoff = policy.InitialDelayMs * Math.Pow(policy.BackoffFactor <= 0 ? 2 : policy.BackoffFactor, Math.Max(0, attempt - 1));
        if (policy.Jitter)
        {
            var jitter = Rng.NextDouble() * 0.2 + 0.9;
            backoff *= jitter;
        }
        return now.AddMilliseconds(backoff);
    }
}
