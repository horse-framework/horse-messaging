using System;

namespace Twino.Server.WebSockets
{
    /// <summary>
    /// Ping meta data for the web socket connection
    /// </summary>
    internal struct SocketPingInfo
    {
        /// <summary>
        /// The client
        /// </summary>
        public ServerSocket Socket;
        
        /// <summary>
        /// Last ping time
        /// </summary>
        public DateTime Update;
        
        /// <summary>
        /// If true, first ping did not send
        /// </summary>
        public bool New;

        public SocketPingInfo(ServerSocket socket)
        {
            Socket = socket;
            Update = DateTime.UtcNow;
            New = true;
        }

        /// <summary>
        /// Sends ping
        /// </summary>
        public void Ping()
        {
            Update = DateTime.UtcNow;

            if (New)
                New = false;

            Socket.Ping();
        }
    }
}