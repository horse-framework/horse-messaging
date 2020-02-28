namespace Twino.Server
{
    /// <summary>
    /// Options for each host
    /// </summary>
    public class HostOptions
    {
        /// <summary>
        /// Server Listening port number
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Accepted hostnames. Set null if all hostnames are accepted.
        /// </summary>
        public string[] Hostnames { get; set; }

        /// <summary>
        /// If true, SSL is enabled and server handshakes with clients
        /// </summary>
        public bool SslEnabled { get; set; }

        /// <summary>
        /// Certificate filename, if there is no certificate or you need to load the certificate
        /// from another source pass null value.
        /// </summary>
        public string SslCertificate { get; set; }

        /// <summary>
        /// Passphare key for the server side certificate.
        /// </summary>
        public string CertificateKey { get; set; }

        /// <summary>
        /// By passes certificate validation, all certificates are welcome.
        /// </summary>
        public bool BypassSslValidation { get; set; }

        /// <summary>
        /// SSL Stream Secure Layer protocol.
        /// Supported protocols are None, Ssl2, Ssl3, Tls, Tls11, Tls12
        /// </summary>
        public string SslProtocol { get; set; }
    }
}