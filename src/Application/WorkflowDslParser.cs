using System.Text.Json;

namespace Application;

public static class WorkflowDslParser
{
    public static WorkflowDsl Parse(string json)
    {
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var name = root.GetProperty("name").GetString() ?? "unnamed";
        var t = root.GetProperty("trigger");
        var trigger = new TriggerDsl(
            t.GetProperty("type").GetString()!,
            t.TryGetProperty("route", out var route) ? route.GetString() : null,
            t.TryGetProperty("eventName", out var evt) ? evt.GetString() : null,
            t.TryGetProperty("secret", out var sec) ? sec.GetString() : null);

        var steps = new List<StepDsl>();
        foreach (var s in root.GetProperty("steps").EnumerateArray())
        {
            var type = s.GetProperty("type").GetString()!;
            var id = s.GetProperty("id").GetString()!;
            var next = s.TryGetProperty("next", out var n) ? n.GetString() : null;
            var retry = ParseRetry(s);
            steps.Add(type switch
            {
                "ai" => new AiStepDsl(id, type, next, s.GetProperty("provider").GetString()!, s.GetProperty("model").GetString()!, s.GetProperty("promptTemplate").GetString()!, s.GetProperty("timeoutSeconds").GetInt32(), retry),
                "http" => new HttpStepDsl(id, type, next, s.GetProperty("method").GetString()!, s.GetProperty("url").GetString()!, ParseHeaders(s), s.TryGetProperty("bodyTemplate", out var b) ? b.GetString() : null, s.GetProperty("timeoutSeconds").GetInt32(), retry),
                "condition" => new ConditionStepDsl(id, type, next, s.GetProperty("expression").GetString()!, s.GetProperty("trueNext").GetString()!, s.GetProperty("falseNext").GetString()!),
                "delay" => new DelayStepDsl(id, type, next, s.TryGetProperty("delaySeconds", out var d) ? d.GetInt32() : null, s.TryGetProperty("untilUtc", out var u) ? u.GetDateTime() : null),
                "queue_publish" => new QueuePublishStepDsl(id, type, next, s.GetProperty("topic").GetString()!, s.GetProperty("routingKey").GetString()!, s.GetProperty("payloadTemplate").GetString()!, retry),
                _ => throw new InvalidOperationException($"Unknown step type {type}")
            });
        }

        Validate(steps);
        return new WorkflowDsl(name, trigger, steps);
    }

    private static Dictionary<string, string>? ParseHeaders(JsonElement s)
    {
        if (!s.TryGetProperty("headers", out var h) || h.ValueKind != JsonValueKind.Object) return null;
        return h.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetString() ?? string.Empty);
    }

    private static RetryPolicyDsl? ParseRetry(JsonElement s)
    {
        if (!s.TryGetProperty("retryPolicy", out var r) || r.ValueKind != JsonValueKind.Object) return null;
        var status = r.TryGetProperty("retryableStatusCodes", out var sc) && sc.ValueKind == JsonValueKind.Array
            ? sc.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Number).Select(x => x.GetInt32()).ToArray()
            : null;
        return new RetryPolicyDsl(
            r.TryGetProperty("maxAttempts", out var ma) ? ma.GetInt32() : 3,
            r.TryGetProperty("initialDelayMs", out var id) ? id.GetInt32() : 500,
            r.TryGetProperty("backoffFactor", out var bf) ? bf.GetDouble() : 2,
            r.TryGetProperty("jitter", out var jt) && jt.ValueKind is JsonValueKind.True or JsonValueKind.False ? jt.GetBoolean() : true,
            status);
    }

    private static void Validate(List<StepDsl> steps)
    {
        if (steps.Count == 0) throw new InvalidOperationException("Workflow requires at least one step.");
        var ids = steps.Select(x => x.Id).ToList();
        if (ids.Count != ids.Distinct().Count()) throw new InvalidOperationException("Duplicate step ids.");

        foreach (var step in steps)
        {
            if (step.Next is not null && !ids.Contains(step.Next)) throw new InvalidOperationException($"Next step missing: {step.Next}");
            if (step is ConditionStepDsl c && (!ids.Contains(c.TrueNext) || !ids.Contains(c.FalseNext)))
                throw new InvalidOperationException("Condition branch step missing.");
        }

        // no cycles for v1
        var edges = new Dictionary<string, List<string>>();
        foreach (var s in steps)
        {
            var list = new List<string>();
            if (s.Next is not null) list.Add(s.Next);
            if (s is ConditionStepDsl c) { list.Add(c.TrueNext); list.Add(c.FalseNext); }
            edges[s.Id] = list;
        }

        var visited = new HashSet<string>();
        var stack = new HashSet<string>();
        bool HasCycle(string node)
        {
            if (!visited.Add(node)) return false;
            stack.Add(node);
            foreach (var n in edges[node])
            {
                if (stack.Contains(n)) return true;
                if (HasCycle(n)) return true;
            }
            stack.Remove(node);
            return false;
        }

        if (HasCycle(steps[0].Id)) throw new InvalidOperationException("Cycles are not allowed.");
    }
}
