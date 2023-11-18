using System;
using System.Threading;
using System.Threading.Tasks;
using Rebus.Bus;
using Rebus.Logging;
using Rebus.Threading;
using Rebus.Transport;

namespace Rebus.Redis.Outbox;

internal class OutboxForwarder : IDisposable, IInitializable
{
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly CancellationToken _cancellationToken;
    private readonly IOutboxStorage _outboxStorage;
    private readonly ITransport _transport;
    private readonly IAsyncTask? _forwarder;
    private readonly IAsyncTask? _orphanedForwarder;
    private readonly IAsyncTask? _cleanup;


    public OutboxForwarder(
        IAsyncTaskFactory asyncTaskFactory,
        IRebusLoggerFactory rebusLoggerFactory,
        IOutboxStorage outboxStorage,
        ITransport transport,
        RedisOutboxConfiguration config)
    {
        if (asyncTaskFactory == null) throw new ArgumentNullException(nameof(asyncTaskFactory));
        _outboxStorage = outboxStorage;
        _transport = transport;
        var logger = rebusLoggerFactory.GetLogger<OutboxForwarder>();

        _cancellationTokenSource = new CancellationTokenSource();
        _cancellationToken = _cancellationTokenSource.Token;

        if (config.ForwardingEnabled)
        {
            _forwarder = asyncTaskFactory.Create("Outbox Forwarder", config.UseBlockingRead ? RunBlockingForwarder : RunForwarder,
                intervalSeconds: config.UseBlockingRead ? 0 : (int) config.ForwardingInterval.TotalSeconds);
        }
        else
        {
            logger.Info("Outbox forwarding is disabled");
        }

        if (config.CleanupEnabled)
        {
            _cleanup = asyncTaskFactory.Create("Outbox Cleanup", RunCleanup,
                intervalSeconds: (int) config.CleanupInterval.TotalSeconds);
        }
        else
        {
            logger.Info("Outbox cleanup is disabled");
        }

        if (config.OrphanedForwardingEnabled)
        {
            _orphanedForwarder = asyncTaskFactory.Create("Orphaned Forwarder", RunOrphanedForwarder,
                intervalSeconds: (int) config.OrphanedForwardingInterval.TotalSeconds);
        }
        else
        {
            logger.Info("Orphaned forwarding is disabled");
        }
    }

    private async Task RunBlockingForwarder()
    {
        while (IsRunning)
        {
            var messages = await _outboxStorage.GetNextMessageBatch();

            if (messages is null) continue;

            using var scope = new RebusTransactionScope();
            foreach (var message in messages)
            {
                var destinationAddress = message.DestinationAddress;
                var transportMessage = message.ToTransportMessage();
                var transactionContext = scope.TransactionContext;

                await _sendRetryUtility.ExecuteAsync(() =>
                    _transport.Send(destinationAddress, transportMessage, transactionContext), _cancellationToken);

                await _outboxStorage.MarkAsDispatched(message);
            }
            await scope.CompleteAsync();
        }
    }

    private async Task RunCleanup()
    {
        await _outboxStorage.CleanupIdleConsumers();
        await _outboxStorage.TrimQueue();
    }

    public void Initialize()
    {
        _forwarder?.Start();
        _cleanup?.Start();
    }

    private bool IsRunning => !_cancellationToken.IsCancellationRequested;

    private async Task RunForwarder()
    {
        // this value is used to loop until there are no more messages send, otherwise under heavy load we would be
        // waiting between batches for the polling interval even though there are more messages to send
        bool anySent;

        do
        {
            anySent = false;
            var messages = await _outboxStorage.GetNextMessageBatch();

            if (messages is null) continue;

            using var scope = new RebusTransactionScope();
            foreach (var message in messages)
            {
                anySent = true;
                var destinationAddress = message.DestinationAddress;
                var transportMessage = message.ToTransportMessage();
                var transactionContext = scope.TransactionContext;

                await _sendRetryUtility.ExecuteAsync(() =>
                    _transport.Send(destinationAddress, transportMessage, transactionContext), _cancellationToken);

                await _outboxStorage.MarkAsDispatched(message);
            }

            await scope.CompleteAsync();
        } while (anySent);
    }

    private async Task RunOrphanedForwarder()
    {
        // this value is used to loop until there are no more messages send, otherwise under heavy load we would be
        // waiting between batches for the polling interval even though there are more messages to send
        bool anySent;

        do
        {
            anySent = false;
            var messages = await _outboxStorage.GetOrphanedMessageBatch();

            if (messages is null) continue;

            using var scope = new RebusTransactionScope();
            foreach (var message in messages)
            {
                anySent = true;
                var destinationAddress = message.DestinationAddress;
                var transportMessage = message.ToTransportMessage();
                var transactionContext = scope.TransactionContext;

                await _sendRetryUtility.ExecuteAsync(() =>
                    _transport.Send(destinationAddress, transportMessage, transactionContext), _cancellationToken);

                await _outboxStorage.MarkAsDispatched(message);
            }

            await scope.CompleteAsync();
        } while (anySent && IsRunning);
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _forwarder?.Dispose();
        _cleanup?.Dispose();
        _orphanedForwarder?.Dispose();
        _cancellationTokenSource.Dispose();
    }

    private static readonly RetryUtility _sendRetryUtility = new(new[]
    {
        TimeSpan.FromSeconds(0.1),
        TimeSpan.FromSeconds(0.1),
        TimeSpan.FromSeconds(0.1),
        TimeSpan.FromSeconds(0.1),
        TimeSpan.FromSeconds(0.1),
        TimeSpan.FromSeconds(0.5),
        TimeSpan.FromSeconds(0.5),
        TimeSpan.FromSeconds(0.5),
        TimeSpan.FromSeconds(0.5),
        TimeSpan.FromSeconds(0.5),
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(1),
    });
}