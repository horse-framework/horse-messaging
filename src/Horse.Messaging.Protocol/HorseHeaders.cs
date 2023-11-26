namespace Horse.Messaging.Protocol;

/// <summary>
/// Known header messages for Horse Protocol
/// </summary>
public class HorseHeaders
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
    /// "Message-Id"
    /// </summary>
    public const string MESSAGE_ID = "Message-Id";

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
    /// "Horse-Mq-Server"
    /// </summary>
    public const string HORSE_MQ_SERVER = "Horse-Mq-Server";

    /// <summary>
    /// "CC"
    /// </summary>
    public const string CC = "CC";

    /// <summary>
    /// "Delay-In"
    /// </summary>
    public const string DELAY_BETWEEN_MESSAGES = "Delay-In";

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
    /// "Queue-Manager"
    /// </summary>
    public const string QUEUE_MANAGER = "Queue-Manager";

    /// <summary>
    /// "Acknowledge"
    /// </summary>
    public const string ACKNOWLEDGE = "Acknowledge";

    /// <summary>
    /// "Queue-Type"
    /// </summary>
    public const string QUEUE_TYPE = "Queue-Type";

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
    /// "Channel-Name"
    /// </summary>
    public const string CHANNEL_NAME = "Channel-Name";

    /// <summary>
    /// "Queue-Topic"
    /// </summary>
    public const string QUEUE_TOPIC = "Queue-Topic";

    /// <summary>
    /// "Filter"
    /// </summary>
    public const string FILTER = "Filter";

    /// <summary>
    /// "Put-Back"
    /// </summary>
    public const string PUT_BACK = "Put-Back";

    /// <summary>
    /// "Put-Back-Delay"
    /// </summary>
    public const string PUT_BACK_DELAY = "Put-Back-Delay";

    /// <summary>
    /// "Delivery"
    /// </summary>
    public const string DELIVERY = "Delivery";

    /// <summary>
    /// "Message-Timeout"
    /// </summary>
    public const string MESSAGE_TIMEOUT = "Message-Timeout";

    /// <summary>
    /// "Ack-Timeout"
    /// </summary>
    public const string ACK_TIMEOUT = "Ack-Timeout";

    /// <summary>
    /// "Client-Limit"
    /// </summary>
    public const string CLIENT_LIMIT = "Client-Limit";

    /// <summary>
    /// "Auto-Destroy"
    /// </summary>
    public const string AUTO_DESTROY = "Auto-Destroy";

    /// <summary>
    /// "Message-Size-Limit"
    /// </summary>
    public const string MESSAGE_SIZE_LIMIT = "Message-Size-Limit";

    /// <summary>
    /// "Susbcribe"
    /// </summary>
    public const string SUBSCRIBE = "Subscribe";
        
    /// <summary>
    /// "Status"
    /// </summary>
    public const string STATUS = "Status";
        
    /// <summary>
    /// "Node-Id"
    /// </summary>
    public const string NODE_ID = "Node-Id";

    /// <summary>
    /// "Node-Name"
    /// </summary>
    public const string NODE_NAME = "Node-Name";
        
    /// <summary>
    /// "Node-Start"
    /// </summary>
    public const string NODE_START = "Node-Start";

    /// <summary>
    /// "Horse-Node"
    /// </summary>
    public const string HORSE_NODE = "Horse-Node";

    /// <summary>
    /// "Node-Host"
    /// </summary>
    public const string NODE_HOST = "Node-Host";
        
    /// <summary>
    /// "Node-Public-Host"
    /// </summary>
    public const string NODE_PUBLIC_HOST = "Node-Public-Host";
        
    /// <summary>
    /// "Yes"
    /// </summary>
    public const string YES = "Yes";

    /// <summary>
    /// "Main-Node"
    /// </summary>
    public const string MAIN_NODE = "Main-Node";
        
    /// <summary>
    /// "Successor-Node"
    /// </summary>
    public const string SUCCESSOR_NODE = "Successor-Node";

    /// <summary>
    /// "Replica-Node"
    /// </summary>
    public const string REPLICA_NODE = "Replica-Node";

    /// <summary>
    /// "Message-Id-Unique-Check"
    /// </summary>
    public const string MESSAGE_ID_UNIQUE_CHECK = "Message-Id-Unique-Check";

    /// <summary>
    /// "Channel-Initial-Message"
    /// </summary>
    public const string CHANNEL_INITIAL_MESSAGE = "Channel-Initial-Message";

    /// <summary>
    /// "Channel-Destroy-Idle-Seconds"
    /// </summary>
    public const string CHANNEL_DESTROY_IDLE_SECONDS = "Channel-Destroy-Idle-Seconds";

    /// <summary>
    /// "Warning-Duration"
    /// </summary>
    public const string WARNING_DURATION = "Warning-Duration";

    /// <summary>
    /// "Tag"
    /// </summary>
    public const string TAG = "Tag";
        
    /// <summary>
    /// "Expiry"
    /// </summary>
    public const string EXPIRY = "Expiry";
        
    /// <summary>
    /// "Warning"
    /// </summary>
    public const string WARNING = "Warning";
        
    /// <summary>
    /// "Warn-Count"
    /// </summary>
    public const string WARN_COUNT = "Warn-Count";

    /// <summary>
    /// "Underlying-Protocol"
    /// </summary>
    public const string UNDERLYING_PROTOCOL = "Underlying-Protocol";

    /// <summary>
    /// "Value"
    /// </summary>
    public const string VALUE = "Value";
}