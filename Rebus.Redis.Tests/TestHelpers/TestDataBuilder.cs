using System.Text;
using Rebus.Messages;
using Rebus.Redis.Tests.Fixtures;

namespace Rebus.Redis.Tests.TestHelpers;

public class TestDataBuilder
{
    public static TransportMessage CreateTransportMessage(
        string? messageId = null,
        string? messageType = null,
        Dictionary<string, string>? additionalHeaders = null,
        byte[]? body = null)
    {
        var headers = new Dictionary<string, string>
        {
            [Headers.MessageId] = messageId ?? Guid.NewGuid().ToString(),
            [Headers.Type] = messageType ?? "TestMessage",
            [Headers.ContentType] = "application/json",
            [Headers.SentTime] = DateTimeOffset.UtcNow.ToString("O")
        };
        
        if (additionalHeaders != null)
        {
            foreach (var kvp in additionalHeaders)
            {
                headers[kvp.Key] = kvp.Value;
            }
        }
        
        return new TransportMessage(
            headers,
            body ?? Encoding.UTF8.GetBytes("{}"));
    }

    public static TestSagaData CreateSagaDataWithState(
        string correlationId,
        int processingCount = 0,
        bool isCompleted = false,
        List<string>? items = null)
    {
        var sagaData = SagaTestFixture.CreateSagaData(correlationId);
        sagaData.ProcessingCount = processingCount;
        sagaData.IsCompleted = isCompleted;
        
        if (items != null)
        {
            sagaData.Items = items;
        }
        
        return sagaData;
    }

    public static List<TransportMessage> CreateBatchOfMessages(int count, string prefix = "msg")
    {
        var messages = new List<TransportMessage>();
        
        for (int i = 0; i < count; i++)
        {
            messages.Add(CreateTransportMessage(
                messageId: $"{prefix}-{i:D4}",
                body: Encoding.UTF8.GetBytes($"{{\"index\": {i}, \"data\": \"Message {i}\"}}")));
        }
        
        return messages;
    }

    public static Dictionary<string, string> CreateAsyncHeaders(
        string replyTo,
        TimeSpan? timeout = null,
        string? correlationId = null)
    {
        var headers = new Dictionary<string, string>
        {
            ["rbs2-reply-to"] = replyTo,
            ["rbs2-correlation-id"] = correlationId ?? Guid.NewGuid().ToString()
        };
        
        if (timeout.HasValue)
        {
            headers["rbs2-timeout"] = timeout.Value.TotalMilliseconds.ToString();
        }
        
        return headers;
    }
}