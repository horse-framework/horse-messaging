using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Internal;
using Horse.Messaging.Client.Queues.Exceptions;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Models;

namespace Horse.Messaging.Client.Queues
{
    /// <summary>
    /// Handler for queue name generator
    /// </summary>
    public delegate string QueueNameHandler(QueueNameHandlerContext context);

    /// <summary>
    /// Queue manager object for Horse client
    /// </summary>
    public class QueueOperator : IDisposable
    {
        internal HorseClient Client { get; }
        private readonly Timer _pullContainerTimeoutHandler;

        internal TypeDescriptorContainer<QueueTypeDescriptor> DescriptorContainer { get; }
        internal List<QueueConsumerRegistration> Registrations { get; private set; } = new List<QueueConsumerRegistration>();
        internal Dictionary<string, PullContainer> PullContainers { get; }

        /// <summary>
        /// Queue name handler
        /// </summary>
        public QueueNameHandler NameHandler { get; set; }

        internal QueueOperator(HorseClient client)
        {
            Client = client;
            DescriptorContainer = new TypeDescriptorContainer<QueueTypeDescriptor>(new QueueTypeResolver(client));
            PullContainers = new Dictionary<string, PullContainer>();
            _pullContainerTimeoutHandler = new Timer(HandleTimeoutPulls, null, 1000, 1000);
        }

        internal async Task OnQueueMessage(HorseMessage message)
        {
            //if message is response for pull request, process pull container
            if (PullContainers.Count > 0 && message.HasHeader)
            {
                string requestId = message.FindHeader(HorseHeaders.REQUEST_ID);
                if (!string.IsNullOrEmpty(requestId))
                {
                    PullContainer container;
                    lock (PullContainers)
                        PullContainers.TryGetValue(requestId, out container);

                    if (container != null)
                    {
                        ProcessPull(requestId, message, container);
                        return;
                    }
                }
            }

            //consume push state queue message
            QueueConsumerRegistration reg = Registrations.FirstOrDefault(x => x.QueueName == message.Target);
            if (reg == null)
                return;

            object model = reg.MessageType == typeof(string)
                ? message.GetStringContent()
                : Client.MessageSerializer.Deserialize(message, reg.MessageType);

            try
            {
                await reg.ConsumerExecuter.Execute(Client, message, model);
            }
            catch (Exception ex)
            {
                Client.OnException(ex, message);
            }
        }

        /// <summary>
        /// Processes pull message
        /// </summary>
        private void ProcessPull(string requestId, HorseMessage message, PullContainer container)
        {
            if (message.Length > 0)
                container.AddMessage(message);

            string noContent = message.FindHeader(HorseHeaders.NO_CONTENT);

            if (!string.IsNullOrEmpty(noContent))
            {
                lock (PullContainers)
                    PullContainers.Remove(requestId);

                container.Complete(noContent);
            }
        }

        /// <summary>
        /// Handles timed out pull requests and removed them
        /// </summary>
        private void HandleTimeoutPulls(object state)
        {
            try
            {
                if (PullContainers.Count > 0)
                {
                    List<PullContainer> timedouts = new List<PullContainer>();
                    lock (PullContainers)
                    {
                        foreach (PullContainer container in PullContainers.Values)
                        {
                            if (container.Status == PullProcess.Receiving && container.LastReceived + Client.PullTimeout < DateTime.UtcNow)
                                timedouts.Add(container);
                        }
                    }

                    foreach (PullContainer container in timedouts)
                    {
                        lock (PullContainers)
                            PullContainers.Remove(container.RequestId);

                        container.Complete(null);
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Releases all resources
        /// </summary>
        public void Dispose()
        {
            _pullContainerTimeoutHandler?.Dispose();
        }

        #region Actions

        /// <summary>
        /// Creates new queue in server
        /// </summary>
        public Task<HorseResult> Create(string queue)
        {
            return Create(queue, null, null, null);
        }

        /// <summary>
        /// Creates new queue in server
        /// </summary>
        public Task<HorseResult> Create(string queue,
            IEnumerable<KeyValuePair<string, string>> additionalHeaders)
        {
            return Create(queue, null, null, additionalHeaders);
        }

        /// <summary>
        /// Creates new queue in server
        /// </summary>
        public Task<HorseResult> Create(string queue,
            string deliveryHandlerHeader,
            IEnumerable<KeyValuePair<string, string>> additionalHeaders)
        {
            return Create(queue, null, deliveryHandlerHeader, additionalHeaders);
        }

        /// <summary>
        /// Creates new queue in server
        /// </summary>
        public async Task<HorseResult> Create(string queue,
            Action<QueueOptions> optionsAction,
            string queueManagerName = null,
            IEnumerable<KeyValuePair<string, string>> additionalHeaders = null)
        {
            HorseMessage message = new HorseMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.CreateQueue;
            message.SetTarget(queue);
            message.WaitResponse = true;
            message.AddHeader(HorseHeaders.QUEUE_NAME, queue);

            if (!string.IsNullOrEmpty(queueManagerName))
                message.AddHeader(HorseHeaders.QUEUE_MANAGER, queueManagerName);

            if (additionalHeaders != null)
                foreach (KeyValuePair<string, string> pair in additionalHeaders)
                    message.AddHeader(pair.Key, pair.Value);

            if (optionsAction != null)
            {
                QueueOptions options = new QueueOptions();
                optionsAction(options);

                message.Content = new MemoryStream();
                await JsonSerializer.SerializeAsync(message.Content, options);
            }

            message.SetMessageId(Client.UniqueIdGenerator.Create());

            return await Client.WaitResponse(message, true);
        }

        /// <summary>
        /// Finds all queues in server
        /// </summary>
        public async Task<HorseModelResult<List<QueueInformation>>> List(string filter = null)
        {
            HorseMessage message = new HorseMessage();
            message.Type = MessageType.Server;
            message.SetMessageId(Client.UniqueIdGenerator.Create());
            message.ContentType = KnownContentTypes.QueueList;

            message.AddHeader(HorseHeaders.FILTER, filter);

            return await Client.SendAndGetJson<List<QueueInformation>>(message);
        }

        /// <summary>
        /// Removes a queue in server.
        /// Required administration permission.
        /// If server has no implementation for administration authorization, request is not allowed.
        /// </summary>
        public async Task<HorseResult> Remove(string queue)
        {
            HorseMessage message = new HorseMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.RemoveQueue;
            message.SetTarget(queue);
            message.WaitResponse = true;
            message.SetMessageId(Client.UniqueIdGenerator.Create());

            return await Client.WaitResponse(message, true);
        }

        /// <summary>
        /// Updates queue options
        /// </summary>
        public async Task<HorseResult> SetOptions(string queue, Action<QueueOptions> optionsAction)
        {
            HorseMessage message = new HorseMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.UpdateQueue;
            message.SetTarget(queue);
            message.WaitResponse = true;
            message.SetMessageId(Client.UniqueIdGenerator.Create());
            message.AddHeader(HorseHeaders.QUEUE_NAME, queue);

            QueueOptions options = new QueueOptions();
            optionsAction(options);

            message.Content = new MemoryStream();
            await JsonSerializer.SerializeAsync(message.Content, options);

            return await Client.WaitResponse(message, true);
        }

        /// <summary>
        /// Clears messages in a queue.
        /// Required administration permission.
        /// If server has no implementation for administration authorization, request is not allowed.
        /// </summary>
        public Task<HorseResult> ClearMessages(string queue, bool clearPriorityMessages, bool clearMessages)
        {
            if (!clearPriorityMessages && !clearMessages)
                return Task.FromResult(HorseResult.Failed());

            HorseMessage message = new HorseMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.ClearMessages;
            message.SetTarget(queue);
            message.WaitResponse = true;
            message.SetMessageId(Client.UniqueIdGenerator.Create());
            message.AddHeader(HorseHeaders.QUEUE_NAME, queue);

            if (clearPriorityMessages)
                message.AddHeader(HorseHeaders.PRIORITY_MESSAGES, "yes");

            if (clearMessages)
                message.AddHeader(HorseHeaders.MESSAGES, "yes");

            return Client.WaitResponse(message, true);
        }

        /// <summary>
        /// Gets all consumers of queue
        /// </summary>
        public async Task<HorseModelResult<List<ClientInformation>>> GetConsumers(string queue)
        {
            HorseMessage message = new HorseMessage();
            message.Type = MessageType.Server;
            message.SetTarget(queue);
            message.ContentType = KnownContentTypes.QueueConsumers;
            message.SetMessageId(Client.UniqueIdGenerator.Create());

            message.AddHeader(HorseHeaders.QUEUE_NAME, queue);

            return await Client.SendAndGetJson<List<ClientInformation>>(message);
        }

        #endregion

        #region Push - Pull

        /// <summary>
        /// Pushes a message to a queue
        /// </summary>
        public Task<HorseResult> PushJson(object jsonObject, bool waitForCommit,
            IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return PushJson(null, jsonObject, waitForCommit, messageHeaders);
        }

        /// <summary>
        /// Pushes a message to a queue
        /// </summary>
        public Task<HorseResult> PushJson(object jsonObject, string messageId, bool waitForCommit,
            IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return PushJson(null, jsonObject, messageId, waitForCommit, messageHeaders);
        }

        /// <summary>
        /// Pushes a message to a queue
        /// </summary>
        public Task<HorseResult> PushJson(string queue, object jsonObject, bool waitForCommit,
            IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return PushJson(queue, jsonObject, null, waitForCommit, messageHeaders);
        }

        /// <summary>
        /// Pushes a message to a queue
        /// </summary>
        public async Task<HorseResult> PushJson(string queue, object jsonObject, string messageId, bool waitForCommit,
            IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            QueueTypeDescriptor descriptor = DescriptorContainer.GetDescriptor(jsonObject.GetType());

            if (!string.IsNullOrEmpty(queue))
                descriptor.QueueName = queue;

            if (NameHandler != null)
                descriptor.QueueName = NameHandler.Invoke(new QueueNameHandlerContext
                {
                    Client = Client,
                    Type = jsonObject.GetType()
                });

            HorseMessage message = descriptor.CreateMessage();

            if (!string.IsNullOrEmpty(messageId))
                message.SetMessageId(messageId);

            message.WaitResponse = waitForCommit;

            if (messageHeaders != null)
                foreach (KeyValuePair<string, string> pair in messageHeaders)
                    message.AddHeader(pair.Key, pair.Value);

            message.Serialize(jsonObject, Client.MessageSerializer);

            if (string.IsNullOrEmpty(message.MessageId) && waitForCommit)
                message.SetMessageId(Client.UniqueIdGenerator.Create());

            return await Client.WaitResponse(message, waitForCommit);
        }

        /// <summary>
        /// Pushes a message to a queue
        /// </summary>
        public async Task<HorseResult> Push(string queue, string content, bool waitForCommit,
            IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return await Push(queue, new MemoryStream(Encoding.UTF8.GetBytes(content)), waitForCommit, messageHeaders);
        }

        /// <summary>
        /// Pushes a message to a queue
        /// </summary>
        public async Task<HorseResult> Push(string queue, string content, string messageId, bool waitForCommit,
            IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return await Push(queue, new MemoryStream(Encoding.UTF8.GetBytes(content)), messageId, waitForCommit, messageHeaders);
        }

        /// <summary>
        /// Pushes a message to a queue
        /// </summary>
        public Task<HorseResult> Push(string queue, MemoryStream content, bool waitForCommit,
            IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return Push(queue, content, null, waitForCommit, messageHeaders);
        }

        /// <summary>
        /// Pushes a message to a queue
        /// </summary>
        public async Task<HorseResult> Push(string queue, MemoryStream content, string messageId, bool waitForCommit,
            IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            HorseMessage message = new HorseMessage(MessageType.QueueMessage, queue, 0);
            message.Content = content;
            message.WaitResponse = waitForCommit;

            if (!string.IsNullOrEmpty(messageId))
                message.SetMessageId(messageId);

            if (messageHeaders != null)
                foreach (KeyValuePair<string, string> pair in messageHeaders)
                    message.AddHeader(pair.Key, pair.Value);

            if (string.IsNullOrEmpty(message.MessageId) && waitForCommit)
                message.SetMessageId(Client.UniqueIdGenerator.Create());

            return await Client.WaitResponse(message, waitForCommit);
        }

        /// <summary>
        /// Request a message from Pull queue
        /// </summary>
        public async Task<PullContainer> Pull(PullRequest request, Func<int, HorseMessage, Task> actionForEachMessage = null)
        {
            HorseMessage message = new HorseMessage(MessageType.QueuePullRequest, request.Queue);
            message.SetMessageId(Client.UniqueIdGenerator.Create());
            message.AddHeader(HorseHeaders.COUNT, request.Count);

            if (request.ClearAfter == ClearDecision.AllMessages)
                message.AddHeader(HorseHeaders.CLEAR, "all");
            else if (request.ClearAfter == ClearDecision.PriorityMessages)
                message.AddHeader(HorseHeaders.CLEAR, "High-Priority");
            else if (request.ClearAfter == ClearDecision.Messages)
                message.AddHeader(HorseHeaders.CLEAR, "Default-Priority");

            if (request.GetQueueMessageCounts)
                message.AddHeader(HorseHeaders.INFO, "yes");

            if (request.Order == MessageOrder.LIFO)
                message.AddHeader(HorseHeaders.ORDER, HorseHeaders.LIFO);

            foreach (KeyValuePair<string, string> pair in request.RequestHeaders)
                message.AddHeader(pair.Key, pair.Value);

            PullContainer container = new PullContainer(message.MessageId, request.Count, actionForEachMessage);
            lock (PullContainers)
                PullContainers.Add(message.MessageId, container);

            HorseResult sent = await Client.SendAsync(message);
            if (sent.Code != HorseResultCode.Ok)
            {
                lock (PullContainers)
                    PullContainers.Remove(message.MessageId);

                container.Complete("Error");
            }

            return await container.GetAwaitableTask();
        }

        #endregion

        #region Join - Leave

        /// <summary>
        /// Subscribes to a queue
        /// </summary>
        public async Task<HorseResult> Subscribe(string queue, bool verifyResponse, IEnumerable<KeyValuePair<string, string>> headers = null)
        {
            HorseMessage message = new HorseMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.QueueSubscribe;
            message.SetTarget(queue);
            message.WaitResponse = verifyResponse;

            if (headers != null)
                foreach (KeyValuePair<string, string> header in headers)
                    message.AddHeader(header.Key, header.Value);

            if (verifyResponse)
                message.SetMessageId(Client.UniqueIdGenerator.Create());

            return await Client.WaitResponse(message, verifyResponse);
        }

        /// <summary>
        /// Re-registers the consumer type for another queue name and subscribes to the queue
        /// </summary>
        public Task<HorseResult> Subscribe<TConsumer, TModel>(string queue, bool verifyResponse, IEnumerable<KeyValuePair<string, string>> headers = null)
            where TConsumer : IQueueConsumer<TModel>
        {
            List<QueueConsumerRegistration> list = Registrations.ToList();
            QueueConsumerRegistration current = list.FirstOrDefault(x => x.ConsumerType == typeof(TConsumer));

            if (current == null)
                throw new HorseQueueException($"{typeof(TConsumer)} Consumer type is not registered");

            QueueConsumerRegistration registration = new QueueConsumerRegistration
            {
                ConsumerExecuter = current.ConsumerExecuter,
                ConsumerType = current.ConsumerType,
                MessageType = current.MessageType,
                QueueName = queue
            };

            foreach (InterceptorTypeDescriptor descriptor in current.InterceptorDescriptors)
                registration.InterceptorDescriptors.Add(descriptor);

            list.Add(registration);
            Registrations = list;

            return Subscribe(queue, verifyResponse, headers);
        }

        /// <summary>
        /// Unsubscribes from a queue
        /// </summary>
        public async Task<HorseResult> Unsubscribe(string queue, bool verifyResponse)
        {
            HorseMessage message = new HorseMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.QueueUnsubscribe;
            message.SetTarget(queue);
            message.WaitResponse = verifyResponse;

            if (verifyResponse)
                message.SetMessageId(Client.UniqueIdGenerator.Create());

            return await Client.WaitResponse(message, verifyResponse);
        }

        #endregion
    }
}