# .NET 8 Event-Driven AI Workflow Engine (Mini n8n)

A production-grade, event-driven workflow orchestration engine built on ASP.NET Core 8 and Clean Architecture. It executes JSON workflow DSL definitions durably with persisted checkpoints, distributed locks, retries, dead-lettering, replay, and real-time observability.

## Key Features
- **Clean Architecture**: Domain / Application / Infrastructure / API separation.
- **Durable execution**: workflow + step state persisted in SQL Server.
- **Idempotency**: Redis fast dedupe + SQL unique constraint enforcement.
- **Distributed locks**: Redis lock per workflow instance.
- **Step types**: AI, HTTP, Condition, Delay, Queue Publish.
- **Resilience**: per-step retry policy (max attempts, backoff, jitter), stuck-running recovery.
- **Dead-letter + replay**: failed instances moved to DLQ table and replayable via API.
- **Outbox pattern**: durable outbox with background publisher worker.
- **Monitoring**: SignalR live updates + dashboard at `/dashboard`.
- **Observability**: OpenTelemetry traces, metrics, logs; OTLP + console (Development).
- **Dockerized**: app + SQL Server + Redis + RabbitMQ + OTEL Collector.

## Architecture (ASCII)
```text
Webhook/API -> MediatR Command -> SQL (instance persisted) -> Internal Queue
                                                      |              |
                                                      v              v
                                                    Redis       Workflow Executor
                                              (lock/idempotency)     |
                                                                      +--> Step Handlers (AI/HTTP/Condition/Delay/Queue)
                                                                      +--> StepExecutions
                                                                      +--> OutboxMessages
                                                                      +--> DeadLetterMessages

Dashboard <-> SignalR Hub <-> runtime events
Scheduler/Recovery Worker -> scans Waiting/Running and requeues safely
OutboxPublisherWorker -> publishes outbox entries to RabbitMQ
```

## Reliability Guarantees
- **No duplicate execution starts** with required `Idempotency-Key` header and DB unique key (`TriggerKey + IdempotencyKey`).
- **Distributed single-run semantics per instance** via Redis lock key.
- **Crash recovery** from persisted `CurrentStepId`, `StepExecutions`, `NextRunAtUtc`.
- **Dead-lettering** when retry budget is exhausted.
- **Replay support** from start or failed step.

## Quickstart (Docker)
```bash
docker compose up --build
```

- Swagger: `http://localhost:8080/swagger`
- Dashboard: `http://localhost:8080/dashboard`

## Example Workflow DSL
See `docs/workflow-dsl.md`.

## Trigger via Webhook
```bash
curl -X POST http://localhost:8080/api/webhooks/ai-triage \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: demo-001" \
  -d '{"ticket":"refund request", "amount": 125.0}'
```

## Observability
- Custom spans: `Workflow.Execute`, `Step.Execute`
- Custom metrics: `step_duration_ms`, `step_retries_total`, `deadletters_total`, `instances_completed_total`
- Exporters: OTLP + console in Development

## Testing
```bash
dotnet build EventDrivenWorkflowEngine.sln
dotnet test EventDrivenWorkflowEngine.sln
```

## Seed Workflows
- `ai-triage-flow`
- `delayed-followup`

Seeded automatically at startup (idempotent).

## Roadmap
- Replace mock AI client with provider adapters (OpenAI/Gemini production implementations).
- Add authenticated dashboard + RBAC.
- Add richer execution visualization and filtering.
- Add CI pipeline with integration tests against Docker services.
