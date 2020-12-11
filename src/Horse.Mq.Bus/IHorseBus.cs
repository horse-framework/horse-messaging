using Horse.Mq.Client.Bus;

namespace Horse.Mq.Bus
{
    /// <summary>
    /// Used for using multiple horse bus in same provider.
    /// Template type is the identifier
    /// </summary>
    public interface IHorseBus<TIdentifier> : IHorseBus
    {
    }
}