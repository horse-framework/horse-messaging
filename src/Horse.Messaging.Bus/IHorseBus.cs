using Horse.Messaging.Client.Bus;

namespace Horse.Messaging.Bus
{
    /// <summary>
    /// Used for using multiple horse bus in same provider.
    /// Template type is the identifier
    /// </summary>
    public interface IHorseBus<TIdentifier> : IHorseBus
    {
    }
}