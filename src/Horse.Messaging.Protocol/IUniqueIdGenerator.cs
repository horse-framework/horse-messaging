namespace Horse.Messaging.Protocol
{
    /// <summary>
    /// Unique Id generator implementation for HorseMessage message id and client unique id creation
    /// </summary>
    public interface IUniqueIdGenerator
    {
        /// <summary>
        /// Creates new unique id
        /// </summary>
        string Create();
    }
}