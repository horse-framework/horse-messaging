using System;
using System.IO;
using Newtonsoft.Json;

namespace Twino.SerializableModel.Serialization
{
    /// <summary>
    /// Wrapper class for Newtonsoft JsonTextReader.
    /// This class simplifies JsonTextReader's usage and adds some functionalities.
    /// </summary>
    public class LightJsonReader : IDisposable
    {
        
        #region Fields - Properties
        
        /// <summary>
        /// Newtonsoft JSON text reader
        /// </summary>
        public JsonTextReader Reader { get; }

        /// <summary>
        /// System string reader
        /// </summary>
        private StringReader _stringReader;

        #endregion
        
        #region Init - Dispose
        
        public LightJsonReader(string json)
        {
            _stringReader = new StringReader(json);
            Reader = new JsonTextReader(_stringReader);
        }

        public void Dispose()
        {
            _stringReader?.Dispose();
            _stringReader = null;
        }
        
        #endregion
        
        #region Read

        /// <summary>
        /// Deserializes IPerformanceCriticalModel, calls Deserialize method of the model.
        /// </summary>
        public T Deserialize<T>() where T : IPerformanceCriticalModel, new()
        {
            T result = new T();
            StartObject();
            result.Deserialize(this);
            EndObject();

            return result;
        }

        /// <summary>
        /// Reads a token from JSON string. Returns true if the token is start object
        /// </summary>
        public bool StartObject()
        {
            Reader.Read();
            return Reader.TokenType == JsonToken.StartObject;
        }

        /// <summary>
        /// Reads a token from JSON string. Returns true if the token is end object
        /// </summary>
        public bool EndObject()
        {
            Reader.Read();
            return Reader.TokenType == JsonToken.EndObject;
        }

        /// <summary>
        /// Reads two token from JSON, one for property name, other for value.
        /// If propertName is null, property name is ignored.
        /// If propertName is not null, equaility will be checked.
        /// If token types or property name does not match InvalidCastException is thrown.
        /// </summary>
        public object Read(string propertName = null)
        {
            bool read = Reader.Read();
            if (!read)
                return default;

            if (Reader.TokenType != JsonToken.PropertyName)
                throw new InvalidCastException("LightJsonParser could not read property name");

            if (!string.IsNullOrEmpty(propertName) && propertName != Reader.Value.ToString())
                throw new InvalidCastException("LightJsonParser could not recognize read property name");

            read = Reader.Read();

            return !read ? default : Reader.Value;
        }

        /// <summary>
        /// Reads value as T type
        /// </summary>
        public T Read<T>(string propertName = null)
        {
            object value = Read(propertName);
            return (T) Convert.ChangeType(value, typeof(T));
        }
        
        #endregion

        #region Primitive Types
        
        /// <summary>
        /// Reads value and directly casts it to string
        /// </summary>
        public string ReadString(string propertName = null)
        {
            return Read(propertName).ToString();
        }

        /// <summary>
        /// Reads value and directly converts it to byte
        /// </summary>
        public byte ReadByte(string propertyName = null)
        {
            return Convert.ToByte(Read(propertyName));
        }

        /// <summary>
        /// Reads value and directly converts it to short
        /// </summary>
        public short ReadInt16(string propertyName = null)
        {
            return Convert.ToInt16(Read(propertyName));
        }

        /// <summary>
        /// Reads value and directly converts it to float
        /// </summary>
        public float ReadSingle(string propertyName = null)
        {
            return Convert.ToSingle(Read(propertyName));
        }
        
        /// <summary>
        /// Reads value and directly converts it to int
        /// </summary>
        public int ReadInt32(string propertyName = null)
        {
            return Convert.ToInt32(Read(propertyName));
        }

        /// <summary>
        /// Reads value and directly converts it to long
        /// </summary>
        public long ReadInt64(string propertyName = null)
        {
            return Convert.ToInt64(Read(propertyName));
        }
        
        /// <summary>
        /// Reads value and directly converts it to double
        /// </summary>
        public double ReadDouble(string propertyName = null)
        {
            return Convert.ToDouble(Read(propertyName));
        }

        /// <summary>
        /// Reads value and directly converts it to C# decimal
        /// </summary>
        public decimal ReadDecimal(string propertyName = null)
        {
            return Convert.ToDecimal(Read(propertyName));
        }

        #endregion
        
    }
}