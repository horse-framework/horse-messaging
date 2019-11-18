using System;
using System.Collections.Generic;

namespace Twino.Core
{
    /// <summary>
    /// Function definition for parameterless web sockets
    /// </summary>
    public delegate void SocketStatusHandler(ClientSocketBase client);

    public abstract class ClientSocketBase : SocketBase
    {
        /// <summary>
        /// Invokes when client connects
        /// </summary>
        public event SocketStatusHandler Connected;

        /// <summary>
        /// Invokes when client disconnects
        /// </summary>
        public event SocketStatusHandler Disconnected;

        public Dictionary<string, string> Properties { get; } = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        public abstract void Connect(string host);
    }
}