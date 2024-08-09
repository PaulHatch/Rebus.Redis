namespace Rebus.Redis;

/// <summary>
/// Represents the async mode for Redis async support.
/// </summary>
public enum AsyncMode
{
    /// <summary>
    /// Enables async support using redis for both client and host mode.
    /// </summary>
    Both,

    /// <summary>
    /// Enables async support using redis for client mode only. This allows requests to be sent and awaited.
    /// </summary>
    Client,

    /// <summary>
    /// Enables async support using redis for host mode only. This allows requests to be received and replied to.
    /// </summary>
    Host
}