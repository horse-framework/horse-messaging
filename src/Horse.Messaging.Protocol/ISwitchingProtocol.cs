using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Horse.Core;

namespace Horse.Messaging.Protocol;

/// <summary>
/// Custom protocol for MQ Clients.
/// Protocol implementation, If a client uses different protocol. 
/// </summary>
public interface ISwitchingProtocol
{
    /// <summary>
    /// Name of the protocol such as horse, websocket
    /// </summary>
    string ProtocolName { get; }

    /// <summary>
    /// Sends PING over custom protocol
    /// </summary>
    void Ping();

    /// <summary>
    /// Sends PONG over custom protocol
    /// </summary>
    void Pong(object pingMessage = null);

    /// <summary>
    /// Sends a HorseMessage over custom protocol
    /// </summary>
    bool Send(HorseMessage message, IList<KeyValuePair<string, string>> additionalHeaders = null);

    /// <summary>
    /// Sends a HorseMessage over custom protocol
    /// </summary>
    Task<bool> SendAsync(HorseMessage message, IList<KeyValuePair<string, string>> additionalHeaders = null);

    /// <summary>
    /// Sends raw data over custom protocol
    /// </summary>
    Task<bool> SendAsync(byte[] data);
    
    /// <summary>
    /// Reads protocol messages over network stream and converts the messages to horse message types
    /// </summary>
    Task<HorseMessage> Read(Stream stream);

    /// <summary>
    /// Completes protocol handshake in client side
    /// </summary>
    Task ClientProtocolHandshake(ConnectionData data, Stream stream);
}