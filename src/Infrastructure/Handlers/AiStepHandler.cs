using Application;

namespace Infrastructure.Handlers;

public sealed class AiStepHandler(IAiClient ai) : IStepHandler
{
    public string StepType => "ai";

    public async Task<StepResult> HandleAsync(StepContext ctx, CancellationToken ct)
    {
        var s = (AiStepDsl)ctx.Step;
        var output = await ai.CompleteAsync(s.Provider, s.Model, s.PromptTemplate, ct);
        return new StepResult.Succeeded(output, s.Next);
    }
}
