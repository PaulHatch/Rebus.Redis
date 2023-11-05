using Rebus.Bus;
using Rebus.Serialization;
using StackExchange.Redis;

namespace Rebus.Redis.Async;

internal class HostInitializer : IInitializable
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ISerializer _objectSerializer;

    public HostInitializer(IConnectionMultiplexer redis, ISerializer objectSerializer)
    {
        _redis = redis;
        _objectSerializer = objectSerializer;
    }

    public void Initialize()
    {
        AsyncHostRedisExtensions.RegisterPublisher(_redis, _objectSerializer);
    }
}