using System.Collections.Generic;
using System.Threading.Tasks;
using Rebus.Transport;

namespace Rebus.Redis.Outbox;

/// <summary>
/// Outbox abstraction that enables truly idempotent message processing and store-and-forward for outgoing messages
/// </summary>
public interface IOutboxStorage
{
    /// <summary>
    /// Gets the next message batch to be sent.
    /// </summary>
    Task<IEnumerable<OutboxMessage>?> GetNextMessageBatch();

    /// <summary>
    /// Marks the specified message as dispatched.
    /// </summary>
    Task MarkAsDispatched(OutboxMessage message);

    /// <summary>
    /// Cleans up acknowledged messages from the outgoing message stream.
    /// </summary>
    Task TrimQueue();

    /// <summary>
    /// Removes idle consumers from the outbox stream consumer group.
    /// </summary>
    Task CleanupIdleConsumers();

    /// <summary>
    /// Returns orphaned messages from the outgoing message stream which have not been acknowledged by the consumer
    /// that received them within the configured timeout.
    /// </summary>
    Task<IEnumerable<OutboxMessage>?> GetOrphanedMessageBatch();
}

/// <summary>
/// Provides a way to store outgoing messages in a Redis stream.
/// </summary>
/// <remarks>
/// Splitting this out from <see cref="IOutboxStorage" /> is a workaround to avoid creating two instances (one for the
/// decorator, one for the forwarder), each with their own dedicated blocking reader connection.
/// </remarks>
internal interface IOutboxQueueStorage
{
    /// <summary>
    /// Adds the specified message to the outgoing message stream (for the current Redis transaction).
    /// </summary>
    /// <param name="message">The message to add</param>
    /// <param name="transaction">The current Redis transaction</param>
    Task Save(OutgoingTransportMessage message, RedisTransaction transaction);
}