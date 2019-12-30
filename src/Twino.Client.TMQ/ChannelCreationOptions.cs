using System.Text;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ
{
    /// <summary>
    /// Channel creation options
    /// </summary>
    public class ChannelCreationOptions : QueueOptions
    {
        /// <summary>
        /// If true, channel supports multiple queues
        /// </summary>
        public bool? AllowMultipleQueues { get; set; }
        
        /// <summary>
        /// Allowed queues for channel
        /// </summary>
        public ushort[] AllowedQueues { get; set; }

        /// <summary>
        /// Registry key for channel event handler
        /// </summary>
        public string EventHandler { get; set; }
        
        /// <summary>
        /// Registry key for channel authenticator
        /// </summary>
        public string Authenticator { get; set; }

        public override string Serialize(ushort contentType)
        {
            string queue = base.Serialize(contentType);
            StringBuilder builder = new StringBuilder();
            if (!string.IsNullOrEmpty(queue))
                builder.Append(queue);

            if (AllowMultipleQueues.HasValue)
                builder.Append(Line(TmqHeaders.ALLOW_MULTIPLE_QUEUES, AllowMultipleQueues.Value));

            if (AllowedQueues != null)
            {
                string list = "";
                foreach (ushort aq in AllowedQueues)
                    list += aq + ",";

                if (list.EndsWith(","))
                    list = list.Substring(0, list.Length - 1);

                builder.Append(Line(TmqHeaders.ALLOWED_QUEUES, list));
            }

            if (!string.IsNullOrEmpty(EventHandler))
                builder.Append(Line(TmqHeaders.CHANNEL_EVENT_HANDLER, EventHandler));

            if (!string.IsNullOrEmpty(Authenticator))
                builder.Append(Line(TmqHeaders.CHANNEL_AUTHENTICATOR, Authenticator));

            return builder.ToString();
        }
    }
}