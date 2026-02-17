# Architecture

```text
                        +------------------------------+
                        |       ASP.NET Core API       |
                        |  - Webhooks / Workflow APIs  |
                        |  - SignalR Hub               |
                        +--------------+---------------+
                                       |
                                       v
                        +------------------------------+
                        |        Application Layer      |
                        |  MediatR Commands/Queries     |
                        +--------------+---------------+
                                       |
                                       v
                        +------------------------------+
                        |      Workflow Executor         |
                        |  - Distributed Lock            |
                        |  - Durable Step Loop           |
                        |  - Retry/Delay/DLQ             |
                        +---+------------------------+---+
                            |                        |
                            v                        v
                   +----------------+      +------------------+
                   |   SQL Server   |      |      Redis       |
                   | Instances/Steps|      | Lock + Idempotent|
                   | Outbox + DLQ   |      +------------------+
                   +--------+-------+
                            |
                            v
                   +------------------+
                   | Outbox Publisher |
                   | RabbitMQ (opt)   |
                   +------------------+
```
