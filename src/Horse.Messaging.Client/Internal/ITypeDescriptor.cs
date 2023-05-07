using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Internal
{
    /// <summary>
    /// Descriptor implementation for sending model types
    /// </summary>
    public interface ITypeDescriptor
    {
        /// <summary>
        /// Creates new horse message from descriptor
        /// </summary>
        HorseMessage CreateMessage(string overwrittenTarget = null);
    }
}