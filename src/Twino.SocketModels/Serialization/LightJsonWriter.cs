using System;
using System.IO;
using Newtonsoft.Json;

namespace Twino.SocketModels.Serialization
{
    /// <summary>
    /// Wrapper class for Newtonsoft JsonTextWriter.
    /// This class simplifies JsonTextWriter's usage and adds some functionalities.
    /// </summary>
    public class LightJsonWriter : IDisposable
    {
        
        #region Fields - Properties
        
        /// <summary>
        /// Newtonsoft JSON text writer
        /// </summary>
        public JsonTextWriter Writer { get; }

        /// <summary>
        /// System string writer
        /// </summary>
        private StringWriter _stringWriter;

        #endregion
        
        #region Init - Dispose
        
        public LightJsonWriter()
        {
            _stringWriter = new StringWriter();
            Writer = new JsonTextWriter(_stringWriter);
        }

        public void Dispose()
        {
            _stringWriter?.Dispose();
            _stringWriter = null;
        }

        #endregion
        
        #region Write
        
        /// <summary>
        /// Writes a start object token to JSON string.
        /// </summary>
        public void StartObject()
        {
            Writer.WriteStartObject();
        }
        
        /// <summary>
        /// Writes two tokens to JSON. One for property name, other for value.
        /// </summary>
        public void Write(string propertyName, object value)
        {
            Writer.WritePropertyName(propertyName);
            Writer.WriteValue(value);
        }

        /// <summary>
        /// Writes a end object token to JSON string.
        /// </summary>
        public void EndObject()
        {
            Writer.WriteEndObject();
        }

        /// <summary>
        /// Gets the string JSON result
        /// </summary>
        public string GetResult()
        {
            return _stringWriter.ToString();
        }

        /// <summary>
        /// Serializes an object.
        /// Calls StartObject, model Serialize and EndObject methods and returns with GetResult method.
        /// </summary>
        public string Serialize(IPerformanceCriticalModel model)
        {
            StartObject();
            model.Serialize(this);
            EndObject();

            return GetResult();
        }
        
        #endregion
        
    }
}