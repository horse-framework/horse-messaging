namespace Twino.SocketModels.Serialization
{
    /// <summary>
    /// Model writing inteface for web socket string messages
    /// </summary>
    public interface IModelWriter
    {
        /// <summary>
        /// Creates serialized string message from T model
        /// </summary>
        string Serialize(ISocketModel model);

    }
}