using System;

namespace Twino.JsonModel.Serialization
{
    /// <summary>
    /// Model reading inteface for web socket string messages
    /// </summary>
    public interface IModelReader
    {
        /// <summary>
        /// Reads T model from serialized string message
        /// </summary>
        T Read<T>(string serialized) where T : IJsonModel, new();

        /// <summary>
        /// Reads T model from serialized string message
        /// </summary>
        T Read<T>(string serialized, bool verify) where T : IJsonModel, new();

        /// <summary>
        /// Reads T model from serialized string message
        /// </summary>
        IJsonModel Read(Type type, string serialized);

        /// <summary>
        /// Reads T model from serialized string message
        /// </summary>
        IJsonModel Read(Type type, string serialized, bool verify);

        /// <summary>
        /// Reads only model type from serialized message
        /// </summary>
        int ReadType(string serialized);
        
    }
}