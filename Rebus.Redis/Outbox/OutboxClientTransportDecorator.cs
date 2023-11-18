using System;
using Rebus.Messages;
using Rebus.Transport;
using System.Threading;
using System.Threading.Tasks;

namespace Rebus.Redis.Outbox;

/// <summary>
/// Decorator for ITransport that intercepts the outgoing messages and stores them in the outbox.
/// </summary>
internal class OutboxClientTransportDecorator : ITransport
{
    private readonly ITransport _transport;
    private readonly IOutboxQueueStorage _outboxQueueStorage;

    public OutboxClientTransportDecorator(ITransport transport, IOutboxQueueStorage outboxQueueStorage)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _outboxQueueStorage = outboxQueueStorage ?? throw new ArgumentNullException(nameof(outboxQueueStorage));
    }

    public void CreateQueue(string address) => _transport.CreateQueue(address);

    public Task Send(string destinationAddress, TransportMessage message, ITransactionContext context)
    {
        var redisTransaction = context.GetOrNull<RedisTransaction>(RedisProvider.CurrentOutboxConnectionKey);

        // skip outbox if there is no transaction active
        return redisTransaction == null ? 
            _transport.Send(destinationAddress, message, context) : 
            _outboxQueueStorage.Save(new OutgoingTransportMessage(message, destinationAddress), redisTransaction);
    }

    public Task<TransportMessage> Receive(ITransactionContext context, CancellationToken cancellationToken) =>
        _transport.Receive(context, cancellationToken);

    public string Address => _transport.Address;
}