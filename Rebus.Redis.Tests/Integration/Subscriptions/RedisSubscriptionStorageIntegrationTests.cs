using FluentAssertions;
using Rebus.Redis.Subscriptions;
using Rebus.Redis.Tests.Fixtures;
using Xunit;

namespace Rebus.Redis.Tests.Integration.Subscriptions;

public class RedisSubscriptionStorageIntegrationTests : IClassFixture<RedisTestFixture>
{
    private readonly RedisTestFixture _redisFixture;

    public RedisSubscriptionStorageIntegrationTests(RedisTestFixture redisFixture)
    {
        _redisFixture = redisFixture;
    }

    [Fact]
    public async Task SubscriptionStorage_ShouldStoreAndRetrieveSubscriptions()
    {
        await _redisFixture.FlushDatabaseAsync();
        
        var storage = new RedisSubscriptionStorage(
            _redisFixture.Connection, 
            "test-subscriptions:", 
            isCentralized: false);
        
        var topic = "test-topic";
        var subscribers = new[]
        {
            "subscriber1@queue1",
            "subscriber2@queue2",
            "subscriber3@queue3"
        };
        
        // Register subscribers
        foreach (var subscriber in subscribers)
        {
            await storage.RegisterSubscriber(topic, subscriber);
        }
        
        // Retrieve subscribers
        var retrieved = await storage.GetSubscriberAddresses(topic);
        
        retrieved.Should().HaveCount(3);
        retrieved.Should().BeEquivalentTo(subscribers);
    }

    [Fact]
    public async Task SubscriptionStorage_ShouldHandleUnregistration()
    {
        await _redisFixture.FlushDatabaseAsync();
        
        var storage = new RedisSubscriptionStorage(
            _redisFixture.Connection, 
            "test-subscriptions:", 
            isCentralized: false);
        
        var topic = "unregister-test";
        var subscriber1 = "subscriber1@queue";
        var subscriber2 = "subscriber2@queue";
        
        // Register subscribers
        await storage.RegisterSubscriber(topic, subscriber1);
        await storage.RegisterSubscriber(topic, subscriber2);
        
        // Verify both are registered
        var before = await storage.GetSubscriberAddresses(topic);
        before.Should().HaveCount(2);
        
        // Unregister one
        await storage.UnregisterSubscriber(topic, subscriber1);
        
        // Verify only one remains
        var after = await storage.GetSubscriberAddresses(topic);
        after.Should().HaveCount(1);
        after.Should().Contain(subscriber2);
        after.Should().NotContain(subscriber1);
    }

    [Fact]
    public async Task SubscriptionStorage_ShouldHandleMultipleTopics()
    {
        await _redisFixture.FlushDatabaseAsync();
        
        var storage = new RedisSubscriptionStorage(
            _redisFixture.Connection, 
            "test-subscriptions:", 
            isCentralized: false);
        
        var topic1 = "topic1";
        var topic2 = "topic2";
        var subscriber1 = "subscriber1@queue";
        var subscriber2 = "subscriber2@queue";
        var subscriber3 = "subscriber3@queue";
        
        // Register different subscribers to different topics
        await storage.RegisterSubscriber(topic1, subscriber1);
        await storage.RegisterSubscriber(topic1, subscriber2);
        await storage.RegisterSubscriber(topic2, subscriber2);
        await storage.RegisterSubscriber(topic2, subscriber3);
        
        // Verify topic1 subscribers
        var topic1Subscribers = await storage.GetSubscriberAddresses(topic1);
        topic1Subscribers.Should().HaveCount(2);
        topic1Subscribers.Should().Contain([subscriber1, subscriber2]);
        
        // Verify topic2 subscribers
        var topic2Subscribers = await storage.GetSubscriberAddresses(topic2);
        topic2Subscribers.Should().HaveCount(2);
        topic2Subscribers.Should().Contain([subscriber2, subscriber3]);
    }

    [Fact]
    public async Task SubscriptionStorage_CentralizedMode_ShouldShareAcrossBuses()
    {
        await _redisFixture.FlushDatabaseAsync();
        
        // Create two storage instances with different prefixes but centralized mode
        var storage1 = new RedisSubscriptionStorage(
            _redisFixture.Connection, 
            "shared:", 
            isCentralized: true);
        
        var storage2 = new RedisSubscriptionStorage(
            _redisFixture.Connection, 
            "shared:", 
            isCentralized: true);
        
        var topic = "shared-topic";
        var subscriber1 = "bus1-subscriber@queue";
        var subscriber2 = "bus2-subscriber@queue";
        
        // Register from different storage instances
        await storage1.RegisterSubscriber(topic, subscriber1);
        await storage2.RegisterSubscriber(topic, subscriber2);
        
        // Both should see all subscribers
        var fromStorage1 = await storage1.GetSubscriberAddresses(topic);
        var fromStorage2 = await storage2.GetSubscriberAddresses(topic);
        
        fromStorage1.Should().HaveCount(2);
        fromStorage2.Should().HaveCount(2);
        fromStorage1.Should().BeEquivalentTo(fromStorage2);
        fromStorage1.Should().Contain([subscriber1, subscriber2]);
    }

    [Fact]
    public async Task SubscriptionStorage_NonCentralizedMode_ShouldIsolateByPrefix()
    {
        await _redisFixture.FlushDatabaseAsync();
        
        // Create two storage instances with different prefixes and non-centralized mode
        var storage1 = new RedisSubscriptionStorage(
            _redisFixture.Connection, 
            "bus1:", 
            isCentralized: false);
        
        var storage2 = new RedisSubscriptionStorage(
            _redisFixture.Connection, 
            "bus2:", 
            isCentralized: false);
        
        var topic = "isolated-topic";
        var subscriber1 = "bus1-subscriber@queue";
        var subscriber2 = "bus2-subscriber@queue";
        
        // Register to same topic from different storage instances
        await storage1.RegisterSubscriber(topic, subscriber1);
        await storage2.RegisterSubscriber(topic, subscriber2);
        
        // Each should only see their own subscribers
        var fromStorage1 = await storage1.GetSubscriberAddresses(topic);
        var fromStorage2 = await storage2.GetSubscriberAddresses(topic);
        
        fromStorage1.Should().HaveCount(1);
        fromStorage2.Should().HaveCount(1);
        fromStorage1.Should().Contain(subscriber1);
        fromStorage2.Should().Contain(subscriber2);
        fromStorage1.Should().NotContain(subscriber2);
        fromStorage2.Should().NotContain(subscriber1);
    }

    [Fact]
    public async Task SubscriptionStorage_DuplicateRegistration_ShouldBeIdempotent()
    {
        await _redisFixture.FlushDatabaseAsync();
        
        var storage = new RedisSubscriptionStorage(
            _redisFixture.Connection, 
            "test-subscriptions:", 
            isCentralized: false);
        
        var topic = "duplicate-test";
        var subscriber = "subscriber@queue";
        
        // Register same subscriber multiple times
        await storage.RegisterSubscriber(topic, subscriber);
        await storage.RegisterSubscriber(topic, subscriber);
        await storage.RegisterSubscriber(topic, subscriber);
        
        // Should still have only one entry
        var subscribers = await storage.GetSubscriberAddresses(topic);
        subscribers.Should().HaveCount(1);
        subscribers.Should().Contain(subscriber);
    }
}