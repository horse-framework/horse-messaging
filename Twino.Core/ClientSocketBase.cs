using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Twino.Core
{
    /// <summary>
    /// Function definition for parameterless web sockets
    /// </summary>
    public delegate void ClientMessageHandler<TMessage>(ClientSocketBase<TMessage> client, TMessage message);

    public abstract class ClientSocketBase<TMessage> : SocketBase
    {
        /// <summary>
        /// Client certificate for SSL client connections.
        /// If null, Twino uses default certificate.
        /// </summary>
        public X509Certificate2 Certificate { get; set; }

        public Dictionary<string, string> Properties { get; } = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

        public event ClientMessageHandler<TMessage> MessageReceived;

        public abstract void Connect(string host);

        /// <summary>
        /// Starts to read from the TCP socket
        /// </summary>
        protected abstract Task Read();

        protected static bool CertificateCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        protected void SetOnMessageReceived(TMessage message)
        {
            MessageReceived?.Invoke(this, message);
        }
    }
}