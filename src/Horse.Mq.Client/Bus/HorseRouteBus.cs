using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Horse.Mq.Client.Connectors;
using Horse.Protocols.Hmq;

namespace Horse.Mq.Client.Bus
{
    /// <summary>
    /// Implementation for route messages and requests
    /// </summary>
    public class HorseRouteBus : IHorseRouteBus
    {
        private readonly HmqStickyConnector _connector;

        /// <summary>
        /// Creates new horse route bus
        /// </summary>
        public HorseRouteBus(HmqStickyConnector connector)
        {
            _connector = connector;
        }

        /// <inheritdoc />
        public HorseClient GetClient()
        {
            return _connector.GetClient();
        }

        #region Publish

        /// <inheritdoc />
        public Task<HorseResult> Publish(string routerName,
                                         string content,
                                         bool waitAcknowledge = false,
                                         IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            HorseClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new HorseResult(HorseResultCode.SendError));

            return client.Routers.Publish(routerName, content, null, waitAcknowledge, 0, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseResult> Publish(string routerName,
                                         string content,
                                         string messageId,
                                         bool waitAcknowledge = false,
                                         IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            HorseClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new HorseResult(HorseResultCode.SendError));

            return client.Routers.Publish(routerName, content, messageId, waitAcknowledge, 0, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseResult> Publish(string routerName,
                                         MemoryStream content,
                                         bool waitAcknowledge = false,
                                         IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            HorseClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new HorseResult(HorseResultCode.SendError));

            return client.Routers.Publish(routerName, content.ToArray(), null, waitAcknowledge, 0, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseResult> Publish(string routerName,
                                         MemoryStream content,
                                         string messageId,
                                         bool waitAcknowledge = false,
                                         IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            HorseClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new HorseResult(HorseResultCode.SendError));

            return client.Routers.Publish(routerName, content.ToArray(), messageId, waitAcknowledge, 0, messageHeaders);
        }

        #endregion

        #region Json

        /// <inheritdoc />
        public Task<HorseResult> PublishJson(object jsonObject,
                                             bool waitAcknowledge = false,
                                             IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            HorseClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new HorseResult(HorseResultCode.SendError));

            return client.Routers.PublishJson(jsonObject, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseResult> PublishJson(string routerName,
                                             object jsonObject,
                                             bool waitAcknowledge = false,
                                             IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return PublishJson(routerName, jsonObject, null, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseResult> PublishJson(string routerName,
                                             object jsonObject,
                                             ushort? contentType = null,
                                             bool waitAcknowledge = false,
                                             IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            HorseClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new HorseResult(HorseResultCode.SendError));

            return client.Routers.PublishJson(routerName, jsonObject, null, waitAcknowledge, contentType, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseResult> PublishJson(string routerName,
                                             object jsonObject,
                                             string messageId,
                                             ushort? contentType = null,
                                             bool waitAcknowledge = false,
                                             IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            HorseClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new HorseResult(HorseResultCode.SendError));

            return client.Routers.PublishJson(routerName, jsonObject, messageId, waitAcknowledge, contentType, messageHeaders);
        }

        #endregion

        #region Request

        /// <inheritdoc />
        public Task<HorseMessage> PublishRequest(string routerName,
                                                 string message,
                                                 ushort contentType = 0,
                                                 IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            HorseClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult<HorseMessage>(null);

            return client.Routers.PublishRequest(routerName, message, contentType, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseResult<TResponse>> PublishRequestJson<TRequest, TResponse>(TRequest request,
                                                                                    IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            HorseClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new HorseResult<TResponse>(default, null, HorseResultCode.SendError));

            return client.Routers.PublishRequestJson<TRequest, TResponse>(request, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseResult<TResponse>> PublishRequestJson<TRequest, TResponse>(string routerName,
                                                                                    TRequest request,
                                                                                    ushort? contentType = null,
                                                                                    IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            HorseClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new HorseResult<TResponse>(default, null, HorseResultCode.SendError));

            return client.Routers.PublishRequestJson<TRequest, TResponse>(routerName, request, contentType, messageHeaders);
        }

        #endregion
    }
}