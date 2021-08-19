using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Channels;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Direct;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Routing;

namespace Horse.Messaging.Server.Transactions
{
    /// <summary>
    /// Manages remote transactions in messaging server
    /// </summary>
    public class TransactionRider
    {
        /// <summary>
        /// Root horse rider object
        /// </summary>
        public HorseRider Rider { get; }

        /// <summary>
        /// Default custom transaction handler implementation for all kind of transactions
        /// </summary>
        public IServerTransactionHandler DefaultHandler { get; set; }

        private readonly Dictionary<string, ServerTransactionContainer> _containers = new Dictionary<string, ServerTransactionContainer>();
        private List<TransactionContainerData> _data = new List<TransactionContainerData>();

        /// <summary>
        /// Creates new transaction rider
        /// </summary>
        public TransactionRider(HorseRider rider)
        {
            Rider = rider;
        }

        /// <summary>
        /// Initializes transaction rider and saved containers
        /// </summary>
        public void Initialize()
        {
            string filename = "Data/transactions.json";

            if (!System.IO.File.Exists(filename))
                return;

            string fileContent = System.IO.File.ReadAllText(filename);
            _data = System.Text.Json.JsonSerializer.Deserialize<List<TransactionContainerData>>(fileContent);

            foreach (TransactionContainerData item in _data)
            {
                ServerTransactionContainer container = new ServerTransactionContainer(item.Name, TimeSpan.FromMilliseconds(item.Timeout));

                container.CommitEndpoint = ResolveAndCreateEndpoint(container, item.CommitType, item.CommitParam);
                container.RollbackEndpoint = ResolveAndCreateEndpoint(container, item.RollbackType, item.RollbackParam);
                container.TimeoutEndpoint = ResolveAndCreateEndpoint(container, item.TimeoutType, item.TimeoutParam);

                container.Handler = string.IsNullOrEmpty(item.HandlerType)
                    ? DefaultHandler
                    : ResolveAndCreateHandler(item.HandlerType);

                if (container.Handler != null)
                    _ = container.Handler.Load(container);

                lock (_containers)
                    _containers.Add(item.Name, container);
            }
        }

        private void SaveTransactionContainers()
        {
            try
            {
                string filename = "Data/transactions.json";
                string fileContent = System.Text.Json.JsonSerializer.Serialize(_data);
                System.IO.File.WriteAllText(filename, fileContent);
            }
            catch (Exception e)
            {
                Rider.SendError("TransactionRider.SaveTransactionContainers", e, string.Empty);
            }
        }

        private Type ResolveType(string fullname)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            Type type = assembly.GetType(fullname);

            if (type == null)
            {
                assembly = Assembly.GetCallingAssembly();
                type = assembly.GetType(fullname);
            }

            return type;
        }

        private IServerTransactionHandler ResolveAndCreateHandler(string fullname)
        {
            Type handlerType = ResolveType(fullname);

            if (handlerType == null)
                throw new Exception($"Transaction Handler type not found: {fullname}");

            IServerTransactionHandler handler = Activator.CreateInstance(handlerType) as IServerTransactionHandler;

            if (handler == null)
                throw new Exception($"Transaction handler creation failed: {fullname}");

            return handler;
        }

        private IServerTransactionEndpoint ResolveAndCreateEndpoint(ServerTransactionContainer container, string fullname, string parameter)
        {
            Type endpointType = ResolveType(fullname);

            if (endpointType == null)
                throw new Exception($"Endpoint Type type not found: {fullname}");

            ConstructorInfo[] ctors = endpointType.GetConstructors()
               .OrderByDescending(x => x.GetParameters().Length)
               .ToArray();

            ConstructorInfo constructor = null;
            object[] parameters = null;

            foreach (ConstructorInfo ctor in ctors)
            {
                ParameterInfo[] parameterInfos = ctor.GetParameters();
                parameters = new object[parameterInfos.Length];
                bool skip = false;

                for (int i = 0; i < parameterInfos.Length; i++)
                {
                    ParameterInfo parameterInfo = parameterInfos[i];

                    if (parameterInfo.ParameterType.IsEquivalentTo(typeof(QueueRider)))
                        parameters[i] = Rider.Queue;

                    else if (parameterInfo.ParameterType.IsEquivalentTo(typeof(DirectRider)))
                        parameters[i] = Rider.Direct;

                    else if (parameterInfo.ParameterType.IsEquivalentTo(typeof(ChannelRider)))
                        parameters[i] = Rider.Channel;

                    else if (parameterInfo.ParameterType.IsEquivalentTo(typeof(RouterRider)))
                        parameters[i] = Rider.Router;

                    else if (parameterInfo.ParameterType.IsEquivalentTo(typeof(HorseRider)))
                        parameters[i] = Rider;

                    if (parameterInfo.ParameterType.IsEquivalentTo(typeof(ServerTransactionContainer)))
                        parameters[i] = container;

                    else if (parameterInfo.ParameterType.IsEquivalentTo(typeof(string)))
                        parameters[i] = parameter;

                    else
                    {
                        skip = true;
                        break;
                    }
                }

                if (skip)
                    continue;

                constructor = ctor;
                break;
            }

            if (constructor == null)
                throw new Exception($"No suitable constructor found for endpoint: {fullname}");

            IServerTransactionEndpoint endpoint = constructor.Invoke(parameters) as IServerTransactionEndpoint;

            if (endpoint == null)
                throw new Exception($"Endpoint creation failed: {fullname}");

            return endpoint;
        }

        /// <summary>
        /// Creates new transaction container.
        /// Each transaction container represents same kind of transactions.
        /// </summary>
        /// <param name="name">Name for the transactions</param>
        /// <param name="timeout">Transaction timeout</param>
        /// <param name="commitEndpoint">Endpoint for commited transactions</param>
        /// <param name="rollbackEndpoint">Endpoint for rolled back transactions</param>
        /// <param name="timeoutEndpoint">Endpoint for timed out transactions</param>
        /// <param name="handler">Custom transaction handler implementation, if exists</param>
        /// <returns></returns>
        public ServerTransactionContainer CreateContainer(string name, TimeSpan timeout,
                                                          IServerTransactionEndpoint commitEndpoint,
                                                          IServerTransactionEndpoint rollbackEndpoint,
                                                          IServerTransactionEndpoint timeoutEndpoint,
                                                          IServerTransactionHandler handler = null)
        {
            ServerTransactionContainer container;

            lock (_containers)
            {
                if (_containers.ContainsKey(name))
                    throw new($"There is already a transaction container with name: {name}");

                container = new ServerTransactionContainer(name, timeout);

                container.CommitEndpoint = commitEndpoint;
                container.RollbackEndpoint = rollbackEndpoint;
                container.TimeoutEndpoint = timeoutEndpoint;
                container.Handler = handler ?? DefaultHandler;

                _containers.Add(name, container);
            }

            TransactionContainerData data = new TransactionContainerData();

            data.Name = name;
            data.Timeout = Convert.ToInt32(timeout.TotalMilliseconds);

            data.HandlerType = container.Handler != null ? container.Handler.GetType().FullName : null;

            data.CommitType = commitEndpoint.GetType().FullName;
            data.CommitParam = commitEndpoint.InitParameter;

            data.RollbackType = rollbackEndpoint.GetType().FullName;
            data.RollbackParam = rollbackEndpoint.InitParameter;

            data.TimeoutType = timeoutEndpoint.GetType().FullName;
            data.TimeoutParam = timeoutEndpoint.InitParameter;

            lock (_data)
                _data.Add(data);

            SaveTransactionContainers();

            return container;
        }

        /// <summary>
        /// Deletes a transaction container
        /// </summary>
        public void DeleteContainer(string name)
        {
            ServerTransactionContainer container;
            bool found;

            lock (_containers)
                found = _containers.TryGetValue(name, out container);

            if (!found)
                return;

            lock (_containers)
                _containers.Remove(name);

            lock (_data)
                _data.RemoveAll(x => x.Name == name);

            SaveTransactionContainers();

            _ = container.Handler?.Destroy(container);
        }

        /// <summary>
        /// Begins new transaction from remote
        /// </summary>
        public async Task Begin(MessagingClient client, HorseMessage message)
        {
            ServerTransactionContainer container;
            lock (_containers)
                _containers.TryGetValue(message.Target, out container);

            if (container == null)
            {
                await client.SendAsync(message.CreateResponse(HorseResultCode.NotFound));
                return;
            }

            bool created = await container.Create(client, message);
            await client.SendAsync(message.CreateResponse(created ? HorseResultCode.Ok : HorseResultCode.Failed));
        }

        /// <summary>
        /// Commits a transaction
        /// </summary>
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

        /// <summary>
        /// Rollbacks a transaction
        /// </summary>
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