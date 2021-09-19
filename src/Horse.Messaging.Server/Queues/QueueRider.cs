using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Events;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Containers;
using Horse.Messaging.Server.Events;
using Horse.Messaging.Server.Helpers;
using Horse.Messaging.Server.Options;
using Horse.Messaging.Server.Security;

namespace Horse.Messaging.Server.Queues
{
    /// <summary>
    /// Manages queues in messaging server
    /// </summary>
    public class QueueRider
    {
        private readonly ArrayContainer<HorseQueue> _queues = new ArrayContainer<HorseQueue>();

        /// <summary>
        /// Locker object for preventing to create duplicated queues when requests are concurrent and auto queue creation is enabled
        /// </summary>
        private readonly SemaphoreSlim _createLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Queue event handlers
        /// </summary>
        public ArrayContainer<IQueueEventHandler> EventHandlers { get; } = new ArrayContainer<IQueueEventHandler>();

        /// <summary>
        /// Queue authenticators
        /// </summary>
        public ArrayContainer<IQueueAuthenticator> Authenticators { get; } = new ArrayContainer<IQueueAuthenticator>();

        /// <summary>
        /// Event handlers to track queue message events
        /// </summary>
        public ArrayContainer<IQueueMessageEventHandler> MessageHandlers { get; } = new ArrayContainer<IQueueMessageEventHandler>();

        /// <summary>
        /// All Queues of the server
        /// </summary>
        public IEnumerable<HorseQueue> Queues => _queues.All();

        /// <summary>
        /// Key specific registered delivery handler methods
        /// </summary>
        internal Dictionary<string, Func<DeliveryHandlerBuilder, Task<IMessageDeliveryHandler>>> DeliveryHandlerFactories { get; } = new(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Root horse rider object
        /// </summary>
        public HorseRider Rider { get; }

        /// <summary>
        /// Default queue options
        /// </summary>
        public QueueOptions Options { get; } = new QueueOptions();

        /// <summary>
        /// Event Manage for HorseEventType.QueueCreate
        /// </summary>
        public EventManager CreateEvent { get; }

        /// <summary>
        /// Event Manage for HorseEventType.QueueRemove
        /// </summary>
        public EventManager RemoveEvent { get; }

        /// <summary>
        /// Event Manage for HorseEventType.QueueStatusChange
        /// </summary>
        public EventManager StatusChangeEvent { get; }

        /// <summary>
        /// Event Manage for HorseEventType.QueueSubscription
        /// </summary>
        public EventManager SubscriptionEvent { get; }

        /// <summary>
        /// Event Manage for HorseEventType.QueueUnsubscription
        /// </summary>
        public EventManager UnsubscriptionEvent { get; }

        /// <summary>
        /// Creates new queue rider
        /// </summary>
        internal QueueRider(HorseRider rider)
        {
            Rider = rider;
            CreateEvent = new EventManager(rider, HorseEventType.QueueCreate);
            RemoveEvent = new EventManager(rider, HorseEventType.QueueRemove);
            StatusChangeEvent = new EventManager(rider, HorseEventType.QueueStatusChange);
            SubscriptionEvent = new EventManager(rider, HorseEventType.QueueSubscription);
            UnsubscriptionEvent = new EventManager(rider, HorseEventType.QueueUnsubscription);
        }

        #region Actions

        /// <summary>
        /// Finds queue by name
        /// </summary>
        public HorseQueue Find(string name)
        {
            return _queues.Find(x => x.Name == name);
        }

        /// <summary>
        /// Creates new queue with default options and default handlers
        /// </summary>
        /// <exception cref="NoNullAllowedException">Thrown when server does not have default delivery handler implementation</exception>
        /// <exception cref="OperationCanceledException">Thrown when queue limit is exceeded for the server</exception>
        /// <exception cref="DuplicateNameException">Thrown when there is already a queue with same id</exception>
        public async Task<HorseQueue> Create(string queueName)
        {
            QueueOptions options = QueueOptions.CloneFrom(Options);
            return await Create(queueName, options);
        }

        /// <summary>
        /// Creates new queue with default handlers
        /// </summary>
        /// <exception cref="NoNullAllowedException">Thrown when server does not have default delivery handler implementation</exception>
        /// <exception cref="OperationCanceledException">Thrown when queue limit is exceeded for the server</exception>
        /// <exception cref="DuplicateNameException">Thrown when there is already a queue with same id</exception>
        public async Task<HorseQueue> Create(string queueName, Action<QueueOptions> optionsAction)
        {
            QueueOptions options = QueueOptions.CloneFrom(Options);
            optionsAction(options);
            return await Create(queueName, options);
        }

        /// <summary>
        /// Creates new queue
        /// </summary>
        /// <exception cref="NoNullAllowedException">Thrown when server does not have default delivery handler implementation</exception>
        /// <exception cref="OperationCanceledException">Thrown when queue limit is exceeded for the server</exception>
        /// <exception cref="DuplicateNameException">Thrown when there is already a queue with same id</exception>
        public Task<HorseQueue> Create(string queueName, QueueOptions options)
        {
            return Create(queueName, options, null, false, false);
        }

        internal async Task<HorseQueue> Create(string queueName,
                                               QueueOptions options,
                                               HorseMessage requestMessage,
                                               bool hideException,
                                               bool returnIfExists,
                                               MessagingClient client = null)
        {
            string handlerName = requestMessage != null ? requestMessage.FindHeader(HorseHeaders.DELIVERY_HANDLER) : "Default";
            if (string.IsNullOrEmpty(handlerName))
                handlerName = "Default";

            DeliveryHandlerFactories.TryGetValue(handlerName, out Func<DeliveryHandlerBuilder, Task<IMessageDeliveryHandler>> deliveryHandlerFactory);

            await _createLock.WaitAsync();
            try
            {
                if (!Filter.CheckNameEligibility(queueName))
                    throw new InvalidOperationException("Invalid queue name");

                if (Rider.Options.QueueLimit > 0 && Rider.Options.QueueLimit >= _queues.Count())
                    throw new OperationCanceledException("Queue limit is exceeded for the server");

                HorseQueue queue = _queues.Find(x => x.Name == queueName);

                if (queue != null)
                {
                    if (returnIfExists)
                        return queue;

                    throw new DuplicateNameException($"The server has already a queue with same name: {queueName}");
                }

                bool typeSpecified = false; //when queue is created by subscriber, it will be initialized if type is specified
                if (requestMessage != null)
                {
                    string queueType = requestMessage.FindHeader(HorseHeaders.QUEUE_TYPE);
                    if (queueType != null)
                    {
                        typeSpecified = true;
                        options.Type = queueType.ToQueueType();
                    }
                }

                queue = new HorseQueue(Rider, queueName, options);
                if (requestMessage != null)
                    queue.UpdateOptionsByMessage(requestMessage);

                DeliveryHandlerBuilder handlerBuilder = new DeliveryHandlerBuilder
                {
                    Server = Rider,
                    Queue = queue
                };
                if (requestMessage != null)
                {
                    handlerBuilder.DeliveryHandlerHeader = requestMessage.FindHeader(HorseHeaders.DELIVERY_HANDLER);
                    handlerBuilder.Headers = requestMessage.Headers;
                }

                bool initialize;
                //if queue creation is triggered by consumer subscription, we might skip initialization
                if (requestMessage != null && requestMessage.Type == MessageType.Server && requestMessage.ContentType == KnownContentTypes.QueueSubscribe)
                    initialize = typeSpecified;
                else
                    initialize = true;

                if (initialize && deliveryHandlerFactory != null)
                {
                    IMessageDeliveryHandler deliveryHandler = await deliveryHandlerFactory(handlerBuilder);
                    await queue.InitializeQueue(deliveryHandler);
                }

                _queues.Add(queue);
                foreach (IQueueEventHandler handler in EventHandlers.All())
                    _ = handler.OnCreated(queue);

                if (initialize)
                    handlerBuilder.TriggerAfterCompleted();

                CreateEvent.Trigger(client, queue.Name);

                return queue;
            }
            catch (Exception e)
            {
                Rider.SendError("CREATE_QUEUE", e, $"QueueName:{queueName}");

                if (!hideException)
                    throw;

                return null;
            }
            finally
            {
                try
                {
                    _createLock.Release();
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Removes a queue from the server
        /// </summary>
        public async Task Remove(string name)
        {
            HorseQueue queue = Find(name);
            if (queue == null)
                return;

            await Remove(queue);
        }

        /// <summary>
        /// Removes a queue from the server
        /// </summary>
        public async Task Remove(HorseQueue queue)
        {
            try
            {
                _queues.Remove(queue);
                queue.SetStatus(QueueStatus.Paused);

                foreach (IQueueEventHandler handler in EventHandlers.All())
                    _ = handler.OnRemoved(queue);

                await queue.Destroy();
                RemoveEvent.Trigger(queue.Name);
            }
            catch (Exception e)
            {
                Rider.SendError("REMOVE_QUEUE", e, $"QueueName:{queue?.Name}");
            }
        }

        #endregion
    }
}