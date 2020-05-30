using System;
using System.Threading.Tasks;
using Twino.MQ.Clients;

namespace Twino.MQ.Events
{
    public class NodeEventManager : EventManager
    {
        public NodeEventManager(string eventName, MqServer server)
            : base(eventName, null, 0)
        {
        }
        
        public async Task Trigger(MqClient client)
        {
            throw new NotImplementedException();
        }
    }
}