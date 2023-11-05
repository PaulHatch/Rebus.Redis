using Rebus.Messages;
using Rebus.Transport;
using System.Collections.Concurrent;

namespace Rebus.Redis.Outbox;

/// <summary>
/// Decorator for ITransport that intercepts the outgoing messages and stores them in the outbox.
/// </summary>
internal class OutboxClientTransportDecorator : ITransport
{
    private readonly ITransport _transport;
    private readonly IOutboxStorage _outboxStorage;

    public OutboxClientTransportDecorator(ITransport transport, IOutboxStorage outboxStorage)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _outboxStorage = outboxStorage ?? throw new ArgumentNullException(nameof(outboxStorage));
    }

    public void CreateQueue(string address) => _transport.CreateQueue(address);

    public Task Send(string destinationAddress, TransportMessage message, ITransactionContext context)
    {
        var redisTransaction = context.GetOrNull<RedisTransaction>(RedisProvider.CurrentOutboxConnectionKey);

        // Leave behavior unchanged if there is no transaction
        
        return redisTransaction == null ? 
            _transport.Send(destinationAddress, message, context) : 
            _outboxStorage.Save(new OutgoingTransportMessage(message, destinationAddress), redisTransaction);
    }

    public Task<TransportMessage> Receive(ITransactionContext context, CancellationToken cancellationToken) =>
        _transport.Receive(context, cancellationToken);

    public string Address => _transport.Address;
}