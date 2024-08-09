using System.Collections.Generic;
using Rebus.Messages;
using Rebus.Serialization;
using StackExchange.Redis;

namespace Rebus.Redis.Outbox;

/// <summary>
/// Represents one single message to be delivered to the transport
/// </summary>
public class OutboxMessage
{
    /// <summary>
    /// Creates a new <see cref="OutboxMessage" /> with the given <paramref name="id" />
    /// </summary>
    /// <param name="id">The (Redis streams) ID for this message.</param>
    public OutboxMessage(RedisValue id)
    {
        Id = id;
    }

    /// <summary>The Redis streams ID for this message.</summary>
    public RedisValue Id { get; }

    /// <summary>The destination address for this message.</summary>
    public string? DestinationAddress { get; set; }

    /// <summary>The headers to be sent with the message.</summary>
    public Dictionary<string, string> Headers { get; } = new();

    /// <summary>The message body, serialized with the configured <see cref="ISerializer" />.</summary>
    public byte[]? Body { get; set; }

    /// <summary>
    /// Gets the <see cref="Headers" /> and <see cref="Body" /> wrapped in a <see cref="TransportMessage" />
    /// </summary>
    public TransportMessage ToTransportMessage()
    {
        return new TransportMessage(Headers, Body);
    }
}