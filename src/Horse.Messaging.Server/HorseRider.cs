using System;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Cache;
using Horse.Messaging.Server.Channels;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Containers;
using Horse.Messaging.Server.Direct;
using Horse.Messaging.Server.Options;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Routing;
using Horse.Messaging.Server.Transactions;
using Horse.Server;

namespace Horse.Messaging.Server
{
    /// <summary>
    /// Horse Messaging Queue Server
    /// </summary>
    public class HorseRider
    {
        #region Properties

        /// <summary>
        /// Queue rider object manages all queues and their operations
        /// </summary>
        public QueueRider Queue { get; }

        /// <summary>
        /// Client rider object manages all clients and their operations
        /// </summary>
        public ClientRider Client { get; }

        /// <summary>
        /// Direct rider object manages all direct messages
        /// </summary>
        public DirectRider Direct { get; }

        /// <summary>
        /// Router rider object manages all routers and their operations
        /// </summary>
        public RouterRider Router { get; }

        /// <summary>
        /// Channel rider object manages all channels and their operations
        /// </summary>
        public ChannelRider Channel { get; }

        /// <summary>
        /// Manages key value memory caches
        /// </summary>
        public HorseCache Cache { get; }

        /// <summary>
        /// Transaction rider object manages all transactions and their operations
        /// </summary>
        public TransactionRider Transaction { get; }

        /// <summary>
        /// Messaging Queue Server Options
        /// </summary>
        public HorseRiderOptions Options { get; internal set; }

        /// <summary>
        /// Underlying Horse Server
        /// </summary>
        public HorseServer Server { get; internal set; }

        /// <summary>
        /// Node server for distribitued systems
        /// </summary>
        public NodeManager NodeManager { get; }

        /// <summary>
        /// Error handlers
        /// </summary>
        public ArrayContainer<IErrorHandler> ErrorHandlers { get; } = new ArrayContainer<IErrorHandler>();

        /// <summary>
        /// Client message received handler (for only server-type messages)
        /// </summary>
        public ArrayContainer<IServerMessageHandler> ServerMessageHandlers { get; } = new ArrayContainer<IServerMessageHandler>();

        /// <summary>
        /// Id generator for messages from server 
        /// </summary>
        public IUniqueIdGenerator MessageIdGenerator { get; internal set; } = new DefaultUniqueIdGenerator();

        /// <summary>
        /// Message content serializer
        /// </summary>
        internal IMessageContentSerializer MessageContentSerializer { get; } = new NewtonsoftContentSerializer();

        private bool _initialized;

        #endregion

        /// <summary>
        /// Creates new Messaging Queue Server
        /// </summary>
        internal HorseRider() : this(null)
        {
        }

        /// <summary>
        /// Creates new Messaging Queue Server
        /// </summary>
        internal HorseRider(HorseRiderOptions options)
        {
            Options = options ?? new HorseRiderOptions();

            Client = new ClientRider(this);
            Queue = new QueueRider(this);
            Direct = new DirectRider(this);
            Router = new RouterRider(this);
            Channel = new ChannelRider(this);
            Transaction = new TransactionRider(this);
            Cache = new HorseCache(this);
            NodeManager = new NodeManager(this);

            Cache.Initialize();
            NodeManager.Initialize();
        }

        /// <summary>
        /// Initializes late initialization required services
        /// </summary>
        internal void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;
            Transaction.Initialize();
        }

        /// <summary>
        /// Trigger error handlers
        /// </summary>
        internal void SendError(string hint, Exception exception, string payload)
        {
            foreach (IErrorHandler handler in ErrorHandlers.All())
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
    }
}