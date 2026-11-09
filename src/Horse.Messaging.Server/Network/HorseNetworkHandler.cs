using System;
using System.Linq;
using System.Threading.Tasks;
using Horse.Core;
using Horse.Core.Protocols;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Cache;
using Horse.Messaging.Server.Channels;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Cluster;
using Horse.Messaging.Server.Direct;
using Horse.Messaging.Server.Events;
using Horse.Messaging.Server.Helpers;
using Horse.Messaging.Server.Logging;
using Horse.Messaging.Server.Plugins;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Routing;
using Horse.Messaging.Server.Security;
using Horse.Messaging.Server.Transactions;

namespace Horse.Messaging.Server.Network;

/// <summary>
/// Message queue server handler
/// </summary>
internal class HorseNetworkHandler : IProtocolConnectionHandler<HorseServerSocket, HorseMessage>
{
    #region Fields

    /// <summary>
    /// Messaging Queue Server
    /// </summary>
    private readonly HorseRider _rider;

    private readonly INetworkMessageHandler _serverHandler;
    private readonly INetworkMessageHandler _queueMessageHandler;
    private readonly INetworkMessageHandler _routerMessageHandler;
    private readonly INetworkMessageHandler _pullRequestHandler;
    private readonly INetworkMessageHandler _clientHandler;
    private readonly INetworkMessageHandler _responseHandler;
    private readonly INetworkMessageHandler _channelHandler;
    private readonly INetworkMessageHandler _eventHandler;
    private readonly INetworkMessageHandler _cacheHandler;
    private readonly INetworkMessageHandler _transactionHandler;
    private readonly INetworkMessageHandler _pluginHandler;

    public HorseNetworkHandler(HorseRider rider)
    {
        _rider = rider;
        _serverHandler = new ServerMessageHandler(rider);
        _queueMessageHandler = new QueueMessageHandler(rider);
        _routerMessageHandler = new RouterMessageHandler(rider);
        _pullRequestHandler = new PullRequestMessageHandler(rider);
        _clientHandler = new DirectMessageHandler(rider);
        _responseHandler = new ResponseMessageHandler(rider);
        _eventHandler = new EventMessageHandler(rider);
        _cacheHandler = new CacheNetworkHandler(rider);
        _channelHandler = new ChannelNetworkHandler(rider);
        _transactionHandler = new TransactionMessageHandler(rider);
        _pluginHandler = new PluginMessageHandler(rider);
    }

    #endregion

    #region Connection

    /// <summary>
    /// Called when a new client is connected via Horse protocol
    /// </summary>
    public async Task<HorseServerSocket> Connected(IHorseServer server, IConnectionInfo connection, ConnectionData data)
    {
        //unique id, name and type are stored trimmed, "victim " must not register next to "victim"
        data.Properties.TryGetValue(HorseHeaders.CLIENT_ID, out string clientId);
        clientId = clientId?.Trim();
        if (string.IsNullOrEmpty(clientId))
            clientId = _rider.Client.ClientIdGenerator.Create();

        //if another client with same unique id is online, do not accept new client
        MessagingClient foundClient = _rider.Client.Find(clientId);
        if (foundClient != null)
        {
            byte[] bytes = HorseProtocolWriter.Create(MessageBuilder.Busy());
            await connection.Socket.SendAsync(bytes);
            return null;
        }

        string nodeValue = data.Properties.GetStringValue(HorseHeaders.HORSE_NODE);
        bool isNode = nodeValue != null && nodeValue.Equals(HorseHeaders.YES, StringComparison.OrdinalIgnoreCase);

        if (!isNode && _rider.Options.ClientLimit > 0 && _rider.Client.GetOnlineClients() >= _rider.Options.ClientLimit)
            return null;

        //creates new mq client object 
        MessagingClient client = new MessagingClient(_rider, connection, _rider.MessageIdGenerator);
        client.Data = data;
        client.UniqueId = clientId;
        client.Token = data.Properties.GetStringValue(HorseHeaders.CLIENT_TOKEN);
        client.Name = data.Properties.GetStringValue(HorseHeaders.CLIENT_NAME);
        client.Type = data.Properties.GetStringValue(HorseHeaders.CLIENT_TYPE);

        if (string.IsNullOrEmpty(client.Name))
            client.Name = "(Unknown)";

        if (string.IsNullOrEmpty(client.Type))
            client.Type = "(Unknown)";

        if (isNode)
        {
            if (_rider.Cluster.Options.SharedSecret != client.Token)
            {
                await client.SendAsync(MessageBuilder.Unauthorized());
                return null;
            }

            NodeClient nodeClient = _rider.Cluster.Clients.FirstOrDefault(x => x.Info.Name == client.Name);
            if (nodeClient == null)
            {
                await client.SendAsync(MessageBuilder.NotFound());
                return null;
            }

            client.IsNodeClient = true;
            client.NodeClient = nodeClient;

            return client;
        }

        //authenticates client
        foreach (IClientAuthenticator authenticator in _rider.Client.Authenticators.All())
        {
            client.IsAuthenticated = await authenticator.Authenticate(_rider, client);
            if (!client.IsAuthenticated)
            {
                await client.SendAsync(MessageBuilder.Unauthorized());
                return null;
            }
        }

        if (!_rider.Cluster.CanClientConnect())
            return null;

        if (_rider.Cluster.Options.Mode == ClusterMode.Reliable && _rider.Cluster.State > NodeState.Main)
        {
            RunClientHandlers(client, true);
            return client;
        }

        //client authenticated, add it into the connected clients list.
        //another connection with the same unique id may have been added while this one was authenticated
        if (!_rider.Client.Add(client))
        {
            await client.SendAsync(MessageBuilder.Busy());
            return null;
        }

        //send response message to the client, client should check unique id,
        //if client's unique id isn't permitted, server will create new id for client and send it as response
        HorseMessage accepted = MessageBuilder.Accepted(client.UniqueId);

        if (_rider.Cluster.Options.Mode == ClusterMode.Reliable && _rider.Cluster.State <= NodeState.Main)
        {
            if (_rider.Cluster.SuccessorNode?.PublicHost != null)
                accepted.AddHeader(HorseHeaders.SUCCESSOR_NODE, _rider.Cluster.SuccessorNode.PublicHost);

            string alternate = string.Join(',', _rider.Cluster.Clients
                .Where(nc => nc.IsConnected && nc.Info.Id != _rider.Cluster.SuccessorNode?.Id)
                .Select(nc => nc.Info.PublicHost));

            if (!string.IsNullOrEmpty(alternate))
                accepted.AddHeader(HorseHeaders.REPLICA_NODE, alternate);
        }

        string underlyingProtocol = data.Properties.GetStringValue(HorseHeaders.UNDERLYING_PROTOCOL);
        if (!string.IsNullOrEmpty(underlyingProtocol))
            client.PendingMessages.Add(accepted);
        else
            await client.SendAsync(accepted);

        RunClientHandlers(client, true);
        return client;
    }

    /// <summary>
    /// Triggers client handlers without waiting.
    /// A failing handler is reported and does not affect the connection or the other handlers.
    /// </summary>
    private void RunClientHandlers(MessagingClient client, bool connected)
    {
        foreach (IClientHandler handler in _rider.Client.Handlers.All())
        {
            try
            {
                HandlerFailureObserver.Observe(_rider,
                    connected ? handler.Connected(_rider, client) : handler.Disconnected(_rider, client),
                    HorseLogEvents.ClientHandler,
                    connected ? "IClientHandler.Connected" : "IClientHandler.Disconnected");
            }
            catch (Exception e)
            {
                HandlerFailureObserver.Report(_rider, e, HorseLogEvents.ClientHandler,
                    connected ? "IClientHandler.Connected" : "IClientHandler.Disconnected");
            }
        }
    }

    /// <summary>
    /// Triggered when handshake is completed and the connection is ready to communicate 
    /// </summary>
    public async Task Ready(IHorseServer server, HorseServerSocket client)
    {
        if (client == null)
            return;

        MessagingClient mc = (MessagingClient)client;

        if (mc.IsNodeClient && mc.NodeClient != null)
        {
            mc.NodeClient.IncomingClientConnected(mc, mc.Data);
            return;
        }

        if (_rider.Cluster.Options.Mode == ClusterMode.Reliable && _rider.Cluster.State > NodeState.Main)
        {
            HorseMessage message = MessageBuilder.StatusCodeMessage(KnownContentTypes.Found, mc.UniqueId);
            if (_rider.Cluster.MainNode != null)
            {
                message.AddHeader(HorseHeaders.NODE_ID, _rider.Cluster.MainNode.Id);
                message.AddHeader(HorseHeaders.NODE_NAME, _rider.Cluster.MainNode.Name);
                message.AddHeader(HorseHeaders.NODE_PUBLIC_HOST, _rider.Cluster.MainNode.PublicHost);

                if (_rider.Cluster.SuccessorNode != null)
                    message.AddHeader(HorseHeaders.SUCCESSOR_NODE, _rider.Cluster.SuccessorNode.PublicHost);
            }

            await client.SendAsync(message);
            client.Disconnect();
        }
    }

    /// <summary>
    /// Called when connected client is connected in Horse protocol
    /// </summary>
    public Task Disconnected(IHorseServer server, HorseServerSocket client)
    {
        MessagingClient messagingClient = (MessagingClient)client;

        if (messagingClient.IsNodeClient)
            return Task.CompletedTask;

        _rider.Client.Remove(messagingClient);
        _rider.Transaction.ClientDisconnected(messagingClient);

        RunClientHandlers(messagingClient, false);

        return Task.CompletedTask;
    }

    #endregion

    #region Receive

    /// <summary>
    /// Called when a new message received from the client
    /// </summary>
    public async Task Received(IHorseServer server, IConnectionInfo info, HorseServerSocket client, HorseMessage message)
    {
        MessagingClient mc = (MessagingClient)client;
        try
        {
            PrepareReceivedMessage(mc, message);
            await RouteToHandler(mc, message).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            //runs only when a handler fails, the message path pays nothing for it
            _rider.SendError(HorseLogLevel.Error, HorseLogEvents.MessageProcess,
                $"Processing {message?.Type} message (content type {message?.ContentType}) for {message?.Target} from {mc?.UniqueId} failed, the connection is closed",
                e);

            client.Disconnect();
        }
    }

    /// <summary>
    /// Sets message id if it's missing and sets the source to the sender.
    /// Called for each received message and for each message in an envelope.
    /// </summary>
    private void PrepareReceivedMessage(MessagingClient mc, HorseMessage message)
    {
        //if client sends anonymous messages and server needs message id, generate new
        if (string.IsNullOrEmpty(message.MessageId))
        {
            //anonymous messages can't be responsed, do not wait response
            message.WaitResponse = false;
            message.SetMessageId(_rider.MessageIdGenerator.Create());
        }

        //node connections relay the messages of clients connected to other nodes, their source is kept.
        //a client can send messages only in its own name.
        if (mc.IsNodeClient)
        {
            if (string.IsNullOrEmpty(message.Source))
                message.SetSource(mc.UniqueId);
        }
        else if (message.Source != mc.UniqueId)
            message.SetSource(mc.UniqueId);
    }

    /// <summary>
    /// Answers a client message that reached a node which cannot process it.
    /// In a Reliable cluster only the main node handles client messages; Replica and Successor
    /// nodes drop them. Dropping silently makes the loss invisible to the producer, so the sender
    /// is told where the main node is and the drop is reported to the error handlers.
    /// </summary>
    private Task RejectMisdirected(MessagingClient mc, HorseMessage message)
    {
        //node to node traffic keeps the previous behaviour, cluster internals are untouched
        if (mc.IsNodeClient)
            return Task.CompletedTask;

        NodeInfo mainNode = _rider.Cluster.MainNode;

        _rider.SendError(HorseLogLevel.Error, HorseLogEvents.ClusterMisdirectedMessage,
            $"{message.Type} message is dropped, node state is {_rider.Cluster.State} and cannot process client messages. Main node: {mainNode?.PublicHost ?? "unknown"}",
            null);

        if (!message.WaitResponse || string.IsNullOrEmpty(message.MessageId))
            return Task.CompletedTask;

        HorseMessage response = message.CreateResponse(HorseResultCode.MisdirectedRequest);

        if (mainNode != null)
        {
            response.AddHeader(HorseHeaders.NODE_ID, mainNode.Id);
            response.AddHeader(HorseHeaders.NODE_NAME, mainNode.Name);
            response.AddHeader(HorseHeaders.NODE_PUBLIC_HOST, mainNode.PublicHost);
        }

        if (_rider.Cluster.SuccessorNode != null)
            response.AddHeader(HorseHeaders.SUCCESSOR_NODE, _rider.Cluster.SuccessorNode.PublicHost);

        return mc.SendAsync(response);
    }

    /// <summary>
    /// Routes message to it's type handler
    /// </summary>
    private Task RouteToHandler(MessagingClient mc, HorseMessage message)
    {
        ClusterMode clusterMode = _rider.Cluster.Options.Mode;
        bool isReplica = _rider.Cluster.Options.Mode == ClusterMode.Reliable && (_rider.Cluster.State == NodeState.Replica || _rider.Cluster.State == NodeState.Successor);

        switch (message.Type)
        {
            case MessageType.Envelope:
                return OpenEnvelope(mc, message);

            case MessageType.Channel:
                //only publishes are relayed to other nodes, each node delivers them to its own subscribers.
                //Relaying subscribe/unsubscribe made the node connection a subscriber on remote nodes,
                //which was never cleaned up when the client disconnected and filled their subscriber slots.
                //Create, update and remove are synced by ChannelClusterNotifier.
                if (!mc.IsNodeClient && clusterMode == ClusterMode.Scaled && message.ContentType == KnownContentTypes.ChannelPush)
                    _rider.Cluster.ProcessMessageFromClient(mc, message);

                return isReplica
                    ? RejectMisdirected(mc, message)
                    : _channelHandler.Handle(mc, message, mc.IsNodeClient);

            case MessageType.QueueMessage:
                return isReplica
                    ? RejectMisdirected(mc, message)
                    : _queueMessageHandler.Handle(mc, message, mc.IsNodeClient);

            case MessageType.Router:
                return isReplica
                    ? RejectMisdirected(mc, message)
                    : _routerMessageHandler.Handle(mc, message, mc.IsNodeClient);

            case MessageType.Cache:
                //cache changes reach the other nodes through CacheClusterNotifier (set, increment, remove, purge).
                //Relaying the client message too applied every set twice on each node, re-broadcast removes
                //and purges from every node, made remote nodes count increments again and ran each read on all nodes.
                return _cacheHandler.Handle(mc, message, mc.IsNodeClient);

            case MessageType.Transaction:
                return isReplica
                    ? RejectMisdirected(mc, message)
                    : _transactionHandler.Handle(mc, message, false);

            case MessageType.QueuePullRequest:
                return isReplica
                    ? RejectMisdirected(mc, message)
                    : _pullRequestHandler.Handle(mc, message, mc.IsNodeClient);

            case MessageType.DirectMessage:
                if (!mc.IsNodeClient && clusterMode == ClusterMode.Scaled)
                    _rider.Cluster.ProcessMessageFromClient(mc, message);

                return isReplica
                    ? RejectMisdirected(mc, message)
                    : _clientHandler.Handle(mc, message, mc.IsNodeClient);

            case MessageType.Response:
                if (!mc.IsNodeClient && clusterMode == ClusterMode.Scaled)
                    _rider.Cluster.ProcessMessageFromClient(mc, message);

                if (clusterMode == ClusterMode.Reliable && mc.IsNodeClient && mc.NodeClient != null)
                    mc.NodeClient.ProcessReceivedMessage(mc, message);

                return isReplica
                    ? Task.CompletedTask
                    : _responseHandler.Handle(mc, message, mc.IsNodeClient);

            case MessageType.Plugin:
                return _pluginHandler.Handle(mc, message, mc.IsNodeClient);

            case MessageType.Server:
                return _serverHandler.Handle(mc, message, mc.IsNodeClient);

            case MessageType.Cluster:
                if (mc.IsNodeClient && mc.NodeClient != null)
                    mc.NodeClient.ProcessReceivedMessage(mc, message);

                return Task.CompletedTask;

            case MessageType.Event:
                return _eventHandler.Handle(mc, message, mc.IsNodeClient);

            case MessageType.Ping:
                return mc.SendRawAsync(PredefinedMessages.PONG);

            case MessageType.Pong:
                mc.KeepAlive();
                break;

            case MessageType.Terminate:
                mc.Disconnect();
                break;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Processes the messages in the envelope one by one, in order, like messages sent one by one on the connection.
    /// They used to run concurrently unless the Wait-Messages header said otherwise (the client never sends it):
    /// they interleaved with each other and with the next messages of the connection, and their exceptions were lost.
    /// The acknowledge is sent after all messages are processed.
    /// An envelope in an envelope is rejected and the connection is closed, see the check below.
    /// </summary>
    private async Task OpenEnvelope(MessagingClient mc, HorseMessage message)
    {
        if (message.Content != null)
        {
            HorseProtocolReader reader = new HorseProtocolReader();
            message.Content.Position = 0;

            while (message.Content.Position < message.Content.Length)
            {
                HorseMessage item = await reader.Read(message.Content);

                if (item == null)
                    break;

                //inner messages are read from memory and complete synchronously, a nested envelope recursed on the same stack:
                //a client could crash the server process with a stack overflow (about 5000 levels, a 60 KB message).
                //The client never nests envelopes. Throwing here closes the connection like any failed message.
                if (item.Type == MessageType.Envelope)
                    throw new InvalidOperationException("Nested envelopes are not supported");

                //messages in the envelope are read from the client's bytes, the source is checked like the envelope's
                PrepareReceivedMessage(mc, item);

                await RouteToHandler(mc, item).ConfigureAwait(false);
            }
        }

        await mc.SendAsync(message.CreateAcknowledge());
    }

    #endregion
}