using System;
using Newtonsoft.Json;

namespace Twino.SerializableModel.Serialization
{
    /// <summary>
    /// Default model reader for Twino libraries
    /// </summary>
    public class TwinoModelReader : IModelReader
    {
        
        private static TwinoModelReader _default;

        /// <summary>
        /// Default, singleton TwinoModelReader class
        /// </summary>
        public static TwinoModelReader Default
        {
            get
            {
                if (_default == null)
                    _default = new TwinoModelReader();

                return _default;
            }
        }
        
        /// <summary>
        /// Reads T model from serialized string message
        /// </summary>
        public T Read<T>(string serialized) where T : ISerializableModel, new()
        {
            return Read<T>(serialized, true);
        }

        /// <summary>
        /// Reads T model from serialized string message
        /// </summary>
        public T Read<T>(string serialized, bool verify) where T : ISerializableModel, new()
        {
            int bodyStartIndex;
            int type = ReadType(serialized, out bodyStartIndex);
            if (type == 0)
                return default;

            string body = ReadBody(serialized, bodyStartIndex);

            bool critical = typeof(IPerformanceCriticalModel).IsAssignableFrom(typeof(T));
            if (critical)
            {
                LightJsonReader reader = new LightJsonReader(body);
                T model = new T();

                IPerformanceCriticalModel pcm = (IPerformanceCriticalModel) model;

                reader.StartObject();
                pcm.Deserialize(reader);
                reader.EndObject();
                
                if (verify && model.Type != type)
                    return default;

                return model;
            }
            else
            {
                T model = JsonConvert.DeserializeObject<T>(body);
                if (verify && model.Type != type)
                    return default;

                return model;
            }
        }

        /// <summary>
        /// Reads T model from serialized string message
        /// </summary>
        public ISerializableModel Read(Type type, string serialized)
        {
            return Read(type, serialized, true);
        }

        /// <summary>
        /// Reads T model from serialized string message
        /// </summary>
        public ISerializableModel Read(Type type, string serialized, bool verify)
        {
            int bodyStartIndex;
            int mtype = ReadType(serialized, out bodyStartIndex);
            if (mtype == 0)
                return null;

            string body = ReadBody(serialized, bodyStartIndex);
            ISerializableModel model;
            bool critical = typeof(IPerformanceCriticalModel).IsAssignableFrom(type);
            if (critical)
            {
                LightJsonReader reader = new LightJsonReader(body);
                
                model = (ISerializableModel) Activator.CreateInstance(type);
                IPerformanceCriticalModel pcm = (IPerformanceCriticalModel)model;

                reader.StartObject();
                pcm.Deserialize(reader);
                reader.EndObject();
            }
            else
            {
                object result = JsonConvert.DeserializeObject(body, type);
                model = (ISerializableModel) result;
            }

            if (model == null || verify && model.Type != mtype)
                return null;

            return model;
        }

        /// <summary>
        /// Reads only model type from serialized message
        /// </summary>
        public int ReadType(string serialized)
        {
            int type = ReadType(serialized, out _);
            return type;
        }

        /// <summary>
        /// Reads only model type from serialized message and returns the index of the body model
        /// </summary>
        private static int ReadType(string message, out int bodyStartIndex)
        {
            int start = -1;
            int end = -1;

            for (int i = 0; i < message.Length; i++)
            {
                if (i > 20)
                {
                    bodyStartIndex = -1;
                    return 0;
                }

                char c = message[i];
                if (c == ' ')
                    continue;

                if (start < 0)
                {
                    if (c == '[')
                        start = i + 1;
                }
                else
                {
                    if (c == ',')
                    {
                        end = i;
                        break;
                    }
                }
            }

            if (start < 0 || end < 0)
            {
                bodyStartIndex = -1;
                return 0;
            }

            bodyStartIndex = end + 1;
            string str = message.Substring(start, end - start).Trim();
            return Convert.ToInt32(str);
        }

        /// <summary>
        /// Reads body from starting index
        /// </summary>
        private static string ReadBody(string message, int startIndex)
        {
            int endIndex = -1;
            bool braceletClosed = false;
            for (int i = message.Length - 1; i > 0; i--)
            {
                char c = message[i];
                if (c == ' ')
                    continue;

                if (c == ']')
                    braceletClosed = true;
                else if (!braceletClosed)
                    return null;
                else
                {
                    endIndex = i + 1;
                    break;
                }
            }

            if (endIndex < startIndex)
                return null;

            string body = message.Substring(startIndex, endIndex - startIndex);
            return body;
        }
    }
}