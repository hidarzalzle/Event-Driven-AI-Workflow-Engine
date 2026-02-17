namespace Domain;

public enum TriggerType { Webhook, Manual, Event }
public enum WorkflowInstanceStatus { Pending, Running, Waiting, Completed, Failed, DeadLettered }
public enum StepType { Ai, Http, Condition, Delay, QueuePublish }
public enum StepExecutionStatus { Pending, Running, Succeeded, Failed, Waiting, Skipped }
