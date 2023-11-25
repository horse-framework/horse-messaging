namespace Horse.Messaging.Protocol.Models;

/// <summary>
/// Router binding information
/// </summary>
public class BindingInformation
{
    /// <summary>
    /// Unique name of the binding
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Binding target name.
    /// For queue bindings, queue name.
    /// For direct bindings client id, type or name.
    /// </summary>
    public string Target { get; set; }

    /// <summary>
    /// Binding content type.
    /// Null, passes same content type from producer to receiver
    /// </summary>
    public ushort? ContentType { get; set; }

    /// <summary>
    /// Binding priority
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Binding interaction type
    /// </summary>
    public BindingInteraction Interaction { get; set; }

    /// <summary>
    /// Binding type
    /// </summary>
    public string BindingType { get; set; }

    /// <summary>
    /// Routing method in binding
    /// </summary>
    public RouteMethod Method { get; set; }
}