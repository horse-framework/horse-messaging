using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Twino.Client.TMQ.Connectors;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Bus
{
    /// <summary>
    /// Implementation for direct messages and requests
    /// </summary>
    public class TwinoDirectBus : ITwinoDirectBus
    {
        private readonly TmqStickyConnector _connector;

        /// <summary>
        /// Creates new direct bus
        /// </summary>
        public TwinoDirectBus(TmqStickyConnector connector)
        {
            _connector = connector;
        }

        /// <inheritdoc />
        public TmqClient GetClient()
        {
            return _connector.GetClient();
        }

        /// <inheritdoc />
        public Task<TwinoResult> SendAsync(string target, ushort contentType, MemoryStream content, bool waitAcknowledge, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TmqClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new TwinoResult(TwinoResultCode.SendError));

            return client.Direct.SendAsync(target, contentType, content, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<TwinoResult> SendByName(string name, ushort contentType, MemoryStream content, bool waitAcknowledge, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TmqClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new TwinoResult(TwinoResultCode.SendError));

            return client.Direct.SendByName(name, contentType, content, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<TwinoResult> SendByType(string type, ushort contentType, MemoryStream content, bool waitAcknowledge, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TmqClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new TwinoResult(TwinoResultCode.SendError));

            return client.Direct.SendByType(type, contentType, content, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<TwinoResult> SendById(string id, ushort contentType, MemoryStream content, bool waitAcknowledge, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TmqClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new TwinoResult(TwinoResultCode.SendError));

            return client.Direct.SendAsync(id, contentType, content, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<TwinoResult> SendJsonByName<T>(string name, ushort contentType, T model, bool waitAcknowledge, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TmqClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new TwinoResult(TwinoResultCode.SendError));

            return client.Direct.SendJsonByName(name, contentType, model, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<TwinoResult> SendJsonByType<T>(string type, ushort contentType, T model, bool waitAcknowledge, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TmqClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new TwinoResult(TwinoResultCode.SendError));

            return client.Direct.SendJsonByType(type, contentType, model, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<TwinoResult> SendJsonById<T>(string id, ushort contentType, T model, bool waitAcknowledge, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TmqClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new TwinoResult(TwinoResultCode.SendError));

            return client.Direct.SendJsonById(id, contentType, model, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<TwinoResult> SendJson(object model, bool waitForAcknowledge = false, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TmqClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new TwinoResult(TwinoResultCode.SendError));

            return client.Direct.SendJson(model, waitForAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<TwinoMessage> Request(string target, ushort contentType, MemoryStream content, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TmqClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult<TwinoMessage>(null);

            return client.Direct.Request(target, contentType, content, messageHeaders);
        }

        /// <inheritdoc />
        public Task<TwinoMessage> Request(string target, ushort contentType, string content, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TmqClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult<TwinoMessage>(null);

            return client.Direct.Request(target, contentType, content, messageHeaders);
        }

        /// <inheritdoc />
        public Task<TwinoMessage> Request(string target, ushort contentType, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TmqClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult<TwinoMessage>(null);

            return client.Direct.Request(target, contentType, messageHeaders);
        }

        /// <inheritdoc />
        public Task<TwinoResult> SendDirectJsonAsync<T>(string target, ushort contentType, T model, bool waitForAcknowledge = false, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TmqClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new TwinoResult(TwinoResultCode.SendError));

            return client.Direct.SendJsonById(target, contentType, model, waitForAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<TwinoResult<TResponse>> RequestJsonAsync<TResponse>(object request, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TmqClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new TwinoResult<TResponse>(default, null, TwinoResultCode.SendError));

            return client.Direct.RequestJson<TResponse>(request, messageHeaders);
        }

        /// <inheritdoc />
        public Task<TwinoResult<TResponse>> RequestJsonAsync<TRequest, TResponse>(string target, ushort contentType, TRequest request, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TmqClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new TwinoResult<TResponse>(default, null, TwinoResultCode.SendError));

            return client.Direct.RequestJson<TResponse>(target, contentType, request, messageHeaders);
        }
    }
}