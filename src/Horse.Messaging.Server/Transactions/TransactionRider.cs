using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Channels;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Direct;
using Horse.Messaging.Server.Helpers;
using Horse.Messaging.Server.Logging;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Routing;

namespace Horse.Messaging.Server.Transactions;

/// <summary>
/// Manages remote transactions in messaging server
/// </summary>
public class TransactionRider
{
    private const string DataFilename = "transactions.json";
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan MaxTimeout = TimeSpan.FromMilliseconds(int.MaxValue);

    /// <summary>
    /// Maximum time to wait for rolling back open transactions on shutdown
    /// </summary>
    public static readonly TimeSpan DefaultShutdownTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Root horse rider object
    /// </summary>
    public HorseRider Rider { get; }

    /// <summary>
    /// Default transaction handler for containers without their own handler.
    /// It is resolved when it is needed, so it can be set at any time.
    /// It is not saved into transactions.json.
    /// </summary>
    public IServerTransactionHandler DefaultHandler { get; set; }

    /// <summary>
    /// True after shutdown has started. New transaction requests are rejected with Busy.
    /// </summary>
    public bool IsShuttingDown => _shuttingDown;

    private readonly ConcurrentDictionary<string, ServerTransactionContainer> _containers = new();
    private readonly ConcurrentDictionary<MessagingClient, ConcurrentDictionary<ServerTransaction, byte>> _clientTransactions = new();
    private int _completions;

    private readonly object _containerLock = new();
    private readonly object _dataLock = new();
    private readonly List<TransactionContainerData> _data = new();

    private Timer _sweeper;
    private int _sweeping;
    private volatile bool _shuttingDown;

    /// <summary>
    /// Creates new transaction rider
    /// </summary>
    public TransactionRider(HorseRider rider)
    {
        Rider = rider;
    }

    #region Containers

    /// <summary>
    /// Finds a transaction container by name
    /// </summary>
    public ServerTransactionContainer Find(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        _containers.TryGetValue(name, out ServerTransactionContainer container);
        return container;
    }

    /// <summary>
    /// Returns all transaction containers
    /// </summary>
    public ServerTransactionContainer[] GetContainers()
    {
        return _containers.Values.ToArray();
    }

    /// <summary>
    /// Creates new transaction container and saves it into transactions.json.
    /// Each transaction container represents same kind of transactions.
    /// </summary>
    /// <param name="name">Name for the transactions</param>
    /// <param name="timeout">Transaction timeout. Must be greater than zero.</param>
    /// <param name="commitEndpoint">Endpoint for committed transactions</param>
    /// <param name="rollbackEndpoint">Endpoint for rolled back transactions</param>
    /// <param name="timeoutEndpoint">Endpoint for timed out transactions. If null, timed out transactions are sent to the rollback endpoint.</param>
    /// <param name="handler">Custom transaction handler for the container. If null, DefaultHandler is used.</param>
    /// <exception cref="DuplicateNameException">Thrown when there is already a container with same name</exception>
    public ServerTransactionContainer CreateContainer(string name, TimeSpan timeout,
        IServerTransactionEndpoint commitEndpoint,
        IServerTransactionEndpoint rollbackEndpoint,
        IServerTransactionEndpoint timeoutEndpoint = null,
        IServerTransactionHandler handler = null)
    {
        return CreateOrUpdate(name, timeout, commitEndpoint, rollbackEndpoint, timeoutEndpoint, handler, false);
    }

    /// <summary>
    /// Creates new transaction container or updates the existing one with the given definition,
    /// and saves it into transactions.json.
    /// Use this method to define containers on each application start:
    /// a container loaded from transactions.json is updated instead of throwing an error.
    /// Open transactions of an updated container keep their deadline and use the new endpoints when they finish.
    /// </summary>
    /// <param name="name">Name for the transactions</param>
    /// <param name="timeout">Transaction timeout. Must be greater than zero.</param>
    /// <param name="commitEndpoint">Endpoint for committed transactions</param>
    /// <param name="rollbackEndpoint">Endpoint for rolled back transactions</param>
    /// <param name="timeoutEndpoint">Endpoint for timed out transactions. If null, timed out transactions are sent to the rollback endpoint.</param>
    /// <param name="handler">Custom transaction handler for the container. If null, DefaultHandler is used.</param>
    public ServerTransactionContainer CreateOrUpdateContainer(string name, TimeSpan timeout,
        IServerTransactionEndpoint commitEndpoint,
        IServerTransactionEndpoint rollbackEndpoint,
        IServerTransactionEndpoint timeoutEndpoint = null,
        IServerTransactionHandler handler = null)
    {
        return CreateOrUpdate(name, timeout, commitEndpoint, rollbackEndpoint, timeoutEndpoint, handler, true);
    }

    private ServerTransactionContainer CreateOrUpdate(string name, TimeSpan timeout,
        IServerTransactionEndpoint commitEndpoint,
        IServerTransactionEndpoint rollbackEndpoint,
        IServerTransactionEndpoint timeoutEndpoint,
        IServerTransactionHandler handler,
        bool updateIfExists)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Transaction container name is required", nameof(name));

        ValidateTimeout(timeout);
        ArgumentNullException.ThrowIfNull(commitEndpoint);
        ArgumentNullException.ThrowIfNull(rollbackEndpoint);

        ServerTransactionContainer container;

        lock (_containerLock)
        {
            if (_containers.TryGetValue(name, out container))
            {
                if (!updateIfExists)
                    throw new DuplicateNameException($"There is already a transaction container with name: {name}");
            }
            else
            {
                container = new ServerTransactionContainer(this, name, timeout);
                _containers[name] = container;
            }

            container.Timeout = timeout;
            container.CommitEndpoint = commitEndpoint;
            container.RollbackEndpoint = rollbackEndpoint;
            container.TimeoutEndpoint = timeoutEndpoint;
            container.Handler = handler;

            SetData(CreateData(container));
        }

        SaveTransactionContainers();
        StartSweeper();

        return container;
    }

    /// <summary>
    /// Deletes a transaction container.
    /// Its open transactions are rolled back, then the handler's Destroy method is called.
    /// Returns false if there is no such container.
    /// </summary>
    public async Task<bool> DeleteContainer(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        ServerTransactionContainer container;

        lock (_containerLock)
        {
            if (!_containers.TryRemove(name, out container))
                return false;

            container.MarkDestroyed();

            lock (_dataLock)
                _data.RemoveAll(x => x.Name == name);
        }

        SaveTransactionContainers();

        await container.RollbackAll();

        IServerTransactionHandler handler = container.Handler ?? DefaultHandler;
        if (handler != null)
        {
            try
            {
                await handler.Destroy(container);
            }
            catch (Exception e)
            {
                Rider.SendError(HorseLogLevel.Error, HorseLogEvents.TransactionHandler, $"Transaction handler Destroy failed for {name}", e);
            }
        }

        return true;
    }

    private static void ValidateTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero || timeout > MaxTimeout)
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, $"Transaction timeout must be greater than zero and less than {MaxTimeout}");
    }

    #endregion

    #region Persistence

    /// <summary>
    /// Loads saved containers from transactions.json.
    /// A container that cannot be loaded is logged and skipped. It stays in the file,
    /// so it is loaded on next start when the problem is fixed or replaced when it is defined again.
    /// </summary>
    internal void Initialize()
    {
        string filename = GetDataFilename();

        if (!File.Exists(filename))
            return;

        List<TransactionContainerData> items;
        try
        {
            string fileContent = File.ReadAllText(filename);
            items = JsonSerializer.Deserialize<List<TransactionContainerData>>(fileContent, SerializerFactory.Default());
        }
        catch (Exception e)
        {
            Rider.SendError(HorseLogLevel.Error, HorseLogEvents.LoadTransactionContainer, $"Transaction containers could not be read from {filename}", e);
            return;
        }

        if (items == null)
            return;

        foreach (TransactionContainerData item in items)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Name))
                continue;

            SetData(item);

            try
            {
                ServerTransactionContainer container = RestoreContainer(item);
                lock (_containerLock)
                    _containers[item.Name] = container;
            }
            catch (Exception e)
            {
                Exception inner = e is TargetInvocationException { InnerException: not null } ? e.InnerException : e;
                Rider.SendError(HorseLogLevel.Error, HorseLogEvents.LoadTransactionContainer, $"Transaction container could not be loaded: {item.Name}", inner);
            }
        }

        if (!_containers.IsEmpty)
            StartSweeper();
    }

    private ServerTransactionContainer RestoreContainer(TransactionContainerData item)
    {
        TimeSpan timeout = TimeSpan.FromMilliseconds(item.Timeout);
        ValidateTimeout(timeout);

        ServerTransactionContainer container = new ServerTransactionContainer(this, item.Name, timeout);

        container.CommitEndpoint = CreateEndpoint(container, item.CommitType, item.CommitParam);
        container.RollbackEndpoint = CreateEndpoint(container, item.RollbackType, item.RollbackParam);
        container.TimeoutEndpoint = string.IsNullOrEmpty(item.TimeoutType) ? null : CreateEndpoint(container, item.TimeoutType, item.TimeoutParam);
        container.Handler = string.IsNullOrEmpty(item.HandlerType) ? null : CreateHandler(item.HandlerType);

        return container;
    }

    private static TransactionContainerData CreateData(ServerTransactionContainer container)
    {
        return new TransactionContainerData
        {
            Name = container.Name,
            Timeout = Convert.ToInt32(container.Timeout.TotalMilliseconds),
            HandlerType = container.Handler != null ? GetTypeName(container.Handler.GetType()) : null,
            CommitType = GetTypeName(container.CommitEndpoint.GetType()),
            CommitParam = container.CommitEndpoint.InitParameter,
            RollbackType = GetTypeName(container.RollbackEndpoint.GetType()),
            RollbackParam = container.RollbackEndpoint.InitParameter,
            TimeoutType = container.TimeoutEndpoint != null ? GetTypeName(container.TimeoutEndpoint.GetType()) : null,
            TimeoutParam = container.TimeoutEndpoint?.InitParameter
        };
    }

    private void SetData(TransactionContainerData data)
    {
        lock (_dataLock)
        {
            _data.RemoveAll(x => x.Name == data.Name);
            _data.Add(data);
        }
    }

    private string GetDataFilename()
    {
        string path = string.IsNullOrEmpty(Rider.Options.DataPath) ? "data" : Rider.Options.DataPath;
        return Path.Combine(path, DataFilename);
    }

    private void SaveTransactionContainers()
    {
        lock (_dataLock)
        {
            try
            {
                string filename = GetDataFilename();
                string directory = Path.GetDirectoryName(filename);

                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                string fileContent = JsonSerializer.Serialize(_data, SerializerFactory.Default());
                File.WriteAllText(filename, fileContent);
            }
            catch (Exception e)
            {
                Rider.SendError(HorseLogLevel.Error, HorseLogEvents.SaveTransactionContainer, "TransactionRider.SaveTransactionContainers", e);
            }
        }
    }

    /// <summary>
    /// Type names are saved with their assembly name (without version),
    /// so types in application assemblies can be loaded after restart and version upgrades.
    /// </summary>
    private static string GetTypeName(Type type)
    {
        return $"{type.FullName}, {type.Assembly.GetName().Name}";
    }

    private static Type ResolveType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return null;

        Type type = Type.GetType(typeName, false);
        if (type != null)
            return type;

        //type name without assembly name or the assembly could not be loaded by name
        string fullname = GetFullname(typeName);
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = assembly.GetType(fullname, false);
            if (type != null)
                return type;
        }

        return null;
    }

    private static string GetFullname(string typeName)
    {
        int depth = 0;
        for (int i = 0; i < typeName.Length; i++)
        {
            char c = typeName[i];
            if (c == '[')
                depth++;
            else if (c == ']')
                depth--;
            else if (c == ',' && depth == 0)
                return typeName[..i].Trim();
        }

        return typeName.Trim();
    }

    private static IServerTransactionHandler CreateHandler(string typeName)
    {
        Type handlerType = ResolveType(typeName);

        if (handlerType == null)
            throw new TypeLoadException($"Transaction handler type not found: {typeName}");

        if (Activator.CreateInstance(handlerType) is not IServerTransactionHandler handler)
            throw new InvalidOperationException($"Transaction handler type does not implement {nameof(IServerTransactionHandler)}: {typeName}");

        return handler;
    }

    private IServerTransactionEndpoint CreateEndpoint(ServerTransactionContainer container, string typeName, string parameter)
    {
        Type endpointType = ResolveType(typeName);

        if (endpointType == null)
            throw new TypeLoadException($"Transaction endpoint type not found: {typeName}");

        ConstructorInfo[] ctors = endpointType.GetConstructors()
            .OrderByDescending(x => x.GetParameters().Length)
            .ToArray();

        foreach (ConstructorInfo ctor in ctors)
        {
            object[] parameters = ResolveParameters(ctor, container, parameter);
            if (parameters == null)
                continue;

            if (ctor.Invoke(parameters) is not IServerTransactionEndpoint endpoint)
                throw new InvalidOperationException($"Transaction endpoint type does not implement {nameof(IServerTransactionEndpoint)}: {typeName}");

            return endpoint;
        }

        throw new MissingMethodException($"No suitable constructor found for transaction endpoint: {typeName}");
    }

    private object[] ResolveParameters(ConstructorInfo ctor, ServerTransactionContainer container, string parameter)
    {
        ParameterInfo[] parameterInfos = ctor.GetParameters();
        object[] parameters = new object[parameterInfos.Length];

        for (int i = 0; i < parameterInfos.Length; i++)
        {
            Type type = parameterInfos[i].ParameterType;

            if (type == typeof(QueueRider))
                parameters[i] = Rider.Queue;
            else if (type == typeof(DirectRider))
                parameters[i] = Rider.Direct;
            else if (type == typeof(ChannelRider))
                parameters[i] = Rider.Channel;
            else if (type == typeof(RouterRider))
                parameters[i] = Rider.Router;
            else if (type == typeof(HorseRider))
                parameters[i] = Rider;
            else if (type == typeof(ServerTransactionContainer))
                parameters[i] = container;
            else if (type == typeof(string))
                parameters[i] = parameter;
            else
                return null;
        }

        return parameters;
    }

    #endregion

    #region Client Requests

    /// <summary>
    /// Begins new transaction from a client request
    /// </summary>
    internal Task<HorseResultCode> Begin(MessagingClient client, HorseMessage message)
    {
        if (_shuttingDown)
            return Task.FromResult(HorseResultCode.Busy);

        string id = message.FindHeader(HorseHeaders.TRANSACTION_ID);
        if (string.IsNullOrEmpty(id))
            return Task.FromResult(HorseResultCode.BadRequest);

        ServerTransactionContainer container = Find(message.Target);
        if (container == null)
            return Task.FromResult(HorseResultCode.NotFound);

        return container.Begin(client, message, id);
    }

    /// <summary>
    /// Commits or rolls back a transaction from a client request
    /// </summary>
    internal Task<HorseResultCode> Finish(MessagingClient client, HorseMessage message, ServerTransactionStatus operation)
    {
        if (_shuttingDown)
            return Task.FromResult(HorseResultCode.Busy);

        string id = message.FindHeader(HorseHeaders.TRANSACTION_ID);
        if (string.IsNullOrEmpty(id))
            return Task.FromResult(HorseResultCode.BadRequest);

        ServerTransactionContainer container = Find(message.Target);
        if (container == null)
            return Task.FromResult(HorseResultCode.NotFound);

        return container.Finish(client, id, operation);
    }

    #endregion

    #region Client Tracking

    internal void TrackClientTransaction(ServerTransaction transaction)
    {
        MessagingClient client = transaction.Client;
        if (client == null)
            return;

        ConcurrentDictionary<ServerTransaction, byte> transactions = _clientTransactions.GetOrAdd(client, _ => new ConcurrentDictionary<ServerTransaction, byte>());
        transactions[transaction] = 0;

        //the client is disconnected while the transaction was being created
        if (!client.IsConnected)
            ClientDisconnected(client);
    }

    internal void UntrackClientTransaction(ServerTransaction transaction)
    {
        MessagingClient client = transaction.Client;
        if (client == null)
            return;

        if (_clientTransactions.TryGetValue(client, out ConcurrentDictionary<ServerTransaction, byte> transactions))
            transactions.TryRemove(transaction, out _);
    }

    /// <summary>
    /// Rolls back all open transactions of the disconnected client in background
    /// </summary>
    internal void ClientDisconnected(MessagingClient client)
    {
        if (!_clientTransactions.TryRemove(client, out ConcurrentDictionary<ServerTransaction, byte> transactions) || transactions.IsEmpty)
            return;

        ServerTransaction[] list = transactions.Keys.ToArray();
        _ = Task.Run(() => Task.WhenAll(list.Select(x => x.Container.FinishByServer(x, ServerTransactionStatus.Rollback))));
    }

    #endregion

    #region Timeout

    private void StartSweeper()
    {
        if (_sweeper != null || _shuttingDown)
            return;

        lock (_containerLock)
        {
            if (_sweeper != null || _shuttingDown)
                return;

            _sweeper = new Timer(_ => Sweep(), null, SweepInterval, SweepInterval);
        }
    }

    private void Sweep()
    {
        if (Interlocked.Exchange(ref _sweeping, 1) == 1)
            return;

        try
        {
            DateTime now = DateTime.UtcNow;
            foreach (ServerTransactionContainer container in _containers.Values)
                container.CheckTimeouts(now);
        }
        catch (Exception e)
        {
            Rider.SendError(HorseLogLevel.Error, HorseLogEvents.TransactionFinish, "Transaction timeout check failed", e);
        }
        finally
        {
            Volatile.Write(ref _sweeping, 0);
        }
    }

    #endregion

    #region Shutdown

    /// <summary>
    /// Called before a transaction is claimed for completion.
    /// The counter is increased before the claim, so shutdown cannot miss a claimed transaction
    /// whoever claims it (client request, disconnect, timeout, shutdown).
    /// </summary>
    internal void BeginCompletion()
    {
        Interlocked.Increment(ref _completions);
    }

    /// <summary>
    /// Called when the claim fails or the claimed transaction is completed
    /// </summary>
    internal void EndCompletion()
    {
        Interlocked.Decrement(ref _completions);
    }

    /// <summary>
    /// Waits until there is no transaction completion in progress
    /// </summary>
    private async Task WaitForCompletions()
    {
        while (Volatile.Read(ref _completions) > 0)
            await Task.Delay(10);
    }

    /// <summary>
    /// Stops accepting transaction requests (they are rejected with Busy) and stops timeout checks.
    /// Open transactions stay open until they are rolled back by ShutdownAsync.
    /// </summary>
    internal void StopAcceptingRequests()
    {
        _shuttingDown = true;

        lock (_containerLock)
        {
            _sweeper?.Dispose();
            _sweeper = null;
        }
    }

    /// <summary>
    /// Shuts down the transaction rider with the default timeout (15 seconds):
    /// new transaction requests are rejected with Busy and all open transactions are rolled back.
    /// On process exit and termination signals it is called during queue shutdown, between two queue flushes.
    /// Call it before stopping the server when the server is stopped by code.
    /// Returns false if the rollbacks did not finish in time.
    /// </summary>
    public Task<bool> ShutdownAsync()
    {
        return ShutdownAsync(DefaultShutdownTimeout);
    }

    /// <summary>
    /// Shuts down the transaction rider:
    /// new transaction requests are rejected with Busy and all open transactions are rolled back.
    /// Waits for the rollbacks at most for the timeout, so a slow or hanging endpoint cannot block shutdown.
    /// Rollbacks that do not finish in time are logged; they may still complete in background.
    /// Returns false if the rollbacks did not finish in time.
    /// </summary>
    public async Task<bool> ShutdownAsync(TimeSpan timeout)
    {
        StopAcceptingRequests();

        //rollbacks are started first: each one claims its transaction synchronously,
        //so the completion counter already includes them when waiting starts
        ServerTransactionContainer[] containers = _containers.Values.ToArray();
        Task rollbacks = Task.WhenAll(containers.Select(x => x.RollbackAll()));
        Task all = Task.WhenAll(rollbacks, WaitForCompletions());

        Task completed = await Task.WhenAny(all, Task.Delay(timeout));
        if (completed == all)
            return true;

        Rider.SendError(HorseLogLevel.Error, HorseLogEvents.TransactionFinish,
            $"Transaction rollbacks did not finish in {timeout.TotalSeconds} seconds on shutdown", null);

        return false;
    }

    #endregion
}
