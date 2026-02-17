using System.Diagnostics;
using System.Text.Json;
using Application;
using Domain;
using Infrastructure.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Execution;

public sealed class WorkflowExecutor(IServiceProvider sp, ILogger<WorkflowExecutor> logger) : IWorkflowExecutor
{
    public async Task ExecuteAsync(Guid workflowInstanceId, CancellationToken ct)
    {
        using var executeActivity = WorkflowTelemetry.Activity.StartActivity("Workflow.Execute", ActivityKind.Internal);
        executeActivity?.SetTag("instance.id", workflowInstanceId);

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lockManager = scope.ServiceProvider.GetRequiredService<ILockManager>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var handlers = scope.ServiceProvider.GetServices<IStepHandler>().ToDictionary(x => x.StepType, StringComparer.OrdinalIgnoreCase);
        var evaluator = scope.ServiceProvider.GetRequiredService<IConditionEvaluator>();
        var broadcaster = scope.ServiceProvider.GetRequiredService<IWorkflowEventBroadcaster>();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxWriter>();

        await using var lockHandle = await lockManager.TryAcquireAsync(workflowInstanceId.ToString("N"), TimeSpan.FromMinutes(2), ct);
        if (lockHandle is null) return;

        var instance = await db.WorkflowInstances.FirstOrDefaultAsync(x => x.Id == workflowInstanceId, ct);
        if (instance is null || instance.Status is WorkflowInstanceStatus.Completed or WorkflowInstanceStatus.DeadLettered) return;

        var version = await db.WorkflowVersions.FirstAsync(x => x.WorkflowDefinitionId == instance.WorkflowDefinitionId && x.VersionNumber == instance.WorkflowVersionNumber, ct);
        var dsl = WorkflowDslParser.Parse(version.DefinitionJson);

        if (instance.Status == WorkflowInstanceStatus.Waiting && instance.NextRunAtUtc > clock.UtcNow) return;
        instance.Start(clock.UtcNow);
        outbox.Add("WorkflowInstanceStarted", new { instance.Id, instance.CorrelationId, at = clock.UtcNow });
        await broadcaster.PublishInstanceUpdateAsync(instance, "instanceUpdated", ct);
        await db.SaveChangesAsync(ct);

        var stepId = instance.CurrentStepId ?? dsl.Steps.First().Id;
        while (!ct.IsCancellationRequested && stepId is not null)
        {
            var step = dsl.Steps.First(x => x.Id == stepId);
            var policy = step.RetryPolicy ?? new RetryPolicyDsl();
            var data = JsonSerializer.Deserialize<Dictionary<string, object?>>(instance.ContextJson) ?? new();
            var execution = new StepExecution
            {
                WorkflowInstanceId = instance.Id,
                StepId = stepId,
                StepType = ParseType(step.Type),
                InputJson = instance.ContextJson
            };
            execution.StartAttempt(clock.UtcNow);
            db.StepExecutions.Add(execution);
            outbox.Add("StepStarted", new { instance.Id, stepId, execution.Attempt, at = clock.UtcNow });
            await broadcaster.PublishStepUpdateAsync(instance.Id, execution, "stepStarted", ct);
            await db.SaveChangesAsync(ct);

            using var stepActivity = WorkflowTelemetry.Activity.StartActivity("Step.Execute", ActivityKind.Internal);
            stepActivity?.SetTag("workflow.name", dsl.Name);
            stepActivity?.SetTag("instance.id", instance.Id);
            stepActivity?.SetTag("step.id", step.Id);
            stepActivity?.SetTag("step.type", step.Type);

            StepResult result;
            try
            {
                if (step is ConditionStepDsl condition)
                {
                    var branch = evaluator.Evaluate(condition.Expression, data) ? condition.TrueNext : condition.FalseNext;
                    result = new StepResult.Skipped(branch);
                }
                else
                {
                    var handler = handlers[step.Type];
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    if (step is AiStepDsl aiStep) timeoutCts.CancelAfter(TimeSpan.FromSeconds(aiStep.TimeoutSeconds));
                    if (step is HttpStepDsl httpStep) timeoutCts.CancelAfter(TimeSpan.FromSeconds(httpStep.TimeoutSeconds));
                    result = await handler.HandleAsync(new StepContext(instance, dsl, step, data, clock), timeoutCts.Token);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Step {StepId} failed", step.Id);
                result = new StepResult.Failed(ex.Message, true);
            }

            var now = clock.UtcNow;
            if (result is StepResult.Succeeded succeeded)
            {
                if (succeeded.Output is not null) data[$"step_{step.Id}"] = succeeded.Output;
                instance.ContextJson = JsonSerializer.Serialize(data);
                execution.Succeed(now, JsonSerializer.Serialize(succeeded.Output));
                stepId = succeeded.NextStepId ?? step.Next;
                instance.CurrentStepId = stepId;
                outbox.Add("StepSucceeded", new { instance.Id, stepId = step.Id, at = now });
                await broadcaster.PublishStepUpdateAsync(instance.Id, execution, "stepSucceeded", ct);

                if (stepId is null)
                {
                    instance.Complete(now);
                    WorkflowTelemetry.CompletedTotal.Add(1);
                    outbox.Add("WorkflowCompleted", new { instance.Id, at = now });
                    await broadcaster.PublishInstanceUpdateAsync(instance, "instanceCompleted", ct);
                }
            }
            else if (result is StepResult.Skipped skipped)
            {
                execution.Succeed(now, "{\"skipped\":true}");
                stepId = skipped.NextStepId;
                instance.CurrentStepId = stepId;
                await broadcaster.PublishStepUpdateAsync(instance.Id, execution, "stepSucceeded", ct);
            }
            else if (result is StepResult.Waiting waiting)
            {
                execution.WaitUntil(waiting.NextRunAtUtc);
                instance.MarkWaiting(waiting.NextRunAtUtc);
                instance.CurrentStepId = step.Id;
                outbox.Add("StepWaiting", new { instance.Id, stepId = step.Id, at = waiting.NextRunAtUtc });
                await broadcaster.PublishStepUpdateAsync(instance.Id, execution, "stepWaiting", ct);
                stepId = null;
            }
            else if (result is StepResult.Failed failed)
            {
                execution.Fail(now, failed.Error);
                var attempts = await db.StepExecutions.CountAsync(x => x.WorkflowInstanceId == instance.Id && x.StepId == step.Id, ct);
                if (failed.Retryable && attempts < policy.MaxAttempts)
                {
                    var nextRetry = RetryCalculator.Next(policy, attempts, now);
                    execution.WaitUntil(nextRetry);
                    instance.MarkWaiting(nextRetry);
                    instance.CurrentStepId = step.Id;
                    WorkflowTelemetry.StepRetriesTotal.Add(1);
                    outbox.Add("StepRetryScheduled", new { instance.Id, stepId = step.Id, attempts, nextRetry });
                    await broadcaster.PublishStepUpdateAsync(instance.Id, execution, "stepWaiting", ct);
                }
                else
                {
                    instance.Fail(failed.Error);
                    instance.MoveToDeadLetter(failed.Error);
                    db.DeadLetterMessages.Add(new DeadLetterMessage
                    {
                        WorkflowInstanceId = instance.Id,
                        Reason = failed.Error,
                        PayloadJson = instance.ContextJson,
                        FailedAtUtc = now
                    });
                    WorkflowTelemetry.DeadlettersTotal.Add(1);
                    outbox.Add("WorkflowDeadLettered", new { instance.Id, reason = failed.Error, at = now });
                    await broadcaster.PublishInstanceUpdateAsync(instance, "instanceFailed", ct);
                    stepId = null;
                }
            }

            if (execution.DurationMs is not null)
                WorkflowTelemetry.StepDurationMs.Record(execution.DurationMs.Value, KeyValuePair.Create<string, object?>("step_id", step.Id));

            await db.SaveChangesAsync(ct);
        }
    }

    private static StepType ParseType(string type) => type.ToLowerInvariant() switch
    {
        "ai" => StepType.Ai,
        "http" => StepType.Http,
        "condition" => StepType.Condition,
        "delay" => StepType.Delay,
        _ => StepType.QueuePublish
    };
}
