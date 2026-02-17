namespace Domain;

public class WorkflowInstance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkflowDefinitionId { get; set; }
    public int WorkflowVersionNumber { get; set; }
    public TriggerType TriggerType { get; set; }
    public string TriggerKey { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public WorkflowInstanceStatus Status { get; private set; } = WorkflowInstanceStatus.Pending;
    public string? CurrentStepId { get; set; }
    public int Attempt { get; set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string? LastError { get; private set; }
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");
    public string ContextJson { get; set; } = "{}";
    public DateTime? NextRunAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public void Start(DateTime now)
    {
        if (Status is WorkflowInstanceStatus.Completed)
            throw new InvalidOperationException("Completed workflow cannot be restarted.");

        Status = WorkflowInstanceStatus.Running;
        StartedAtUtc = StartedAtUtc == default ? now : StartedAtUtc;
        LastError = null;
    }

    public void MarkWaiting(DateTime nextRunAtUtc)
    {
        Status = WorkflowInstanceStatus.Waiting;
        NextRunAtUtc = nextRunAtUtc;
    }

    public void Complete(DateTime now)
    {
        Status = WorkflowInstanceStatus.Completed;
        CompletedAtUtc = now;
        NextRunAtUtc = null;
        LastError = null;
    }

    public void Fail(string reason)
    {
        Status = WorkflowInstanceStatus.Failed;
        LastError = reason;
    }

    public void MoveToDeadLetter(string reason)
    {
        Status = WorkflowInstanceStatus.DeadLettered;
        LastError = reason;
    }

    public void PrepareReplay(string? stepId, DateTime now)
    {
        CurrentStepId = stepId;
        LastError = null;
        CompletedAtUtc = null;
        Status = WorkflowInstanceStatus.Pending;
        NextRunAtUtc = now;
    }
}
