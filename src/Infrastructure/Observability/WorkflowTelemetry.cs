using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Infrastructure.Observability;

public static class WorkflowTelemetry
{
    public static readonly ActivitySource Activity = new("WorkflowEngine");
    public static readonly Meter Meter = new("WorkflowEngine.Metrics");
    public static readonly Histogram<double> StepDurationMs = Meter.CreateHistogram<double>("step_duration_ms");
    public static readonly Counter<long> StepRetriesTotal = Meter.CreateCounter<long>("step_retries_total");
    public static readonly Counter<long> DeadlettersTotal = Meter.CreateCounter<long>("deadletters_total");
    public static readonly Counter<long> CompletedTotal = Meter.CreateCounter<long>("instances_completed_total");
}
