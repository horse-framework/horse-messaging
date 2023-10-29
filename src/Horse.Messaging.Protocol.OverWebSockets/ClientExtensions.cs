using Horse.Messaging.Client;

namespace Horse.Messaging.Server.OverWebSockets;

/// <summary>
/// Client Extension methods for Horse Protocol over WebSocket configuration
/// </summary>
public static class ClientExtensions
{
    /// <summary>
    /// Uses WebSocket protocol as underlying protocol.
    /// Horse Protocol messages are sent and received over websocket protocol
    /// </summary>
    public static HorseClientBuilder UseHorseOverWebSockets(this HorseClientBuilder builder)
    {
        HorseClient client = builder.GetClient();
        SwitchingClientProtocol protocol = new SwitchingClientProtocol(client);
        builder.UseSwitchingProtocol(protocol);
        return builder;
    }

    /// <summary>
    /// Implements horse over websockets to the client
    /// </summary>
    public static void UseHorseOverWebSockets(this HorseClient client)
    {
        client.SwitchingProtocol = new SwitchingClientProtocol(client);
    }
}