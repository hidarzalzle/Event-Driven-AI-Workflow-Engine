namespace Domain;

public class StepExecution
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkflowInstanceId { get; set; }
    public string StepId { get; set; } = string.Empty;
    public StepType StepType { get; set; }
    public StepExecutionStatus Status { get; private set; } = StepExecutionStatus.Pending;
    public int Attempt { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? EndedAtUtc { get; private set; }
    public long? DurationMs { get; private set; }
    public string? InputJson { get; set; }
    public string? OutputJson { get; private set; }
    public string? Error { get; private set; }
    public DateTime? NextRunAtUtc { get; private set; }

    public void StartAttempt(DateTime now)
    {
        Attempt += 1;
        Status = StepExecutionStatus.Running;
        StartedAtUtc = now;
    }

    public void Succeed(DateTime now, string? output)
    {
        Status = StepExecutionStatus.Succeeded;
        EndedAtUtc = now;
        DurationMs = (long)(now - StartedAtUtc).TotalMilliseconds;
        OutputJson = output;
        Error = null;
    }

    public void Fail(DateTime now, string error)
    {
        Status = StepExecutionStatus.Failed;
        EndedAtUtc = now;
        DurationMs = (long)(now - StartedAtUtc).TotalMilliseconds;
        Error = error;
    }

    public void WaitUntil(DateTime nextRunAtUtc)
    {
        Status = StepExecutionStatus.Waiting;
        NextRunAtUtc = nextRunAtUtc;
    }
}
