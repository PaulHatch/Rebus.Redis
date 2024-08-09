using System.Collections.Generic;
using Rebus.Bus;
using Rebus.Serialization;
using StackExchange.Redis;

namespace Rebus.Redis.Async;

internal class HostInitializer : IInitializable
{
    private readonly IReadOnlyDictionary<string, IConnectionMultiplexer> _additionalConnections;
    private readonly IConnectionMultiplexer _connection;
    private readonly ISerializer _objectSerializer;

    public HostInitializer(
        IConnectionMultiplexer connection,
        ISerializer objectSerializer,
        IReadOnlyDictionary<string, IConnectionMultiplexer> additionalConnections)
    {
        _connection = connection;
        _objectSerializer = objectSerializer;
        _additionalConnections = additionalConnections;
    }

    public void Initialize()
    {
        AsyncHostRedisExtensions.RegisterPublisher(_connection, _objectSerializer, _additionalConnections);
    }
}