using FluentAssertions;
using NSubstitute;
using Rebus.Redis.Subscriptions;
using StackExchange.Redis;
using Xunit;

namespace Rebus.Redis.Tests.Unit.Subscriptions;

public class RedisSubscriptionStorageTests
{
    private readonly IDatabase _database;
    private readonly IConnectionMultiplexer _connection;
    private readonly RedisSubscriptionStorage _storage;
    private readonly string _prefix = "subscriptions:test-bus:";

    public RedisSubscriptionStorageTests()
    {
        _database = Substitute.For<IDatabase>();
        _connection = Substitute.For<IConnectionMultiplexer>();
        _connection.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_database);
        
        _storage = new RedisSubscriptionStorage(_connection, _prefix, isCentralized: false);
    }

    [Fact]
    public async Task GetSubscriberAddresses_WhenSubscribersExist_ShouldReturnAddresses()
    {
        var topic = "TestTopic";
        var subscribers = new RedisValue[]
        {
            "subscriber1@host1",
            "subscriber2@host2",
            "subscriber3@host3"
        };
        
        _database.SetMembersAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(subscribers);

        var result = await _storage.GetSubscriberAddresses(topic);

        result.Should().HaveCount(3);
        result.Should().Contain(subscribers.Select(s => s.ToString()));
        await _database.Received(1).SetMembersAsync(
            Arg.Is<RedisKey>(k => k.ToString() == $"{_prefix}{topic}"),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task GetSubscriberAddresses_WhenNoSubscribers_ShouldReturnEmptyArray()
    {
        var topic = "EmptyTopic";
        
        _database.SetMembersAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns([]);

        var result = await _storage.GetSubscriberAddresses(topic);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task RegisterSubscriber_ShouldAddSubscriberToSet()
    {
        var topic = "TestTopic";
        var subscriberAddress = "subscriber@host";
        
        _database.SetAddAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(true);

        await _storage.RegisterSubscriber(topic, subscriberAddress);

        await _database.Received(1).SetAddAsync(
            Arg.Is<RedisKey>(k => k.ToString() == $"{_prefix}{topic}"),
            subscriberAddress,
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task RegisterSubscriber_WhenAlreadyRegistered_ShouldNotThrow()
    {
        var topic = "TestTopic";
        var subscriberAddress = "subscriber@host";
        
        _database.SetAddAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(false); // Indicates already exists

        var action = async () => await _storage.RegisterSubscriber(topic, subscriberAddress);

        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UnregisterSubscriber_ShouldRemoveSubscriberFromSet()
    {
        var topic = "TestTopic";
        var subscriberAddress = "subscriber@host";
        
        _database.SetRemoveAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(true);

        await _storage.UnregisterSubscriber(topic, subscriberAddress);

        await _database.Received(1).SetRemoveAsync(
            Arg.Is<RedisKey>(k => k.ToString() == $"{_prefix}{topic}"),
            subscriberAddress,
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task UnregisterSubscriber_WhenNotRegistered_ShouldNotThrow()
    {
        var topic = "TestTopic";
        var subscriberAddress = "subscriber@host";
        
        _database.SetRemoveAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(false); // Indicates didn't exist

        var action = async () => await _storage.UnregisterSubscriber(topic, subscriberAddress);

        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task IsCentralized_WhenTrue_ShouldUseSharedKey()
    {
        var connection = Substitute.For<IConnectionMultiplexer>();
        connection.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_database);
        
        var centralizedPrefix = "subscriptions:";
        var centralizedStorage = new RedisSubscriptionStorage(connection, centralizedPrefix, isCentralized: true);
        var topic = "TestTopic";
        
        _database.SetMembersAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns([]);

        await centralizedStorage.GetSubscriberAddresses(topic);

        await _database.Received(1).SetMembersAsync(
            Arg.Is<RedisKey>(k => k.ToString() == $"{centralizedPrefix}{topic}"),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task MultipleTopics_ShouldMaintainSeparateSubscriberLists()
    {
        var topic1 = "Topic1";
        var topic2 = "Topic2";
        var subscriber1 = "subscriber1@host";
        var subscriber2 = "subscriber2@host";
        
        _database.SetAddAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(true);

        await _storage.RegisterSubscriber(topic1, subscriber1);
        await _storage.RegisterSubscriber(topic2, subscriber2);

        await _database.Received(1).SetAddAsync(
            Arg.Is<RedisKey>(k => k.ToString().Contains(topic1)),
            subscriber1,
            Arg.Any<CommandFlags>());
        
        await _database.Received(1).SetAddAsync(
            Arg.Is<RedisKey>(k => k.ToString().Contains(topic2)),
            subscriber2,
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task RegisterMultipleSubscribersToSameTopic_ShouldAddAll()
    {
        var topic = "TestTopic";
        var subscribers = new[] { "sub1@host", "sub2@host", "sub3@host" };
        
        _database.SetAddAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(true);

        foreach (var subscriber in subscribers)
        {
            await _storage.RegisterSubscriber(topic, subscriber);
        }

        foreach (var subscriber in subscribers)
        {
            await _database.Received(1).SetAddAsync(
                Arg.Is<RedisKey>(k => k.ToString() == $"{_prefix}{topic}"),
                subscriber,
                Arg.Any<CommandFlags>());
        }
    }
}