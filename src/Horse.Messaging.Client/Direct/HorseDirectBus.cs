using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Direct
{
    /// <summary>
    /// Implementation for direct messages and requests
    /// </summary>
    public class HorseDirectBus<TIdentifier> : HorseDirectBus, IHorseDirectBus<TIdentifier>
    {
        /// <summary>
        /// Creates new horse route bus
        /// </summary>
        public HorseDirectBus(HorseClient client) : base(client)
        {
        }
    }

    /// <summary>
    /// Implementation for direct messages and requests
    /// </summary>
    public class HorseDirectBus : IHorseDirectBus
    {
        private readonly HorseClient _client;

        /// <summary>
        /// Creates new horse route bus
        /// </summary>
        public HorseDirectBus(HorseClient client)
        {
            _client = client;
        }

        /// <inheritdoc />
        public HorseClient GetClient()
        {
            return _client;
        }

        /// <inheritdoc />
        public Task<HorseResult> SendAsync(string target, ushort contentType, MemoryStream content, bool waitForCommit, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return _client.Direct.SendAsync(target, contentType, content, waitForCommit, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseResult> SendByName(string name, ushort contentType, MemoryStream content, bool waitForCommit, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return _client.Direct.SendByName(name, contentType, content, waitForCommit, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseResult> SendByType(string type, ushort contentType, MemoryStream content, bool waitAcknowledge, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return _client.Direct.SendByType(type, contentType, content, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseResult> SendById(string id, ushort contentType, MemoryStream content, bool waitAcknowledge, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return _client.Direct.SendAsync(id, contentType, content, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseResult> SendJsonByName<T>(string name, ushort contentType, T model, bool waitAcknowledge, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return _client.Direct.SendJsonByName(name, contentType, model, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseResult> SendJsonByType<T>(string type, ushort contentType, T model, bool waitAcknowledge, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return _client.Direct.SendJsonByType(type, contentType, model, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseResult> SendJsonById<T>(string id, ushort contentType, T model, bool waitAcknowledge, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return _client.Direct.SendJsonById(id, contentType, model, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseResult> SendJson(object model, bool waitForAcknowledge = false, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return _client.Direct.SendJson(model, waitForAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseMessage> Request(string target, ushort contentType, MemoryStream content, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return _client.Direct.Request(target, contentType, content, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseMessage> Request(string target, ushort contentType, string content, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return _client.Direct.Request(target, contentType, content, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseMessage> Request(string target, ushort contentType, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return _client.Direct.Request(target, contentType, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseResult> SendDirectJsonAsync<T>(string target, ushort contentType, T model, bool waitForAcknowledge = false, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return _client.Direct.SendJsonById(target, contentType, model, waitForAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseResult<TResponse>> RequestJsonAsync<TResponse>(object request, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return _client.Direct.RequestJson<TResponse>(request, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseResult<TResponse>> RequestJsonAsync<TRequest, TResponse>(string target, TRequest request, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return _client.Direct.RequestJson<TResponse>(target, null, request, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseResult<TResponse>> RequestJsonAsync<TRequest, TResponse>(string target, ushort contentType, TRequest request, IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return _client.Direct.RequestJson<TResponse>(target, contentType, request, messageHeaders);
        }
    }
}