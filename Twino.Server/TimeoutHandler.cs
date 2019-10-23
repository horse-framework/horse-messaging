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
            _timer = new Thread(Cycle);
            _timer.IsBackground = true;
            _timer.Start();
        }

        internal void Stop()
        {
            _running = false;
            _timer.Abort();
            _timer = null;
        }
        
        private void Cycle()
        {
            while (_running)
            {
                Thread.Sleep(_tickInterval);
                AddIncomingItems();
                List<HandshakeInfo> removing = new List<HandshakeInfo>();

                foreach (HandshakeInfo handshake in _handshakes)
                {
                    if (handshake.Client == null || !handshake.Client.Connected)
                    {
                        removing.Add(handshake);
                        continue;
                    }

                    if (handshake.State == ConnectionStates.Http)
                    {
                        if (handshake.MaxAlive < DateTime.UtcNow)
                            removing.Add(handshake);
                    }
                    else if (handshake.State == ConnectionStates.WebSocket)
                        removing.Add(handshake);
                    else if (handshake.State > ConnectionStates.Pending || handshake.Timeout < DateTime.UtcNow)
                        removing.Add(handshake);
                }

                if (removing.Count == 0)
                    continue;

                foreach (HandshakeInfo handshake in removing)
                {
                    if (handshake.State == ConnectionStates.Pending && handshake.Timeout < DateTime.UtcNow)
                        handshake.Close();

                    else if (handshake.State == ConnectionStates.Http && handshake.MaxAlive < DateTime.UtcNow)
                        handshake.Close();
                }

                foreach (HandshakeInfo state in removing)
                    _handshakes.Remove(state);
            }
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
                    _handshakes.AddRange(_incoming);
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
            _incoming.Add(handshake);
        }
    }
}