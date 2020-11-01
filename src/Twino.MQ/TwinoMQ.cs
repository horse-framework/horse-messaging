using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
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
        #region Fields

        private readonly SafeList<TwinoQueue> _queues;
        private readonly SafeList<MqClient> _clients;
        private readonly SafeList<IRouter> _routers;
        private IClientHandler[] _clientHandlers = new IClientHandler[0];
        private IErrorHandler[] _errorHandlers = new IErrorHandler[0];
        private IQueueEventHandler[] _queueEventHandlers = new IQueueEventHandler[0];
        private IServerMessageHandler[] _messageHandlers = new IServerMessageHandler[0];
        private IQueueAuthenticator[] _queueAuthenticators = new IQueueAuthenticator[0];
        private IClientAuthenticator[] _authenticators = new IClientAuthenticator[0];
        private IClientAuthorization[] _authorizations = new IClientAuthorization[0];
        private IAdminAuthorization[] _adminAuthorizations = new IAdminAuthorization[0];

        /// <summary>
        /// Locker object for preventing to create duplicated queues when requests are concurrent and auto queue creation is enabled
        /// </summary>
        private readonly SemaphoreSlim _createQueueLocker = new SemaphoreSlim(1, 1);

        #endregion

        #region Properties

        /// <summary>
        /// Messaging Queue Server Options
        /// </summary>
        public TwinoMqOptions Options { get; internal set; }

        /// <summary>
        /// All Queues of the server
        /// </summary>
        public IEnumerable<TwinoQueue> Queues => _queues.GetAsClone();

        /// <summary>
        /// All connected clients in the server
        /// </summary>
        public IEnumerable<MqClient> Clients => _clients.GetAsClone();

        /// <summary>
        /// All Queues of the server
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
        /// Client authenticator implementations.
        /// If null, all clients will be accepted.
        /// </summary>
        public IEnumerable<IClientAuthenticator> Authenticators => _authenticators;

        /// <summary>
        /// Authorization implementations for client operations
        /// </summary>
        public IEnumerable<IClientAuthorization> Authorizations => _authorizations;

        /// <summary>
        /// Authorization implementations for administration operations
        /// </summary>
        public IEnumerable<IAdminAuthorization> AdminAuthorizations => _adminAuthorizations;

        /// <summary>
        /// Client connect and disconnect operations
        /// </summary>
        public IEnumerable<IClientHandler> ClientHandlers => _clientHandlers;

        /// <summary>
        /// Error handlers
        /// </summary>
        public IEnumerable<IErrorHandler> ErrorHandlers => _errorHandlers;

        /// <summary>
        /// Client message received handler (for only server-type messages)
        /// </summary>
        public IEnumerable<IServerMessageHandler> ServerMessageHandlers => _messageHandlers;

        /// <summary>
        /// Queue event handlers
        /// </summary>
        public IEnumerable<IQueueEventHandler> QueueEventHandlers => _queueEventHandlers;

        /// <summary>
        /// Queue authenticators
        /// </summary>
        public IEnumerable<IQueueAuthenticator> QueueAuthenticators => _queueAuthenticators;

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
        /// Triggered when a queue is created 
        /// </summary>
        public QueueEventManager OnQueueCreated { get; set; }

        /// <summary>
        /// Triggered when a queue is updated 
        /// </summary>
        public QueueEventManager OnQueueUpdated { get; set; }

        /// <summary>
        /// Triggered when a queue is removed 
        /// </summary>
        public QueueEventManager OnQueueRemoved { get; set; }

        #endregion

        #region Constructors - Init

        /// <summary>
        /// Creates new Messaging Queue Server
        /// </summary>
        public TwinoMQ() : this(null)
        {
        }

        /// <summary>
        /// Creates new Messaging Queue Server
        /// </summary>
        public TwinoMQ(TwinoMqOptions options)
        {
            Options = options ?? new TwinoMqOptions();

            _routers = new SafeList<IRouter>(256);
            _queues = new SafeList<TwinoQueue>(256);
            _clients = new SafeList<MqClient>(2048);

            NodeManager = new NodeManager(this);

            NodeManager.Initialize();

            OnClientConnected = new ClientEventManager(EventNames.ClientConnected, this);
            OnClientDisconnected = new ClientEventManager(EventNames.ClientDisconnected, this);
            OnQueueCreated = new QueueEventManager(this, EventNames.QueueCreated);
            OnQueueUpdated = new QueueEventManager(this, EventNames.QueueUpdated);
            OnQueueRemoved = new QueueEventManager(this, EventNames.QueueRemoved);
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Adds queue event handler
        /// </summary>
        public void AddQueueEventHandler(IQueueEventHandler handler)
        {
            List<IQueueEventHandler> list = _queueEventHandlers.ToList();
            list.Add(handler);
            _queueEventHandlers = list.ToArray();
        }

        /// <summary>
        /// Removes queue event handler
        /// </summary>
        public void RemoveQueueEventHandler(IQueueEventHandler handler)
        {
            List<IQueueEventHandler> list = _queueEventHandlers.ToList();
            list.Remove(handler);
            _queueEventHandlers = list.ToArray();
        }

        /// <summary>
        /// Adds client handler
        /// </summary>
        public void AddClientHandler(IClientHandler handler)
        {
            List<IClientHandler> list = _clientHandlers.ToList();
            list.Add(handler);
            _clientHandlers = list.ToArray();
        }

        /// <summary>
        /// Removes client handler
        /// </summary>
        public void RemoveClientHandler(IClientHandler handler)
        {
            List<IClientHandler> list = _clientHandlers.ToList();
            list.Remove(handler);
            _clientHandlers = list.ToArray();
        }

        /// <summary>
        /// Adds error handler
        /// </summary>
        public void AddErrorHandler(IErrorHandler handler)
        {
            List<IErrorHandler> list = _errorHandlers.ToList();
            list.Add(handler);
            _errorHandlers = list.ToArray();
        }

        /// <summary>
        /// Removes error handler
        /// </summary>
        public void RemoveErrorHandler(IErrorHandler handler)
        {
            List<IErrorHandler> list = _errorHandlers.ToList();
            list.Remove(handler);
            _errorHandlers = list.ToArray();
        }

        /// <summary>
        /// Adds Message handler
        /// </summary>
        public void AddMessageHandler(IServerMessageHandler handler)
        {
            List<IServerMessageHandler> list = _messageHandlers.ToList();
            list.Add(handler);
            _messageHandlers = list.ToArray();
        }

        /// <summary>
        /// Removes Message handler
        /// </summary>
        public void RemoveMessageHandler(IServerMessageHandler handler)
        {
            List<IServerMessageHandler> list = _messageHandlers.ToList();
            list.Remove(handler);
            _messageHandlers = list.ToArray();
        }

        /// <summary>
        /// Adds Queue authenticator
        /// </summary>
        public void AddQueueAuthenticator(IQueueAuthenticator handler)
        {
            List<IQueueAuthenticator> list = _queueAuthenticators.ToList();
            list.Add(handler);
            _queueAuthenticators = list.ToArray();
        }

        /// <summary>
        /// Removes Queue authenticator
        /// </summary>
        public void RemoveQueueAuthenticator(IQueueAuthenticator handler)
        {
            List<IQueueAuthenticator> list = _queueAuthenticators.ToList();
            list.Remove(handler);
            _queueAuthenticators = list.ToArray();
        }

        /// <summary>
        /// Adds Client authenticator
        /// </summary>
        public void AddClientAuthenticator(IClientAuthenticator handler)
        {
            List<IClientAuthenticator> list = _authenticators.ToList();
            list.Add(handler);
            _authenticators = list.ToArray();
        }

        /// <summary>
        /// Removes Client authenticator
        /// </summary>
        public void RemoveClientAuthenticator(IClientAuthenticator handler)
        {
            List<IClientAuthenticator> list = _authenticators.ToList();
            list.Remove(handler);
            _authenticators = list.ToArray();
        }

        /// <summary>
        /// Adds Client authorization
        /// </summary>
        public void AddClientAuthorization(IClientAuthorization handler)
        {
            List<IClientAuthorization> list = _authorizations.ToList();
            list.Add(handler);
            _authorizations = list.ToArray();
        }

        /// <summary>
        /// Removes Client authorization
        /// </summary>
        public void RemoveClientAuthorization(IClientAuthorization handler)
        {
            List<IClientAuthorization> list = _authorizations.ToList();
            list.Remove(handler);
            _authorizations = list.ToArray();
        }

        /// <summary>
        /// Adds Admin authorization
        /// </summary>
        public void AddAdminAuthorization(IAdminAuthorization handler)
        {
            List<IAdminAuthorization> list = _adminAuthorizations.ToList();
            list.Add(handler);
            _adminAuthorizations = list.ToArray();
        }

        /// <summary>
        /// Removes Admin authorization
        /// </summary>
        public void RemoveAdminAuthorization(IAdminAuthorization handler)
        {
            List<IAdminAuthorization> list = _adminAuthorizations.ToList();
            list.Remove(handler);
            _adminAuthorizations = list.ToArray();
        }

        /// <summary>
        /// Trigger error handlers
        /// </summary>
        internal void SendError(string hint, Exception exception, string payload)
        {
            foreach (IErrorHandler handler in _errorHandlers)
            {
                //don't crash by end-user exception
                try
                {
                    handler.Error(hint, exception, payload);
                }
                catch
                {
                }
            }
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
        /// Creates new queue with default options and default handlers
        /// </summary>
        /// <exception cref="NoNullAllowedException">Thrown when server does not have default delivery handler implementation</exception>
        /// <exception cref="OperationCanceledException">Thrown when queue limit is exceeded for the server</exception>
        /// <exception cref="DuplicateNameException">Thrown when there is already a queue with same id</exception>
        public async Task<TwinoQueue> CreateQueue(string queueName)
        {
            QueueOptions options = QueueOptions.CloneFrom(Options);
            return await CreateQueue(queueName, options);
        }

        /// <summary>
        /// Creates new queue with default handlers
        /// </summary>
        /// <exception cref="NoNullAllowedException">Thrown when server does not have default delivery handler implementation</exception>
        /// <exception cref="OperationCanceledException">Thrown when queue limit is exceeded for the server</exception>
        /// <exception cref="DuplicateNameException">Thrown when there is already a queue with same id</exception>
        public async Task<TwinoQueue> CreateQueue(string queueName, Action<QueueOptions> optionsAction)
        {
            QueueOptions options = QueueOptions.CloneFrom(Options);
            optionsAction(options);
            return await CreateQueue(queueName, options);
        }

        /// <summary>
        /// Creates new queue
        /// </summary>
        /// <exception cref="NoNullAllowedException">Thrown when server does not have default delivery handler implementation</exception>
        /// <exception cref="OperationCanceledException">Thrown when queue limit is exceeded for the server</exception>
        /// <exception cref="DuplicateNameException">Thrown when there is already a queue with same id</exception>
        public Task<TwinoQueue> CreateQueue(string queueName, QueueOptions options)
        {
            return CreateQueue(queueName, options, DeliveryHandlerFactory);
        }

        /// <summary>
        /// Creates new queue in the server
        /// </summary>
        /// <exception cref="NoNullAllowedException">Thrown when server does not have default delivery handler implementation</exception>
        /// <exception cref="OperationCanceledException">Thrown when queue limit is exceeded for the server</exception>
        /// <exception cref="DuplicateNameException">Thrown when there is already a queue with same id</exception>
        public Task<TwinoQueue> CreateQueue(string queueName,
                                            QueueOptions options,
                                            Func<DeliveryHandlerBuilder, Task<IMessageDeliveryHandler>> asyncHandler)
        {
            return CreateQueue(queueName, options, null, asyncHandler, false, false);
        }

        internal async Task<TwinoQueue> CreateQueue(string queueName,
                                                    QueueOptions options,
                                                    TwinoMessage requestMessage,
                                                    Func<DeliveryHandlerBuilder, Task<IMessageDeliveryHandler>> asyncHandler,
                                                    bool hideException,
                                                    bool returnIfExists)
        {
            await _createQueueLocker.WaitAsync();
            try
            {
                if (!Filter.CheckNameEligibility(queueName))
                    throw new InvalidOperationException("Invalid queue name");

                if (Options.QueueLimit > 0 && Options.QueueLimit >= _queues.Count)
                    throw new OperationCanceledException("Queue limit is exceeded for the server");

                TwinoQueue queue = _queues.Find(x => x.Name == queueName);

                if (queue != null)
                {
                    if (returnIfExists)
                        return queue;

                    throw new DuplicateNameException($"The server has already a queue with same name: {queueName}");
                }

                string topic = null;
                bool statusSpecified = false; //when queue is created by subscriber, it will be initialized if status is specified
                if (requestMessage != null)
                {
                    string waitForAck = requestMessage.FindHeader(TwinoHeaders.ACKNOWLEDGE);
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

                    string queueStatus = requestMessage.FindHeader(TwinoHeaders.QUEUE_STATUS);
                    if (queueStatus != null)
                    {
                        statusSpecified = true;
                        options.Status = QueueStatusHelper.FindStatus(queueStatus);
                    }

                    topic = requestMessage.FindHeader(TwinoHeaders.QUEUE_TOPIC);

                    string delay = requestMessage.FindHeader(TwinoHeaders.DELAY_BETWEEN_MESSAGES);
                    if (!string.IsNullOrEmpty(delay))
                        options.DelayBetweenMessages = Convert.ToInt32(delay);
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
                    handlerBuilder.DeliveryHandlerHeader = requestMessage.FindHeader(TwinoHeaders.DELIVERY_HANDLER);
                    handlerBuilder.Headers = requestMessage.Headers;
                }

                bool initialize;
                //if queue creation is triggered by consumer subscription, we might skip initialization
                if (requestMessage != null && requestMessage.Type == MessageType.Server && requestMessage.ContentType == KnownContentTypes.Subscribe)
                    initialize = statusSpecified;
                else
                    initialize = true;

                if (initialize)
                {
                    IMessageDeliveryHandler deliveryHandler = await asyncHandler(handlerBuilder);
                    queue.InitializeQueue(deliveryHandler);
                }

                _queues.Add(queue);
                foreach (IQueueEventHandler handler in _queueEventHandlers)
                    await handler.OnCreated(queue);

                if (initialize)
                    handlerBuilder.TriggerAfterCompleted();

                OnQueueCreated.Trigger(queue);
                return queue;
            }
            catch (Exception e)
            {
                SendError("CREATE_QUEUE", e, $"QueueName:{queueName}");

                if (!hideException)
                    throw;

                return null;
            }
            finally
            {
                try
                {
                    _createQueueLocker.Release();
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Removes a queue from the server
        /// </summary>
        public async Task RemoveQueue(string name)
        {
            TwinoQueue queue = FindQueue(name);
            if (queue == null)
                return;

            await RemoveQueue(queue);
        }

        /// <summary>
        /// Removes a queue from the server
        /// </summary>
        public async Task RemoveQueue(TwinoQueue queue)
        {
            try
            {
                _queues.Remove(queue);
                await queue.SetStatus(QueueStatus.Stopped);

                foreach (IQueueEventHandler handler in _queueEventHandlers)
                    await handler.OnRemoved(queue);

                OnQueueRemoved.Trigger(queue);
                await queue.Destroy();
            }
            catch (Exception e)
            {
                SendError("REMOVE_QUEUE", e, $"QueueName:{queue?.Name}");
            }
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
            await client.UnsubscribeFromAllQueues();
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
            try
            {
                if (!Filter.CheckNameEligibility(name))
                    throw new InvalidOperationException("Invalid router name");

                if (_routers.Find(x => x.Name == name) != null)
                    throw new DuplicateNameException();

                Router router = new Router(this, name, method);
                _routers.Add(router);
                return router;
            }
            catch (Exception e)
            {
                SendError("ADD_ROUTER", e, $"RouterName:{name}");
                throw;
            }
        }

        /// <summary>
        /// Adds new router to server server routers
        /// Throws exception if name is not eligible
        /// </summary>
        public void AddRouter(IRouter router)
        {
            try
            {
                if (!Filter.CheckNameEligibility(router.Name))
                    throw new InvalidOperationException("Invalid router name");

                if (_routers.Find(x => x.Name == router.Name) != null)
                    throw new DuplicateNameException();

                _routers.Add(router);
            }
            catch (Exception e)
            {
                SendError("ADD_ROUTER", e, $"RouterName:{router?.Name}");
                throw;
            }
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