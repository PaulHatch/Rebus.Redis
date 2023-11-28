using Rebus.Pipeline;
using Rebus.Transport;
using StackExchange.Redis;

namespace Rebus.Redis;

/// <summary>
/// Provider for <see cref="StackExchange.Redis.RedisTransaction"/>, this is one of the two primary classes that are
/// responsible for managing the Redis connection, specifically providing scoped transactional support.
/// </summary>
internal class RedisProvider
{
    internal const string CurrentOutboxConnectionKey = "redis-outbox-connection";
    private readonly IConnectionMultiplexer _redis;

    /// <summary>
    /// Creates a new instance of the <see cref="RedisProvider"/> class.
    /// </summary>
    /// <param name="redis">The Redis connection to use.</param>
    public RedisProvider(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    /// <summary>
    /// Creates a <see cref="RedisTransaction"/> with a new transaction. Note that this is the only place where new
    /// Redis transactions are created.
    /// </summary>
    /// <returns>A new <see cref="RedisTransaction"/> instance.</returns>
    public RedisTransaction GetWithTransaction()
    {
        return new RedisTransaction(_redis, _redis.GetDatabase().CreateTransaction());
    }

    /// <summary>
    /// Creates a <see cref="RedisTransaction"/> without a transaction, which can be used to run operations outside
    /// the context of a transaction.
    /// </summary>
    /// <returns>A new <see cref="RedisTransaction"/> instance.</returns>
    public RedisTransaction GetWithoutTransaction()
    {
        return new RedisTransaction(_redis);
    }

    /// <summary>
    /// Gets the database for the Redis connection. Used to to run operations that never need to be part of a transaction.
    /// </summary>
    public IDatabaseAsync Database => _redis.GetDatabase();

    /// <summary>
    /// Gets an existing <see cref="RedisTransaction"/> from the current Rebus transaction scope if one exists, or
    /// creates a new non-transactional <see cref="RedisTransaction"/> if one does not exist.
    /// </summary>
    /// <param name="context">
    /// The Rebus <see cref="ITransactionContext"/> to check for a transaction. If none is provided, the current
    /// "MessageContext.Current.TransactionContext" is used.
    /// </param>
    /// <returns>The existing <see cref="RedisTransaction"/> instance or a new one if none exists.</returns>
    public RedisTransaction GetForScope(ITransactionContext? context = null)
    {
        var currentContext = context ?? MessageContext.Current?.TransactionContext;

        if (currentContext?.Items.TryGetValue(CurrentOutboxConnectionKey, out var result) == true &&
            result is RedisTransaction redisTransaction)
        {
            return redisTransaction;
        }

        return GetWithoutTransaction();
    }
}