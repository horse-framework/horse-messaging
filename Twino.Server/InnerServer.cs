using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace Twino.Server
{
    public class InnerServer
    {
        public TcpListener Listener { get; set; }
        public Thread Handle { get; set; }

        /// <summary>
        /// Certificate for SSL Server listening operations
        /// </summary>
        public X509Certificate2 Certificate { get; set; }

        /// <summary>
        /// Inner server listening options
        /// </summary>
        public HostOptions Options { get; set; }
        
        /// <summary>
        /// Request timeout timer for disposing incompleted connections
        /// </summary>
        internal TimeoutHandler TimeoutHandler { get; set; }

    }

}