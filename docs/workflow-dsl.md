# Workflow DSL

```json
{
  "name": "ai-triage-flow",
  "trigger": {
    "type": "webhook",
    "route": "ai-triage",
    "secret": "optional-hmac-secret"
  },
  "steps": [
    {
      "id": "ai1",
      "type": "ai",
      "provider": "mock",
      "model": "gpt-4o-mini",
      "promptTemplate": "Classify request",
      "timeoutSeconds": 30,
      "retryPolicy": { "maxAttempts": 3, "initialDelayMs": 500 },
      "next": "cond1"
    },
    {
      "id": "cond1",
      "type": "condition",
      "expression": "amount > 100",
      "trueNext": "http1",
      "falseNext": "q1"
    },
    {
      "id": "http1",
      "type": "http",
      "method": "POST",
      "url": "https://postman-echo.com/post",
      "headers": { "x-source": "workflow-engine" },
      "bodyTemplate": "{\"message\":\"hello\"}",
      "timeoutSeconds": 20,
      "retryPolicy": {
        "maxAttempts": 3,
        "initialDelayMs": 1000,
        "backoffFactor": 2,
        "jitter": true,
        "retryableStatusCodes": [429, 500, 502, 503, 504]
      },
      "next": "q1"
    },
    {
      "id": "q1",
      "type": "queue_publish",
      "topic": "workflow.events",
      "routingKey": "triage.done",
      "payloadTemplate": "{\"done\":true}"
    }
  ]
}
```

## Validation rules
- Step IDs must be unique.
- `next`, `trueNext`, and `falseNext` targets must exist.
- At least one step is required.
- Cycles are disallowed in v1.
