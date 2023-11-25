using Horse.Messaging.Protocol.Events;
using Horse.Messaging.Server.Containers;
using Horse.Messaging.Server.Events;

namespace Horse.Messaging.Server.Direct;

/// <summary>
/// Manages direct messages
/// </summary>
public class DirectRider
{
    /// <summary>
    /// Root horse rider object
    /// </summary>
    public HorseRider Rider { get; }

    /// <summary>
    /// Direct message event handlers
    /// </summary>
    public ArrayContainer<IDirectMessageHandler> MessageHandlers { get; } = new();

    /// <summary>
    /// Event Manager for HorseEventType.DirectMessage
    /// </summary>
    public EventManager DirectEvent { get; }

    /// <summary>
    /// Event Manager for HorseEventType.DirectMessageResponse
    /// </summary>
    public EventManager ResponseEvent { get; }

    /// <summary>
    /// Creates new direct rider
    /// </summary>
    internal DirectRider(HorseRider rider)
    {
        Rider = rider;
        DirectEvent = new EventManager(rider, HorseEventType.DirectMessage);
        ResponseEvent = new EventManager(rider, HorseEventType.DirectMessageResponse);
    }
}