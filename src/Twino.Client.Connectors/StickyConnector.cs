using System;
using System.Threading;
using Twino.Core;

namespace Twino.Client.Connectors
{
    /// <summary>
    /// Connects to the server and keeps this connection.
    /// If the connector disconnected from the server because of some reason,
    /// It reconnects. If the reconnection is failed, it waits for interval and re-tries.
    /// This connector is always keeps the connection up.
    /// </summary>
    public class StickyConnector<TClient, TMessage> : ConnectorBase<TClient, TMessage>
        where TClient : ClientSocketBase<TMessage>, new()
    {
        /// <summary>
        /// True when trying to connect.
        /// This field is created to avoid multiple connection tries at same time.
        /// </summary>
        private bool _connecting;

        /// <summary>
        /// Re-connect delay duration
        /// </summary>
        public TimeSpan Interval { get; set; }

        private ThreadTimer _timer;

        /// <summary>
        /// Creates new sticky connector
        /// </summary>
        public StickyConnector(TimeSpan reconnectInterval, Func<TClient> createInstance = null)
            : base(createInstance)
        {
            Interval = reconnectInterval;
        }

        /// <summary>
        /// Starts the connector and connects to the server
        /// </summary>
        public override void Run()
        {
            _running = true;
            ConnectSafe();

            if (_timer != null)
                return;

            _timer = new ThreadTimer(() =>
            {
                if (!IsRunning)
                {
                    _timer.Stop();
                    _timer = null;
                    return;
                }

                if (NeedReconnect() && !_connecting)
                    ConnectSafe();
            }, Interval);

            _timer.Start(ThreadPriority.BelowNormal);
        }

        /// <summary>
        /// Connects without throwing exception, handles it and raises connector's exception event
        /// </summary>
        private void ConnectSafe()
        {
            try
            {
                _connecting = true;
                Connect();
            }
            catch (Exception ex)
            {
                RaiseException(ex);
            }
            finally
            {
                _connecting = false;
            }
        }
        
        /// <summary>
        /// Aborts the connector and disconnects from the server.
        /// </summary>
        public override void Abort()
        {
            _running = false;
            Disconnect();
        }
    }
}