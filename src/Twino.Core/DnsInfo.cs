namespace Twino.Core
{
    /// <summary>
    /// Protocol information for DnsInfo class
    /// </summary>
    public enum Protocol
    {
        /// <summary>
        /// Unrecognized protocol
        /// </summary>
        Unknown,

        /// <summary>
        /// http:// or https://
        /// </summary>
        Http,

        /// <summary>
        /// ws:// or wss://
        /// </summary>
        WebSocket,

        /// <summary>
        /// tmq:// or tmqs://
        /// </summary>
        Tmq
    }

    /// <summary>
    /// All information created from a URL
    /// </summary>
    public class DnsInfo
    {
        /// <summary>
        /// IP Address
        /// </summary>
        public string IPAddress { get; set; }

        /// <summary>
        /// Hostname (usually domain name or url)
        /// </summary>
        public string Hostname { get; set; }

        /// <summary>
        /// If URL starts with https or wss,
        /// or if the remote port is 443, this property will set true
        /// </summary>
        public bool SSL { get; set; }

        /// <summary>
        /// Remote port.
        /// If an IP address specified after domain (like a.com:82) the value is the specified port.
        /// Otherwise, if SSL true this value will set 443, if not 80
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Path after protocol, domain and port information.
        /// Usually starts with "/"
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Protocol type
        /// </summary>
        public Protocol Protocol { get; set; }
    }
}