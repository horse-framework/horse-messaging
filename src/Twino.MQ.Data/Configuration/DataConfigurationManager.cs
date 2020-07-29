using System;
using System.Threading.Tasks;
using Twino.MQ.Queues;

namespace Twino.MQ.Data.Configuration
{
    internal class DataConfigurationManager
    {
        /// <summary>
        /// Loads configurations
        /// </summary>
        public DataConfiguration Load()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Saves current configurations
        /// </summary>
        public void Save()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Adds new queue into configurations
        /// </summary>
        public void Add(ChannelQueue queue)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Removes queue from configurations
        /// </summary>
        public void Remove(ChannelQueue queue)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Loads messages of queues in configuration
        /// </summary>
        public Task LoadQueues(MqServer server)
        {
            throw new NotImplementedException();
        }
    }
}