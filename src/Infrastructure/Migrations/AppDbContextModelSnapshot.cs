using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
partial class AppDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkflowDefinition>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<WorkflowVersion>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.WorkflowDefinitionId);
        });

        modelBuilder.Entity<WorkflowInstance>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.TriggerKey, x.IdempotencyKey }).IsUnique();
            b.HasIndex(x => new { x.Status, x.NextRunAtUtc });
        });

        modelBuilder.Entity<StepExecution>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.WorkflowInstanceId, x.StepId, x.Status });
            b.HasIndex(x => new { x.WorkflowInstanceId, x.StartedAtUtc });
        });

        modelBuilder.Entity<DeadLetterMessage>(b => b.HasKey(x => x.Id));
        modelBuilder.Entity<OutboxMessage>(b => b.HasKey(x => x.Id));
    }
}
