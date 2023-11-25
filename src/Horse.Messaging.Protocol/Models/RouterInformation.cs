namespace Horse.Messaging.Protocol.Models;

/// <summary>
/// Router Information
/// </summary>
public class RouterInformation
{
    /// <summary>
    /// Route name.
    /// Must be unique.
    /// Can't include " ", "*" or ";"
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// If true, messages are routed to bindings.
    /// If false, messages are not routed.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Route method. Defines how messages will be routed.
    /// </summary>
    public RouteMethod Method { get; set; }
}