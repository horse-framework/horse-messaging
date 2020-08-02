using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Twino.MQ.Data")]

namespace Twino.MQ
{
    /// <summary>
    /// Twino MQ Builder
    /// </summary>
    public class TwinoMqBuilder
    {
        internal TwinoMQ Server { get; set; }

        /// <summary>
        /// Creates new Twino MQ Builder
        /// </summary>
        public static TwinoMqBuilder Create()
        {
            TwinoMqBuilder builder = new TwinoMqBuilder();
            builder.Server = new TwinoMQ();
            return builder;
        }

        /// <summary>
        /// Gets Twino MQ Object
        /// </summary>
        public TwinoMQ Build()
        {
            return Server;
        }
    }
}