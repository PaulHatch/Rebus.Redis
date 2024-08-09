using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rebus.Messages;
using Rebus.Pipeline;
using Rebus.Serialization;
using StackExchange.Redis;

namespace Rebus.Redis.Async;

/// <summary>
/// Helper class for receiving Redis async requests and sending responses.
/// </summary>
public static class AsyncHostRedisExtensions
{
    private static IConnectionMultiplexer? _connection;
    private static ISerializer? _serializer;
    private static IReadOnlyDictionary<string, IConnectionMultiplexer>? _additionalConnections;

    internal static void RegisterPublisher(
        IConnectionMultiplexer connection,
        ISerializer serializer,
        IReadOnlyDictionary<string, IConnectionMultiplexer> additionalConnections)
    {
        _connection = connection;
        _serializer = serializer;
        _additionalConnections = additionalConnections;
    }

    private static IDatabaseAsync GetDatabase(string address)
    {
        var connection = _additionalConnections?.ContainsKey(address) == true
            ? _additionalConnections[address]
            : _connection;

        return connection?.GetDatabase() ??
               throw new InvalidOperationException("Redis host support has not been initialized");
    }

    /// <summary>
    /// Gets a <see cref="ReplyContext" /> from a <see cref="IMessageContext" /> which can be used to reply to the
    /// message. If the message context does not contain the required headers, null is returned.
    /// </summary>
    /// <param name="context">Message context to extract reply context from.</param>
    /// <returns>A reply context or null if the reply headers are not present.</returns>
    public static ReplyContext? GetReplyContext(this IMessageContext context)
    {
        return GetContextFromHeaders(context.Headers, false);
    }

    /// <summary>
    /// Gets a <see cref="ReplyContext" /> from a <see cref="TransportMessage" /> which can be used to reply to the
    /// message. If the message context does not contain the required headers, null is returned.
    /// </summary>
    /// <param name="message">Message to extract reply context from.</param>
    /// <returns>A reply context or null if the reply headers are not present.</returns>
    public static ReplyContext? GetReplyContext(this TransportMessage message)
    {
        return GetContextFromHeaders(message.Headers, false);
    }

    /// <summary>
    /// Gets a <see cref="ReplyContext" /> from a <see cref="TransportMessage" /> similar to
    /// <see cref="GetReplyContext(TransportMessage)" /> except that the reply to header is the one considered as the
    /// destination.
    /// </summary>
    /// <param name="message">Message to extract reply context from.</param>
    /// <returns>A reply context or null if the reply headers are not present.</returns>
    internal static ReplyContext? GetReplyToContext(this TransportMessage message)
    {
        return GetContextFromHeaders(message.Headers, true);
    }

    /// <summary>
    /// Publishes a reply to the recipient from the reply context.
    /// </summary>
    /// <param name="context">Context to reply to.</param>
    /// <param name="response">
    /// Response to send. This value will be serialized and sent to to the pending task.
    /// </param>
    /// <typeparam name="TResponse">Response type to send.</typeparam>
    public static async Task RedisReplyAsync<TResponse>(this ReplyContext context, TResponse response)
    {
        var message = response switch
        {
            TransportMessage tm => tm,
            Message m => await (_serializer?.Serialize(m) ??
                                throw new InvalidOperationException("Redis host support has not been initialized")),
            _ => await (_serializer?.Serialize(new Message(new Dictionary<string, string>(), response)) ??
                        throw new InvalidOperationException("Redis host support has not been initialized"))
        };
        var payload = AsyncPayload.Success(context.MessageID, message);

        var db = GetDatabase(context.SenderAddress);
        await db.PublishAsync(RedisChannel.Literal(context.SubscriberID), payload.ToJson());
    }

    /// <summary>
    /// Publishes a failed response to the recipient from the reply context, the pending task will throw an exception
    /// using the provided message.
    /// </summary>
    /// <param name="context">Context to reply to.</param>
    /// <param name="message">The message for the exception to throw.</param>
    public static async Task RedisFailAsync(this ReplyContext context, string message)
    {
        var db = GetDatabase(context.SenderAddress);
        var payload = AsyncPayload.Failed(context.MessageID, message);
        await db.PublishAsync(RedisChannel.Literal(context.SubscriberID), payload.ToJson());
    }

    /// <summary>
    /// Publishes a cancel response to the recipient from the reply context, the pending task will be cancelled.
    /// </summary>
    /// <param name="context">Context to reply to.</param>
    public static async Task RedisCancelAsync(this ReplyContext context)
    {
        var db = GetDatabase(context.SenderAddress);
        var payload = AsyncPayload.Cancelled(context.MessageID);
        await db.PublishAsync(RedisChannel.Literal(context.SubscriberID), payload.ToJson());
    }

    /// <summary>
    /// Publishes a reply to the recipient from the message context, if no reply headers are present no action is taken.
    /// </summary>
    /// <param name="context">Message context to reply to.</param>
    /// <param name="response">
    /// Response to send. This value will be serialized and sent to to the pending task.
    /// </param>
    /// <typeparam name="TResponse">Response type to send.</typeparam>
    public static async Task RedisReplyAsync<TResponse>(this IMessageContext context, TResponse response)
    {
        var replyContext = context.GetReplyContext();
        if (replyContext is not null)
        {
            await replyContext.RedisReplyAsync(response);
        }
    }

    /// <summary>
    /// Publishes a failed response to the recipient from the reply context, the pending task will throw an exception
    /// using the provided message.
    /// </summary>
    /// <param name="context">Message context to reply to.</param>
    /// <param name="message">The message for the exception to throw.</param>
    public static async Task RedisFailAsync(this IMessageContext context, string message)
    {
        var replyContext = context.GetReplyContext();
        if (replyContext is not null)
        {
            await replyContext.RedisFailAsync(message);
        }
    }

    /// <summary>
    /// Publishes a cancel response to the recipient from the reply context, the pending task will be cancelled.
    /// </summary>
    /// <param name="context">Message context to reply to.</param>
    public static async Task RedisCancelAsync(this IMessageContext context)
    {
        var replyContext = context.GetReplyContext();
        if (replyContext is not null)
        {
            await replyContext.RedisCancelAsync();
        }
    }

    /// <summary>
    /// Gets the timeout set for the request from the message context so that the recipient of a message can determine
    /// how long the client will be waiting for a reply. If no timeout is set, null is returned.
    /// </summary>
    /// <param name="context">Message context to check.</param>
    /// <returns>The timeout duration, if set.</returns>
    public static TimeSpan? GetReplyTimeout(this IMessageContext context)
    {
        if (!context.Headers.ContainsKey(AsyncHeaders.Timeout) ||
            long.TryParse(context.Headers[AsyncHeaders.Timeout], out var timeout))
        {
            return null;
        }

        return TimeSpan.FromMilliseconds(timeout);
    }

    /// <summary>
    /// Produces a new <see cref="ReplyContext" /> based on the provided context information.
    /// </summary>
    /// <param name="headers"></param>
    /// <param name="fromReplyTo">
    /// Set to true to get headers for an outgoing message or false to get them from
    /// and incoming message.
    /// </param>
    /// <returns>A new reply context for the headers provided.</returns>
    private static ReplyContext? GetContextFromHeaders(IDictionary<string, string> headers, bool fromReplyTo)
    {
        var messageIDHeader = fromReplyTo ? Headers.InReplyTo : Headers.MessageId;
        if (!headers.ContainsKey(messageIDHeader) || !headers[messageIDHeader].StartsWith(AsyncHeaders.MessageIDPrefix))
        {
            return null;
        }

        var headerValue = headers[messageIDHeader];
        var subscriberID = headerValue.Substring(AsyncHeaders.MessageIDPrefix.Length, 36);
        var messageID = headerValue.Substring(AsyncHeaders.MessageIDPrefix.Length + 37, 36);
        var senderAddress = headers.TryGetValue(Headers.SenderAddress, out var header) ? header : "default";

        return new ReplyContext(senderAddress, subscriberID, messageID);
    }
}