using System;

namespace Twino.SocketModels.Serialization
{
    /// <summary>
    /// Model reading inteface for web socket string messages
    /// </summary>
    public interface IModelReader
    {
        /// <summary>
        /// Reads T model from serialized string message
        /// </summary>
        T Read<T>(string serialized) where T : ISocketModel, new();

        /// <summary>
        /// Reads T model from serialized string message
        /// </summary>
        T Read<T>(string serialized, bool verify) where T : ISocketModel, new();

        /// <summary>
        /// Reads T model from serialized string message
        /// </summary>
        ISocketModel Read(Type type, string serialized);

        /// <summary>
        /// Reads T model from serialized string message
        /// </summary>
        ISocketModel Read(Type type, string serialized, bool verify);

        /// <summary>
        /// Reads only model type from serialized message
        /// </summary>
        int ReadType(string serialized);
        
    }
}