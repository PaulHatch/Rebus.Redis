using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Rebus.Bus;
using Rebus.Logging;
using Rebus.Messages;
using Rebus.Serialization;
using StackExchange.Redis;

namespace Rebus.Redis.Async;

/// <summary>
/// Helper class for sending Redis async requests and receiving responses.
/// </summary>
public static class AsyncClientRedisExtensions
{
    private static bool _isInitialized;

    private static readonly ConcurrentDictionary<string, (TaskCompletionSource<object?> tcs, Type? type)> _messages =
        new();

    private static string? _subscriberID;
    private static ISerializer? _serializer;
    private static ILog? _log;

    internal static void RegisterListener(
        IConnectionMultiplexer redis,
        ISerializer serializer,
        IRebusLoggerFactory loggerFactory,
        CancellationToken shutdownToken)
    {
        if (_isInitialized)
        {
            throw new InvalidOperationException("The listener has already been initialized.");
        }

        var db = redis.GetDatabase();
        do
        {
            // This is really overkill...
            var subscriberID = Guid.NewGuid().ToString();
            var result = db.Execute("PUBSUB", "CHANNELS", subscriberID);
            if (!result.IsNull)
            {
                _subscriberID = subscriberID;
            }
        } while (_subscriberID is null);

        _isInitialized = true;
        _serializer = serializer;
        // AsyncClientRedisExtensions is static, so we can't use the logger factory directly on this class
        _log = loggerFactory.GetLogger<ReplyContext>();
        var subscriber = redis.GetSubscriber();
        subscriber.Subscribe(RedisChannel.Literal(_subscriberID), HandleResponseMessage);

        shutdownToken.Register(() =>
        {
            _log?.Info("Shutting down Redis listener");
            subscriber.UnsubscribeAll();
        });
    }

    private static async void HandleResponseMessage(RedisChannel channel, RedisValue value)
    {
        if (_serializer is null)
        {
            throw new /*Unreachable*/Exception("No serializer has been registered.");
        }

        if (value.IsNull)
        {
            throw new ArgumentNullException(nameof(value), "A null value was received from Redis.");
        }

        var payload = AsyncPayload.FromJson(value!);

        if (!_messages.TryRemove(payload.MessageID, out var pendingTask))
        {
            _log?.Warn("Received a reply for {messageId} but no pending task was found for that ID.",
                payload.MessageID);
            return;
        }

        switch (payload.ResponseType)
        {
            case ResponseType.Error:
                pendingTask.tcs.SetException(new RedisAsyncException(payload.Body, payload.MessageID));
                break;
            case ResponseType.Success:
                if (payload.Body.Length > 0)
                {
                    var message = await _serializer.Deserialize(payload.ToTransportMessage());
                    pendingTask.tcs.SetResult(message.Body);
                }
                else
                {
                    pendingTask.tcs.SetResult(null!);
                }

                break;
            case ResponseType.Cancelled:
                pendingTask.tcs.SetCanceled();
                break;
            default:
                return;
        }
    }

    /// <summary>
    /// Extension method on <see cref="IBus" /> that allows for asynchronously sending a request and dispatching
    /// the received reply to the continuation.
    /// </summary>
    /// <typeparam name="TReply">
    /// Specifies the expected type of the reply. Can be any type compatible with the actually
    /// received reply
    /// </typeparam>
    /// <param name="bus">The bus API to use when sending the request</param>
    /// <param name="request">The request message</param>
    /// <param name="optionalHeaders">Headers to be included in the request message</param>
    /// <param name="timeout">
    /// Optionally specifies the max time to wait for a reply. If this time is exceeded, a
    /// <see cref="TimeoutException" /> is thrown
    /// </param>
    /// <param name="externalCancellationToken">
    /// An external cancellation token from some outer context that cancels waiting for
    /// a reply
    /// </param>
    /// <returns></returns>
    public static async Task<TReply> SendRequest<TReply>(
        this IBus bus,
        object request,
        IDictionary<string, string>? optionalHeaders = null,
        TimeSpan? timeout = null,
        CancellationToken externalCancellationToken = default)
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("The Redis listener has not been initialized.");
        }

        if (bus == null)
        {
            throw new ArgumentNullException(nameof(bus));
        }

        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var maxWaitTime = timeout ?? TimeSpan.FromSeconds(15);
        if (optionalHeaders?.TryGetValue(Headers.MessageId, out var messageID) is not true)
        {
            messageID = Guid.NewGuid().ToString();
        }

        using var timeoutCancellationTokenSource = new CancellationTokenSource(maxWaitTime);
        using var cancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token,
                externalCancellationToken);

        var taskCompletionSource =
            new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var task = taskCompletionSource.Task;

        var timeoutCancellationToken = timeoutCancellationTokenSource.Token;
        var cancellationToken = cancellationTokenSource.Token;

        cancellationToken.Register(() =>
        {
            if (_messages.TryRemove(messageID, out _))
            {
                taskCompletionSource.SetCanceled();
            }
        }, false);

        if (!_messages.TryAdd(messageID, (taskCompletionSource, typeof(TReply))))
        {
            throw new /*Unreachable*/Exception(
                $"Could not add response ID {messageID} to the dictionary of pending messages.");
        }

        var headers = optionalHeaders ?? new Dictionary<string, string>();
        headers[AsyncHeaders.Timeout] = maxWaitTime.Milliseconds.ToString();
        headers[Headers.MessageId] = string.Concat(
            AsyncHeaders.MessageIDPrefix,
            _subscriberID ?? throw new /*Unreachable*/Exception("Subscriber ID is null"),
            ":",
            messageID);

        await bus.Send(request, headers);

        try
        {
            var result = await task;

            if (result is TReply reply)
            {
                return reply;
            }

            throw new InvalidCastException($"Could not return message {messageID} as a {typeof(TReply)}");
        }
        catch (OperationCanceledException) when (timeoutCancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Did not receive reply for request with in-reply-to ID '{messageID}' within {maxWaitTime} timeout");
        }
    }
}