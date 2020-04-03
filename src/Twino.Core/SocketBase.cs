using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Twino.Core.Protocols;

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
        /// When client is disconnected and disposed,
        /// The message will be sent to all event subscribers.
        /// Sometimes multiple errors occur when the connection is failed.
        /// To avoid multiple event fires, this field is used.
        /// </summary>
        private volatile bool _disconnectedWarn;

        /// <summary>
        /// SslStream does not support concurrent write operations.
        /// This semaphore is used to handle that issue
        /// </summary>
        private SemaphoreSlim _ss;

        /// <summary>
        /// Triggered when the client is connected
        /// </summary>
        public event SocketStatusHandler Connected;

        /// <summary>
        /// Triggered when the client is disconnected
        /// </summary>
        public event SocketStatusHandler Disconnected;

        /// <summary>
        /// Last message received or sent date.
        /// Used for preventing unnecessary ping/pong traffic
        /// </summary>
        public DateTime LastAliveDate { get; protected set; } = DateTime.UtcNow;

        /// <summary>
        /// True, If a pong must received asap
        /// </summary>
        internal bool PongRequired { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Socket base constructor for client sockets
        /// </summary>
        protected SocketBase()
        {
        }

        /// <summary>
        /// Socket base constructor for server sockets
        /// </summary>
        protected SocketBase(IConnectionInfo info)
        {
            IsSsl = info.IsSsl;
            IsConnected = true;
            Stream = info.GetStream();

            if (IsSsl)
                _ss = new SemaphoreSlim(1, 1);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Ends write operation and completed callback
        /// </summary>
        private void EndWrite(IAsyncResult ar)
        {
            try
            {
                if (IsSsl)
                {
                    Stream.EndWrite(ar);
                    ReleaseSslLock();
                }
                else
                    Stream.EndRead(ar);
            }
            catch
            {
                ReleaseSslLock();
                Disconnect();
            }
        }

        /// <summary>
        /// Sends byte array message to the socket client.
        /// </summary>
        public async Task<bool> SendAsync(byte[] data)
        {
            try
            {
                if (Stream == null || data == null)
                {
                    ReleaseSslLock();
                    return false;
                }

                if (IsSsl)
                {
                    if (_ss == null)
                        _ss = new SemaphoreSlim(1, 1);

                    await _ss.WaitAsync();
                }

                await Stream.WriteAsync(data);

                if (IsSsl)
                    ReleaseSslLock();

                return true;
            }
            catch
            {
                ReleaseSslLock();
                Disconnect();
                return false;
            }
        }

        /// <summary>
        /// Sends byte array message to the socket client.
        /// </summary>
        public bool Send(byte[] data)
        {
            try
            {
                if (Stream == null || data == null)
                    return false;

                if (IsSsl)
                {
                    if (_ss == null)
                        _ss = new SemaphoreSlim(1, 1);

                    _ss.Wait();
                }

                Stream.BeginWrite(data, 0, data.Length, EndWrite, data);
                return true;
            }
            catch
            {
                Disconnect();
                return false;
            }
        }

        /// <summary>
        /// Updates socket alive date
        /// </summary>
        public void KeepAlive()
        {
            LastAliveDate = DateTime.UtcNow;
            PongRequired = false;
        }

        /// <summary>
        /// Releases ssl semaphore 
        /// </summary>
        private void ReleaseSslLock()
        {
            if (!IsSsl)
                return;

            try
            {
                if (_ss != null && _ss.CurrentCount == 0)
                    _ss.Release();
            }
            catch
            {
            }
        }

        #endregion

        #region Abstract Methods

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
        /// Called when client's protocol has switched
        /// </summary>
        protected virtual void OnProtocolSwitched(ITwinoProtocol previous, ITwinoProtocol current)
        {
            //not abstract, override is not must. but we do not have anything to do here.
        }

        /// <summary>
        /// Triggers virtual connected method
        /// </summary>
        internal void SetOnConnected()
        {
            OnConnected();
        }

        /// <summary>
        /// Triggers virtual protocol switched
        /// </summary>
        internal void SetOnProtocolSwitched(ITwinoProtocol previous, ITwinoProtocol current)
        {
            OnProtocolSwitched(previous, current);
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