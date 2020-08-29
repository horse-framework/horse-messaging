namespace Twino.Protocols.TMQ
{
    /// <summary>
    /// Known header messages for TMQ Protocol
    /// </summary>
    public class TwinoHeaders
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
        /// "Reason"
        /// </summary>
        public const string REASON = "Reason";

        /// <summary>
        /// "none"
        /// </summary>
        public const string NACK_REASON_NONE = "none";

        /// <summary>
        /// "error"
        /// </summary>
        public const string NACK_REASON_ERROR = "error";

        /// <summary>
        /// "no-consumers"
        /// </summary>
        public const string NACK_REASON_NO_CONSUMERS = "no-consumers";

        /// <summary>
        /// "timeout"
        /// </summary>
        public const string NACK_REASON_TIMEOUT = "timeout";

        /// <summary>
        /// "Twino-MQ-Server"
        /// </summary>
        public const string TWINO_MQ_SERVER = "Twino-MQ-Server";

        /// <summary>
        /// "CC"
        /// </summary>
        public const string CC = "CC";

        /// <summary>
        /// "Request-Id"
        /// </summary>
        public const string REQUEST_ID = "Request-Id";

        /// <summary>
        /// "No-Content"
        /// </summary>
        public const string NO_CONTENT = "No-Content";

        /// <summary>
        /// "Empty"
        /// </summary>
        public const string EMPTY = "Empty";

        /// <summary>
        /// "Unauthorized"
        /// </summary>
        public const string UNAUTHORIZED = "Unauthorized";

        /// <summary>
        /// "Unacceptable"
        /// </summary>
        public const string UNACCEPTABLE = "Unacceptable";

        /// <summary>
        /// "No-Queue"
        /// </summary>
        public const string NO_QUEUE = "No-Queue";

        /// <summary>
        /// "Id-Required"
        /// </summary>
        public const string ID_REQUIRED = "Id-Required";

        /// <summary>
        /// "End"
        /// </summary>
        public const string END = "End";

        /// <summary>
        /// "Error"
        /// </summary>
        public const string ERROR = "Error";

        /// <summary>
        /// "Index"
        /// </summary>
        public const string INDEX = "Index";

        /// <summary>
        /// "Count"
        /// </summary>
        public const string COUNT = "Count";

        /// <summary>
        /// "Order"
        /// </summary>
        public const string ORDER = "Order";

        /// <summary>
        /// "Clear"
        /// </summary>
        public const string CLEAR = "Clear";

        /// <summary>
        /// "Info"
        /// </summary>
        public const string INFO = "Info";

        /// <summary>
        /// "LIFO"
        /// </summary>
        public const string LIFO = "LIFO";

        /// <summary>
        /// "Priority-Messages"
        /// </summary>
        public const string PRIORITY_MESSAGES = "Priority-Messages";

        /// <summary>
        /// "Messages"
        /// </summary>
        public const string MESSAGES = "Messages";

        /// <summary>
        /// "Delivery-Handler"
        /// </summary>
        public const string DELIVERY_HANDLER = "Delivery-Handler";

        /// <summary>
        /// "Acknowledge"
        /// </summary>
        public const string ACKNOWLEDGE = "Acknowledge";

        /// <summary>
        /// "Queue-Status"
        /// </summary>
        public const string QUEUE_STATUS = "Queue-Status";

        /// <summary>
        /// "Route-Method"
        /// </summary>
        public const string ROUTE_METHOD = "Route-Method";

        /// <summary>
        /// "Binding-Name"
        /// </summary>
        public const string BINDING_NAME = "Binding-Name";

        /// <summary>
        /// "Queue-Name"
        /// </summary>
        public const string QUEUE_NAME = "Queue-Name";

        /// <summary>
        /// "Queue-Topic"
        /// </summary>
        public const string QUEUE_TOPIC = "Queue-Topic";
        
        /// <summary>
        /// "Filter"
        /// </summary>
        public const string FILTER = "Filter";
        
        /// <summary>
        /// "Init-Queue"
        /// </summary>
        public const string INIT_QUEUE = "Init-Queue";

    }
}