using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace Twino.Server
{
    /// <summary>
    /// Listener object model for TCP port listening operation 
    /// </summary>
    public class HostListener
    {
        /// <summary>
        /// Port listener object of the host
        /// </summary>
        public TcpListener Listener { get; set; }

        /// <summary>
        /// TCP connection accepting thread
        /// </summary>
        public Thread Handle { get; set; }

        /// <summary>
        /// Certificate for SSL Server listening operations
        /// </summary>
        public X509Certificate2 Certificate { get; set; }

        /// <summary>
        /// Host listener options
        /// </summary>
        public HostOptions Options { get; set; }

        /// <summary>
        /// Request keep alive manager for disposing incompleted connections
        /// </summary>
        internal KeepAliveManager KeepAliveManager { get; set; }

    }

}