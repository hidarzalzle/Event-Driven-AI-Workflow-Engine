namespace Domain;

public class DeadLetterMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkflowInstanceId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime FailedAtUtc { get; set; } = DateTime.UtcNow;
    public string PayloadJson { get; set; } = "{}";
}

public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = "{}";
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAtUtc { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}
