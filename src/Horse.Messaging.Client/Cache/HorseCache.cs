using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Models;

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
        public async Task<HorseCacheData<string>> GetString(string key)
        {
            HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.GetCache);
            HorseResult result = await _client.SendAndGetAck(message);

            if (result == null || result.Code != HorseResultCode.Ok)
                return null;

            HorseCacheData<string> data = CreateCacheData<string>(key, message);
            data.Value = result.Message.GetStringContent();
            return data;
        }

        /// <inheritdoc />
        public async Task<HorseCacheData<byte[]>> GetData(string key)
        {
            HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.GetCache);
            HorseResult result = await _client.SendAndGetAck(message);

            if (result == null || result.Code != HorseResultCode.Ok)
                return null;

            HorseCacheData<byte[]> data = CreateCacheData<byte[]>(key, message);
            data.Value = result.Message.Content.ToArray();
            return data;
        }

        /// <inheritdoc />
        public async Task<HorseCacheData<TData>> Get<TData>(string key)
        {
            HorseMessage message = new HorseMessage(MessageType.Cache, key, KnownContentTypes.GetCache);
            HorseModelResult<TData> result = await _client.SendAndGetJson<TData>(message);

            if (result == null)
                return default;

            HorseCacheData<TData> data = CreateCacheData<TData>(key, message);
            data.Value = result.Model;
            return data;
        }

        private static HorseCacheData<T> CreateCacheData<T>(string key, HorseMessage message)
        {
            string expiry = message.FindHeader(HorseHeaders.EXPIRY);
            string warning = message.FindHeader(HorseHeaders.WARNING);
            string warnCount = message.FindHeader(HorseHeaders.WARN_COUNT);

            HorseCacheData<T> data = new HorseCacheData<T>
            {
                Key = key,
                IsFirstWarnedClient = message.HighPriority,
                Expiration = string.IsNullOrEmpty(expiry) ? 0 : Convert.ToInt64(expiry),
                WarningDate = string.IsNullOrEmpty(warning) ? 0 : Convert.ToInt64(warning),
                WarnCount = string.IsNullOrEmpty(warnCount) ? 0 : Convert.ToInt32(warnCount)
            };

            return data;
        }

        /// <summary>
        /// Finds in all cache keys
        /// </summary>
        public async Task<HorseModelResult<List<CacheInformation>>> List(string filter = null)
        {
            HorseMessage message = new HorseMessage();
            message.Type = MessageType.Cache;
            message.SetMessageId(_client.UniqueIdGenerator.Create());
            message.ContentType = KnownContentTypes.GetCacheList;
            message.AddHeader(HorseHeaders.FILTER, filter);
            return await _client.SendAndGetJson<List<CacheInformation>>(message);
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