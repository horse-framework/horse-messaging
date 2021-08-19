using System;
using System.IO;
using System.Threading.Tasks;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Cache
{
    internal class HorseCache<TIdentifier> : HorseCache, IHorseCache<TIdentifier>
    {
        internal HorseCache(HorseClient client) : base(client)
        {
        }
    }

    internal class HorseCache : IHorseCache
    {
        private readonly HorseClient _client;

        internal HorseCache(HorseClient client)
        {
            _client = client;
        }

        #region Get

        /// <inheritdoc />
        public async Task<string> GetString(string key)
        {
            HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.GetCache);
            HorseResult result = await _client.SendAndGetAck(message);

            if (result == null || result.Code != HorseResultCode.Ok)
                return null;

            return result.Message.GetStringContent();
        }

        /// <inheritdoc />
        public async Task<byte[]> GetData(string key)
        {
            HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.GetCache);
            HorseResult result = await _client.SendAndGetAck(message);

            if (result == null || result.Code != HorseResultCode.Ok)
                return null;

            return result.Message.Content.ToArray();
        }

        /// <inheritdoc />
        public async Task<TData> Get<TData>(string key)
        {
            HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.GetCache);
            HorseModelResult<TData> result = await _client.SendAndGetJson<TData>(message);

            if (result == null)
                return default;

            return result.Model;
        }

        #endregion

        #region Set

        /// <inheritdoc />
        public Task<HorseResult> Set<TData>(string key, TData data)
        {
            HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.SetCache);
            _client.MessageSerializer.Serialize(message, data);
            return _client.SendAndGetAck(message);
        }

        /// <inheritdoc />
        public Task<HorseResult> Set<TData>(string key, TData data, TimeSpan duration)
        {
            HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.SetCache);
            _client.MessageSerializer.Serialize(message, data);
            message.SetOrAddHeader(HorseHeaders.MESSAGE_TIMEOUT, Convert.ToInt32(duration.TotalSeconds).ToString());
            return _client.SendAndGetAck(message);
        }

        /// <inheritdoc />
        public Task<HorseResult> SetString(string key, string data)
        {
            HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.SetCache);
            message.SetStringContent(data);
            return _client.SendAndGetAck(message);
        }

        /// <inheritdoc />
        public Task<HorseResult> SetString(string key, string data, TimeSpan duration)
        {
            HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.SetCache);
            message.SetStringContent(data);
            message.SetOrAddHeader(HorseHeaders.MESSAGE_TIMEOUT, Convert.ToInt32(duration.TotalSeconds).ToString());
            return _client.SendAndGetAck(message);
        }

        /// <inheritdoc />
        public Task<HorseResult> SetData(string key, byte[] data)
        {
            HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.SetCache);
            message.Content = new MemoryStream(data);
            message.CalculateLengths();
            return _client.SendAndGetAck(message);
        }

        /// <inheritdoc />
        public Task<HorseResult> SetData(string key, byte[] data, TimeSpan duration)
        {
            HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.SetCache);
            message.Content = new MemoryStream(data);
            message.CalculateLengths();
            message.SetOrAddHeader(HorseHeaders.MESSAGE_TIMEOUT, Convert.ToInt32(duration.TotalSeconds).ToString());
            return _client.SendAndGetAck(message);
        }

        #endregion

        #region Remove

        /// <inheritdoc />
        public Task<HorseResult> Remove(string key)
        {
            HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.RemoveCache);
            return _client.SendAndGetAck(message);
        }

        /// <inheritdoc />
        public Task<HorseResult> Purge()
        {
            HorseMessage message = new HorseMessage(MessageType.Cache, null, KnownContentTypes.PurgeCache);
            return _client.SendAndGetAck(message);
        }

        #endregion
    }
}