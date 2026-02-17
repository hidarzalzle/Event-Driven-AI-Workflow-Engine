using Domain;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Integration.Tests;

public class WebhookFlowIntegrationTests(TestWebAppFactory factory) : IClassFixture<TestWebAppFactory>
{
    [Fact]
    public async Task Webhook_ShouldRunWorkflow_ToCompleted()
    {
        using var client = factory.CreateClient();

        var dsl = new
        {
            name = "test-flow",
            trigger = new { type = "webhook", route = "test-flow" },
            steps = new object[]
            {
                new { id = "ai1", type = "ai", provider = "mock", model = "m", promptTemplate = "hello", timeoutSeconds = 5, next = "q1" },
                new { id = "q1", type = "queue_publish", topic = "t", routingKey = "r", payloadTemplate = "{}" }
            }
        };

        var createResp = await client.PostAsJsonAsync("/api/workflows", new
        {
            name = "test-flow",
            description = "integration",
            triggerKey = "test-flow",
            definitionJson = JsonSerializer.Serialize(dsl)
        });
        createResp.EnsureSuccessStatusCode();

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/test-flow")
        {
            Content = JsonContent.Create(new { message = "hello" })
        };
        req.Headers.Add("Idempotency-Key", $"it-{Guid.NewGuid():N}");

        var response = await client.SendAsync(req);
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Accepted);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        var instanceId = body!["instanceId"].GetGuid();

        WorkflowInstanceStatus? status = null;
        for (var i = 0; i < 30; i++)
        {
            await Task.Delay(200);
            var instance = await client.GetFromJsonAsync<WorkflowInstance>($"/api/instances/{instanceId}");
            status = instance!.Status;
            if (status == WorkflowInstanceStatus.Completed) break;
        }

        status.Should().Be(WorkflowInstanceStatus.Completed);

        var steps = await client.GetFromJsonAsync<List<StepExecution>>($"/api/instances/{instanceId}/steps");
        steps.Should().NotBeNull();
        steps!.Count.Should().BeGreaterOrEqualTo(2);
    }
}
