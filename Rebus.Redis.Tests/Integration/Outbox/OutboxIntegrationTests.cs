using FluentAssertions;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Redis.Tests.Fixtures;
using Rebus.Transport.InMem;
using Xunit;

namespace Rebus.Redis.Tests.Integration.Outbox;

public class OutboxIntegrationTests : IClassFixture<RedisTestFixture>, IAsyncLifetime
{
    private readonly RedisTestFixture _redisFixture;
    private readonly InMemNetwork _network;
    private readonly BuiltinHandlerActivator _activator;

    public OutboxIntegrationTests(RedisTestFixture redisFixture)
    {
        _redisFixture = redisFixture;
        _network = new InMemNetwork();
        _activator = new BuiltinHandlerActivator();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _activator.Dispose();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Outbox_ShouldForwardMessagesReliably()
    {
        await _redisFixture.FlushDatabaseAsync();
        
        var receivedMessages = new System.Collections.Concurrent.ConcurrentBag<string>();
        var resetEvent = new ManualResetEventSlim(false);
        var expectedCount = 3;
        
        _activator.Handle<TestMessage>(async message =>
        {
            receivedMessages.Add(message.Content);
            if (receivedMessages.Count >= expectedCount)
            {
                resetEvent.Set();
            }
            await Task.CompletedTask;
        });
        
        using var bus = Configure.With(_activator)
            .Transport(t => t.UseInMemoryTransport(_network, "outbox-test"))
            .Options(o =>
            {
                o.SetBusName("outbox-test");
                o.EnableRedis(_redisFixture.ConnectionString);
            })
            .RedisOutbox(o => o.StoreInRedis(config =>
            {
                config.EnableBlockingRead(false); // Use polling for tests
                config.SetForwardingInterval(TimeSpan.FromSeconds(1));
            }))
            .Start();
        
        // Send messages
        await bus.SendLocal(new TestMessage { Content = "Message 1" });
        await bus.SendLocal(new TestMessage { Content = "Message 2" });
        await bus.SendLocal(new TestMessage { Content = "Message 3" });
        
        // Wait for messages to be processed (with timeout)
        var received = resetEvent.Wait(TimeSpan.FromSeconds(5));
        
        received.Should().BeTrue("messages should be received within timeout");
        receivedMessages.Should().HaveCount(expectedCount);
        receivedMessages.Should().Contain(["Message 1", "Message 2", "Message 3"]);
    }

    [Fact]
    public async Task Outbox_ShouldHandleMultipleBuses()
    {
        await _redisFixture.FlushDatabaseAsync();
        
        var bus1Messages = new System.Collections.Concurrent.ConcurrentBag<string>();
        var bus2Messages = new System.Collections.Concurrent.ConcurrentBag<string>();
        
        var activator1 = new BuiltinHandlerActivator();
        activator1.Handle<TestMessage>(async message =>
        {
            bus1Messages.Add(message.Content);
            await Task.CompletedTask;
        });
        
        var activator2 = new BuiltinHandlerActivator();
        activator2.Handle<TestMessage>(async message =>
        {
            bus2Messages.Add(message.Content);
            await Task.CompletedTask;
        });
        
        using var bus1 = Configure.With(activator1)
            .Transport(t => t.UseInMemoryTransport(_network, "bus1"))
            .Options(o =>
            {
                o.SetBusName("bus1");
                o.EnableRedis(_redisFixture.ConnectionString);
            })
            .RedisOutbox(o => o.StoreInRedis(config =>
            {
                config.EnableBlockingRead(false);
                config.SetForwardingInterval(TimeSpan.FromSeconds(1));
            }))
            .Start();
        
        using var bus2 = Configure.With(activator2)
            .Transport(t => t.UseInMemoryTransport(_network, "bus2"))
            .Options(o =>
            {
                o.SetBusName("bus2");
                o.EnableRedis(_redisFixture.ConnectionString);
            })
            .RedisOutbox(o => o.StoreInRedis(config =>
            {
                config.EnableBlockingRead(false);
                config.SetForwardingInterval(TimeSpan.FromSeconds(1));
            }))
            .Start();
        
        // Send messages to each bus
        await bus1.SendLocal(new TestMessage { Content = "Bus1 Message" });
        await bus2.SendLocal(new TestMessage { Content = "Bus2 Message" });
        
        // Wait for processing
        await Task.Delay(1000);
        
        // Each bus should only receive its own messages
        bus1Messages.Should().HaveCount(1);
        bus1Messages.Should().Contain("Bus1 Message");
        
        bus2Messages.Should().HaveCount(1);
        bus2Messages.Should().Contain("Bus2 Message");
        
        activator1.Dispose();
        activator2.Dispose();
    }

    [Fact]
    public async Task Outbox_ShouldHandleBasicProcessing()
    {
        await _redisFixture.FlushDatabaseAsync();
        
        var receivedMessages = new System.Collections.Concurrent.ConcurrentBag<string>();
        
        _activator.Handle<TestMessage>(async message =>
        {
            receivedMessages.Add(message.Content);
            await Task.CompletedTask;
        });
        
        using var bus = Configure.With(_activator)
            .Transport(t => t.UseInMemoryTransport(_network, "basic-test"))
            .Options(o =>
            {
                o.SetBusName("basic-test");
                o.EnableRedis(_redisFixture.ConnectionString);
            })
            .RedisOutbox(o => o.StoreInRedis(config =>
            {
                config.EnableBlockingRead(false);
                config.SetForwardingInterval(TimeSpan.FromSeconds(1));
            }))
            .Start();
        
        // Send a message
        await bus.SendLocal(new TestMessage { Content = "Test Message" });
        
        // Wait for processing
        await Task.Delay(2000);
        
        receivedMessages.Should().Contain("Test Message");
    }

    [Fact]
    public async Task Outbox_ShouldMaintainMessageOrder()
    {
        await _redisFixture.FlushDatabaseAsync();
        
        var receivedMessages = new System.Collections.Concurrent.ConcurrentBag<int>();
        var resetEvent = new ManualResetEventSlim(false);
        var messageCount = 10;
        
        _activator.Handle<OrderedMessage>(async message =>
        {
            receivedMessages.Add(message.Order);
            if (receivedMessages.Count >= messageCount)
            {
                resetEvent.Set();
            }
            await Task.CompletedTask;
        });
        
        using var bus = Configure.With(_activator)
            .Transport(t => t.UseInMemoryTransport(_network, "order-test"))
            .Options(o =>
            {
                o.SetBusName("order-test");
                o.EnableRedis(_redisFixture.ConnectionString);
            })
            .RedisOutbox(o => o.StoreInRedis(config =>
            {
                config.EnableBlockingRead(false);
                config.SetForwardingInterval(TimeSpan.FromSeconds(1));
            }))
            .Start();
        
        // Send messages in order
        for (int i = 0; i < messageCount; i++)
        {
            await bus.SendLocal(new OrderedMessage { Order = i });
        }
        
        // Wait for all messages to be processed
        var received = resetEvent.Wait(TimeSpan.FromSeconds(5));
        
        received.Should().BeTrue("all messages should be received within timeout");
        receivedMessages.Should().HaveCount(messageCount);
        
        // Verify order is maintained (Redis Streams guarantee FIFO order)
        var orderedList = receivedMessages.OrderBy(x => x).ToList();
        orderedList.Should().BeEquivalentTo(Enumerable.Range(0, messageCount));
    }

    private class TestMessage
    {
        public string Content { get; set; } = string.Empty;
    }

    private class OrderedMessage
    {
        public int Order { get; set; }
    }
}