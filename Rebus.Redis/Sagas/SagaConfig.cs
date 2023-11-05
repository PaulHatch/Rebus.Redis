using Rebus.Config;
using Rebus.Redis;
using Rebus.Redis.Sagas;
using Rebus.Sagas;
using Rebus.Transport;

// ReSharper disable once CheckNamespace
namespace Rebus.Config;

public static class SagaConfig
{
    /// <summary>
    /// Configures Rebus to store sagas in memory. Please note that while this method can be used for production purposes
    /// (if you need a saga storage that is pretty fast), it is probably better to use a persistent storage (like SQL Server
    /// or another database), because the state of all sagas will be lost in case the endpoint is restarted.
    /// </summary>
    /// <param name="redis">The Redis instance to use.</param>
    /// <param name="shardCount">
    /// The number of hash maps to use for storage of correlation ID indexes per sage type, defaults to 1. For
    /// high-volume systems, setting this to a higher number can improve performance of concurrent lookups.
    /// </param>
    public static void StoreInRedis(
        this StandardConfigurer<ISagaStorage> configurer,
        int shardCount = 1)
    {
        if (configurer == null) throw new ArgumentNullException(nameof(configurer));
        if (shardCount < 1)
            throw new ArgumentOutOfRangeException(nameof(shardCount), "Shard count must be greater than 0");

        configurer.Register(c => new RedisSagaStorage(c.Get<RedisProvider>(),
            shardCount));
    }
}