using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Queues
{
    internal class HorseQueueBus<TIdentifier> : HorseQueueBus, IHorseQueueBus<TIdentifier>
    {
        public HorseQueueBus(HorseClient client) : base(client)
        {
        }
    }

    /// <summary>
    /// Implementation for queue messages and requests
    /// </summary>
    public class HorseQueueBus : IHorseQueueBus
    {
        private readonly HorseClient _client;

        /// <summary>
        /// Creates new horse route bus
        /// </summary>
        public HorseQueueBus(HorseClient client)
        {
            _client = client;
        }

        /// <inheritdoc />
        public HorseClient GetClient()
        {
            return _client;
        }

        /// <inheritdoc />
        public Task<HorseResult> Push(string queue,
                                      MemoryStream content,
                                      bool waitAcknowledge = false,
                                      IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return _client.Queue.Push(queue, content, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseResult> Push(string queue,
                                      string content,
                                      bool waitAcknowledge = false,
                                      IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return _client.Queue.Push(queue, content, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseResult> Push(string queue,
                                      MemoryStream content,
                                      string messageId,
                                      bool waitAcknowledge = false,
                                      IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return _client.Queue.Push(queue, content, messageId, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseResult> Push(string queue,
                                      string content,
                                      string messageId,
                                      bool waitAcknowledge = false,
                                      IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return _client.Queue.Push(queue, content, messageId, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseResult> PushJson(object jsonObject,
                                          bool waitAcknowledge = false,
                                          IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return _client.Queue.PushJson(jsonObject, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseResult> PushJson(string queue,
                                          object jsonObject,
                                          bool waitAcknowledge = false,
                                          IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return _client.Queue.PushJson(queue, jsonObject, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseResult> PushJson(object jsonObject,
                                          string messageId,
                                          bool waitAcknowledge = false,
                                          IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return _client.Queue.PushJson(jsonObject, messageId, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseResult> PushJson(string queue,
                                          object jsonObject,
                                          string messageId,
                                          bool waitAcknowledge = false,
                                          IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return _client.Queue.PushJson(queue, jsonObject, messageId, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<PullContainer> Pull(PullRequest request,
                                        Func<int, HorseMessage, Task> actionForEachMessage = null)
        {
            return _client.Queue.Pull(request, actionForEachMessage);
        }
    }
}