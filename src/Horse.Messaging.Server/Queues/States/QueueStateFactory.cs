using System;
using Horse.Messaging.Server.Queues.Store;

namespace Horse.Messaging.Server.Queues.States
{
    internal class QueueStateFactory
    {
        internal static Tuple<IQueueState, IQueueMessageStore> Create(HorseQueue queue, QueueType type)
        {
            IQueueState state = null;
            IQueueMessageStore store = null;

            switch (type)
            {
                case QueueType.Push:
                    state = new PushQueueState(queue);
                    store = new LinkedMessageStore(queue);
                    break;

                case QueueType.RoundRobin:
                    state = new RoundRobinQueueState(queue);
                    store = new LinkedMessageStore(queue);
                    break;

                case QueueType.Pull:
                    state = new PullQueueState(queue);
                    store = new LinkedMessageStore(queue);
                    break;
            }

            return new Tuple<IQueueState, IQueueMessageStore>(state, store);
        }
    }
}