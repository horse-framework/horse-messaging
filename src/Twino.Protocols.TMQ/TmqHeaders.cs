namespace Twino.Protocols.TMQ
{
    public class TmqHeaders
    {
        public static readonly string CLIENT_ID = "Client-Id";
        public static readonly string CLIENT_TOKEN = "Client-Token";
        public static readonly string CLIENT_NAME = "Client-Name";
        public static readonly string CLIENT_TYPE = "Client-Type";
        public static readonly string CLIENT_ACCEPT = "Client-Accept";

        public static readonly string VALUE_ACCEPTED = "Accepted";
        public static readonly string VALUE_UNAUTHORIZED = "Unauthorized";
        public static readonly string VALUE_BUSY = "Busy";

        public static readonly string CONTENT_TYPE = "Content-Type";
        public static readonly string ONLY_FIRST_ACQUIRER = "Only-First-Acquirer";
        public static readonly string REQUEST_ACKNOWLEDGE = "Request-Acknowledge";
        public static readonly string ACKNOWLEDGE_TIMEOUT = "Acknowledge-Timeout";
        public static readonly string MESSAGE_TIMEOUT = "Message-Timeout";
        public static readonly string USE_MESSAGE_ID = "Use-Message-Id";
        public static readonly string WAIT_FOR_ACKNOWLEDGE = "Wait-For-Acknowledge";
        public static readonly string HIDE_CLIENT_NAMES = "Hide-Client-Names";
        public static readonly string QUEUE_STATUS = "Queue-Status";
    }
}