using System.IO;
using System.Text;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Helpers
{
    /// <summary>
    /// TMQ Header message builder
    /// </summary>
    public class MessageBuilder
    {
        /// <summary>
        /// Tmq Message
        /// </summary>
        private TmqMessage _message;

        /// <summary>
        /// String content (key value based, http header like) builder
        /// </summary>
        private StringBuilder _content;
        
        private MessageBuilder()
        {
        }

        /// <summary>
        /// Starts to build new TmqMessage
        /// </summary>
        public static MessageBuilder Create(ushort contentType)
        {
            MessageBuilder builder = new MessageBuilder();
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = contentType;
            builder._message = message;
            builder._content = new StringBuilder();

            return builder;
        }

        /// <summary>
        /// Add new key/value pair into the message
        /// </summary>
        public void Add(string key, string value)
        {
            _content.Append(key + ": " + value + "\r\n");
        }

        /// <summary>
        /// Finishes the message preparation and returns
        /// </summary>
        public TmqMessage Get()
        {
            _message.Content = new MemoryStream(Encoding.UTF8.GetBytes(_content.ToString()));
            _message.CalculateLengths();
            return _message;
        }

        #region Status Code Messages

        public static TmqMessage Accepted(string clientId)
        {
            return StatusCodeMessage(KnownContentTypes.Accepted, clientId);
        }

        public static TmqMessage BadRequest()
        {
            return StatusCodeMessage(KnownContentTypes.BadRequest);
        }

        public static TmqMessage NotFound()
        {
            return StatusCodeMessage(KnownContentTypes.NotFound);
        }

        public static TmqMessage Unauthorized()
        {
            return StatusCodeMessage(KnownContentTypes.Unauthorized);
        }

        public static TmqMessage Busy()
        {
            return StatusCodeMessage(KnownContentTypes.Busy);
        }

        public static TmqMessage StatusCodeMessage(ushort contentType, string target = null)
        {
            TmqMessage message = new TmqMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.Accepted;
            message.Target = target;
            message.FirstAcquirer = true;
                
            message.CalculateLengths();
            return message;
        }

        public static TmqMessage ResponseStatus(TmqMessage request, ushort status)
        {
            TmqMessage response = new TmqMessage();
            response.Type = MessageType.Response;
            response.MessageId = request.MessageId;
            response.Target = request.Source;
            response.Source = request.Target;
            response.ContentType = status;
            response.FirstAcquirer = true;

            response.CalculateLengths();
            return response;
        }

        #endregion
    }
}