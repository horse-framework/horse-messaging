using System;

namespace Twino.Server.WebSockets
{
    /// <summary>
    /// Ping info for the web socket connection
    /// </summary>
    internal class SocketPingInfo
    {
        /// <summary>
        /// The client
        /// </summary>
        public ServerSocket Socket { get; }
        
        /// <summary>
        /// Last ping time
        /// </summary>
        public DateTime Last { get; private set; }
        
        /// <summary>
        /// If true, first ping did not send
        /// </summary>
        public bool New { get; private set; }
        
        public SocketPingInfo(ServerSocket socket)
        {
            Socket = socket;
            Last = DateTime.UtcNow;
            New = true;
        }

        /// <summary>
        /// Sends ping
        /// </summary>
        public void Ping()
        {
            Last = DateTime.UtcNow;
            
            if (New)
                New = false;

            Socket.Ping();
        }
    }
}