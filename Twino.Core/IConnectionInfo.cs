using System;
using System.Net.Sockets;
using Twino.Core.Protocols;

namespace Twino.Core
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
        /// Connection is accepted as a pipe protocol such as websocket, TMQ, AMQP, MQTT. It's still alive and will.
        /// </summary>
        Pipe,

        /// <summary>
        /// Connection is accepted and first response is sent.
        /// Keeping alive and waiting next HTTP requests via same TCP connection.
        /// </summary>
        Http
    }

    public interface IConnectionInfo
    {
        /// <summary>
        /// TCP Client of the connection
        /// </summary>
        TcpClient Client { get; }

        /// <summary>
        /// The time the connection dispose if operation can't complete
        /// </summary>
        DateTime Timeout { get; set; }

        /// <summary>
        /// The max alive time for HTTP Requests
        /// </summary>
        DateTime MaxAlive { get; set; }

        /// <summary>
        /// If true, request read and proceed successfuly.
        /// If false, timeout timer is waiting for the process.
        /// </summary>
        ConnectionStates State { get; set; }

        /// <summary>
        /// Current data transfer protocol of the active connection
        /// </summary>
        TwinoProtocol Protocol { get; set; }

        /// <summary>
        /// Closes connection and releases all sources
        /// </summary>
        void Close();
    }
}