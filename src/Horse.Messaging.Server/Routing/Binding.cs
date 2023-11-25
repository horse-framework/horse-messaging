using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;

namespace Horse.Messaging.Server.Routing;

/// <summary>
/// Router binding
/// </summary>
public abstract class Binding
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
    /// Parent Router object of the binding
    /// </summary>
    internal Router Router { get; set; }

    /// <summary>
    /// Routing method
    /// </summary>
    public RouteMethod RouteMethod { get; set; }

    /// <summary>
    /// Sends the message to binding receivers
    /// </summary>
    public abstract Task<bool> Send(MessagingClient sender, HorseMessage message);
}