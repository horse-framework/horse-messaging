using System;
using System.Collections.Generic;
using System.Threading;

namespace Twino.Server
{
    /// <summary>
    /// Request timeout timer for disposing incompleted connections
    /// </summary>
    internal class TimeoutHandler
    {
        private readonly List<HandshakeInfo> _incoming = new List<HandshakeInfo>();
        private readonly List<HandshakeInfo> _handshakes = new List<HandshakeInfo>();

        private Thread _timer;
        private readonly int _timeoutMilliseconds;
        private readonly int _tickInterval;
        private bool _running;

        internal TimeoutHandler(int timeoutMilliseconds, int tickInterval)
        {
            _timeoutMilliseconds = timeoutMilliseconds;
            _tickInterval = tickInterval;
        }

        /// <summary>
        /// Runs the timeout timer process
        /// </summary>
        internal void Start()
        {
            _running = true;
            _timer = new Thread(() =>
            {
                while (_running)
                {
                    //don't let exit, if there is an exception, somehow
                    try
                    {
                        Tick();
                    }
                    catch
                    {
                    }
                }
            });
            
            _timer.IsBackground = true;
            _timer.Priority = ThreadPriority.BelowNormal;
            _timer.Start();
        }

        /// <summary>
        /// Stop the time out handler
        /// </summary>
        internal void Stop()
        {
            _running = false;
            _timer.Abort();
            _timer = null;
        }

        /// <summary>
        /// On every tick, adds new clients to time out handling list
        /// Checks the clients if they should be removed due to timeout reason
        /// </summary>
        private void Tick()
        {
            Thread.Sleep(_tickInterval);

            //add incoming clients to the timeout handle list
            AddIncomingItems();
            List<HandshakeInfo> removing = new List<HandshakeInfo>();

            foreach (HandshakeInfo handshake in _handshakes)
            {
                //directly remove if client is disconnected
                if (handshake.Client == null || !handshake.Client.Connected)
                {
                    removing.Add(handshake);
                    continue;
                }

                //if at least 1 request is responsed and the connection is http wait until for keep alive
                if (handshake.State == ConnectionStates.Http)
                {
                    if (handshake.MaxAlive < DateTime.UtcNow)
                        removing.Add(handshake);
                }

                //if client is websocket, we dont need handling timeout, anymore
                else if (handshake.State == ConnectionStates.WebSocket)
                    removing.Add(handshake);

                //the request could not receive yet but time is up
                else if (handshake.State > ConnectionStates.Pending || handshake.Timeout < DateTime.UtcNow)
                    removing.Add(handshake);
            }

            if (removing.Count == 0)
                return;

            foreach (HandshakeInfo handshake in removing)
            {
                //in removing list, there are some websocket connections.
                //we need to check before close if they were not websocket connection

                if (handshake.State == ConnectionStates.Pending && handshake.Timeout < DateTime.UtcNow)
                    handshake.Close();

                else if (handshake.State == ConnectionStates.Http && handshake.MaxAlive < DateTime.UtcNow)
                    handshake.Close();
            }

            foreach (HandshakeInfo state in removing)
                _handshakes.Remove(state);
        }

        /// <summary>
        /// Inserts recently added items to timeout items list
        /// </summary>
        private void AddIncomingItems()
        {
            lock (_incoming)
            {
                if (_incoming.Count > 0)
                {
                    foreach (HandshakeInfo i in _incoming)
                    {
                        if (i.Client == null || !i.Client.Connected || i.State == ConnectionStates.WebSocket)
                            continue;

                        _handshakes.Add(i);
                    }

                    _incoming.Clear();
                }
            }
        }

        /// <summary>
        /// Adds new connection to the list.
        /// This connection's timeout will be set in this method and starts it's timeout span
        /// </summary>
        public void Add(HandshakeInfo handshake)
        {
            handshake.Timeout = DateTime.UtcNow.AddMilliseconds(_timeoutMilliseconds);

            lock (_incoming)
                _incoming.Add(handshake);
        }
    }
}