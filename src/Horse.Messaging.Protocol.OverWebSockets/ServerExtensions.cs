using Horse.Messaging.Protocol;
using Horse.Server;
using Horse.WebSocket.Protocol;

namespace Horse.Messaging.Server.OverWebSockets;

/// <summary>
/// Server Extension methods for Horse Protocol over WebSocket configuration
/// </summary>
public static class ServerExtensions
{
    /// <summary>
    /// Uses Horse Protocol over WebSockets
    /// </summary>
    public static HorseServer UseHorseOverWebsockets(this HorseServer server, Action<HostOptions> configure)
    {
        HorseProtocol horseProtocol = server.FindProtocol("horse") as HorseProtocol;

        if (horseProtocol == null)
            throw new InvalidOperationException("Complete Horse implementation before using Horse over WebSockets");

        OverWsHandler handler = new OverWsHandler(horseProtocol.GetHandler());
        HorseWebSocketProtocol protocol = new HorseWebSocketProtocol(server, handler);
        server.UseProtocol(protocol);

        HostOptions options = new HostOptions();
        configure(options);
        server.Options.Hosts.Add(options);

        return server;
    }

    /// <summary>
    /// Uses Horse Protocol over WebSockets
    /// </summary>
    public static HorseServer UseHorseOverWebsockets(this HorseServer server, HostOptions host)
    {
        HorseProtocol horseProtocol = server.FindProtocol("horse") as HorseProtocol;

        if (horseProtocol == null)
            throw new InvalidOperationException("Complete Horse implementation before using Horse over WebSockets");

        OverWsHandler handler = new OverWsHandler(horseProtocol.GetHandler());
        HorseWebSocketProtocol protocol = new HorseWebSocketProtocol(server, handler);
        server.UseProtocol(protocol);

        server.Options.Hosts.Add(host);

        return server;
    }
}