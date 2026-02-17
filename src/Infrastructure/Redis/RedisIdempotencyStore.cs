using Application;
using StackExchange.Redis;

namespace Infrastructure.Redis;

public sealed class RedisIdempotencyStore(IConnectionMultiplexer redis) : IIdempotencyStore
{
    public Task<bool> TryBeginAsync(string key, TimeSpan ttl, CancellationToken ct)
        => redis.GetDatabase().StringSetAsync($"idem:{key}", "1", ttl, When.NotExists);
}
