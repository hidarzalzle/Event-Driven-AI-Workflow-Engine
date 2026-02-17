using Application;
using Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Integration.Tests;

public class TestWebAppFactory : WebApplicationFactory<Program>
{
    private DbConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IIdempotencyStore>();
            services.RemoveAll<ILockManager>();
            services.RemoveAll<IQueuePublisher>();

            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));

            services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
            services.AddSingleton<ILockManager, NoopLockManager>();
            services.AddSingleton<IQueuePublisher, NoopQueuePublisher>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection?.Dispose();
    }
}

public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly HashSet<string> _keys = [];
    public Task<bool> TryBeginAsync(string key, TimeSpan ttl, CancellationToken ct)
    {
        lock (_keys) return Task.FromResult(_keys.Add(key));
    }
}

public sealed class NoopLockManager : ILockManager
{
    private sealed class D : IAsyncDisposable { public ValueTask DisposeAsync() => ValueTask.CompletedTask; }
    public Task<IAsyncDisposable?> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken ct) => Task.FromResult<IAsyncDisposable?>(new D());
}

public sealed class NoopQueuePublisher : IQueuePublisher
{
    public Task PublishAsync(string topic, string routingKey, string payload, CancellationToken ct) => Task.CompletedTask;
}
