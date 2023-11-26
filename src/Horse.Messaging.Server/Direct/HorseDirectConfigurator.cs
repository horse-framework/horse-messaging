using Horse.Messaging.Server.Containers;

namespace Horse.Messaging.Server.Direct;

/// <summary>
/// Direct message configurator
/// </summary>
public class HorseDirectConfigurator
{
    /// <summary>
    /// Direct message event handlers
    /// </summary>
    public ArrayContainer<IDirectMessageHandler> MessageHandlers => Rider.Direct.MessageHandlers;

    /// <summary>
    /// Horse rider
    /// </summary>
    public HorseRider Rider { get; }

    internal HorseDirectConfigurator(HorseRider rider)
    {
        Rider = rider;
    }
}