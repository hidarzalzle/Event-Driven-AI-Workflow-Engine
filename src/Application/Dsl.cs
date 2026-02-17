namespace Application;

public record WorkflowDsl(string Name, TriggerDsl Trigger, IReadOnlyList<StepDsl> Steps);
public record TriggerDsl(string Type, string? Route, string? EventName, string? Secret);
public abstract record StepDsl(string Id, string Type, string? Next, RetryPolicyDsl? RetryPolicy);
public record RetryPolicyDsl(int MaxAttempts = 3, int InitialDelayMs = 500, double BackoffFactor = 2, bool Jitter = true, int[]? RetryableStatusCodes = null);
public record AiStepDsl(string Id, string Type, string? Next, string Provider, string Model, string PromptTemplate, int TimeoutSeconds, RetryPolicyDsl? RetryPolicy) : StepDsl(Id, Type, Next, RetryPolicy);
public record HttpStepDsl(string Id, string Type, string? Next, string Method, string Url, Dictionary<string,string>? Headers, string? BodyTemplate, int TimeoutSeconds, RetryPolicyDsl? RetryPolicy) : StepDsl(Id, Type, Next, RetryPolicy);
public record ConditionStepDsl(string Id, string Type, string? Next, string Expression, string TrueNext, string FalseNext) : StepDsl(Id, Type, Next, null);
public record DelayStepDsl(string Id, string Type, string? Next, int? DelaySeconds, DateTime? UntilUtc) : StepDsl(Id, Type, Next, null);
public record QueuePublishStepDsl(string Id, string Type, string? Next, string Topic, string RoutingKey, string PayloadTemplate, RetryPolicyDsl? RetryPolicy) : StepDsl(Id, Type, Next, RetryPolicy);
