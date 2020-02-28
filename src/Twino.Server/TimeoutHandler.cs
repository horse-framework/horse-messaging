using System;
using System.Collections.Generic;
using System.Threading;
using Twino.Core;

namespace Twino.Server
{
    /// <summary>
    /// Request timeout timer for disposing incompleted connections
    /// </summary>
    internal class TimeoutHandler
    {
        /// <summary>
        /// newly connected clients. created for optimizing (to not lock real collection)
        /// </summary>
        private readonly List<ConnectionInfo> _incoming = new List<ConnectionInfo>();

        /// <summary>
        /// active tcp clients that are checked if they are timed out
        /// </summary>
        private readonly List<ConnectionInfo> _connections = new List<ConnectionInfo>();

        /// <summary>
        /// timeout timer
        /// </summary>
        private ThreadTimer _timer;

        /// <summary>
        /// clients' timeout total milliseconds
        /// </summary>
        private readonly int _timeoutMilliseconds;

        /// <summary>
        /// timer interval
        /// </summary>
        private readonly TimeSpan _tickInterval;

        /// <summary>
        /// Creates new timeout handler with specified timeout milliseconds and check timer interval
        /// </summary>
        internal TimeoutHandler(int timeoutMilliseconds, int tickInterval)
        {
            _timeoutMilliseconds = timeoutMilliseconds;
            _tickInterval = TimeSpan.FromMilliseconds(tickInterval);
        }

        /// <summary>
        /// Runs the timeout timer process
        /// </summary>
        internal void Start()
        {
            _timer = new ThreadTimer(Tick, _tickInterval);
            _timer.Start(ThreadPriority.Lowest);
        }

        /// <summary>
        /// Stop the time out handler
        /// </summary>
        internal void Stop()
        {
            _timer.Stop();
            _timer = null;
        }

        /// <summary>
        /// On every tick, adds new clients to time out handling list
        /// Checks the clients if they should be removed due to timeout reason
        /// </summary>
        private void Tick()
        {
            //add incoming clients to the timeout handle list
            AddIncomingItems();

            if (_connections.Count == 0)
                return;

            List<ConnectionInfo> removing = new List<ConnectionInfo>();

            foreach (ConnectionInfo info in _connections)
            {
                //directly remove if client is disconnected
                if (info.Client == null || !info.Client.Connected)
                {
                    removing.Add(info);
                    continue;
                }

                //if at least 1 request is responsed and the connection is http wait until for keep alive
                if (info.State == ConnectionStates.Http)
                {
                    if (info.MaxAlive < DateTime.UtcNow)
                        removing.Add(info);
                }

                //if client is websocket, we dont need handling timeout, anymore
                else if (info.State == ConnectionStates.Pipe)
                    removing.Add(info);

                //the request could not receive yet but time is up
                else if (info.State > ConnectionStates.Pending || info.Timeout < DateTime.UtcNow)
                    removing.Add(info);
            }

            if (removing.Count == 0)
                return;

            foreach (ConnectionInfo info in removing)
            {
                //in removing list, there are some websocket connections.
                //we need to check before close if they were not websocket connection

                if (info.State == ConnectionStates.Pending && info.Timeout < DateTime.UtcNow)
                    info.Close();

                else if (info.State == ConnectionStates.Http && info.MaxAlive < DateTime.UtcNow)
                    info.Close();
            }

            foreach (ConnectionInfo state in removing)
                _connections.Remove(state);
        }

        /// <summary>
        /// Inserts recently added items to timeout items list
        /// </summary>
        private void AddIncomingItems()
        {
            lock (_incoming)
                if (_incoming.Count > 0)
                {
                    foreach (ConnectionInfo i in _incoming)
                    {
                        if (i.Client == null || !i.Client.Connected || i.State == ConnectionStates.Pipe)
                            continue;

                        _connections.Add(i);
                    }

                    _incoming.Clear();
                }
        }

        /// <summary>
        /// Adds new connection to the list.
        /// This connection's timeout will be set in this method and starts it's timeout span
        /// </summary>
        public void Add(ConnectionInfo info)
        {
            info.Timeout = DateTime.UtcNow.AddMilliseconds(_timeoutMilliseconds);

            lock (_incoming)
                _incoming.Add(info);
        }
    }
}