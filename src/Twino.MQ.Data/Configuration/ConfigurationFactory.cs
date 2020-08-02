using System;
using System.Threading.Tasks;

namespace Twino.MQ.Data.Configuration
{
    internal static class ConfigurationFactory
    {
        /// <summary>
        /// Current configuration
        /// </summary>
        internal static DataConfiguration Configuration { get; private set; }

        /// <summary>
        /// Configuration manager
        /// </summary>
        internal static DataConfigurationManager Manager { get; private set; }

        /// <summary>
        /// Configuration builder
        /// </summary>
        internal static DataConfigurationBuilder Builder { get; private set; }

        /// <summary>
        /// Initializes configurations and creates builder and manager
        /// </summary>
        public static void Initialize(DataConfigurationBuilder builder)
        {
            if (Builder != null)
                throw new InvalidOperationException("Data configuration is already initialized");

            Builder = builder;
            Manager = new DataConfigurationManager();
            Configuration = Manager.Load(builder.ConfigFile);
        }
    }
}