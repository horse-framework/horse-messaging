using System;
using System.Collections.Generic;
using System.Threading;
using Twino.Server.WebSockets;

namespace Twino.Server
{
    /// <summary>
    /// Manages ping and pong messages for connected websocket clients
    /// </summary>
    internal class Pinger
    {
        /// <summary>
        /// Connected websocket clients
        /// </summary>
        private readonly List<SocketPingInfo> _clients = new List<SocketPingInfo>();

        /// <summary>
        /// Newly connected websocket clients
        /// </summary>
        private readonly List<SocketPingInfo> _incoming = new List<SocketPingInfo>();

        /// <summary>
        /// Disconnected clients that they are not removed from the container yet
        /// </summary>
        private readonly List<ServerSocket> _outgoing = new List<ServerSocket>();

        /// <summary>
        /// Pinger timer
        /// </summary>
        private Timer _timer;

        private static readonly int TIMER_INTERVAL = 5000;

        /// <summary>
        /// Ping time interval
        /// </summary>
        private readonly TimeSpan _interval;

        private readonly TwinoServer _server;

        public Pinger(TwinoServer server, TimeSpan interval)
        {
            _server = server;
            _interval = interval;
        }

        /// <summary>
        /// Starts to ping connected clients
        /// </summary>
        public void Start()
        {
            _timer = new Timer(state => Tick(), null, TIMER_INTERVAL, TIMER_INTERVAL);
        }

        /// <summary>
        /// Stops ping / pong operation and releases all resources
        /// </summary>
        public void Stop()
        {
            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }

            _clients.Clear();

            lock (_incoming)
                _incoming.Clear();

            lock (_outgoing)
                _outgoing.Clear();
        }

        /// <summary>
        /// Add new client to pinger
        /// </summary>
        public void AddClient(ServerSocket socket)
        {
            lock (_incoming)
                _incoming.Add(new SocketPingInfo(socket));
        }

        /// <summary>
        /// Remove a client from pinger
        /// </summary>
        public void RemoveClient(ServerSocket socket)
        {
            lock (_outgoing)
                _outgoing.Add(socket);
        }

        /// <summary>
        /// Adds newly connected clients, ping connected clients and removes disconnected clients
        /// </summary>
        private void Tick()
        {
            try
            {
                AddIncomingSockets();
                PingClients();
                RemoveOutgoingSockets();
            }
            catch (Exception ex)
            {
                if (_server.Logger != null)
                    _server.Logger.LogException("PINGER_TICK", ex);
            }
        }

        /// <summary>
        /// Adds newly connected clients to client container
        /// </summary>
        private void AddIncomingSockets()
        {
            lock (_incoming)
            {
                if (_incoming.Count == 0)
                    return;

                foreach (SocketPingInfo info in _incoming)
                    _clients.Add(info);

                _incoming.Clear();
            }
        }

        /// <summary>
        /// Sends ping message to connected clients.
        /// If clients are disconnected and did not respose previous ping message, they are moved to removing list.
        /// </summary>
        private void PingClients()
        {
            foreach (SocketPingInfo info in _clients)
            {
                if (info.Socket == null || !info.Socket.IsConnected)
                {
                    RemoveClient(info.Socket);
                    continue;
                }

                if (!info.New && info.Socket.PongTime < info.Last)
                {
                    info.Socket.Disconnect();
                    continue;
                }

                if (info.Last + _interval > DateTime.UtcNow)
                    continue;

                info.Ping();
            }
        }

        /// <summary>
        /// Removes clients which are added removing list.
        /// </summary>
        private void RemoveOutgoingSockets()
        {
            lock (_outgoing)
            {
                if (_outgoing.Count == 0)
                    return;

                _clients.RemoveAll(x => _outgoing.Contains(x.Socket));
                _outgoing.Clear();
            }
        }
    }
}