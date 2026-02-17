using Application;
using StackExchange.Redis;

namespace Infrastructure.Redis;

public sealed class RedisLockManager(IConnectionMultiplexer redis) : ILockManager
{
    public async Task<IAsyncDisposable?> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken ct)
    {
        var token = Guid.NewGuid().ToString("N");
        var db = redis.GetDatabase();
        if (!await db.StringSetAsync($"lock:{key}", token, ttl, When.NotExists)) return null;
        return new Releaser(db, $"lock:{key}", token);
    }

    private sealed class Releaser(IDatabase db, string key, string value) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            const string script = "if redis.call('GET', KEYS[1]) == ARGV[1] then return redis.call('DEL', KEYS[1]) else return 0 end";
            await db.ScriptEvaluateAsync(script, [key], [value]);
        }
    }
}
