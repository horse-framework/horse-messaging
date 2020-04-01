using System;
using System.Collections.Generic;
using System.Threading;
using Twino.Core;

namespace Twino.Server
{
    /// <summary>
    /// Manages ping and pong messages for connected piped clients
    /// </summary>
    internal class Pinger : IPinger
    {
        /// <summary>
        /// Connected piped clients
        /// </summary>
        private readonly List<SocketBase> _clients = new List<SocketBase>();

        /// <summary>
        /// Newly connected piped clients
        /// </summary>
        private readonly List<SocketBase> _incoming = new List<SocketBase>();

        /// <summary>
        /// Disconnected clients that they are not removed from the container yet
        /// </summary>
        private readonly List<SocketBase> _outgoing = new List<SocketBase>();

        /// <summary>
        /// Pinger timer
        /// </summary>
        private ThreadTimer _timer;

        private static readonly TimeSpan TIMER_INTERVAL = TimeSpan.FromMilliseconds(5000);

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
            _timer = new ThreadTimer(Tick, TIMER_INTERVAL);
            _timer.Start(ThreadPriority.Lowest);
        }

        /// <summary>
        /// Stops ping / pong operation and releases all resources
        /// </summary>
        public void Stop()
        {
            if (_timer != null)
            {
                _timer.Stop();
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
        public void Add(SocketBase socket)
        {
            lock (_incoming)
                _incoming.Add(socket);
        }

        /// <summary>
        /// Remove a client from pinger
        /// </summary>
        public void Remove(SocketBase socket)
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

                foreach (SocketBase socket in _incoming)
                    _clients.Add(socket);

                _incoming.Clear();
            }
        }

        /// <summary>
        /// Sends ping message to connected clients.
        /// If clients are disconnected and did not respose previous ping message, they are moved to removing list.
        /// </summary>
        private void PingClients()
        {
            foreach (SocketBase socket in _clients)
            {
                if (socket == null || !socket.IsConnected)
                {
                    Remove(socket);
                    continue;
                }

                if (socket.LastAliveDate + _interval > DateTime.UtcNow)
                {
                    if (socket.PongRequired)
                        socket.PongRequired = false;

                    continue;
                }

                if (socket.PongRequired)
                {
                    socket.Disconnect();
                    Remove(socket);
                }
                else
                {
                    socket.PongRequired = true;
                    socket.Ping();
                }
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

                _clients.RemoveAll(x => _outgoing.Contains(x));
                _outgoing.Clear();
            }
        }
    }
}