using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;

namespace Horse.Messaging.Server.Transactions
{
    public class TransactionRider
    {
        public HorseRider Rider { get; }

        private readonly Dictionary<string, ServerTransactionContainer> _containers = new Dictionary<string, ServerTransactionContainer>();

        public TransactionRider(HorseRider rider)
        {
            Rider = rider;
        }

        public Task Initialize()
        {
            return Task.CompletedTask;
        }

        public ServerTransactionContainer CreateContainer(string name, TimeSpan timeout,
                                                          IServerTransactionEndpoint commitEndpoint,
                                                          IServerTransactionEndpoint rollbackEndpoint,
                                                          IServerTransactionEndpoint timeoutEndpoint,
                                                          IServerTransactionHandler handler = null)
        {
            lock (_containers)
            {
                if (_containers.ContainsKey(name))
                    throw new($"There is already a transaction container with name: {name}");

                ServerTransactionContainer container = new ServerTransactionContainer(name, timeout);
                container.CommitEndpoint = commitEndpoint;
                container.RollbackEndpoint = rollbackEndpoint;
                container.TimeoutEndpoint = timeoutEndpoint;
                container.Handler = handler;

                _containers.Add(name, container);
                return container;
            }
        }

        public Task Begin(MessagingClient client, HorseMessage message)
        {
            ServerTransactionContainer container;
            lock (_containers)
                _containers.TryGetValue(message.Target, out container);

            if (container == null)
            {
                return client.SendAsync(message.CreateResponse(HorseResultCode.NotFound));
            }

            container.Create(client, message);
            return Task.CompletedTask;
        }

        public Task Commit(MessagingClient client, HorseMessage message)
        {
            ServerTransactionContainer container;
            lock (_containers)
                _containers.TryGetValue(message.Target, out container);

            if (container == null)
            {
                return client.SendAsync(message.CreateResponse(HorseResultCode.NotFound));
            }

            return container.Commit(client, message);
        }

        public Task Rollback(MessagingClient client, HorseMessage message)
        {
            ServerTransactionContainer container;
            lock (_containers)
                _containers.TryGetValue(message.Target, out container);

            if (container == null)
            {
                return client.SendAsync(message.CreateResponse(HorseResultCode.NotFound));
            }

            return container.Rollback(client, message);
        }
    }
}