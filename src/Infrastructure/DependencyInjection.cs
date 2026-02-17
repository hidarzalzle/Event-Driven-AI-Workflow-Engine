using System.Security.Cryptography;
using System.Text;
using Application;
using Domain;
using Infrastructure.Execution;
using Infrastructure.Handlers;
using Infrastructure.Messaging;
using Infrastructure.Redis;
using Infrastructure.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(o => o.UseSqlServer(config.GetConnectionString("SqlServer")));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IInternalWorkflowQueue, ChannelWorkflowQueue>();
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(config.GetConnectionString("Redis")!));
        services.AddSingleton<IIdempotencyStore, RedisIdempotencyStore>();
        services.AddSingleton<ILockManager, RedisLockManager>();
        services.AddSingleton<IConditionEvaluator, NCalcConditionEvaluator>();
        services.AddSingleton<IAiClient, MockAiClient>();
        services.AddSingleton<IQueuePublisher, RabbitQueuePublisher>();

        services.AddScoped<IWorkflowExecutor, WorkflowExecutor>();
        services.AddScoped<IOutboxWriter, EfOutboxWriter>();
        services.TryAddSingleton<IWorkflowEventBroadcaster, NullWorkflowEventBroadcaster>();
        services.AddScoped<IStepHandler, HttpStepHandler>();
        services.AddScoped<IStepHandler, AiStepHandler>();
        services.AddScoped<IStepHandler, DelayStepHandler>();
        services.AddScoped<IStepHandler, QueuePublishStepHandler>();

        services.AddHttpClient("workflow-http");

        services.AddHostedService<WorkflowQueueWorker>();
        services.AddHostedService<SchedulerWorker>();
        services.AddHostedService<OutboxPublisherWorker>();

        return services;
    }

    public static async Task SeedAsync(this IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        if (await db.WorkflowDefinitions.AnyAsync()) return;

        var triageDsl = "{\"name\":\"ai-triage-flow\",\"trigger\":{\"type\":\"webhook\",\"route\":\"ai-triage\",\"secret\":\"secret-123\"},\"steps\":[{\"id\":\"ai1\",\"type\":\"ai\",\"provider\":\"mock\",\"model\":\"gpt-4o-mini\",\"promptTemplate\":\"Classify request\",\"timeoutSeconds\":30,\"retryPolicy\":{\"maxAttempts\":3,\"initialDelayMs\":500},\"next\":\"cond1\"},{\"id\":\"cond1\",\"type\":\"condition\",\"expression\":\"1==1\",\"trueNext\":\"http1\",\"falseNext\":\"q1\"},{\"id\":\"http1\",\"type\":\"http\",\"method\":\"POST\",\"url\":\"https://postman-echo.com/post\",\"timeoutSeconds\":20,\"retryPolicy\":{\"maxAttempts\":3,\"initialDelayMs\":1000},\"next\":\"q1\"},{\"id\":\"q1\",\"type\":\"queue_publish\",\"topic\":\"workflow.events\",\"routingKey\":\"triage.done\",\"payloadTemplate\":\"{\\\"done\\\":true}\"}]}";
        var delayedDsl = "{\"name\":\"delayed-followup\",\"trigger\":{\"type\":\"webhook\",\"route\":\"delayed-followup\"},\"steps\":[{\"id\":\"d1\",\"type\":\"delay\",\"delaySeconds\":10,\"next\":\"h1\"},{\"id\":\"h1\",\"type\":\"http\",\"method\":\"POST\",\"url\":\"https://postman-echo.com/post\",\"timeoutSeconds\":20,\"retryPolicy\":{\"maxAttempts\":2,\"initialDelayMs\":1000}}]}";

        var triage = new WorkflowDefinition { Name = "ai-triage-flow", Description = "Webhook -> AI -> Condition -> HTTP -> Queue", TriggerKey = "ai-triage", CurrentVersion = 1 };
        triage.Versions.Add(new WorkflowVersion { VersionNumber = 1, DefinitionJson = triageDsl, DefinitionHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(triageDsl))) });

        var delayed = new WorkflowDefinition { Name = "delayed-followup", Description = "Webhook -> Delay -> HTTP", TriggerKey = "delayed-followup", CurrentVersion = 1 };
        delayed.Versions.Add(new WorkflowVersion { VersionNumber = 1, DefinitionJson = delayedDsl, DefinitionHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(delayedDsl))) });

        db.WorkflowDefinitions.AddRange(triage, delayed);
        await db.SaveChangesAsync();
    }
}
