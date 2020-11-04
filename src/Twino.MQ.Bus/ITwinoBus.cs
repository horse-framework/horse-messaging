using Twino.MQ.Client.Bus;

namespace Twino.MQ.Bus
{
    /// <summary>
    /// Used for using multiple twino bus in same provider.
    /// Template type is the identifier
    /// </summary>
    public interface ITwinoBus<TIdentifier> : ITwinoBus
    {
    }
}