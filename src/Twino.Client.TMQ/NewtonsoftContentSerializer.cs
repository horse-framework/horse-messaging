using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ
{
    /// <summary>
    /// JSON Message Content serializer uses Newtonsoft library
    /// </summary>
    public class NewtonsoftContentSerializer : IMessageContentSerializer
    {
        private readonly JsonSerializerSettings _settings;

        /// <summary>
        /// Creates new JSON serializer
        /// </summary>
        public NewtonsoftContentSerializer(JsonSerializerSettings settings = null)
        {
            _settings = settings;
        }

        /// <summary>
        /// Serializes message content and converts the result to byte array
        /// </summary>
        /// <param name="model">Model that will be serialized into the tmq message</param>
        /// <param name="message">Message</param>
        public void Serialize(object model, TmqMessage message)
        {
            var serialized = _settings != null
                                 ? JsonConvert.SerializeObject(model, model.GetType(), _settings)
                                 : JsonConvert.SerializeObject(model);

            byte[] bytes = Encoding.UTF8.GetBytes(serialized);
            message.Content = new MemoryStream(bytes);
            message.Content.Position = 0;
        }

        /// <summary>
        /// Deserializes message content and returns the object
        /// </summary>
        /// <param name="type">Model type</param>
        /// <param name="message">Message</param>
        public object Deserialize(Type type, TmqMessage message)
        {
            if (message.Content == null)
                return null;
            
            string content = message.GetStringContent();
            
            if (string.IsNullOrEmpty(content))
                return null;

            var model = _settings != null
                            ? JsonConvert.DeserializeObject(content, type, _settings)
                            : JsonConvert.DeserializeObject(content, type);

            return model;
        }
    }
}