using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;

namespace Twino.Server
{
    /// <summary>
    /// States for each request.
    /// </summary>
    public enum ConnectionStates
    {
        /// <summary>
        /// Request still handling.
        /// Server has no information what the request for.
        /// HTTP, WebSocket or something different.
        /// </summary>
        Pending,

        /// <summary>
        /// Connection is completed.
        /// </summary>
        Closed,

        /// <summary>
        /// Connection is accepted as web socket. It's still alive and will.
        /// </summary>
        WebSocket,

        /// <summary>
        /// Connection is accepted and first response is sent.
        /// Keeping alive and waiting next HTTP requests via same TCP connection.
        /// </summary>
        Http
    }

    /// <summary>
    /// After handshaking completed the state object will be passed to the callback function.
    /// In Twino SSL Handshaking this object type is HandshakeState class
    /// </summary>
    internal class ConnectionInfo
    {
        /// <summary>
        /// TCP Client of the connection
        /// </summary>
        public TcpClient Client { get; private set; }

        /// <summary>
        /// If the handshaking is a real SSL, the stream will be SslStream.
        /// If not, this value will be null
        /// </summary>
        public SslStream SslStream { get; set; }

        /// <summary>
        /// If there is no real or fake SSL handkshaking plain stream is used.
        /// </summary>
        public NetworkStream PlainStream { get; private set; }

        /// <summary>
        /// The time the connection dispose if operation can't complete
        /// </summary>
        public DateTime Timeout { get; set; }

        /// <summary>
        /// The max alive time for HTTP Requests
        /// </summary>
        public DateTime MaxAlive { get; set; }

        /// <summary>
        /// If true, request read and proceed successfuly.
        /// If false, timeout timer is waiting for the process.
        /// </summary>
        public ConnectionStates State { get; set; } = ConnectionStates.Pending;

        /// <summary>
        /// Host listener object of the connection
        /// </summary>
        public HostListener Server { get; private set; }

        public ConnectionInfo(TcpClient client, HostListener server)
        {
            Client = client;
            Server = server;
            PlainStream = client.GetStream();
        }

        /// <summary>
        /// Returns the using network stream
        /// </summary>
        /// <returns></returns>
        internal Stream GetStream()
        {
            if (SslStream != null)
                return SslStream;

            return PlainStream;
        }

        /// <summary>
        /// Closes and disposes all resources of the request
        /// </summary>
        public void Close()
        {
            if (State == ConnectionStates.Closed)
                return;
            
            State = ConnectionStates.Closed;

            try
            {
                Stream stream = GetStream();
                if (stream != null)
                    stream.Dispose();

                if (Client != null)
                {
                    Client.Close();
                    Client.Dispose();
                }
            }
            catch
            {
            }

            SslStream = null;
            PlainStream = null;
        }
    }
}