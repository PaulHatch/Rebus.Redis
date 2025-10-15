using FluentAssertions;
using Rebus.Redis.Async;
using Xunit;

namespace Rebus.Redis.Tests.Unit.Async;

public class ReplyContextTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        var senderAddress = "test-sender";
        var subscriberId = "subscriber-123";
        var messageId = "message-456";

        var context = new ReplyContext(senderAddress, subscriberId, messageId);

        context.Should().NotBeNull();
        context.SenderAddress.Should().Be(senderAddress);
        context.SubscriberID.Should().Be(subscriberId);
        context.MessageID.Should().Be(messageId);
    }

    [Fact]
    public void Constructor_WithNullSenderAddress_ShouldThrowArgumentNullException()
    {
        var action = () => new ReplyContext(null!, "subscriber-123", "message-456");

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("senderAddress");
    }

    [Fact]
    public void Constructor_WithNullSubscriberId_ShouldThrowArgumentNullException()
    {
        var action = () => new ReplyContext("test-sender", null!, "message-456");

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("subscriberID");
    }

    [Fact]
    public void Constructor_WithNullMessageId_ShouldThrowArgumentNullException()
    {
        var action = () => new ReplyContext("test-sender", "subscriber-123", null!);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("messageID");
    }

    [Fact]
    public void Properties_ShouldBeReadOnly()
    {
        var context = new ReplyContext("test-sender", "subscriber-123", "message-456");

        // Properties should be get-only
        context.SenderAddress.Should().Be("test-sender");
        context.SubscriberID.Should().Be("subscriber-123");
        context.MessageID.Should().Be("message-456");
    }

    [Fact]
    public void MultipleContexts_WithSameValues_ShouldBeDistinct()
    {
        var context1 = new ReplyContext("sender", "subscriber", "message");
        var context2 = new ReplyContext("sender", "subscriber", "message");

        context1.Should().NotBeSameAs(context2);
        context1.SenderAddress.Should().Be(context2.SenderAddress);
        context1.SubscriberID.Should().Be(context2.SubscriberID);
        context1.MessageID.Should().Be(context2.MessageID);
    }

    [Fact]
    public void Context_ShouldHandleSpecialCharacters()
    {
        var senderAddress = "test-sender:with:colons";
        var subscriberId = "subscriber|with|pipes";
        var messageId = "message/with/slashes";

        var context = new ReplyContext(senderAddress, subscriberId, messageId);

        context.SenderAddress.Should().Be(senderAddress);
        context.SubscriberID.Should().Be(subscriberId);
        context.MessageID.Should().Be(messageId);
    }
}