namespace Domain;

public class WorkflowDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TriggerKey { get; set; } = string.Empty;
    public int CurrentVersion { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<WorkflowVersion> Versions { get; set; } = [];
}

public class WorkflowVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkflowDefinitionId { get; set; }
    public int VersionNumber { get; set; }
    public string DefinitionJson { get; set; } = "{}";
    public string DefinitionHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
