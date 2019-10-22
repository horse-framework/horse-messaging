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
        private readonly List<HandshakeInfo> _handshakes = new List<HandshakeInfo>();

        private Thread _timer;

        public bool Running { get; set; }
        private readonly int _timeoutMilliseconds;

        public TimeoutHandler(int timeoutMilliseconds)
        {
            _timeoutMilliseconds = timeoutMilliseconds;
        }

        /// <summary>
        /// Runs the timeout timer process
        /// </summary>
        public void Run()
        {
            Running = true;
            _timer = new Thread(() =>
            {
                while (Running)
                {
                    Thread.Sleep(500);
                    List<HandshakeInfo> removing = new List<HandshakeInfo>();

                    lock (_handshakes)
                    {
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

                    lock (_handshakes)
                        foreach (HandshakeInfo state in removing)
                            _handshakes.Remove(state);
                }
            });

            _timer.IsBackground = true;
            _timer.Start();
        }

        /// <summary>
        /// Adds new connection to the list.
        /// This connection's timeout will be set in this method and starts it's timeout span
        /// </summary>
        public void Add(HandshakeInfo handshake)
        {
            handshake.Timeout = DateTime.UtcNow.AddMilliseconds(_timeoutMilliseconds);
            lock (_handshakes)
                _handshakes.Add(handshake);
        }

    }
}