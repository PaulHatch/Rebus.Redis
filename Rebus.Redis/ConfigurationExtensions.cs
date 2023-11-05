using Rebus.Config;
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

        var redis = ConnectionMultiplexer.Connect(connectionString);
        configurer.Register<IConnectionMultiplexer>(r => redis, "redis");
        configurer.Register<RedisProvider>(r => 
            new RedisProvider(r.Get<IConnectionMultiplexer>()), "Connection Provider");

        if (config.HostAsyncEnabled)
        {
            configurer.Register(c =>
            {
                //var rebusLoggerFactory = c.Get<IRebusLoggerFactory>();
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

            configurer.Register(r =>
                    new HostInitializer(r.Get<IConnectionMultiplexer>(), r.Get<ISerializer>()),
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

    private static void DefaultConfig(RebusRedisConfig a)
    {
        a.EnableAsync();
    }
}

public class RebusRedisConfig
{
    public RebusRedisConfig EnableAsync(bool enableClient = true, bool enableHost = true)
    {
        ClientAsyncEnabled = enableClient;
        HostAsyncEnabled = enableHost;
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

    internal HashSet<string> AdditionalHeaders { get; } = new();
    internal bool ClientAsyncEnabled { get; private set; }
    internal bool HostAsyncEnabled { get; private set; }
}