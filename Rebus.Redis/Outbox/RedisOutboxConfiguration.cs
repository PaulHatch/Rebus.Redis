using System;

namespace Rebus.Redis.Outbox;

public class RedisOutboxConfiguration
{
    /// <summary>
    /// Specifies the name of the outbox which will be used as the key for the Redis stream used by the outbox, if not
    /// set, the Rebus bus name will be used. If no bus name is set an exception will be thrown.
    /// </summary>
    public RedisOutboxConfiguration UseOutboxName(string outboxName)
    {
        if (string.IsNullOrWhiteSpace(outboxName))
        {
            throw new ArgumentException("Outbox name cannot be null or whitespace.", nameof(outboxName));
        }

        OutboxName = outboxName;
        return this;
    }

    /// <summary>
    /// Sets a consumer group name for the outbox consumer, defaults to "outbox-consumer".
    /// </summary>
    public RedisOutboxConfiguration UseConsumerGroupName(string consumerGroupName)
    {
        if (string.IsNullOrWhiteSpace(consumerGroupName))
        {
            throw new ArgumentException("Consumer group name cannot be null or whitespace.", nameof(consumerGroupName));
        }

        ConsumerGroupName = consumerGroupName;
        return this;
    }

    /// <summary>
    /// Sets a consumer name for the outbox consumer. If not set, a random GUID will be generated. This could be useful
    /// to improve monitoring if you have known consumer names you can provide.
    /// </summary>
    public RedisOutboxConfiguration UseConsumerName(string consumerName)
    {
        if (string.IsNullOrWhiteSpace(consumerName))
        {
            throw new ArgumentException("Consumer name cannot be null or whitespace.", nameof(consumerName));
        }

        ConsumerName = consumerName;
        return this;
    }

    /// <summary>
    /// Sets the minimum time before an idle consumer will be removed from the consumer group. Defaults to 15 minutes.
    /// This will be used to remove consumers that have crashed or been shut down without removing themselves from the
    /// consumer group. Must be at least 1 second.
    /// </summary>
    public RedisOutboxConfiguration SetIdleConsumerTimeout(TimeSpan idleConsumerTimeout)
    {
        if (idleConsumerTimeout < TimeSpan.FromSeconds(1))
        {
            throw new ArgumentOutOfRangeException(nameof(idleConsumerTimeout));
        }

        IdleConsumerTimeout = idleConsumerTimeout;
        return this;
    }

    /// <summary>
    /// Sets the interval between cleanup runs. Defaults to 1 minute. The cleanup task runs to remove idle consumers
    /// and trim the outbox queue. Must be at least 1 second.
    /// </summary>
    public RedisOutboxConfiguration SetCleanupInterval(TimeSpan cleanupInterval)
    {
        if (cleanupInterval < TimeSpan.FromSeconds(1))
        {
            throw new ArgumentOutOfRangeException(nameof(cleanupInterval));
        }

        CleanupInterval = cleanupInterval;
        return this;
    }

    /// <summary>
    /// Sets either the blocking duration or the polling interval to check for new messages depending on whether
    /// blocking reads is enabled. Defaults to 5 seconds.
    /// </summary>
    public RedisOutboxConfiguration SetForwardingInterval(TimeSpan forwardingInterval)
    {
        if (forwardingInterval < TimeSpan.FromSeconds(1))
        {
            throw new ArgumentOutOfRangeException(nameof(forwardingInterval));
        }

        ForwardingInterval = forwardingInterval;
        return this;
    }

    /// <summary>
    /// Sets the number of messages to trim the outbox stream to. Defaults to 1000.
    /// </summary>
    public RedisOutboxConfiguration SetTrimSize(int trimSize)
    {
        if (trimSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(trimSize));
        }

        TrimSize = trimSize;
        return this;
    }

    /// <summary>
    /// Sets the number of messages to read from the outbox stream in a single batch. Defaults to 100.
    /// </summary>
    public RedisOutboxConfiguration SetMessageBatchSize(int messageBatchSize)
    {
        if (messageBatchSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(messageBatchSize));
        }

        MessageBatchSize = messageBatchSize;
        return this;
    }

    /// <summary>
    /// Sets the time before a message is considered orphaned and will be retried. This occurs when another consumer
    /// consumes an outgoing message from the outbox stream but does not acknowledge having sent it within the given
    /// time. Defaults to 30 seconds, must be at least 1 second.
    /// </summary>
    public RedisOutboxConfiguration SetOrphanedMessageTimeout(TimeSpan orphanedMessageTimeout)
    {
        if (orphanedMessageTimeout < TimeSpan.FromSeconds(1))
        {
            throw new ArgumentOutOfRangeException(nameof(orphanedMessageTimeout));
        }

        OrphanedMessageTimeout = orphanedMessageTimeout;
        return this;
    }

    /// <summary>
    /// Enables (default) or disables the cleanup task.
    /// </summary>
    public RedisOutboxConfiguration EnableCleanup(bool enableCleanup = true)
    {
        CleanupEnabled = true;
        return this;
    }

    /// <summary>
    /// Enables (default) or disables forwarding of outgoing messages. This must be enabled on at least one job
    /// somewhere in order for outgoing messages to be sent.
    /// </summary>
    public RedisOutboxConfiguration EnableForwarding(bool enableForwarding = true)
    {
        ForwardingEnabled = enableForwarding;
        return this;
    }
    
    /// <summary>
    /// Enables (default) or disables forwarding of outgoing messages. This must be enabled on at least one job
    /// somewhere in order for outgoing messages to be sent.
    /// </summary>
    public RedisOutboxConfiguration EnableOrphanedForwarding(bool enableOrphanedForwarding = true)
    {
        OrphanedForwardingEnabled = enableOrphanedForwarding;
        return this;
    }
    
    /// <summary>
    /// Enables or disables blocking read mode for the main outbox forwarder. Defaults to true. When enabled, the outbox
    /// will use a blocking read on a dedicated connection to wait for new messages. When disabled, the outbox will poll
    /// for new messages.
    /// </summary>
    public RedisOutboxConfiguration EnableBlockingRead(bool enableBlockingRead = true)
    {
        UseBlockingRead = enableBlockingRead;
        return this;
    }

    internal bool CleanupEnabled { get; private set; } = true;
    internal bool ForwardingEnabled { get; private set; } = true;
    internal bool OrphanedForwardingEnabled { get; private set; } = true;

    internal int TrimSize { get; private set; } = 1000;
    internal TimeSpan CleanupInterval { get; private set; } = TimeSpan.FromMinutes(1);
    internal TimeSpan ForwardingInterval { get; private set; } = TimeSpan.FromSeconds(5);
    internal TimeSpan OrphanedForwardingInterval { get; private set; } = TimeSpan.FromSeconds(1);
    internal string? OutboxName { get; private set; }
    internal string? ConsumerGroupName { get; private set; } = "outbox-consumer";
    internal string? ConsumerName { get; private set; } = Guid.NewGuid().ToString();
    internal TimeSpan IdleConsumerTimeout { get; private set; } = TimeSpan.FromMinutes(15);
    internal int MessageBatchSize { get; private set; } = 100;
    internal TimeSpan OrphanedMessageTimeout { get; private set; } = TimeSpan.FromSeconds(30);
    internal bool UseBlockingRead { get; private set; } = true;
}