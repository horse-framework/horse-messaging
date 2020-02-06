using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("Twino.MQ.Data")]
[assembly: InternalsVisibleTo("Twino.MQ.Server")]

namespace Twino.Protocols.TMQ
{
    /// <summary>
    /// TMQ Protocol message
    /// </summary>
    public class TmqMessage
    {
        #region Properties

        /// <summary>
        /// If true, receiver is the first acquirer of the message
        /// </summary>
        public bool FirstAcquirer { get; set; }

        /// <summary>
        /// If true, message should be at first element in the queue
        /// </summary>
        public bool HighPriority { get; set; }

        /// <summary>
        /// Message type
        /// </summary>
        public MessageType Type { get; set; }

        /// <summary>
        /// True means, client is pending a response.
        /// Sending response is not mandatory but it SHOULD sent.
        /// </summary>
        public bool ResponseRequired { get; set; }

        /// <summary>
        /// True means, client is pending a response.
        /// If acknowledge isn't sent, server will complete process as not acknowledged
        /// </summary>
        public bool AcknowledgeRequired { get; set; }

        /// <summary>
        /// Message TTL value. Default is 16
        /// </summary>
        public int Ttl { get; set; } = 16;

        /// <summary>
        /// Message Id length
        /// </summary>
        internal int MessageIdLength { get; set; }

        /// <summary>
        /// Message unique id
        /// </summary>
        public string MessageId { get; internal set; }

        /// <summary>
        /// Message target id length
        /// </summary>
        internal int TargetLength { get; set; }

        /// <summary>
        /// Message target (channel name, client name or server)
        /// </summary>
        public string Target { get; internal set; }

        /// <summary>
        /// Message source length
        /// </summary>
        internal int SourceLength { get; set; }

        /// <summary>
        /// Message source client unique id, channel unique id or server
        /// </summary>
        public string Source { get; internal set; }

        /// <summary>
        /// Message content length
        /// </summary>
        public ulong Length { get; internal set; }

        /// <summary>
        /// Content type code.
        /// May be useful to know how content should be read, convert, serialize/deserialize
        /// </summary>
        public ushort ContentType { get; set; }

        /// <summary>
        /// Message content stream
        /// </summary>
        public MemoryStream Content { get; set; }

        /// <summary>
        /// For putting some extra data to the message.
        /// This property is not proceed by any operation.
        /// </summary>
        public object Payload { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates new empty TMQ Protocol message
        /// </summary>
        public TmqMessage()
        {
        }

        /// <summary>
        /// Creates new TMQ Protocol message with specified type
        /// </summary>
        public TmqMessage(MessageType type)
        {
            Type = type;
        }

        /// <summary>
        /// Creates new TMQ Protocol message with specified type and target
        /// </summary>
        public TmqMessage(MessageType type, string target)
        {
            Type = type;
            SetTarget(target);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Changes id of the message
        /// </summary>
        public void SetMessageId(string id)
        {
            MessageId = id;
            MessageIdLength = string.IsNullOrEmpty(id) ? 0 : Encoding.UTF8.GetByteCount(id);
        }

        /// <summary>
        /// Changes source of the message
        /// </summary>
        public void SetSource(string source)
        {
            Source = source;
            SourceLength = string.IsNullOrEmpty(source) ? 0 : Encoding.UTF8.GetByteCount(source);
        }

        /// <summary>
        /// Changes target of the message
        /// </summary>
        public void SetTarget(string target)
        {
            Target = target;
            TargetLength = string.IsNullOrEmpty(target) ? 0 : Encoding.UTF8.GetByteCount(target);
        }
        
        /// <summary>
        /// Checks message id, source, target and content properties.
        /// If they have a value, sets to length properties to their lengths
        /// </summary>
        public void CalculateLengths()
        {
            MessageIdLength = string.IsNullOrEmpty(MessageId) ? 0 : Encoding.UTF8.GetByteCount(MessageId);
            SourceLength = string.IsNullOrEmpty(Source) ? 0 : Encoding.UTF8.GetByteCount(Source);
            TargetLength = string.IsNullOrEmpty(Target) ? 0 : Encoding.UTF8.GetByteCount(Target);
            Length = Content != null ? (ulong) Content.Length : 0;
        }

        /// <summary>
        /// Converts content to string and returns
        /// </summary>
        public override string ToString()
        {
            if (Content == null)
                return string.Empty;

            return Encoding.UTF8.GetString(Content.ToArray());
        }

        /// <summary>
        /// Sets message content as string content
        /// </summary>
        public void SetStringContent(string content)
        {
            if (string.IsNullOrEmpty(content))
                return;

            Content = new MemoryStream(Encoding.UTF8.GetBytes(content));
            Length = Content != null ? (ulong) Content.Length : 0;
        }

        /// <summary>
        /// Sets message content as json serialized object
        /// </summary>
        public async Task SetJsonContent(object value)
        {
            Content = new MemoryStream();
            await System.Text.Json.JsonSerializer.SerializeAsync(Content, value, value.GetType());
            Length = Content != null ? (ulong) Content.Length : 0;
        }

        /// <summary>
        /// Reads content and deserializes to from json string
        /// </summary>
        public async Task<TModel> GetJsonContent<TModel>()
        {
            return await System.Text.Json.JsonSerializer.DeserializeAsync<TModel>(Content);
        }

        #endregion

        #region Create

        /// <summary>
        /// Create an acknowledge message of the message
        /// </summary>
        public TmqMessage CreateAcknowledge()
        {
            TmqMessage message = new TmqMessage();

            message.FirstAcquirer = FirstAcquirer;
            message.HighPriority = HighPriority;
            message.Type = MessageType.Acknowledge;
            message.SetMessageId(MessageId);
            message.ContentType = ContentType;
            message.SetTarget(Type == MessageType.Channel ? Target : Source);

            return message;
        }

        /// <summary>
        /// Create a response message of the message
        /// </summary>
        public TmqMessage CreateResponse()
        {
            TmqMessage message = new TmqMessage();

            message.FirstAcquirer = FirstAcquirer;
            message.HighPriority = HighPriority;
            message.Type = MessageType.Response;
            message.SetMessageId(MessageId);
            message.SetTarget(Type == MessageType.Channel ? Target : Source);

            return message;
        }

        #endregion
    }
}