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
        /// "Accepted"
        /// </summary>
        public const string VALUE_ACCEPTED = "Accepted";

        /// <summary>
        /// "Unauthorized"
        /// </summary>
        public const string VALUE_UNAUTHORIZED = "Unauthorized";

        /// <summary>
        /// "Busy"
        /// </summary>
        public const string VALUE_BUSY = "Busy";

        /// <summary>
        /// "Content-Type"
        /// </summary>
        public const string CONTENT_TYPE = "Content-Type";

        /// <summary>
        /// "Only-First-Acquirer"
        /// </summary>
        public const string ONLY_FIRST_ACQUIRER = "Only-First-Acquirer";

        /// <summary>
        /// "Request-Acknowledge"
        /// </summary>
        public const string REQUEST_ACKNOWLEDGE = "Request-Acknowledge";

        /// <summary>
        /// "Acknowledge-Timeout"
        /// </summary>
        public const string ACKNOWLEDGE_TIMEOUT = "Acknowledge-Timeout";

        /// <summary>
        /// "Message-Timeout"
        /// </summary>
        public const string MESSAGE_TIMEOUT = "Message-Timeout";

        /// <summary>
        /// "Use-Message-Id"
        /// </summary>
        public const string USE_MESSAGE_ID = "Use-Message-Id";

        /// <summary>
        /// "Wait-For-Acknowledge"
        /// </summary>
        public const string WAIT_FOR_ACKNOWLEDGE = "Wait-For-Acknowledge";

        /// <summary>
        /// "Hide-Client-Names"
        /// </summary>
        public const string HIDE_CLIENT_NAMES = "Hide-Client-Names";

        /// <summary>
        /// "Queue-Status"
        /// </summary>
        public const string QUEUE_STATUS = "Queue-Status";

        /// <summary>
        /// "Allow-Multiple-Queues"
        /// </summary>
        public const string ALLOW_MULTIPLE_QUEUES = "Allow-Multiple-Queues";

        /// <summary>
        /// "Client-Limit"
        /// </summary>
        public const string CLIENT_LIMIT = "Client-Limit";

        /// <summary>
        /// "Queue-Limit"
        /// </summary>
        public const string QUEUE_LIMIT = "Queue-Limit";

        /// <summary>
        /// "Allowed-Queues"
        /// </summary>
        public const string ALLOWED_QUEUES = "Allowed-Queues";

        /// <summary>
        /// "Message-Delivery-Handler"
        /// </summary>
        public const string MESSAGE_DELIVERY_HANDLER = "Message-Delivery-Handler";

        /// <summary>
        /// "Channel-Event-Handler"
        /// </summary>
        public const string CHANNEL_EVENT_HANDLER = "Channel-Event-Handler";

        /// <summary>
        /// "Channel-Authenticator"
        /// </summary>
        public const string CHANNEL_AUTHENTICATOR = "Channel-Authenticator";

        /// <summary>
        /// "Twino-MQ-Server"
        /// </summary>
        public const string TWINO_MQ_SERVER = "Twino-MQ-Server";
    }
}