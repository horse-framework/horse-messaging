namespace Horse.Messaging.Protocol.Models;

/// <summary>
/// Connected client information
/// </summary>
public class ClientInformation
{
    /// <summary>
    /// Client's unique id
    /// If it's null or empty, server will create new unique id for the client.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Client name
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Client type.
    /// If different type of clients join your server, you can categorize them with this type value
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Total online duration of client in milliseconds
    /// </summary>
    public long Online { get; set; }

    /// <summary>
    /// If true, client authenticated by server's IClientAuthenticator implementation
    /// </summary>
    public bool IsAuthenticated { get; set; }
}