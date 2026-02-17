using MediatR;

namespace Application;

public record StartWorkflowCommand(Guid WorkflowDefinitionId, string TriggerKey, string IdempotencyKey, string ContextJson, string CorrelationId) : IRequest<Guid>;
