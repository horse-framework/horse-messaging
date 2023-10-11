using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Models;

namespace Horse.Messaging.Client
{
    /// <summary>
    /// JSON Message Content serializer that uses <see cref="JsonSerializer"/> under the hood.
    /// </summary>
    public class SystemJsonContentSerializer : IMessageContentSerializer
    {
        private readonly JsonSerializerOptions _options;

        readonly BindingInformationSerializerContext _bindingContext;
        readonly CacheInformationSerializerContext _cacheContext;
        readonly ChannelInformationSerializerContext _channelContext;
        readonly ClientInformationSerializerContext _clientContext;
        readonly NodeInformationSerializerContext _nodeContext;
        readonly QueueInformationSerializerContext _queueContext;
        readonly RouterInformationSerializerContext _routerContext;
        readonly HorseMessageSerializerContext _horseMessageContext;

        /// <summary>
        /// Creates a new JSON serializer.
        /// </summary>
        public SystemJsonContentSerializer(JsonSerializerOptions options = null)
        {
            _options = options ?? new JsonSerializerOptions {PropertyNameCaseInsensitive = true};

            _bindingContext = new BindingInformationSerializerContext(new JsonSerializerOptions(_options));
            _cacheContext = new CacheInformationSerializerContext(new JsonSerializerOptions(_options));
            _channelContext = new ChannelInformationSerializerContext(new JsonSerializerOptions(_options));
            _clientContext = new ClientInformationSerializerContext(new JsonSerializerOptions(_options));
            _nodeContext = new NodeInformationSerializerContext(new JsonSerializerOptions(_options));
            _queueContext = new QueueInformationSerializerContext(new JsonSerializerOptions(_options));
            _routerContext = new RouterInformationSerializerContext(new JsonSerializerOptions(_options));
            _horseMessageContext = new HorseMessageSerializerContext(new JsonSerializerOptions(_options));
        }

        private JsonSerializerContext GetJsonContextForType(string type) => type switch
        {
            nameof(BindingInformation) => _bindingContext,
            nameof(CacheInformation) => _cacheContext,
            nameof(ChannelInformation) => _channelContext,
            nameof(ClientInformation) => _clientContext,
            nameof(NodeInformation) => _nodeContext,
            nameof(QueueInformation) => _queueContext,
            nameof(RouterInformation) => _routerContext,
            nameof(HorseMessage) => _horseMessageContext,

            _ => null
        };

        /// <summary>
        /// Serializes message content and converts the result to byte array
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="model">Model that will be serialized into the horse message</param>
        public void Serialize(HorseMessage message, object model)
        {
            var context = GetJsonContextForType(model.GetType().Name);

            byte[] array;

            if (context != null)
                array = JsonSerializer.SerializeToUtf8Bytes(model, model.GetType(), context);
            else
                array = JsonSerializer.SerializeToUtf8Bytes(model, model.GetType(), _options);

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

            var context = GetJsonContextForType(type.Name);

            if (context != null)
                return JsonSerializer.Deserialize(span, type, context);
            else
                return JsonSerializer.Deserialize(span, type, _options);
        }
    }
}