using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading.Tasks;
using Twino.Core;
using Twino.Core.Http;

namespace Twino.Server.WebSockets
{
    #region Event Delegates

    /// <summary>
    /// Function definition for parameterless web sockets
    /// </summary>
    public delegate void ServerSocketStatusHandler(ServerSocket client);

    #endregion

    /// <summary>
    /// Server-Side Socket class
    /// </summary>
    public class ServerSocket : SocketBase
    {
        #region Events - Properties

        /// <summary>
        /// Server of the server-side client socket class
        /// </summary>
        public TwinoServer Server { get; }

        /// <summary>
        /// Client's HTTP Request information
        /// </summary>
        public HttpRequest Request { get; }

        /// <summary>
        /// Triggered when the client is connected
        /// </summary>
        public event ServerSocketStatusHandler Connected;

        /// <summary>
        /// Triggered when the client is disconnected
        /// </summary>
        public event ServerSocketStatusHandler Disconnected;

        #endregion

        public ServerSocket(TwinoServer server, HttpRequest request, TcpClient client)
        {
            Server = server;
            Stream = request.Response.NetworkStream;
            Request = request;
            Client = client;
            IsConnected = true;
            IsSsl = Stream is SslStream;
        }

        /// <summary>
        /// Init the clients first operations and starts to read until disconnect
        /// </summary>
        internal async Task Start()
        {
            if (Server.Container != null)
                Server.Container.Add(this);

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
        /// Sends ping to the remote client
        /// </summary>
        public void Ping()
        {
            byte[] data = WebSocketWriter.CreatePing();
            Send(data);
        }

        /// <summary>
        /// Disconnects the connection between server socket and client socket
        /// </summary>
        public override void Disconnect()
        {
            Server.Pinger.RemoveClient(this);
            base.Disconnect();

            if (Server.Container != null)
                Server.Container.Remove(this);
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
            Server.SetClientDisconnected(this);
        }
    }
}