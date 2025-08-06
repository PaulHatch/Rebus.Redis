using System;
using System.Collections.Generic;
using System.Linq;

namespace Rebus.Redis;

/// <summary>
/// Configuration helper for configuring Redis support.
/// </summary>
public class RebusRedisConfig
{
    internal HashSet<string> AdditionalHeaders { get; } = [];
    internal bool ClientAsyncEnabled { get; private set; }
    internal bool HostAsyncEnabled { get; private set; }
    internal Dictionary<string, string> AdditionalConnections { get; } = new();

    /// <summary>
    /// Enables async support for the Redis transport. This will allow the transport to send and receive messages, or
    /// client-only (sending messages and awaiting replies) or host-only (receiving messages and sending replies) mode
    /// can be enabled.
    /// </summary>
    /// <param name="mode">
    /// Indicates the async mode to enable, defaults to <see cref="AsyncMode.Both" /> if not specified.
    /// </param>
    public RebusRedisConfig EnableAsync(AsyncMode mode = AsyncMode.Both)
    {
        (ClientAsyncEnabled, HostAsyncEnabled) = mode switch
        {
            AsyncMode.Both => (true, true),
            AsyncMode.Client => (true, false),
            AsyncMode.Host => (false, true),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };

        return this;
    }

    /// <summary>
    /// Adds a foreign Redis connection to the configuration. This will be used to publish replies to messages across
    /// service boundaries. If a reply is sent for a message matching the specified bus name (e.g. the
    /// <code>rbs2-sender-address</code> header), the reply will be published to the specified connection. If no matches
    /// are found, the default connection will be used instead. Note that this setting is only relevant for
    /// <see cref="AsyncMode.Both" /> and <see cref="AsyncMode.Host" /> modes.
    /// </summary>
    /// <param name="senderAddress">The address to map connection for.</param>
    /// <param name="connectionString">The Redis connection string to publish replies to.</param>
    public RebusRedisConfig RouteRepliesTo(string senderAddress, string connectionString)
    {
        AdditionalConnections.Add(senderAddress, connectionString);
        return this;
    }

    /// <summary>
    /// Specify additional headers that should be included in the transport message. By default, only the
    /// type, content type, and content encoding headers are included. Only headers required by the serializer
    /// are necessary as routing is handled in a separate channel.
    /// </summary>
    /// <param name="headers">Headers to include</param>
    public RebusRedisConfig IncludeTransportHeader(params string[] headers)
    {
        foreach (var header in headers.Distinct())
        {
            AdditionalHeaders.Add(header);
        }

        return this;
    }
}