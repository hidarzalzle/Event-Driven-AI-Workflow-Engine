using System.Threading.Channels;
using Application;

namespace Infrastructure.Execution;

public sealed class ChannelWorkflowQueue : IInternalWorkflowQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();

    public ValueTask EnqueueAsync(Guid instanceId, CancellationToken ct) => _channel.Writer.WriteAsync(instanceId, ct);

    public ValueTask<Guid> DequeueAsync(CancellationToken ct) => _channel.Reader.ReadAsync(ct);
}
