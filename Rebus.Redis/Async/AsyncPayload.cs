using System.Text.Json;
using System.Text.Json.Serialization;
using Rebus.Messages;

namespace Rebus.Redis.Async;

/// <summary>
/// Represents a response to a Redis async request to be delivered via Redis pub/sub.
/// </summary>
internal record AsyncPayload
{
    // The headers will be used for deserialization
    public static readonly HashSet<string> IncludedHeaders = new()
    {
        Rebus.Messages.Headers.Type,
        Rebus.Messages.Headers.ContentType,
        Rebus.Messages.Headers.ContentEncoding
    };

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [JsonConstructor]
    public AsyncPayload(
        Dictionary<string, string>? headers = default,
        string? body = default,
        ResponseType responseType = ResponseType.Success)
    {
        Headers = headers ?? new Dictionary<string, string>();
        Body = body ?? string.Empty;
        ResponseType = responseType;
    }

    // Success constructor
    private AsyncPayload(string messageID, TransportMessage message)
    {
        Headers = new Dictionary<string, string>(message.Headers
            .Where(h => IncludedHeaders.Contains(h.Key)))
        {
            [Rebus.Messages.Headers.MessageId] = messageID
        };
        Body = Convert.ToBase64String(message.Body);
        ResponseType = ResponseType.Success;
    }

    // Error constructor
    private AsyncPayload(string messageID, string errorMessage)
    {
        Headers = new Dictionary<string, string>
        {
            {Rebus.Messages.Headers.MessageId, messageID}
        };
        Body = errorMessage;
        ResponseType = ResponseType.Error;
    }

    // Cancelled constructor
    private AsyncPayload(string messageID)
    {
        Headers = new Dictionary<string, string>
        {
            {Rebus.Messages.Headers.MessageId, messageID}
        };
        Body = string.Empty;
        ResponseType = ResponseType.Cancelled;
    }

    public static AsyncPayload Success(string messageID, TransportMessage message) => new(messageID, message);
    public static AsyncPayload Failed(string messageID, string errorMessage) => new(messageID, errorMessage);
    public static AsyncPayload Cancelled(string messageID) => new(messageID);

    [JsonIgnore] public string MessageID => Headers[Rebus.Messages.Headers.MessageId];
    public Dictionary<string, string> Headers { get; }
    public string Body { get; }
    public ResponseType ResponseType { get; }

    public string ToJson() => JsonSerializer.Serialize(this, _jsonOptions);
    public TransportMessage ToTransportMessage() => new(Headers, Convert.FromBase64String(Body));

    public static AsyncPayload FromJson(string json) =>
        JsonSerializer.Deserialize<AsyncPayload>(json, _jsonOptions)
        ?? throw new InvalidOperationException("Could not deserialize AsyncPayload");
}