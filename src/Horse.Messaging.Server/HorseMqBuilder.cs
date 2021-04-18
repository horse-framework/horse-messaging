using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Horse.Messaging.Data")]

namespace Horse.Messaging.Server
{
    /// <summary>
    /// Horse MQ Builder
    /// </summary>
    public class HorseMqBuilder
    {
        internal HorseMq Server { get; set; }

        internal HorseMqBuilder()
        {
        }

        /// <summary>
        /// Creates new Horse MQ Builder
        /// </summary>
        public static HorseMqBuilder Create()
        {
            HorseMqBuilder builder = new HorseMqBuilder();
            builder.Server = new HorseMq();
            return builder;
        }

        /// <summary>
        /// Gets Horse MQ Object
        /// </summary>
        public HorseMq Build()
        {
            return Server;
        }
    }
}