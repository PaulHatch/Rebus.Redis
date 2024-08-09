using System;

namespace Rebus.Redis.Async;

/// <summary>
/// Represents an error returned for a Redis async call.
/// </summary>
public class RedisAsyncException : Exception
{
    /// <summary>
    /// Creates a new async exception.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="messageID">The ID of the message the exception was thrown for.</param>
    public RedisAsyncException(string message, string messageID) : base(message)
    {
        MessageID = messageID;
    }

    /// <summary>
    /// Gets the ID of the message the exception was thrown for.
    /// </summary>
    public string MessageID { get; }
}