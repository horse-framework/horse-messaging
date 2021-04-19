using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Horse.Messaging.Data")]

namespace Horse.Messaging.Server
{
    /// <summary>
    /// Horse MQ Builder
    /// </summary>
    public class HorseRiderBuilder
    {
        internal HorseRider Rider { get; set; }

        internal HorseRiderBuilder()
        {
        }

        /// <summary>
        /// Creates new Horse MQ Builder
        /// </summary>
        public static HorseRiderBuilder Create()
        {
            HorseRiderBuilder builder = new HorseRiderBuilder();
            builder.Rider = new HorseRider();
            return builder;
        }

        /// <summary>
        /// Gets Horse MQ Object
        /// </summary>
        public HorseRider Build()
        {
            return Rider;
        }
    }
}