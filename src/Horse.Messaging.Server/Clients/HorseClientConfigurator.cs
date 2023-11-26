using Horse.Messaging.Server.Containers;
using Horse.Messaging.Server.Security;

namespace Horse.Messaging.Server.Clients;

/// <summary>
/// Horse client configurator
/// </summary>
public class HorseClientConfigurator
{
    /// <summary>
    /// Client connect and disconnect operations
    /// </summary>
    public ArrayContainer<IClientHandler> Handlers => Rider.Client.Handlers;

    /// <summary>
    /// Client authenticator implementations.
    /// If null, all clients will be accepted.
    /// </summary>
    public ArrayContainer<IClientAuthenticator> Authenticators => Rider.Client.Authenticators;

    /// <summary>
    /// Authorization implementations for client operations
    /// </summary>
    public ArrayContainer<IClientAuthorization> Authorizations => Rider.Client.Authorizations;

    /// <summary>
    /// Authorization implementations for administration operations
    /// </summary>
    public ArrayContainer<IAdminAuthorization> AdminAuthorizations => Rider.Client.AdminAuthorizations;

    /// <summary>
    /// Horse rider
    /// </summary>
    public HorseRider Rider { get; }

    internal HorseClientConfigurator(HorseRider rider)
    {
        Rider = rider;
    }
}