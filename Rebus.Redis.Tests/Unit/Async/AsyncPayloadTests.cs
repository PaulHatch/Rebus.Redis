using System.Text;
using FluentAssertions;
using Rebus.Messages;
using Rebus.Redis.Async;
using Xunit;

namespace Rebus.Redis.Tests.Unit.Async;

public class AsyncPayloadTests
{
    [Fact]
    public void Success_ShouldCreatePayloadCorrectly()
    {
        var messageId = Guid.NewGuid().ToString();
        var headers = new Dictionary<string, string>
        {
            [Headers.ContentType] = "application/json",
            [Headers.Type] = "TestMessage"
        };
        var body = Encoding.UTF8.GetBytes("test message body");
        var transportMessage = new TransportMessage(headers, body);

        var payload = AsyncPayload.Success(messageId, transportMessage);

        payload.Should().NotBeNull();
        payload.MessageID.Should().Be(messageId);
        payload.Headers.Should().ContainKey(Headers.MessageId);
        payload.Headers[Headers.MessageId].Should().Be(messageId);
        payload.Body.Should().Be(Convert.ToBase64String(body));
        payload.ResponseType.Should().Be(ResponseType.Success);
    }

    [Fact]
    public void Failed_ShouldCreateErrorPayload()
    {
        var messageId = Guid.NewGuid().ToString();
        var errorMessage = "Test error message";

        var payload = AsyncPayload.Failed(messageId, errorMessage);

        payload.Should().NotBeNull();
        payload.MessageID.Should().Be(messageId);
        payload.Body.Should().Be(errorMessage);
        payload.ResponseType.Should().Be(ResponseType.Error);
    }

    [Fact]
    public void Cancelled_ShouldCreateCancelledPayload()
    {
        var messageId = Guid.NewGuid().ToString();

        var payload = AsyncPayload.Cancelled(messageId);

        payload.Should().NotBeNull();
        payload.MessageID.Should().Be(messageId);
        payload.Body.Should().BeEmpty();
        payload.ResponseType.Should().Be(ResponseType.Cancelled);
    }

    [Fact]
    public void ToTransportMessage_ShouldConvertBackCorrectly()
    {
        var originalHeaders = new Dictionary<string, string>
        {
            [Headers.MessageId] = Guid.NewGuid().ToString(),
            [Headers.Type] = "TestMessage"
        };
        var originalBody = new byte[] { 1, 2, 3, 4, 5 };
        var base64Body = Convert.ToBase64String(originalBody);
        var payload = new AsyncPayload(originalHeaders, base64Body, ResponseType.Success);

        var transportMessage = payload.ToTransportMessage();

        transportMessage.Headers.Should().BeEquivalentTo(originalHeaders);
        transportMessage.Body.Should().BeEquivalentTo(originalBody);
    }

    [Fact]
    public void JsonSerialization_ShouldPreserveAllProperties()
    {
        var messageId = Guid.NewGuid().ToString();
        var headers = new Dictionary<string, string>
        {
            [Headers.MessageId] = messageId,
            [Headers.Type] = "TestMessage"
        };
        var body = Convert.ToBase64String(new byte[] { 10, 20, 30 });
        var payload = new AsyncPayload(headers, body, ResponseType.Error);

        var json = payload.ToJson();
        var deserialized = AsyncPayload.FromJson(json);

        deserialized.Should().NotBeNull();
        deserialized.Headers.Should().BeEquivalentTo(payload.Headers);
        deserialized.Body.Should().Be(payload.Body);
        deserialized.ResponseType.Should().Be(payload.ResponseType);
    }

    [Fact]
    public void Constructor_WithNullParameters_ShouldUseDefaults()
    {
        var payload = new AsyncPayload();

        payload.Headers.Should().NotBeNull();
        payload.Headers.Should().BeEmpty();
        payload.Body.Should().NotBeNull();
        payload.Body.Should().BeEmpty();
        payload.ResponseType.Should().Be(ResponseType.Success);
    }

    [Fact]
    public void Success_WithLargePayload_ShouldHandleCorrectly()
    {
        var messageId = Guid.NewGuid().ToString();
        var largeBody = new byte[1024 * 1024]; // 1MB
        Random.Shared.NextBytes(largeBody);
        var transportMessage = new TransportMessage(
            new Dictionary<string, string> { [Headers.Type] = "LargeMessage" }, 
            largeBody);

        var payload = AsyncPayload.Success(messageId, transportMessage);

        payload.Body.Should().Be(Convert.ToBase64String(largeBody));
        var decoded = Convert.FromBase64String(payload.Body);
        decoded.Should().BeEquivalentTo(largeBody);
    }
}