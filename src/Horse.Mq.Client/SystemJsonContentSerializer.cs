using System;
using System.IO;
using System.Text.Json;
using Horse.Protocols.Hmq;

namespace Horse.Mq.Client
{
    /// <summary>
    /// JSON Message Content serializer uses System.Text.Json library
    /// </summary>
    public class SystemJsonContentSerializer : IMessageContentSerializer
    {
        private readonly JsonSerializerOptions _options;

        /// <summary>
        /// Creates new JSON serializer
        /// </summary>
        public SystemJsonContentSerializer(JsonSerializerOptions options)
        {
            _options = options;
        }

        /// <summary>
        /// Serializes message content and converts the result to byte array
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="model">Model that will be serialized into the horse message</param>
        public void Serialize(HorseMessage message, object model)
        {
            byte[] array = JsonSerializer.SerializeToUtf8Bytes(model, model.GetType(), _options);
            message.Content = new MemoryStream(array);
            message.Content.Position = 0;
        }

        /// <summary>
        /// Deserializes message content and returns the object
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="type">Model type</param>
        public object Deserialize(HorseMessage message, Type type)
        {
            if (message.Content == null || message.Content.Length < 1)
                return null;

            ReadOnlySpan<byte> span = message.Content.ToArray();
            return JsonSerializer.Deserialize(span, type, _options);
        }
    }
}