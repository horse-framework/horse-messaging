using System;
using Horse.Core.Protocols;
using Horse.Messaging.Protocol;
using Horse.Protocols.Http;
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
        InitializeHorseOverWebsockets(server);
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
        InitializeHorseOverWebsockets(server);
        server.Options.Hosts.Add(host);
        return server;
    }

    private static void InitializeHorseOverWebsockets(HorseServer server)
    {
        HorseProtocol horseProtocol = server.FindProtocol("horse") as HorseProtocol;

        if (horseProtocol == null)
            throw new InvalidOperationException("Complete Horse implementation before using Horse over WebSockets");

        IHorseProtocol http = server.FindProtocol("http");
        if (http == null)
        {
            HorseHttpProtocol httpProtocol = new HorseHttpProtocol(server, new WebSocketHttpHandler(), HttpOptions.CreateDefault());
            server.UseProtocol(httpProtocol);
        }

        OverWsHandler handler = new OverWsHandler(horseProtocol.GetHandler());
        HorseWebSocketProtocol protocol = new HorseWebSocketProtocol(server, handler);
        server.UseProtocol(protocol);
    }
}