using System;
using System.Collections.Generic;
using System.Threading;
using Rebus.Config;
using Rebus.Exceptions;
using Rebus.Logging;
using Rebus.Pipeline;
using Rebus.Pipeline.Send;
using Rebus.Redis.Async;
using Rebus.Serialization;
using StackExchange.Redis;

namespace Rebus.Redis;

/// <summary>
/// Configuration helper for adding Redis support.
/// </summary>
public static class ConfigurationExtensions
{
    public static void EnableRedis(
        this OptionsConfigurer configurer,
        string connectionString,
        Action<RebusRedisConfig>? configure = null)
    {
        if (configurer == null) throw new ArgumentNullException(nameof(configurer));

        var config = new RebusRedisConfig();
        (configure ?? DefaultConfig).Invoke(config);

        try
        {
            var redis = ConnectionMultiplexer.Connect(EnsureClientName(connectionString, "rebus-redis"));
            configurer.Register<IConnectionMultiplexer>(_ => redis, "redis");
        }
        catch (Exception e)
        {
            throw new RebusConfigurationException(e, "Could not connect to Redis");
        }

        configurer.Register<RedisProvider>(r =>
            new RedisProvider(r.Get<IConnectionMultiplexer>()), "Connection Provider");

        if (config.HostAsyncEnabled)
        {
            configurer.Register(_ =>
            {
                var step = new ReplyStepHandler();
                return step;
            });

            configurer.Decorate<IPipeline>(c =>
            {
                var pipeline = c.Get<IPipeline>();
                var step = c.Get<ReplyStepHandler>();
                return new PipelineStepInjector(pipeline)
                    .OnSend(step, PipelineRelativePosition.Before, typeof(SendOutgoingMessageStep));
            });


            var additionalConnections = new Dictionary<string, IConnectionMultiplexer>();
            foreach (var connection in config.AdditionalConnections)
            {
                try
                {
                    additionalConnections.Add(connection.Key,
                        ConnectionMultiplexer.Connect(EnsureClientName(connectionString,
                            $"rebus-redis-{connection.Key}")));
                }
                catch (Exception e)
                {
                    throw new RebusConfigurationException(e, $"Could not connect to Redis for '{connection.Key}'");
                }
            }

            configurer.Register<Dictionary<string, IConnectionMultiplexer>>(
                _ => additionalConnections,
                "Additional Redis connections");


            configurer.Register(r =>
                    new HostInitializer(
                        r.Get<IConnectionMultiplexer>(),
                        r.Get<ISerializer>(),
                        r.Get<Dictionary<string, IConnectionMultiplexer>>()),
                "Host initializer");

            configurer.Decorate<IPipeline>(r =>
            {
                r.Get<HostInitializer>();
                return r.Get<IPipeline>();
            }, "Trigger host initialization");
        }

        if (config.ClientAsyncEnabled)
        {
            configurer.Register(r =>
                    new ClientInitializer(
                        r.Get<IConnectionMultiplexer>(),
                        r.Get<ISerializer>(),
                        r.Get<IRebusLoggerFactory>(),
                        r.Get<CancellationToken>()),
                "Client initializer");

            configurer.Decorate<IPipeline>(r =>
            {
                r.Get<ClientInitializer>();
                return r.Get<IPipeline>();
            }, "Trigger client initialization");
        }

        foreach (var header in config.AdditionalHeaders)
        {
            if (AsyncPayload.IncludedHeaders.Contains(header)) continue;
            AsyncPayload.IncludedHeaders.Add(header);
        }
    }

    private static string EnsureClientName(string connectionString, string name)
    {
        var config = ConfigurationOptions.Parse(connectionString);
        config.ClientName ??= name;
        return config.ToString();
    }

    private static void DefaultConfig(RebusRedisConfig a)
    {
        a.EnableAsync();
    }
}