using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Twino.Core
{
    /// <summary>
    /// Function definition for parameterless web sockets
    /// </summary>
    public delegate void ServerSocketStatusHandler(ServerSocketBase client);


    public abstract class ServerSocketBase : SocketBase
    {
        #region Events - Properties

        /// <summary>
        /// Server of the server-side client socket class
        /// </summary>
        public ITwinoServer Server { get; }

        /// <summary>
        /// Triggered when the client is connected
        /// </summary>
        public event ServerSocketStatusHandler Connected;

        /// <summary>
        /// Triggered when the client is disconnected
        /// </summary>
        public event ServerSocketStatusHandler Disconnected;

        #endregion

        protected ServerSocketBase(ITwinoServer server, Stream stream, TcpClient client)
        {
            Server = server;
            Stream = stream;
            Client = client;
            IsConnected = true;
            IsSsl = Stream is SslStream;
        }

        /// <summary>
        /// Init the clients first operations and starts to read until disconnect
        /// </summary>
        internal async Task Start()
        {
            IsConnected = true;
            OnConnected();

            try
            {
                while (IsConnected)
                    await Read();
            }
            catch
            {
                Disconnect();
            }
        }

        /// <summary>
        /// Called when an error has occured
        /// </summary>
        protected override void OnError(string hint, Exception ex)
        {
            if (Server.Logger != null)
                Server.Logger.LogException(hint, ex);
        }

        /// <summary>
        /// Client is connected
        /// </summary>
        protected override void OnConnected()
        {
            Connected?.Invoke(this);
        }

        /// <summary>
        /// Client is disconnected
        /// </summary>
        protected override void OnDisconnected()
        {
            Disconnected?.Invoke(this);
        }
    }
}