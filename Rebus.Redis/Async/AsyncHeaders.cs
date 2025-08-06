namespace Rebus.Redis.Async;

internal static class AsyncHeaders
{
    /// <summary>The maximum time in milliseconds the caller has chosen to wait for a response.</summary>
    internal const string Timeout = "async-timeout";

    /// <summary>A prefix for message ID header value indicating it was sent requesting a Redis response.</summary>
    internal const string MessageIDPrefix = "redis-async:";
}