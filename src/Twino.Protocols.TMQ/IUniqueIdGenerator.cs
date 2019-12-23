namespace Twino.Protocols.TMQ
{
    /// <summary>
    /// Unique Id generator implementation for TmqMessage message id and client unique id creation
    /// </summary>
    public interface IUniqueIdGenerator
    {
        /// <summary>
        /// Creates new unique id
        /// </summary>
        string Create();
    }
}