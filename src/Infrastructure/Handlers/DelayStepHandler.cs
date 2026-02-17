using Application;

namespace Infrastructure.Handlers;

public sealed class DelayStepHandler : IStepHandler
{
    public string StepType => "delay";

    public Task<StepResult> HandleAsync(StepContext ctx, CancellationToken ct)
    {
        var s = (DelayStepDsl)ctx.Step;
        var next = s.UntilUtc ?? ctx.Clock.UtcNow.AddSeconds(s.DelaySeconds ?? 5);
        return Task.FromResult<StepResult>(new StepResult.Waiting(next));
    }
}
