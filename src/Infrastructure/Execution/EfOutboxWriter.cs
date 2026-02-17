using System.Text.Json;
using Application;
using Domain;

namespace Infrastructure.Execution;

public sealed class EfOutboxWriter(AppDbContext db) : IOutboxWriter
{
    public void Add(string type, object payload)
    {
        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = type,
            Payload = JsonSerializer.Serialize(payload),
            OccurredAtUtc = DateTime.UtcNow
        });
    }
}
