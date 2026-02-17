using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DeadLetterMessages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                FailedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_DeadLetterMessages", x => x.Id));

        migrationBuilder.CreateTable(
            name: "OutboxMessages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                Attempts = table.Column<int>(type: "int", nullable: false),
                LastError = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_OutboxMessages", x => x.Id));

        migrationBuilder.CreateTable(
            name: "WorkflowDefinitions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                TriggerKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CurrentVersion = table.Column<int>(type: "int", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_WorkflowDefinitions", x => x.Id));

        migrationBuilder.CreateTable(
            name: "WorkflowInstances",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                WorkflowVersionNumber = table.Column<int>(type: "int", nullable: false),
                TriggerType = table.Column<int>(type: "int", nullable: false),
                TriggerKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                IdempotencyKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                CurrentStepId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Attempt = table.Column<int>(type: "int", nullable: false),
                StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CorrelationId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ContextJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                NextRunAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_WorkflowInstances", x => x.Id));

        migrationBuilder.CreateTable(
            name: "WorkflowVersions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VersionNumber = table.Column<int>(type: "int", nullable: false),
                DefinitionJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                DefinitionHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WorkflowVersions", x => x.Id);
                table.ForeignKey("FK_WorkflowVersions_WorkflowDefinitions_WorkflowDefinitionId", x => x.WorkflowDefinitionId, "WorkflowDefinitions", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "StepExecutions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StepId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                StepType = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                Attempt = table.Column<int>(type: "int", nullable: false),
                StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                EndedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                DurationMs = table.Column<long>(type: "bigint", nullable: true),
                InputJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                OutputJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                NextRunAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_StepExecutions", x => x.Id));

        migrationBuilder.CreateIndex(name: "IX_WorkflowDefinitions_Name", table: "WorkflowDefinitions", column: "Name", unique: true);
        migrationBuilder.CreateIndex(name: "IX_WorkflowInstances_Status_NextRunAtUtc", table: "WorkflowInstances", columns: new[] { "Status", "NextRunAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_WorkflowInstances_TriggerKey_IdempotencyKey", table: "WorkflowInstances", columns: new[] { "TriggerKey", "IdempotencyKey" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_WorkflowVersions_WorkflowDefinitionId", table: "WorkflowVersions", column: "WorkflowDefinitionId");
        migrationBuilder.CreateIndex(name: "IX_StepExecutions_WorkflowInstanceId_StepId_Status", table: "StepExecutions", columns: new[] { "WorkflowInstanceId", "StepId", "Status" });
        migrationBuilder.CreateIndex(name: "IX_StepExecutions_WorkflowInstanceId_StartedAtUtc", table: "StepExecutions", columns: new[] { "WorkflowInstanceId", "StartedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("DeadLetterMessages");
        migrationBuilder.DropTable("OutboxMessages");
        migrationBuilder.DropTable("StepExecutions");
        migrationBuilder.DropTable("WorkflowVersions");
        migrationBuilder.DropTable("WorkflowInstances");
        migrationBuilder.DropTable("WorkflowDefinitions");
    }
}
