using System;
using Rebus.Config;
using Rebus.Exceptions;
using Rebus.Logging;
using Rebus.Pipeline;
using Rebus.Redis.Outbox;
using Rebus.Retry.Simple;
using Rebus.Threading;
using Rebus.Transport;

// ReSharper disable once CheckNamespace
namespace Rebus.Redis;

/// <summary>
/// Configuration extensions for the experimental outbox support
/// </summary>
public static class RedisOutboxConfigurationExtensions
{
    /// <summary>
    /// Configures Rebus to use an outbox.
    /// This will store a (message ID, source queue) tuple for all processed messages, and under this tuple any messages sent/published will
    /// also be stored, thus enabling truly idempotent message processing.
    /// </summary>
    public static RebusConfigurer Outbox(this RebusConfigurer configurer,
        Action<StandardConfigurer<IOutboxStorage>> configure)
    {
        if (configurer == null) throw new ArgumentNullException(nameof(configurer));
        if (configure == null) throw new ArgumentNullException(nameof(configure));

        configurer.Options(o =>
        {
            configure(StandardConfigurer<IOutboxStorage>.GetConfigurerFrom(o));

            // end here if the outbox was not configured in the configure action provided by the caller
            if (!o.Has<IOutboxStorage>()) return;

            o.Decorate<ITransport>(
                c => new OutboxClientTransportDecorator(c.Get<ITransport>(), c.Get<IOutboxQueueStorage>()));

            o.Register(c =>
            {
                var asyncTaskFactory = c.Get<IAsyncTaskFactory>();
                var rebusLoggerFactory = c.Get<IRebusLoggerFactory>();
                var outboxStorage = c.Get<IOutboxStorage>();
                var transport = c.Get<ITransport>();
                var config = c.Get<RedisOutboxConfiguration>();
                
                return new OutboxForwarder(
                    asyncTaskFactory,
                    rebusLoggerFactory,
                    outboxStorage,
                    transport,
                    config);
            });

            o.Decorate(c =>
            {
                _ = c.Get<OutboxForwarder>();
                return c.Get<Options>();
            });

            o.Decorate<IPipeline>(c =>
            {
                var pipeline = c.Get<IPipeline>();
                var redisProvider = c.Get<RedisProvider>();
                var step = new OutboxIncomingStep(redisProvider);
                return new PipelineStepInjector(pipeline)
                    .OnReceive(step, PipelineRelativePosition.After, typeof(DefaultRetryStep));
            });
        });

        return configurer;
    }

    /// <summary>
    /// Configures the outbox to store pending messages in Redis. You must configure the Redis provider in the Rebus
    /// options to enable Redis outbox support.
    /// </summary>
    public static void StoreInRedis(
        this StandardConfigurer<IOutboxStorage> configurer,
        Action<RedisOutboxConfiguration>? configure = default)
    {
        if (configurer == null) throw new ArgumentNullException(nameof(configurer));
        var outboxConfig = new RedisOutboxConfiguration();
        configure?.Invoke(outboxConfig);
        configurer.OtherService<RedisOutboxConfiguration>()
            .Register(_ => outboxConfig);

        configurer.OtherService<IOutboxStorage>()
            .Register(r =>
            {
                var config = r.Get<RedisOutboxConfiguration>();
                var options = r.Get<Options>();
                var outboxName = config.OutboxName ??
                                 options.OptionalBusName.AddSuffix() ??
                                 throw new RebusConfigurationException(
                                     "Either an outbox name or a bus name must be specified");
                
                return new RedisOutboxStorage(
                    outboxName,
                    r.Get<RedisProvider>(),
                    config,
                    r.Get<IRebusLoggerFactory>());
            },"Outbox Storage");
        
        configurer.OtherService<IOutboxQueueStorage>()
            .Register(r =>
            {
                var config = r.Get<RedisOutboxConfiguration>();
                var options = r.Get<Options>();
                var outboxName = config.OutboxName ??
                                 options.OptionalBusName.AddSuffix() ??
                                 throw new RebusConfigurationException(
                                     "Either an outbox name or a bus name must be specified");
                
                return new RedisOutboxQueueStorage(outboxName);
            });
    }

    private static string? AddSuffix(this string? name) => name is null ? null : $"{name}-outbox";
}