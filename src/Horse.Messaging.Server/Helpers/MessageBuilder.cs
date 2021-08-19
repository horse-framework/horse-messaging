using System.Text;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Server.Helpers
{
    /// <summary>
    /// Horse Header message builder
    /// </summary>
    public class MessageBuilder
    {
        /// <summary>
        /// Horse Message
        /// </summary>
        private HorseMessage _message;

        /// <summary>
        /// String content (key value based, http header like) builder
        /// </summary>
        private StringBuilder _content;

        private MessageBuilder()
        {
        }

        /// <summary>
        /// Starts to build new HorseMessage
        /// </summary>
        public static MessageBuilder Create(ushort contentType)
        {
            MessageBuilder builder = new MessageBuilder();
            HorseMessage message = new HorseMessage();
            message.Type = MessageType.Server;
            message.ContentType = contentType;
            builder._message = message;
            builder._content = new StringBuilder();

            return builder;
        }

        /// <summary>
        /// Creates new Pull Request response message with no content
        /// </summary>
        internal static HorseMessage CreateNoContentPullResponse(HorseMessage request, string reason)
        {
            HorseMessage msg = new HorseMessage(MessageType.QueueMessage);
            msg.SetMessageId(request.MessageId);
            msg.SetTarget(request.Target);
            msg.ContentType = request.ContentType;
            msg.AddHeader(HorseHeaders.REQUEST_ID, request.MessageId);
            msg.AddHeader(HorseHeaders.NO_CONTENT, reason);
            return msg;
        }

        #region Status Code Messages

        /// <summary>
        /// Creates new Accepted status code message
        /// </summary>
        public static HorseMessage Accepted(string clientId)
        {
            return StatusCodeMessage(KnownContentTypes.Accepted, clientId);
        }

        /// <summary>
        /// Creates new BadRequest status code message
        /// </summary>
        public static HorseMessage BadRequest()
        {
            return StatusCodeMessage(KnownContentTypes.BadRequest);
        }

        /// <summary>
        /// Creates new NotFound status code message
        /// </summary>
        public static HorseMessage NotFound()
        {
            return StatusCodeMessage(KnownContentTypes.NotFound);
        }

        /// <summary>
        /// Creates new Unauthorized status code message
        /// </summary>
        public static HorseMessage Unauthorized()
        {
            return StatusCodeMessage(KnownContentTypes.Unauthorized);
        }

        /// <summary>
        /// Creates new Busy status code message
        /// </summary>
        public static HorseMessage Busy()
        {
            return StatusCodeMessage(KnownContentTypes.Busy);
        }

        /// <summary>
        /// Creates new status code message
        /// </summary>
        public static HorseMessage StatusCodeMessage(ushort contentType, string target = null)
        {
            HorseMessage message = new HorseMessage();

            message.Type = MessageType.Server;
            message.ContentType = contentType;
            message.SetTarget(target);

            return message;
        }

        /// <summary>
        /// Creates new response message, with no content, of the message.
        /// </summary>
        public static HorseMessage StatusResponse(HorseMessage request, ushort status)
        {
            HorseMessage response = new HorseMessage();

            response.Type = MessageType.Response;
            response.SetMessageId(request.MessageId);
            response.ContentType = status;

            response.SetTarget(request.Type == MessageType.QueueMessage
                                   ? request.Target
                                   : request.Source);

            return response;
        }

        #endregion
    }
}