namespace Horse.Messaging.Protocol;

/// <summary>
/// Implementation of clients which uses another protocol.
/// Switching protocol is being used as underyling protocol and it transmits messages of Horse Protocol. 
/// </summary>
public interface ISwitchingProtocolClient
{
    /// <summary>
    /// Custom protocol for the client
    /// </summary>
    public ISwitchingProtocol SwitchingProtocol { get; set; }
}