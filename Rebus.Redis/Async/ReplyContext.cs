using System;

namespace Rebus.Redis.Async;

/// <summary>
/// Represents a recipient waiting for a reply. This class is a POCO and is safe to serialize/deserialize as well as
/// storing the <see cref="SubscriberID" /> and <see cref="MessageID" /> properties and reconstructing the object
/// later.
/// </summary>
public class ReplyContext
{
    /// <summary>Creates a new reply context.</summary>
    /// <param name="senderAddress">The sender address of the recipient, must not be null.</param>
    /// <param name="subscriberID">The subscriber ID of the recipient, must not be null.</param>
    /// <param name="messageID">The message ID of the recipient, must not be null.</param>
    public ReplyContext(string senderAddress, string subscriberID, string messageID)
    {
        SenderAddress = senderAddress ?? throw new ArgumentNullException(nameof(senderAddress));
        SubscriberID = subscriberID ?? throw new ArgumentNullException(nameof(subscriberID));
        MessageID = messageID ?? throw new ArgumentNullException(nameof(messageID));
    }

    /// <summary>Gets the sender address of the recipient.</summary>
    public string SenderAddress { get; }

    /// <summary>Gets the subscriber ID of the recipient.</summary>
    public string SubscriberID { get; }

    /// <summary>Gets the response ID of the recipient.</summary>
    public string MessageID { get; }
}