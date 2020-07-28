using System;
using System.Threading.Tasks;
using Twino.MQ.Queues;

namespace Twino.MQ.Data
{
    internal class DataConfigurationManager
    {
        public DataConfigurationManager(DataConfiguration configuration)
        {
        }
        
        public void Load()
        {
        }

        public void Save()
        {
        }

        public void Add(ChannelQueue queue)
        {
            throw new NotImplementedException();
        }

        public void Remove(ChannelQueue queue)
        {
            throw new NotImplementedException();
        }

        public Task LoadQueues()
        {
            throw new NotImplementedException();
        }
    }
}