using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Twino.Client.TMQ;
using Twino.MQ.Clients;
using Twino.MQ.Events;
using Twino.MQ.Helpers;
using Twino.MQ.Options;
using Twino.MQ.Queues;
using Twino.MQ.Routing;
using Twino.MQ.Security;
using Twino.Protocols.TMQ;
using Twino.Server;

namespace Twino.MQ
{
    /// <summary>
    /// Twino Messaging Queue Server
    /// </summary>
    public class TwinoMQ
    {
        #region Properties

        /// <summary>
        /// Messaging Queue Server Options
        /// </summary>
        public TwinoMqOptions Options { get; internal set; }

        private readonly SafeList<TwinoQueue> _queues;

        /// <summary>
        /// All Queues of the server
        /// </summary>
        public IEnumerable<TwinoQueue> Queues => _queues.GetAsClone();

        private readonly SafeList<MqClient> _clients;

        /// <summary>
        /// All connected clients in the server
        /// </summary>
        public IEnumerable<MqClient> Clients => _clients.GetAsClone();

        private readonly SafeList<IRouter> _routers;

        /// <summary>
        /// All channels of the server
        /// </summary>
        public IEnumerable<IRouter> Routers => _routers.GetAsClone();

        /// <summary>
        /// Underlying Twino Server
        /// </summary>
        public TwinoServer Server { get; internal set; }

        /// <summary>
        /// Node server for distribitued systems
        /// </summary>
        public NodeManager NodeManager { get; }

        /// <summary>
        /// Client authenticator implementation.
        /// If null, all clients will be accepted.
        /// </summary>
        public IClientAuthenticator Authenticator { get; internal set; }

        /// <summary>
        /// Authorization implementation for client operations
        /// </summary>
        public IClientAuthorization Authorization { get; internal set; }

        /// <summary>
        /// Authorization implementation for administration operations
        /// </summary>
        public IAdminAuthorization AdminAuthorization { get; internal set; }

        /// <summary>
        /// Client connect and disconnect operations
        /// </summary>
        public IClientHandler ClientHandler { get; internal set; }

        /// <summary>
        /// Client message received handler (for only server-type messages)
        /// </summary>
        public IServerMessageHandler ServerMessageHandler { get; internal set; }

        /// <summary>
        /// Channel event handler
        /// </summary>
        public IQueueEventHandler QueueEventHandler { get; internal set; }

        /// <summary>
        /// Default channel authenticatır
        /// If channels do not have their own custom authenticator and this value is not null,
        /// this authenticator will authenticate the clients
        /// </summary>
        public IQueueAuthenticator QueueAuthenticator { get; internal set; }

        /// <summary>
        /// Delivery handler creator method
        /// </summary>
        internal Func<DeliveryHandlerBuilder, Task<IMessageDeliveryHandler>> DeliveryHandlerFactory { get; set; }

        /// <summary>
        /// Id generator for messages from server 
        /// </summary>
        public IUniqueIdGenerator MessageIdGenerator { get; internal set; } = new DefaultUniqueIdGenerator();

        /// <summary>
        /// Id generator for clients which has no specified unique id 
        /// </summary>
        public IUniqueIdGenerator ClientIdGenerator { get; internal set; } = new DefaultUniqueIdGenerator();

        /// <summary>
        /// Locker object for preventing to create duplicated channels when requests are concurrent and auto channel creation is enabled
        /// </summary>
        private readonly SemaphoreSlim _findOrCreateQueueLocker = new SemaphoreSlim(1, 1);

        internal IMessageContentSerializer MessageContentSerializer { get; } = new NewtonsoftContentSerializer();

        #endregion

        #region Events

        /// <summary>
        /// Triggered when a client is connected 
        /// </summary>
        public ClientEventManager OnClientConnected { get; set; }

        /// <summary>
        /// Triggered when a client is disconnected 
        /// </summary>
        public ClientEventManager OnClientDisconnected { get; set; }

        /// <summary>
        /// Triggered when a channel is created 
        /// </summary>
        public QueueEventManager OnQueueCreated { get; set; }

        /// <summary>
        /// Triggered when a channel is removed 
        /// </summary>
        public QueueEventManager OnQueueRemoved { get; set; }

        #endregion

        #region Constructors - Init

        /// <summary>
        /// Creates new Messaging Queue Server
        /// </summary>
        public TwinoMQ(IClientAuthenticator authenticator = null, IClientAuthorization authorization = null)
            : this(null, authenticator, authorization)
        {
        }

        /// <summary>
        /// Creates new Messaging Queue Server
        /// </summary>
        public TwinoMQ(TwinoMqOptions options,
                       IClientAuthenticator authenticator = null,
                       IClientAuthorization authorization = null)
        {
            Options = options ?? new TwinoMqOptions();
            Authenticator = authenticator;
            Authorization = authorization;

            _routers = new SafeList<IRouter>(256);
            _queues = new SafeList<TwinoQueue>(256);
            _clients = new SafeList<MqClient>(2048);

            NodeManager = new NodeManager(this);

            NodeManager.Initialize();

            OnClientConnected = new ClientEventManager(EventNames.ClientConnected, this);
            OnClientDisconnected = new ClientEventManager(EventNames.ClientDisconnected, this);
            OnQueueCreated = new QueueEventManager(this, EventNames.QueueCreated);
            OnQueueRemoved = new QueueEventManager(this, EventNames.QueueRemoved);
        }

        #endregion

        #region Queue Actions

        /// <summary>
        /// Finds queue by name
        /// </summary>
        public TwinoQueue FindQueue(string name)
        {
            return _queues.Find(x => x.Name == name);
        }

        /// <summary>
        /// Creates new queue in the channel with default options and default handlers
        /// </summary>
        /// <exception cref="NoNullAllowedException">Thrown when server does not have default delivery handler implementation</exception>
        /// <exception cref="OperationCanceledException">Thrown when queue limit is exceeded for the channel</exception>
        /// <exception cref="DuplicateNameException">Thrown when there is already a queue with same id</exception>
        public async Task<TwinoQueue> CreateQueue(string queueName)
        {
            QueueOptions options = QueueOptions.CloneFrom(Options);
            return await CreateQueue(queueName, options);
        }

        /// <summary>
        /// Creates new queue in the channel with default handlers
        /// </summary>
        /// <exception cref="NoNullAllowedException">Thrown when server does not have default delivery handler implementation</exception>
        /// <exception cref="OperationCanceledException">Thrown when queue limit is exceeded for the channel</exception>
        /// <exception cref="DuplicateNameException">Thrown when there is already a queue with same id</exception>
        public async Task<TwinoQueue> CreateQueue(string queueName, Action<QueueOptions> optionsAction)
        {
            QueueOptions options = QueueOptions.CloneFrom(Options);
            optionsAction(options);
            return await CreateQueue(queueName, options);
        }

        /// <summary>
        /// Creates new queue in the channel
        /// </summary>
        /// <exception cref="NoNullAllowedException">Thrown when server does not have default delivery handler implementation</exception>
        /// <exception cref="OperationCanceledException">Thrown when queue limit is exceeded for the channel</exception>
        /// <exception cref="DuplicateNameException">Thrown when there is already a queue with same id</exception>
        public Task<TwinoQueue> CreateQueue(string queueName, QueueOptions options)
        {
            return CreateQueue(queueName, options, DeliveryHandlerFactory);
        }

        /// <summary>
        /// Creates new queue in the channel
        /// </summary>
        /// <exception cref="NoNullAllowedException">Thrown when server does not have default delivery handler implementation</exception>
        /// <exception cref="OperationCanceledException">Thrown when queue limit is exceeded for the channel</exception>
        /// <exception cref="DuplicateNameException">Thrown when there is already a queue with same id</exception>
        public Task<TwinoQueue> CreateQueue(string queueName,
                                            QueueOptions options,
                                            Func<DeliveryHandlerBuilder, Task<IMessageDeliveryHandler>> asyncHandler)
        {
            return CreateQueue(queueName, options, null, asyncHandler);
        }

        internal async Task<TwinoQueue> CreateQueue(string queueName,
                                                    QueueOptions options,
                                                    TwinoMessage requestMessage,
                                                    Func<DeliveryHandlerBuilder, Task<IMessageDeliveryHandler>> asyncHandler)
        {
            if (!Filter.CheckNameEligibility(queueName))
                throw new InvalidOperationException("Invalid channel name");

            if (Options.QueueLimit > 0 && Options.QueueLimit >= _queues.Count)
                throw new OperationCanceledException("Queue limit is exceeded for the channel");

            TwinoQueue queue = _queues.Find(x => x.Name == queueName);

            if (queue != null)
                throw new DuplicateNameException($"The channel has already a queue with same name: {queueName}");

            string topic = null;
            if (requestMessage != null)
            {
                string waitForAck = requestMessage.FindHeader(TmqHeaders.ACKNOWLEDGE);
                if (!string.IsNullOrEmpty(waitForAck))
                    switch (waitForAck.Trim().ToLower())
                    {
                        case "none":
                            options.Acknowledge = QueueAckDecision.None;
                            break;
                        case "request":
                            options.Acknowledge = QueueAckDecision.JustRequest;
                            break;
                        case "wait":
                            options.Acknowledge = QueueAckDecision.WaitForAcknowledge;
                            break;
                    }

                string queueStatus = requestMessage.FindHeader(TmqHeaders.QUEUE_STATUS);
                if (queueStatus != null)
                    options.Status = QueueStatusHelper.FindStatus(queueStatus);

                topic = requestMessage.FindHeader(TmqHeaders.QUEUE_TOPIC);
            }

            queue = new TwinoQueue(this, queueName, options);
            if (!string.IsNullOrEmpty(topic))
                queue.Topic = topic;
            
            DeliveryHandlerBuilder handlerBuilder = new DeliveryHandlerBuilder
                                                    {
                                                        Server = this,
                                                        Queue = queue
                                                    };
            if (requestMessage != null)
            {
                handlerBuilder.DeliveryHandlerHeader = requestMessage.FindHeader(TmqHeaders.DELIVERY_HANDLER);
                handlerBuilder.Headers = requestMessage.Headers;
            }

            IMessageDeliveryHandler deliveryHandler = await asyncHandler(handlerBuilder);
            queue.SetMessageDeliveryHandler(deliveryHandler);
            _queues.Add(queue);

            if (QueueEventHandler != null)
                await QueueEventHandler.OnCreated(queue);

            handlerBuilder.TriggerAfterCompleted();
            //todo: ! OnQueueCreated.Trigger(queue);
            return queue;
        }


        /// <summary>
        /// Searches the queue, if queue could not be found, it will be created
        /// </summary>
        public async Task<TwinoQueue> FindOrCreateQueue(string name)
        {
            await _findOrCreateQueueLocker.WaitAsync();
            try
            {
                TwinoQueue queue = FindQueue(name);
                if (queue == null)
                    queue = await CreateQueue(name);

                return queue;
            }
            finally
            {
                _findOrCreateQueueLocker.Release();
            }
        }

        /// <summary>
        /// Removes a queue from the channel
        /// </summary>
        public async Task RemoveQueue(string name)
        {
            TwinoQueue queue = FindQueue(name);
            if (queue == null)
                return;

            await RemoveQueue(queue);
        }

        /// <summary>
        /// Removes a queue from the channel
        /// </summary>
        public async Task RemoveQueue(TwinoQueue queue)
        {
            _queues.Remove(queue);
            await queue.SetStatus(QueueStatus.Stopped);

            if (QueueEventHandler != null)
                await QueueEventHandler.OnRemoved(queue);

            //todo: ! OnQueueRemoved.Trigger(queue);
            await queue.Destroy();
        }

        #endregion

        #region Client Actions

        /// <summary>
        /// Adds new client to the server
        /// </summary>
        internal void AddClient(MqClient client)
        {
            _clients.Add(client);
            OnClientConnected.Trigger(client);
        }

        /// <summary>
        /// Removes the client from the server
        /// </summary>
        internal async Task RemoveClient(MqClient client)
        {
            _clients.Remove(client);
            await client.LeaveFromAllChannels();
            OnClientDisconnected.Trigger(client);
        }

        /// <summary>
        /// Finds client from unique id
        /// </summary>
        public MqClient FindClient(string uniqueId)
        {
            return _clients.Find(x => x.UniqueId == uniqueId);
        }

        /// <summary>
        /// Finds all connected client with specified name
        /// </summary>
        public List<MqClient> FindClientByName(string name)
        {
            return _clients.FindAll(x => x.Name == name);
        }

        /// <summary>
        /// Finds all connected client with specified type
        /// </summary>
        public List<MqClient> FindClientByType(string type)
        {
            return _clients.FindAll(x => x.Type == type);
        }

        /// <summary>
        /// Returns online clients
        /// </summary>
        public int GetOnlineClients()
        {
            return _clients.Count;
        }

        #endregion

        #region Router Actions

        /// <summary>
        /// Creates new Router and adds it to server routers.
        /// Throws exception if name is not eligible
        /// </summary>
        public IRouter AddRouter(string name, RouteMethod method)
        {
            if (!Filter.CheckNameEligibility(name))
                throw new InvalidOperationException("Invalid router name");

            if (_routers.Find(x => x.Name == name) != null)
                throw new DuplicateNameException();

            Router router = new Router(this, name, method);
            _routers.Add(router);
            return router;
        }

        /// <summary>
        /// Adds new router to server server routers
        /// Throws exception if name is not eligible
        /// </summary>
        public void AddRouter(IRouter router)
        {
            if (!Filter.CheckNameEligibility(router.Name))
                throw new InvalidOperationException("Invalid router name");

            if (_routers.Find(x => x.Name == router.Name) != null)
                throw new DuplicateNameException();

            _routers.Add(router);
        }

        /// <summary>
        /// Removes the router from server routers
        /// </summary>
        public void RemoveRouter(IRouter router)
        {
            _routers.Remove(router);
        }

        /// <summary>
        /// Finds router by it's name
        /// </summary>
        public IRouter FindRouter(string name)
        {
            return _routers.Find(x => x.Name == name);
        }

        #endregion
    }
}