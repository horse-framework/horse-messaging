using System;

namespace Twino.MQ.Data
{
    internal static class ConfigurationFactory
    {
        internal static DataConfiguration Configuration { get; private set; }

        internal static DataConfigurationManager Manager { get; private set; }

        internal static DataConfigurationBuilder Builder { get; private set; }

        public static void Initialize(DataConfigurationBuilder builder)
        {
            if (Builder != null)
                throw new InvalidOperationException("Data configuration is already initialized");
            
            Builder = builder;
            Manager = new DataConfigurationManager();
            Configuration = Manager.Load();
        }
    }
}