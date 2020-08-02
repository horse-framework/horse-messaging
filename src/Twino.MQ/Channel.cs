using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Twino.MQ.Clients;
using Twino.MQ.Events;
using Twino.MQ.Helpers;
using Twino.MQ.Options;
using Twino.MQ.Queues;
using Twino.Protocols.TMQ;

namespace Twino.MQ
{
    /// <summary>
    /// Messaging Queue Channel
    /// </summary>
    public class Channel
    {
        #region Properties

        /// <summary>
        /// Unique channel name (not case-sensetive)
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Server of the channel
        /// </summary>
        public TwinoMQ Server { get; }

        /// <summary>
        /// Channel options
        /// </summary>
        public ChannelOptions Options { get; }

        private readonly SafeList<QueueId> _allowedQueues = new SafeList<QueueId>(32);

        /// <summary>
        /// Allowed queue id list in this channel
        /// </summary>
        public IEnumerable<QueueId> AllowedQueues => _allowedQueues.GetUnsafeList();

        /// <summary>
        /// Active channel queues
        /// </summary>
        public IEnumerable<ChannelQueue> Queues => _queues.GetUnsafeList();

        /// <summary>
        /// Clone list of active channel queues
        /// </summary>
        public IEnumerable<ChannelQueue> QueuesClone => _queues.GetAsClone();

        private readonly SafeList<ChannelQueue> _queues;

        /// <summary>
        /// Clients in the channel as thread-unsafe list
        /// </summary>
        public IEnumerable<ChannelClient> ClientsUnsafe => _clients.GetUnsafeList();

        /// <summary>
        /// Clients in the channel as cloned list
        /// </summary>
        public List<ChannelClient> ClientsClone => _clients.GetAsClone();

        private readonly SafeList<ChannelClient> _clients;

        /// <summary>
        /// Locker object for preventing to create duplicated queues when requests are concurrent and auto queue creation is enabled
        /// </summary>
        private readonly SemaphoreSlim _findOrCreateQueueLocker = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Payload object for end-user usage
        /// </summary>
        public object Payload { get; set; }

        /// <summary>
        /// Triggered when a queue is created 
        /// </summary>
        public QueueEventManager OnQueueCreated { get; }

        /// <summary>
        /// Triggered when a queue is updated 
        /// </summary>
        public QueueEventManager OnQueueUpdated { get; }

        /// <summary>
        /// Triggered when a queue is removed 
        /// </summary>
        public QueueEventManager OnQueueRemoved { get; }

        /// <summary>
        /// Triggered when a client is joined 
        /// </summary>
        public SubscriptionEventManager OnClientJoined { get; }

        /// <summary>
        /// Triggered when a client is left 
        /// </summary>
        public SubscriptionEventManager OnClientLeft { get; }

        #endregion

        #region Constructors

        internal Channel(TwinoMQ server, ChannelOptions options, string name)
        {
            Server = server;
            Options = options;
            Name = name;

            _queues = new SafeList<ChannelQueue>(8);
            _clients = new SafeList<ChannelClient>(256);

            OnQueueCreated = new QueueEventManager(server, EventNames.QueueCreated, this);
            OnQueueUpdated = new QueueEventManager(server, EventNames.QueueUpdated, this);
            OnQueueRemoved = new QueueEventManager(server, EventNames.QueueRemoved, this);
            OnClientJoined = new SubscriptionEventManager(server, EventNames.ClientJoined, this);
            OnClientLeft = new SubscriptionEventManager(server, EventNames.ClientLeft, this);
        }

        /// <summary>
        /// Destroys the channel, clears all queues and clients 
        /// </summary>
        public async Task Destroy()
        {
            _clients.Clear();

            List<ChannelQueue> queues = _queues.GetAsClone();
            _queues.Clear();

            foreach (ChannelQueue queue in queues)
                await queue.Destroy();

            OnQueueCreated.Dispose();
            OnQueueUpdated.Dispose();
            OnQueueRemoved.Dispose();
            OnClientJoined.Dispose();
            OnClientLeft.Dispose();
        }

        /// <summary>
        /// If all queues are empty and there is no client, destroys the channel 
        /// </summary>
        private async Task CheckAutoDestroy()
        {
            List<ChannelQueue> list = _queues.GetAsClone();

            foreach (ChannelQueue queue in list)
            {
                if (!queue.IsEmpty())
                    return;
            }

            await Server.RemoveChannel(this);
        }

        #endregion

        #region Queue Actions

        /// <summary>
        /// Finds queue by content type
        /// </summary>
        public ChannelQueue FindQueue(ushort queueId)
        {
            return _queues.Find(x => x.Id == queueId);
        }

        /// <summary>
        /// Creates new queue in the channel with default options and default handlers
        /// </summary>
        /// <exception cref="NoNullAllowedException">Thrown when server does not have default delivery handler implementation</exception>
        /// <exception cref="OperationCanceledException">Thrown when queue limit is exceeded for the channel</exception>
        /// <exception cref="DuplicateNameException">Thrown when there is already a queue with same id</exception>
        public async Task<ChannelQueue> CreateQueue(ushort queueId)
        {
            ChannelQueueOptions options = ChannelQueueOptions.CloneFrom(Options);
            return await CreateQueue(queueId, options);
        }

        /// <summary>
        /// Creates new queue in the channel with default handlers
        /// </summary>
        /// <exception cref="NoNullAllowedException">Thrown when server does not have default delivery handler implementation</exception>
        /// <exception cref="OperationCanceledException">Thrown when queue limit is exceeded for the channel</exception>
        /// <exception cref="DuplicateNameException">Thrown when there is already a queue with same id</exception>
        public async Task<ChannelQueue> CreateQueue(ushort queueId, Action<ChannelQueueOptions> optionsAction)
        {
            ChannelQueueOptions options = ChannelQueueOptions.CloneFrom(Options);
            optionsAction(options);
            return await CreateQueue(queueId, options);
        }

        /// <summary>
        /// Creates new queue in the channel
        /// </summary>
        /// <exception cref="NoNullAllowedException">Thrown when server does not have default delivery handler implementation</exception>
        /// <exception cref="OperationCanceledException">Thrown when queue limit is exceeded for the channel</exception>
        /// <exception cref="DuplicateNameException">Thrown when there is already a queue with same id</exception>
        public Task<ChannelQueue> CreateQueue(ushort queueId, ChannelQueueOptions options)
        {
            return CreateQueue(queueId, options, Server.DeliveryHandlerFactory);
        }

        /// <summary>
        /// Creates new queue in the channel
        /// </summary>
        /// <exception cref="NoNullAllowedException">Thrown when server does not have default delivery handler implementation</exception>
        /// <exception cref="OperationCanceledException">Thrown when queue limit is exceeded for the channel</exception>
        /// <exception cref="DuplicateNameException">Thrown when there is already a queue with same id</exception>
        public Task<ChannelQueue> CreateQueue(ushort queueId,
                                              ChannelQueueOptions options,
                                              Func<DeliveryHandlerBuilder, Task<IMessageDeliveryHandler>> asyncHandler)
        {
            return CreateQueue(queueId, options, null, asyncHandler);
        }

        internal async Task<ChannelQueue> CreateQueue(ushort queueId,
                                                      ChannelQueueOptions options,
                                                      TmqMessage requestMessage,
                                                      Func<DeliveryHandlerBuilder, Task<IMessageDeliveryHandler>> asyncHandler)
        {
            //multiple queues are not allowed
            if (!Options.AllowMultipleQueues && _queues.Count > 0)
                return null;

            //if content type is not allowed for this channel, return null
            if (Options.AllowedQueues != null && Options.AllowedQueues.Length > 0)
                if (!Options.AllowedQueues.Contains(queueId))
                    return null;

            if (Options.QueueLimit > 0 && Options.QueueLimit >= _queues.Count)
                throw new OperationCanceledException("Queue limit is exceeded for the channel");

            ChannelQueue queue = _queues.Find(x => x.Id == queueId);

            if (queue != null)
                throw new DuplicateNameException($"The channel has already a queue with same content type: {queueId}");

            queue = new ChannelQueue(this, queueId, options);
            DeliveryHandlerBuilder handlerBuilder = new DeliveryHandlerBuilder
                                                    {
                                                        Server = Server,
                                                        Channel = this,
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

            if (Server.ChannelEventHandler != null)
                await Server.ChannelEventHandler.OnQueueCreated(queue, this);

            handlerBuilder.TriggerAfterCompleted();
            OnQueueCreated.Trigger(queue);
            return queue;
        }


        /// <summary>
        /// Searches the queue, if queue could not be found, it will be created
        /// </summary>
        public async Task<ChannelQueue> FindOrCreateQueue(ushort id)
        {
            await _findOrCreateQueueLocker.WaitAsync();
            try
            {
                ChannelQueue queue = FindQueue(id);
                if (queue == null)
                    queue = await CreateQueue(id);

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
        public async Task RemoveQueue(ushort queueId)
        {
            ChannelQueue queue = FindQueue(queueId);
            if (queue == null)
                return;

            await RemoveQueue(queue);
        }

        /// <summary>
        /// Removes a queue from the channel
        /// </summary>
        public async Task RemoveQueue(ChannelQueue queue)
        {
            _queues.Remove(queue);
            await queue.SetStatus(QueueStatus.Stopped);

            if (Server.ChannelEventHandler != null)
                await Server.ChannelEventHandler.OnQueueRemoved(queue, this);

            OnQueueRemoved.Trigger(queue);
            await queue.Destroy();
        }

        #endregion

        #region Client Actions

        /// <summary>
        /// Returns client count in the channel
        /// </summary>
        /// <returns></returns>
        public int ClientsCount()
        {
            return _clients.Count;
        }

        /// <summary>
        /// Adds the client to the channel
        /// </summary>
        public async Task<ClientJoinResult> AddClient(MqClient client)
        {
            if (Server.ChannelAuthenticator != null)
            {
                bool allowed = await Server.ChannelAuthenticator.Authenticate(this, client);
                if (!allowed)
                    return ClientJoinResult.Unauthorized;
            }

            if (Options.ClientLimit > 0 && _clients.Count >= Options.ClientLimit)
                return ClientJoinResult.Full;

            ChannelClient cc = new ChannelClient(this, client);
            _clients.Add(cc);
            client.Join(cc);

            if (Server.ChannelEventHandler != null)
                await Server.ChannelEventHandler.OnClientJoined(cc);

            IEnumerable<ChannelQueue> list = _queues.GetAsClone();
            foreach (ChannelQueue queue in list)
                _ = queue.Trigger();

            OnClientJoined.Trigger(cc);

            return ClientJoinResult.Success;
        }

        /// <summary>
        /// Removes client from the channel
        /// </summary>
        public async Task RemoveClient(ChannelClient client)
        {
            _clients.Remove(client);
            client.Client.Leave(client);

            if (Server.ChannelEventHandler != null)
                await Server.ChannelEventHandler.OnClientLeft(client);

            if (Options.DestroyWhenEmpty && _clients.Count == 0)
                await CheckAutoDestroy();

            OnClientLeft.Trigger(client);
        }

        /// <summary>
        /// Removes client from the channel, does not call MqClient's remove method
        /// </summary>
        internal async Task RemoveClientSilent(ChannelClient client)
        {
            _clients.Remove(client);

            if (Server.ChannelEventHandler != null)
                await Server.ChannelEventHandler.OnClientLeft(client);

            if (Options.DestroyWhenEmpty && _clients.Count == 0)
                await CheckAutoDestroy();

            OnClientLeft.Trigger(client);
        }

        /// <summary>
        /// Removes client from the channel
        /// </summary>
        public async Task<bool> RemoveClient(MqClient client)
        {
            ChannelClient cc = _clients.FindAndRemove(x => x.Client == client);

            if (cc == null)
                return false;

            client.Leave(cc);

            if (Server.ChannelEventHandler != null)
                await Server.ChannelEventHandler.OnClientLeft(cc);

            if (Options.DestroyWhenEmpty && _clients.Count == 0)
                await CheckAutoDestroy();

            OnClientLeft.Trigger(cc);
            return true;
        }

        /// <summary>
        /// Finds client in the channel
        /// </summary>
        public ChannelClient FindClient(string uniqueId)
        {
            return _clients.Find(x => x.Client.UniqueId == uniqueId);
        }

        /// <summary>
        /// Finds client in the channel
        /// </summary>
        public ChannelClient FindClient(MqClient client)
        {
            return _clients.Find(x => x.Client == client);
        }

        /// <summary>
        /// Gets next client with round robin algorithm and updates index
        /// </summary>
        internal ChannelClient GetNextRRClient(ref int index)
        {
            List<ChannelClient> clients = _clients.GetAsClone();
            if (index < 0 || index + 1 >= clients.Count)
            {
                if (clients.Count == 0)
                    return null;

                index = 0;
                return clients[0];
            }

            index++;
            return clients[index];
        }

        #endregion
    }
}