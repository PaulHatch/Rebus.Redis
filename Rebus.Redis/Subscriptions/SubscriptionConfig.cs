using Rebus.Config;
using Rebus.Exceptions;
using Rebus.Subscriptions;
using StackExchange.Redis;

namespace Rebus.Redis.Subscriptions;

/// <summary>
/// Configuration helper for adding Redis support for subscription storage.
/// </summary>
public static class SubscriptionConfig
{
    /// <summary>
    /// Configures Rebus to use Redis to store subscriptions.
    /// </summary>
    /// <param name="prefix">String to prefix all subscription keys with. Defaults to "rebus-subscription:".</param>
    /// <param name="isCentralized">True if the subscription storage is centralized. Defaults to false.</param>
    /// <param name="connectionString">
    /// Override connection string to the Redis server, by the default the connection specified when the
    /// <see cref="OptionsConfigurer.EnableRedis()" /> extension method was called. Set this value if using a different
    /// Redis server for subscription storage.
    /// </param>
    public static void StoreInRedis(this StandardConfigurer<ISubscriptionStorage> configurer,
        string prefix = "rebus-subscription:",
        bool isCentralized = false,
        string? connectionString = null)
    {
        configurer.Register(r =>
        {
            var connection = connectionString != null
                ? ConnectionMultiplexer.Connect(connectionString)
                : r.Get<IConnectionMultiplexer>()
                  ?? throw new RebusConfigurationException("Redis connection has not been configured");

            return new RedisSubscriptionStorage(connection, prefix, isCentralized);
        });
    }
}