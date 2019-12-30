namespace Twino.Protocols.TMQ
{
    public class TmqHeaders
    {
        public const string CLIENT_ID = "Client-Id";
        public const string CLIENT_TOKEN = "Client-Token";
        public const string CLIENT_NAME = "Client-Name";
        public const string CLIENT_TYPE = "Client-Type";
        public const string CLIENT_ACCEPT = "Client-Accept";

        public const string VALUE_ACCEPTED = "Accepted";
        public const string VALUE_UNAUTHORIZED = "Unauthorized";
        public const string VALUE_BUSY = "Busy";

        public const string CONTENT_TYPE = "Content-Type";
        public const string ONLY_FIRST_ACQUIRER = "Only-First-Acquirer";
        public const string REQUEST_ACKNOWLEDGE = "Request-Acknowledge";
        public const string ACKNOWLEDGE_TIMEOUT = "Acknowledge-Timeout";
        public const string MESSAGE_TIMEOUT = "Message-Timeout";
        public const string USE_MESSAGE_ID = "Use-Message-Id";
        public const string WAIT_FOR_ACKNOWLEDGE = "Wait-For-Acknowledge";
        public const string HIDE_CLIENT_NAMES = "Hide-Client-Names";
        public const string QUEUE_STATUS = "Queue-Status";
        public const string ALLOW_MULTIPLE_QUEUES = "Allow-Multiple-Queues";
        public const string ALLOWED_QUEUES = "Allowed-Queues";
        
        public const string MESSAGE_DELIVERY_HANDLER = "Message-Delivery-Handler";
        public const string CHANNEL_EVENT_HANDLER = "Channel-Event-Handler";
        public const string CHANNEL_AUTHENTICATOR = "Channel-Authenticator";
        
        public const string TWINO_MQ_SERVER = "Twino-MQ-Server";
    }
}