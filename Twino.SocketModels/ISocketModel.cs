namespace Twino.SocketModels
{
    /// <summary>
    /// Network package interface.
    /// In order to use Twino.SocketModels library,
    /// all models must be implemented from this interface
    /// </summary>
    public interface ISocketModel
    {
        /// <summary>
        /// Model type as integer.
        /// All model types must have unique id.
        /// YOu can use some known model types via KnownModelTypes class
        /// </summary>
        int Type { get; set; }
    }
}
