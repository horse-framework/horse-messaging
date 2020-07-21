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
        /// Creates new Pull Request response message with no content
        /// </summary>
        internal static TmqMessage CreateNoContentPullResponse(TmqMessage request, string reason)
        {
            TmqMessage msg = new TmqMessage(MessageType.QueueMessage);
            msg.SetMessageId(request.MessageId);
            msg.SetTarget(request.Target);
            msg.ContentType = request.ContentType;
            msg.AddHeader(TmqHeaders.REQUEST_ID, request.MessageId);
            msg.AddHeader(TmqHeaders.NO_CONTENT, reason);
            return msg;
        }

        #region Status Code Messages

        /// <summary>
        /// Creates new Accepted status code message
        /// </summary>
        public static TmqMessage Accepted(string clientId)
        {
            return StatusCodeMessage(KnownContentTypes.Accepted, clientId);
        }

        /// <summary>
        /// Creates new BadRequest status code message
        /// </summary>
        public static TmqMessage BadRequest()
        {
            return StatusCodeMessage(KnownContentTypes.BadRequest);
        }

        /// <summary>
        /// Creates new NotFound status code message
        /// </summary>
        public static TmqMessage NotFound()
        {
            return StatusCodeMessage(KnownContentTypes.NotFound);
        }

        /// <summary>
        /// Creates new Unauthorized status code message
        /// </summary>
        public static TmqMessage Unauthorized()
        {
            return StatusCodeMessage(KnownContentTypes.Unauthorized);
        }

        /// <summary>
        /// Creates new Busy status code message
        /// </summary>
        public static TmqMessage Busy()
        {
            return StatusCodeMessage(KnownContentTypes.Busy);
        }

        /// <summary>
        /// Creates new status code message
        /// </summary>
        public static TmqMessage StatusCodeMessage(ushort contentType, string target = null)
        {
            TmqMessage message = new TmqMessage();

            message.Type = MessageType.Server;
            message.ContentType = contentType;
            message.SetTarget(target);
            message.FirstAcquirer = true;

            return message;
        }

        /// <summary>
        /// Creates new response message, with no content, of the message.
        /// </summary>
        public static TmqMessage StatusResponse(TmqMessage request, ushort status)
        {
            TmqMessage response = new TmqMessage();

            response.Type = MessageType.Response;
            response.SetMessageId(request.MessageId);
            response.ContentType = status;
            response.FirstAcquirer = true;

            response.SetTarget(request.Type == MessageType.QueueMessage
                                   ? request.Target
                                   : request.Source);

            return response;
        }

        #endregion
    }
}