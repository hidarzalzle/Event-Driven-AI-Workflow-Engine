using System.Security.Cryptography;
using System.Text;
using Api.Hubs;
using Application;
using Domain;
using Infrastructure;
using Infrastructure.Observability;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(StartWorkflowCommand).Assembly));
builder.Services.AddSignalR();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<IWorkflowEventBroadcaster, SignalRWorkflowEventBroadcaster>();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("mini-n8n"))
    .WithTracing(t =>
    {
        t.AddSource(WorkflowTelemetry.Activity.Name)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddOtlpExporter();
        if (builder.Environment.IsDevelopment()) t.AddConsoleExporter();
    })
    .WithMetrics(m =>
    {
        m.AddMeter(WorkflowTelemetry.Meter.Name)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddOtlpExporter();
        if (builder.Environment.IsDevelopment()) m.AddConsoleExporter();
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
await app.Services.SeedAsync();

app.UseSwagger();
app.UseSwaggerUI();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapHub<WorkflowHub>("/hubs/workflows");

app.MapPost("/api/workflows", async (AppDbContext db, CreateWorkflowRequest req, CancellationToken ct) =>
{
    _ = WorkflowDslParser.Parse(req.DefinitionJson);
    var def = new WorkflowDefinition { Name = req.Name, Description = req.Description, TriggerKey = req.TriggerKey, CurrentVersion = 1 };
    def.Versions.Add(new WorkflowVersion
    {
        VersionNumber = 1,
        DefinitionJson = req.DefinitionJson,
        DefinitionHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(req.DefinitionJson)))
    });
    db.WorkflowDefinitions.Add(def);
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/workflows/{def.Id}", new { def.Id });
});

app.MapPost("/api/workflows/{id:guid}/versions", async (Guid id, VersionRequest req, AppDbContext db, CancellationToken ct) =>
{
    _ = WorkflowDslParser.Parse(req.DefinitionJson);
    var def = await db.WorkflowDefinitions.FirstAsync(x => x.Id == id, ct);
    var nextVersion = def.CurrentVersion + 1;
    def.CurrentVersion = nextVersion;
    db.WorkflowVersions.Add(new WorkflowVersion
    {
        WorkflowDefinitionId = id,
        VersionNumber = nextVersion,
        DefinitionJson = req.DefinitionJson,
        DefinitionHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(req.DefinitionJson)))
    });
    await db.SaveChangesAsync(ct);
    return Results.Ok(new { version = nextVersion });
});

app.MapGet("/api/workflows/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
    Results.Ok(await db.WorkflowDefinitions.Include(x => x.Versions).FirstAsync(x => x.Id == id, ct)));

app.MapGet("/api/workflows/{id:guid}/versions", async (Guid id, AppDbContext db, CancellationToken ct) =>
    Results.Ok(await db.WorkflowVersions.Where(x => x.WorkflowDefinitionId == id).OrderBy(x => x.VersionNumber).ToListAsync(ct)));

app.MapGet("/api/workflows/{id:guid}/dsl", async (Guid id, AppDbContext db, CancellationToken ct) =>
{
    var def = await db.WorkflowDefinitions.FirstAsync(x => x.Id == id, ct);
    var version = await db.WorkflowVersions.FirstAsync(x => x.WorkflowDefinitionId == id && x.VersionNumber == def.CurrentVersion, ct);
    return Results.Ok(WorkflowDslParser.Parse(version.DefinitionJson));
});

app.MapPost("/api/webhooks/{workflowKey}", async (string workflowKey, HttpContext httpContext, IMediator mediator, AppDbContext db, CancellationToken ct) =>
{
    if (!httpContext.Request.Headers.TryGetValue("Idempotency-Key", out var header) || string.IsNullOrWhiteSpace(header.FirstOrDefault()))
        return Results.BadRequest(new { error = "Idempotency-Key header is required." });

    var idempotencyKey = header.First()!;
    var payload = await new StreamReader(httpContext.Request.Body).ReadToEndAsync(ct);
    if (string.IsNullOrWhiteSpace(payload)) payload = "{}";

    var definition = await db.WorkflowDefinitions.FirstOrDefaultAsync(x => x.TriggerKey == workflowKey, ct);
    if (definition is null) return Results.NotFound(new { error = "Workflow trigger not found." });

    var instanceId = await mediator.Send(new StartWorkflowCommand(
        definition.Id,
        workflowKey,
        idempotencyKey,
        payload,
        httpContext.TraceIdentifier), ct);

    return Results.Accepted($"/api/instances/{instanceId}", new { instanceId, correlationId = httpContext.TraceIdentifier });
});

app.MapPost("/api/workflows/{id:guid}/start", async (Guid id, ManualStartRequest request, IMediator mediator, CancellationToken ct) =>
{
    var instanceId = await mediator.Send(new StartWorkflowCommand(id, request.TriggerKey, request.IdempotencyKey, request.ContextJson, request.CorrelationId ?? Guid.NewGuid().ToString("N")), ct);
    return Results.Accepted($"/api/instances/{instanceId}", new { instanceId });
});

app.MapGet("/api/instances", async (AppDbContext db, string? status, int take, CancellationToken ct) =>
{
    var query = db.WorkflowInstances.AsQueryable();
    if (Enum.TryParse<WorkflowInstanceStatus>(status, true, out var s)) query = query.Where(x => x.Status == s);
    return Results.Ok(await query.OrderByDescending(x => x.StartedAtUtc).Take(take > 0 ? take : 50).ToListAsync(ct));
});

app.MapGet("/api/instances/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
    Results.Ok(await db.WorkflowInstances.FirstAsync(x => x.Id == id, ct)));

app.MapGet("/api/instances/{id:guid}/steps", async (Guid id, AppDbContext db, CancellationToken ct) =>
    Results.Ok(await db.StepExecutions.Where(x => x.WorkflowInstanceId == id).OrderBy(x => x.StartedAtUtc).ToListAsync(ct)));

app.MapGet("/api/deadletters", async (AppDbContext db, CancellationToken ct) =>
    Results.Ok(await db.DeadLetterMessages.OrderByDescending(x => x.FailedAtUtc).Take(100).ToListAsync(ct)));

app.MapPost("/api/instances/{id:guid}/replay", async (Guid id, ReplayRequest request, AppDbContext db, IInternalWorkflowQueue queue, CancellationToken ct) =>
{
    var instance = await db.WorkflowInstances.FirstOrDefaultAsync(x => x.Id == id, ct);
    if (instance is null) return Results.NotFound();

    var failedStep = await db.StepExecutions
        .Where(x => x.WorkflowInstanceId == id && x.Status == StepExecutionStatus.Failed)
        .OrderByDescending(x => x.StartedAtUtc)
        .FirstOrDefaultAsync(ct);

    if (request.Mode.Equals("from_start", StringComparison.OrdinalIgnoreCase))
        instance.PrepareReplay(null, DateTime.UtcNow);
    else if (request.Mode.Equals("from_failed_step", StringComparison.OrdinalIgnoreCase))
        instance.PrepareReplay(failedStep?.StepId, DateTime.UtcNow);
    else
        return Results.BadRequest(new { error = "mode must be from_start or from_failed_step" });

    await db.SaveChangesAsync(ct);
    await queue.EnqueueAsync(id, ct);

    return Results.Accepted($"/api/instances/{id}", new { replayed = true, mode = request.Mode });
});

app.Run();

public partial class Program;
public record CreateWorkflowRequest(string Name, string Description, string TriggerKey, string DefinitionJson);
public record VersionRequest(string DefinitionJson);
public record ManualStartRequest(string TriggerKey, string IdempotencyKey, string ContextJson, string? CorrelationId);
public record ReplayRequest(string Mode);
