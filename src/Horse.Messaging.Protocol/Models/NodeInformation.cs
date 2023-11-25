namespace Horse.Messaging.Protocol.Models;

/// <summary>
/// Instance information
/// </summary>
public class NodeInformation
{
    /// <summary>
    /// Instance unique id
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Instance name
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Instance host name
    /// </summary>
    public string Host { get; set; }

    /// <summary>
    /// Node public host name
    /// </summary>
    public string PublicHost { get; set; }
        
    /// <summary>
    /// Node States: Main, Successor, Replica, Single
    /// </summary>
    public string State { get; set; }

    /// <summary>
    /// True, if connection is alive
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// lifetime in milliseconds
    /// </summary>
    public long Lifetime { get; set; }
}