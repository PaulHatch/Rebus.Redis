using FluentAssertions;
using NSubstitute;
using Rebus.Messages;
using Rebus.Pipeline;
using Rebus.Pipeline.Send;
using Rebus.Redis.Async;
using Rebus.Transport;
using Xunit;

namespace Rebus.Redis.Tests.Unit.Async;

public class ReplyStepHandlerTests
{
    private readonly ReplyStepHandler _handler;
    private readonly ITransactionContext _transactionContext;

    public ReplyStepHandlerTests()
    {
        _handler = new ReplyStepHandler();
        _transactionContext = Substitute.For<ITransactionContext>();
    }

    private OutgoingStepContext CreateContext(TransportMessage transportMessage)
    {
        var logicalMessage = new Message(transportMessage.Headers, new object());
        var destinationAddresses = new DestinationAddresses(["test-destination"]);
        var context = new OutgoingStepContext(logicalMessage, _transactionContext, destinationAddresses);
        context.Save(transportMessage);
        return context;
    }

    [Fact]
    public async Task Process_WithoutReplyContext_ShouldCallNext()
    {
        var message = new TransportMessage(
            new Dictionary<string, string> { ["test-header"] = "test-value" },
            [1, 2, 3]);
        var context = CreateContext(message);
        
        var nextCalled = false;
        Func<Task> next = () =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        await _handler.Process(context, next);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Process_WithException_ShouldPropagateException()
    {
        var message = new TransportMessage(
            new Dictionary<string, string>(),
            [1, 2, 3]);
        var context = CreateContext(message);
        
        var expectedException = new InvalidOperationException("Test exception");
        Func<Task> next = () => throw expectedException;

        var action = async () => await _handler.Process(context, next);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Test exception");
    }

    [Fact]
    public async Task Process_WithMultipleMessages_ShouldProcessEachCorrectly()
    {
        var processCount = 0;
        Func<Task> next = () =>
        {
            processCount++;
            return Task.CompletedTask;
        };

        var message1 = new TransportMessage(new Dictionary<string, string>(), []);
        var context1 = CreateContext(message1);
        await _handler.Process(context1, next);
        
        var message2 = new TransportMessage(new Dictionary<string, string>(), []);
        var context2 = CreateContext(message2);
        await _handler.Process(context2, next);

        processCount.Should().Be(2);
    }

    [Fact]
    public async Task Process_AlwaysCallsNextOrRedisReply()
    {
        // The handler should either call next() for normal messages
        // or handle Redis replies for messages with reply context
        // Since we can't easily mock the extension method GetReplyToContext,
        // we test that the handler completes without error
        
        var message = new TransportMessage(
            new Dictionary<string, string> { [Headers.MessageId] = Guid.NewGuid().ToString() },
            [1, 2, 3]);
        var context = CreateContext(message);
        
        var nextCalled = false;
        Func<Task> next = () =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        await _handler.Process(context, next);
        
        // For messages without reply context, next should be called
        nextCalled.Should().BeTrue();
    }
}