using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Horse.Core;
using Horse.Core.Protocols;

namespace Horse.Messaging.Protocol;

/// <summary>
/// Horse protocol class for Horse Protocol
/// </summary>
public class HorseProtocol : IHorseProtocol
{
    /// <summary>
    /// Protocol name: horse
    /// </summary>
    public string Name => "horse";

    /// <summary>
    /// Protocol connection handler
    /// </summary>
    private readonly IProtocolConnectionHandler<HorseServerSocket, HorseMessage> _handler;

    /// <summary>
    /// Server object
    /// </summary>
    private readonly IHorseServer _server;

    /// <summary>
    /// Creates new Horse Protocol handler
    /// </summary>
    public HorseProtocol(IHorseServer server, IProtocolConnectionHandler<HorseServerSocket, HorseMessage> handler)
    {
        _server = server;
        _handler = handler;
    }

    /// <summary>
    /// Returns Horse Protocol Message Handler
    /// </summary>
    public IProtocolConnectionHandler<HorseServerSocket, HorseMessage> GetHandler() => _handler;

    /// <summary>
    /// Checks if received data is a Horse protocol message
    /// </summary>
    public async Task<ProtocolHandshakeResult> Handshake(IConnectionInfo info, byte[] data)
    {
        ProtocolHandshakeResult result = new ProtocolHandshakeResult();

        if (data.Length < 8)
            return await Task.FromResult(result);

        ProtocolVersion version = CheckProtocol(data);
        result.Accepted = version != ProtocolVersion.Unknown;
        if (!result.Accepted)
            return result;

        HorseProtocolReader reader = new HorseProtocolReader();
        HorseMessage message = await reader.Read(info.GetStream());

        //sends protocol message
        await info.GetStream().WriteAsync(PredefinedMessages.PROTOCOL_BYTES_V4);

        bool alive = await ProcessFirstMessage(message, info, result);
        if (!alive)
            return result;

        result.PipeConnection = true;
        info.State = ConnectionStates.Pipe;
        info.Protocol = this;

        return result;
    }

    /// <summary>
    /// Reads first Hello message from client
    /// </summary>
    private async Task<bool> ProcessFirstMessage(HorseMessage message, IConnectionInfo info, ProtocolHandshakeResult handshakeResult)
    {
        if (message.Type != MessageType.Server || message.ContentType != KnownContentTypes.Hello)
            return false;

        ConnectionData connectionData = new ConnectionData();
        message.Content.Position = 0;
        await connectionData.ReadFromStream(message.Content);

        HorseServerSocket socket = await _handler.Connected(_server, info, connectionData);
        if (socket == null)
        {
            info.Close();
            return false;
        }

        info.State = ConnectionStates.Pipe;
        handshakeResult.Socket = socket;
        _server.HeartbeatManager?.Add(socket);

        socket.SetCleanupAction(s =>
        {
            _server.HeartbeatManager?.Remove(socket);
            _handler.Disconnected(_server, s);
        });

        return true;
    }

    /// <summary>
    /// Switching protocols to Horse is not supported
    /// </summary>
    public Task<ProtocolHandshakeResult> SwitchTo(IConnectionInfo info, ConnectionData data)
    {
        throw new NotSupportedException();
    }

    /// <summary>
    /// Handles the connection and reads received Horse messages
    /// </summary>
    public async Task HandleConnection(IConnectionInfo info, ProtocolHandshakeResult handshakeResult)
    {
        //if user makes a mistake in ready method, we should not interrupt connection handling
        try
        {
            await _handler.Ready(_server, (HorseServerSocket)handshakeResult.Socket);
        }
        catch (Exception e)
        {
            // ignore
        }

        HorseServerSocket socket = (HorseServerSocket)handshakeResult.Socket;
        HorseProtocolReader reader = new HorseProtocolReader();
        OrderedMessageProcessor processor = new OrderedMessageProcessor(this, info, socket);

        while (info.Client != null && info.Client.Connected)
        {
            HorseMessage message = await reader.Read(info.GetStream());
            if (message == null)
            {
                info.Close();
                return;
            }

            //the connection is alive when its messages are read, even if processing of a previous message waits.
            //It was updated after the previous message was processed: a message waiting for a queue lock kept
            //the PONGs of the connection unread and the heartbeat disconnected a healthy client.
            if (socket.SmartHealthCheck || message.Type == MessageType.Pong)
                socket.KeepAlive();

            await processor.Enqueue(message);
        }
    }

    private async Task ProcessMessage(IConnectionInfo info, HorseMessage message, HorseServerSocket socket)
    {
        //if user makes a mistake in received method, we should not interrupt connection handling
        try
        {
            await _handler.Received(_server, info, socket, message);
        }
        catch (Exception)
        {
            // ignore
        }
    }

    /// <summary>
    /// Processes the messages of a connection one by one, in the order they are read.
    /// Reading the connection does not wait for processing: while a message is processed (a push waiting
    /// for a queue lock, for example), next messages are read and kept in order, so PING and PONG messages
    /// of the connection are still read. When nothing is processed, a message is processed on the reading
    /// task, like before.
    /// When <see cref="MaxPendingMessages"/> messages are waiting, reading waits until one is processed.
    /// </summary>
    private sealed class OrderedMessageProcessor
    {
        /// <summary>
        /// Maximum count of read messages waiting for processing before reading waits
        /// </summary>
        private const int MaxPendingMessages = 1024;

        private readonly HorseProtocol _protocol;
        private readonly IConnectionInfo _info;
        private readonly HorseServerSocket _socket;
        private readonly Queue<HorseMessage> _pending = new();
        private bool _processing;
        private TaskCompletionSource _space;

        public OrderedMessageProcessor(HorseProtocol protocol, IConnectionInfo info, HorseServerSocket socket)
        {
            _protocol = protocol;
            _info = info;
            _socket = socket;
        }

        /// <summary>
        /// Processes the message or keeps it to process after the messages before it.
        /// Returns a task that completes when the next message can be read.
        /// </summary>
        public Task Enqueue(HorseMessage message)
        {
            lock (_pending)
            {
                if (_processing)
                {
                    _pending.Enqueue(message);
                    if (_pending.Count < MaxPendingMessages)
                        return Task.CompletedTask;

                    _space = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    return _space.Task;
                }

                _processing = true;
            }

            _ = Process(message);
            return Task.CompletedTask;
        }

        private async Task Process(HorseMessage message)
        {
            while (true)
            {
                await _protocol.ProcessMessage(_info, message, _socket);

                bool done;
                TaskCompletionSource space = null;
                lock (_pending)
                {
                    //messages of a closed connection are not processed, as before:
                    //reading stopped when the connection was closed.
                    //A subscription of a disconnected client would never be removed.
                    if (!_socket.IsConnected)
                        _pending.Clear();

                    done = _pending.Count == 0;
                    if (done)
                        _processing = false;
                    else
                        message = _pending.Dequeue();

                    if (_space != null && _pending.Count < MaxPendingMessages)
                    {
                        space = _space;
                        _space = null;
                    }
                }

                space?.TrySetResult();

                if (done)
                    return;
            }
        }
    }

    /// <summary>
    /// Checks data if Horse protocol data
    /// </summary>
    private static ProtocolVersion CheckProtocol(byte[] data)
    {
        ReadOnlySpan<byte> span = data;
        bool v2 = span.StartsWith(PredefinedMessages.PROTOCOL_BYTES_V4);
        if (v2)
            return ProtocolVersion.Version2;

        return ProtocolVersion.Unknown;
    }
}