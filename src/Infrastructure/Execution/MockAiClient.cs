using Application;

namespace Infrastructure.Execution;

public sealed class MockAiClient : IAiClient
{
    public Task<string> CompleteAsync(string provider, string model, string prompt, CancellationToken ct)
        => Task.FromResult($"[{provider}/{model}] {prompt}"[..Math.Min(120, $"[{provider}/{model}] {prompt}".Length)]);
}
