using Application;
using Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IApplicationDbContext
{
    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();
    public DbSet<WorkflowVersion> WorkflowVersions => Set<WorkflowVersion>();
    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();
    public DbSet<StepExecution> StepExecutions => Set<StepExecution>();
    public DbSet<DeadLetterMessage> DeadLetterMessages => Set<DeadLetterMessage>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<WorkflowDefinition>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.HasIndex(x => x.Name).IsUnique();
            e.HasMany(x => x.Versions).WithOne().HasForeignKey(x => x.WorkflowDefinitionId);
        });

        b.Entity<WorkflowVersion>();

        b.Entity<WorkflowInstance>(e =>
        {
            e.Property(x => x.TriggerKey).HasMaxLength(200);
            e.Property(x => x.IdempotencyKey).HasMaxLength(200);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => new { x.TriggerKey, x.IdempotencyKey }).IsUnique();
            e.HasIndex(x => new { x.Status, x.NextRunAtUtc });
        });

        b.Entity<StepExecution>(e =>
        {
            e.Property(x => x.StepId).HasMaxLength(200);
            e.HasIndex(x => new { x.WorkflowInstanceId, x.StepId, x.Status });
            e.HasIndex(x => new { x.WorkflowInstanceId, x.StartedAtUtc });
        });

        b.Entity<OutboxMessage>(e =>
        {
            e.HasIndex(x => new { x.PublishedAtUtc, x.OccurredAtUtc });
        });
    }
}
