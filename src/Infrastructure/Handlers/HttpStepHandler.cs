using System.Net;
using System.Text;
using Application;
using Polly;
using Polly.Retry;

namespace Infrastructure.Handlers;

public sealed class HttpStepHandler(IHttpClientFactory factory) : IStepHandler
{
    public string StepType => "http";

    public async Task<StepResult> HandleAsync(StepContext ctx, CancellationToken ct)
    {
        var step = (HttpStepDsl)ctx.Step;
        var client = factory.CreateClient("workflow-http");
        var retryCodes = step.RetryPolicy?.RetryableStatusCodes ?? [408, 429, 500, 502, 503, 504];

        var pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = Math.Max(0, (step.RetryPolicy?.MaxAttempts ?? 1) - 1),
                Delay = TimeSpan.FromMilliseconds(step.RetryPolicy?.InitialDelayMs ?? 200),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = step.RetryPolicy?.Jitter ?? true,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .HandleResult(r => retryCodes.Contains((int)r.StatusCode))
            })
            .Build();

        try
        {
            var response = await pipeline.ExecuteAsync(async token =>
            {
                using var req = new HttpRequestMessage(new HttpMethod(step.Method), step.Url);
                if (step.Headers is not null)
                    foreach (var h in step.Headers)
                        req.Headers.TryAddWithoutValidation(h.Key, h.Value);
                if (!string.IsNullOrWhiteSpace(step.BodyTemplate))
                    req.Content = new StringContent(step.BodyTemplate, Encoding.UTF8, "application/json");
                return await client.SendAsync(req, token);
            }, ct);

            var content = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                var retryable = retryCodes.Contains((int)response.StatusCode) || response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
                return new StepResult.Failed($"HTTP {(int)response.StatusCode}: {content}", retryable);
            }

            return new StepResult.Succeeded(content, step.Next);
        }
        catch (Exception ex)
        {
            return new StepResult.Failed(ex.Message, true);
        }
    }
}
