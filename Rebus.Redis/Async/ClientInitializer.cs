using Rebus.Bus;
using Rebus.Logging;
using Rebus.Serialization;
using StackExchange.Redis;

namespace Rebus.Redis.Async;

internal class ClientInitializer : IInitializable
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ISerializer _objectSerializer;
    private readonly IRebusLoggerFactory _loggerFactory;
    private readonly CancellationToken _shutdownToken;

    public ClientInitializer(
        IConnectionMultiplexer redis,
        ISerializer objectSerializer,
        IRebusLoggerFactory loggerFactory,
        CancellationToken shutdownToken)
    {
        _redis = redis;
        _objectSerializer = objectSerializer;
        _loggerFactory = loggerFactory;
        _shutdownToken = shutdownToken;
    }

    public void Initialize()
    {
        AsyncClientRedisExtensions.RegisterListener(_redis, _objectSerializer, _loggerFactory, _shutdownToken);
    }
}