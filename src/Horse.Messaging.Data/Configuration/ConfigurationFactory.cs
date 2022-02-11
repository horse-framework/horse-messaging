using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Test.Persistency")]

namespace Horse.Messaging.Data.Configuration
{
    public static class ConfigurationFactory
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
        public static DataConfigurationBuilder Builder { get; private set; }

        /// <summary>
        /// Initializes configurations and creates builder and manager
        /// </summary>
        public static void Initialize(DataConfigurationBuilder builder)
        {
            if (Builder != null) throw new InvalidOperationException("Data configurator is already initialized before.");
            Builder = builder;
            Manager = new DataConfigurationManager();
            Configuration = Manager.Load(builder.ConfigFile);
        }

        /// <summary>
        /// Destroys configuration factory options
        /// </summary>
        internal static void Destroy()
        {
            Builder = null;
            Manager = null;
            Configuration = null;
        }
    }
}