using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("Twino.Server")]

namespace Twino.Core
{
    /// <summary>
    /// Function definition for parameterless web sockets
    /// </summary>
    public delegate void SocketStatusHandler(SocketBase client);

    /// <summary>
    /// Base class for web socket clients.
    /// Server-side client and Client-side client classes are derived from this class
    /// </summary>
    public abstract class SocketBase
    {
        #region Properties

        /// <summary>
        /// TcpClient class of the socket class
        /// </summary>
        protected TcpClient Client { get; set; }

        /// <summary>
        /// TcpClient network stream (with SSL or without SSL, depends on the requested URL or server certificate)
        /// </summary>
        protected Stream Stream { get; set; }

        /// <summary>
        /// Client's connected status
        /// </summary>
        public bool IsConnected { get; protected set; }

        /// <summary>
        /// Gets the connection is over SSL or not
        /// </summary>
        public bool IsSsl { get; protected set; }

        /// <summary>
        /// The last PONG received time. If this time is before last second PING time, the client will be removed.
        /// </summary>
        internal DateTime PongTime { get; set; }

        /// <summary>
        /// When client is disconnected and disposed,
        /// The message will be sent to all event subscribers.
        /// Sometimes multiple errors occur when the connection is failed.
        /// To avoid multiple event fires, this field is used.
        /// </summary>
        private volatile bool _disconnectedWarn;

        /// <summary>
        /// If true, messages will be proceed async
        /// </summary>
        protected bool AsyncMessaging { get; set; }

        /// <summary>
        /// After endWrite called, this value will be set as true.
        /// This value is used for manipulating SslStream multiple write operation
        /// </summary>
        private volatile bool _writeCompleted = true;

        /// <summary>
        /// Triggered when the client is connected
        /// </summary>
        public event SocketStatusHandler Connected;

        /// <summary>
        /// Triggered when the client is disconnected
        /// </summary>
        public event SocketStatusHandler Disconnected;

        #endregion

        protected SocketBase()
        {
            PongTime = DateTime.UtcNow.AddSeconds(30);
        }

        #region Methods

        /// <summary>
        /// Ends write operation and completed callback
        /// </summary>
        private void EndWrite(IAsyncResult ar)
        {
            try
            {
                lock (Stream)
                {
                    Stream.EndWrite(ar);
                    _writeCompleted = true;
                }
            }
            catch
            {
                _writeCompleted = true;
                Disconnect();
            }
        }

        /// <summary>
        /// Sends prepared byte array message to the socket client.
        /// </summary>
        public bool Send(byte[] preparedData)
        {
            try
            {
                if (Stream == null || preparedData == null)
                    return false;

                lock (Stream)
                {
                    if (!_writeCompleted)
                        SendQueue(preparedData);
                    else
                    {
                        _writeCompleted = false;
                        Stream.BeginWrite(preparedData, 0, preparedData.Length, EndWrite, preparedData);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _writeCompleted = true;
                Disconnect();
                return false;
            }
        }

        /// <summary>
        /// Disconnects client and disposes all streams belongs it
        /// </summary>
        public virtual void Disconnect()
        {
            try
            {
                IsConnected = false;

                if (Stream != null)
                    Stream.Dispose();

                if (Client != null)
                    Client.Close();

                Client = null;
                Stream = null;
            }
            catch
            {
            }

            if (!_disconnectedWarn)
            {
                _disconnectedWarn = true;
                OnDisconnected();
            }
        }

        /// <summary>
        /// Over SslStream, writing another package before first package's callback is called, throws NotSupportedException.
        /// This method is for fixing that issue.
        /// When Send method is called before previous callback, sending operation will call this method.
        /// This method waits for callback operation async, and calls send method again.
        /// </summary>
        private void SendQueue(byte[] data)
        {
            SpinWait wait = new SpinWait();
            DateTime until = DateTime.UtcNow.AddSeconds(5);

            Task.Factory.StartNew(() =>
            {
                while (!_writeCompleted)
                {
                    wait.SpinOnce();
                    if (DateTime.UtcNow > until)
                        return;
                }

                Send(data);
            });
        }

        #endregion

        #region Abstract Methods

        /// <summary>
        /// Triggered when client is connected
        /// </summary>
        protected virtual void OnConnected()
        {
            Connected?.Invoke(this);
        }

        /// <summary>
        /// Triggered when client is disconnected
        /// </summary>
        protected virtual void OnDisconnected()
        {
            Disconnected?.Invoke(this);
        }

        /// <summary>
        /// Sends ping
        /// </summary>
        /// <returns></returns>
        public abstract void Ping();

        /// <summary>
        /// Sends pong
        /// </summary>
        /// <returns></returns>
        public abstract void Pong();

        #endregion
    }
}