using System.IO;
using System.Text;
using Twino.Protocols.TMQ;

namespace Twino.MQ.Helpers
{
    /// <summary>
    /// TMQ Header message builder
    /// </summary>
    public class HeaderMessageBuilder
    {
        /// <summary>
        /// Tmq Message
        /// </summary>
        private TmqMessage _message;
        
        /// <summary>
        /// String content (key value based, http header like) builder
        /// </summary>
        private StringBuilder _content;

        private HeaderMessageBuilder()
        {
        }

        /// <summary>
        /// Starts to build new TmqMessage
        /// </summary>
        public static HeaderMessageBuilder Create()
        {
            HeaderMessageBuilder builder = new HeaderMessageBuilder();
            TmqMessage message = new TmqMessage();

            message.Source = TmqHeaders.SOURCE_TARGET_SERVER;
            message.Target = TmqHeaders.CLIENT_ID;
            message.Type = MessageType.Client;
            message.ContentType = TmqHeaders.HEADER_CONTENT_TYPE;

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
    }
}