using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rebus.Subscriptions;
using StackExchange.Redis;

namespace Rebus.Redis.Subscriptions;

/// <summary>
/// Stores subscriptions in Redis.
/// </summary>
public class RedisSubscriptionStorage : ISubscriptionStorage
{
    private readonly IConnectionMultiplexer _connnection;
    private readonly string _prefix;

    /// <summary>
    /// Creates a new instance of the Redis subscription storage.
    /// </summary>
    /// <param name="connection">Connection to use.</param>
    /// <param name="prefix">Key prefix.</param>
    /// <param name="isCentralized">True if a single Redis instance is used for all buses.</param>
    public RedisSubscriptionStorage(
        IConnectionMultiplexer connection,
        string prefix,
        bool isCentralized)
    {
        _connnection = connection;
        _prefix = prefix;
        IsCentralized = isCentralized;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetSubscriberAddresses(string topic)
    {
        var db = _connnection.GetDatabase();
        var members = await db.SetMembersAsync(GetKey(topic));
        return members.Select(m => m.ToString()).ToList();
    }

    /// <inheritdoc />
    public Task RegisterSubscriber(string topic, string subscriberAddress)
    {
        var db = _connnection.GetDatabase();
        return db.SetAddAsync(GetKey(topic), subscriberAddress);
    }

    /// <inheritdoc />
    public Task UnregisterSubscriber(string topic, string subscriberAddress)
    {
        var db = _connnection.GetDatabase();
        return db.SetRemoveAsync(GetKey(topic), subscriberAddress);
    }

    /// <inheritdoc />
    public bool IsCentralized { get; }

    private RedisKey GetKey(string topic) => string.Concat(_prefix, topic);
}