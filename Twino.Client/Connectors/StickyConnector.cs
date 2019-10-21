using System;
using System.Threading;

namespace Twino.Client.Connectors
{
    /// <summary>
    /// Connects to the server and keeps this connection.
    /// If the connector disconnected from the server because of some reason,
    /// It reconnects. If the reconnection is failed, it waits for interval and re-tries.
    /// This connector is always keeps the connection up.
    /// </summary>
    public class StickyConnector : ConnectorBase
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

        private Thread _thread;

        public StickyConnector(TimeSpan reconnectInterval)
        {
            Interval = reconnectInterval;
        }

        /// <summary>
        /// Starts the connector and connects to the server
        /// </summary>
        public override void Run()
        {
            _running = true;

            _thread = new Thread(() =>
            {
                while (IsRunning)
                {
                    if (NeedReconnect() && !_connecting)
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

                    Thread.Sleep(Convert.ToInt32(Interval.TotalMilliseconds));
                }
            });

            _thread.IsBackground = true;
            _thread.Start();
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