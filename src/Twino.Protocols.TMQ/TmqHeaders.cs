namespace Twino.Protocols.TMQ
{
    /// <summary>
    /// Known header messages for TMQ Protocol
    /// </summary>
    public class TmqHeaders
    {
        /// <summary>
        /// "Client-Id"
        /// </summary>
        public const string CLIENT_ID = "Client-Id";

        /// <summary>
        /// "Client-Token"
        /// </summary>
        public const string CLIENT_TOKEN = "Client-Token";

        /// <summary>
        /// "Client-Name"
        /// </summary>
        public const string CLIENT_NAME = "Client-Name";

        /// <summary>
        /// "Client-Type"
        /// </summary>
        public const string CLIENT_TYPE = "Client-Type";

        /// <summary>
        /// "Client-Accept"
        /// </summary>
        public const string CLIENT_ACCEPT = "Client-Accept";

        /// <summary>
        /// "Nack-Reason"
        /// </summary>
        public const string NEGATIVE_ACKNOWLEDGE_REASON = "Nack-Reason";

        /// <summary>
        /// "none"
        /// </summary>
        public const string NACK_REASON_NONE = "none";

        /// <summary>
        /// "timeout"
        /// </summary>
        public const string NACK_REASON_TIMEOUT = "timeout";

        /// <summary>
        /// "Twino-MQ-Server"
        /// </summary>
        public const string TWINO_MQ_SERVER = "Twino-MQ-Server";

        /// <summary>
        /// "Channel-Name"
        /// </summary>
        public const string CHANNEL_NAME = "Channel-Name";
        
        /// <summary>
        /// "CC"
        /// </summary>
        public const string CC = "CC";

        /// <summary>
        /// "Queue-Id"
        /// </summary>
        public const string QUEUE_ID = "Queue-Id";
    }
}