using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Horse.Mq.Client.Connectors;
using Horse.Mq.Client.Models;
using Horse.Protocols.Hmq;

namespace Horse.Mq.Client.Bus
{
    /// <summary>
    /// Implementation for queue messages and requests
    /// </summary>
    public class HorseQueueBus : IHorseQueueBus
    {
        private readonly HmqStickyConnector _connector;

        /// <summary>
        /// Creates new queue bus
        /// </summary>
        public HorseQueueBus(HmqStickyConnector connector)
        {
            _connector = connector;
        }

        /// <inheritdoc />
        public HorseClient GetClient()
        {
            return _connector.GetClient();
        }

        /// <inheritdoc />
        public Task<HorseResult> Push(string queue,
                                      MemoryStream content,
                                      bool waitAcknowledge = false,
                                      IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            HorseClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new HorseResult(HorseResultCode.SendError));

            return client.Queues.Push(queue, content, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseResult> Push(string queue,
                                      string content,
                                      bool waitAcknowledge = false,
                                      IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            HorseClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new HorseResult(HorseResultCode.SendError));

            return client.Queues.Push(queue, content, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseResult> Push(string queue,
                                      MemoryStream content,
                                      string messageId,
                                      bool waitAcknowledge = false,
                                      IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            HorseClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new HorseResult(HorseResultCode.SendError));

            return client.Queues.Push(queue, content, messageId, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseResult> Push(string queue,
                                      string content,
                                      string messageId,
                                      bool waitAcknowledge = false,
                                      IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            HorseClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new HorseResult(HorseResultCode.SendError));

            return client.Queues.Push(queue, content, messageId, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseResult> PushJson(object jsonObject,
                                          bool waitAcknowledge = false,
                                          IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            HorseClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new HorseResult(HorseResultCode.SendError));

            return client.Queues.PushJson(jsonObject, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseResult> PushJson(string queue,
                                          object jsonObject,
                                          bool waitAcknowledge = false,
                                          IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            HorseClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new HorseResult(HorseResultCode.SendError));

            return client.Queues.PushJson(queue, jsonObject, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseResult> PushJson(object jsonObject,
                                          string messageId,
                                          bool waitAcknowledge = false,
                                          IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            HorseClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new HorseResult(HorseResultCode.SendError));

            return client.Queues.PushJson(jsonObject, messageId, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<HorseResult> PushJson(string queue,
                                          object jsonObject,
                                          string messageId,
                                          bool waitAcknowledge = false,
                                          IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            HorseClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new HorseResult(HorseResultCode.SendError));

            return client.Queues.PushJson(queue, jsonObject, messageId, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<PullContainer> Pull(PullRequest request,
                                        Func<int, HorseMessage, Task> actionForEachMessage = null)
        {
            HorseClient client = _connector.GetClient();
            if (client == null)
            {
                PullContainer con = new PullContainer(null, 0, null);
                con.Complete(HorseHeaders.ERROR);
                return con.GetAwaitableTask();
            }

            return client.Queues.Pull(request, actionForEachMessage);
        }
    }
}