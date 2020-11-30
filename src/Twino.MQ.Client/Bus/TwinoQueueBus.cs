using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Twino.MQ.Client.Connectors;
using Twino.MQ.Client.Models;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Client.Bus
{
    /// <summary>
    /// Implementation for queue messages and requests
    /// </summary>
    public class TwinoQueueBus : ITwinoQueueBus
    {
        private readonly TmqStickyConnector _connector;

        /// <summary>
        /// Creates new queue bus
        /// </summary>
        public TwinoQueueBus(TmqStickyConnector connector)
        {
            _connector = connector;
        }

        /// <inheritdoc />
        public TmqClient GetClient()
        {
            return _connector.GetClient();
        }

        /// <inheritdoc />
        public Task<TwinoResult> Push(string queue,
                                      MemoryStream content,
                                      bool waitAcknowledge = false,
                                      IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TmqClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new TwinoResult(TwinoResultCode.SendError));

            return client.Queues.Push(queue, content, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<TwinoResult> Push(string queue,
                                      string content,
                                      bool waitAcknowledge = false,
                                      IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TmqClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new TwinoResult(TwinoResultCode.SendError));

            return client.Queues.Push(queue, content, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<TwinoResult> Push(string queue,
                                      MemoryStream content,
                                      string messageId,
                                      bool waitAcknowledge = false,
                                      IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TmqClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new TwinoResult(TwinoResultCode.SendError));

            return client.Queues.Push(queue, content, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<TwinoResult> Push(string queue,
                                      string content,
                                      string messageId,
                                      bool waitAcknowledge = false,
                                      IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TmqClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new TwinoResult(TwinoResultCode.SendError));

            return client.Queues.Push(queue, content, messageId, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<TwinoResult> PushJson(object jsonObject,
                                          bool waitAcknowledge = false,
                                          IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TmqClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new TwinoResult(TwinoResultCode.SendError));

            return client.Queues.PushJson(jsonObject, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<TwinoResult> PushJson(string queue,
                                          object jsonObject,
                                          bool waitAcknowledge = false,
                                          IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TmqClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new TwinoResult(TwinoResultCode.SendError));

            return client.Queues.PushJson(queue, jsonObject, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<TwinoResult> PushJson(object jsonObject,
                                          string messageId,
                                          bool waitAcknowledge = false,
                                          IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TmqClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new TwinoResult(TwinoResultCode.SendError));

            return client.Queues.PushJson(jsonObject, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<TwinoResult> PushJson(string queue,
                                          object jsonObject,
                                          string messageId,
                                          bool waitAcknowledge = false,
                                          IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TmqClient client = _connector.GetClient();
            if (client == null)
                return Task.FromResult(new TwinoResult(TwinoResultCode.SendError));

            return client.Queues.PushJson(queue, jsonObject, messageId, waitAcknowledge, messageHeaders);
        }

        /// <inheritdoc />
        public Task<PullContainer> Pull(PullRequest request,
                                        Func<int, TwinoMessage, Task> actionForEachMessage = null)
        {
            TmqClient client = _connector.GetClient();
            if (client == null)
            {
                PullContainer con = new PullContainer(null, 0, null);
                con.Complete(TwinoHeaders.ERROR);
                return con.GetAwaitableTask();
            }

            return client.Queues.Pull(request, actionForEachMessage);
        }
    }
}