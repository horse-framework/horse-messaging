using System;
using System.Threading.Tasks;

namespace Test.Mq
{
    public class InstanceTest
    {
        /// <summary>
        /// Pushes a message to the queue, consumer receives from other instance
        /// </summary>
        public async Task PushAndReceive()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Pushes a message to the queue, consumer receives from other instance and sends acknowledge
        /// </summary>
        public async Task PushAndReceiveWithAck()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Two clients are connected different instances.
        /// One sends message, other receives
        /// </summary>
        public async Task DirectMessageToClientInOtherInstance()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Two clients are connected different instances.
        /// One sends message, other receives and responses.
        /// </summary>
        public async Task RequestToClientInOtherInstance()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Decision sends to other instance queue
        /// </summary>
        public async Task ApplyDecisionOverNode()
        {
            throw new NotImplementedException();
        }
    }
}