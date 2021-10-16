using System;
using System.Data;
using System.Threading.Tasks;
using Horse.Messaging.Server.Containers;
using Horse.Messaging.Server.Queues.Managers;
using Horse.Messaging.Server.Security;

namespace Horse.Messaging.Server.Queues
{
    /// <summary>
    /// Queue Rider configurator
    /// </summary>
    public class HorseQueueConfigurator
    {
        /// <summary>
        /// Queue event handlers
        /// </summary>
        public ArrayContainer<IQueueEventHandler> EventHandlers => Rider.Queue.EventHandlers;

        /// <summary>
        /// Queue authenticators
        /// </summary>
        public ArrayContainer<IQueueAuthenticator> Authenticators => Rider.Queue.Authenticators;

        /// <summary>
        /// Event handlers to track queue message events
        /// </summary>
        public ArrayContainer<IQueueMessageEventHandler> MessageHandlers => Rider.Queue.MessageHandlers;

        /// <summary>
        /// Default queue options
        /// </summary>
        public QueueOptions Options => Rider.Queue.Options;

        /// <summary>
        /// Horse rider
        /// </summary>
        public HorseRider Rider { get; }

        internal HorseQueueConfigurator(HorseRider rider)
        {
            Rider = rider;
        }

        /// <summary>
        /// Implements a message delivery handler factory
        /// </summary>
        public HorseQueueConfigurator UseDeliveryHandler(Func<QueueManagerBuilder, Task<IHorseQueueManager>> queueManager)
        {
            return UseCustomQueueManager("Default", queueManager);
        }

        /// <summary>
        /// Implements a message delivery handler factory
        /// </summary>
        public HorseQueueConfigurator UseCustomQueueManager(string name, Func<QueueManagerBuilder, Task<IHorseQueueManager>> queueManager)
        {
            if (Rider.Queue.QueueManagerFactories.ContainsKey(name))
                throw new DuplicateNameException($"There is already registered delivery handler with same name: {name}");

            Rider.Queue.QueueManagerFactories.Add(name, queueManager);

            if (!name.Equals("DEFAULT", StringComparison.InvariantCultureIgnoreCase) && !Rider.Queue.QueueManagerFactories.ContainsKey("DEFAULT"))
                Rider.Queue.QueueManagerFactories.Add("DEFAULT", Rider.Queue.QueueManagerFactories[name]);

            return this;
        }

        /// <summary>
        /// Implements non-durable basic delivery handler with ack
        /// </summary>
        /// <param name="config">Queue configurator action right after manager creation</param>
        public HorseQueueConfigurator UseMemoryQueues(Action<HorseQueue> config = null)
        {
            return UseMemoryQueues("Default", config);
        }

        /// <summary>
        /// Implements non-durable basic delivery handler with ack
        /// </summary>
        /// <param name="name">Delivery handler name</param>
        /// <param name="config">Queue configurator action right after manager creation</param>
        public HorseQueueConfigurator UseMemoryQueues(string name, Action<HorseQueue> config = null)
        {
            if (Rider.Queue.QueueManagerFactories.ContainsKey(name))
                throw new DuplicateNameException($"There is already registered Ack delivery handler with same name: {name}");

            Rider.Queue.QueueManagerFactories.Add(name, b =>
            {
                MemoryQueueManager queueManager = new MemoryQueueManager(b.Queue);
                b.Queue.Manager = queueManager;
                config?.Invoke(b.Queue);
                return Task.FromResult<IHorseQueueManager>(queueManager);
            });

            if (!name.Equals("DEFAULT", StringComparison.InvariantCultureIgnoreCase) && !Rider.Queue.QueueManagerFactories.ContainsKey("DEFAULT"))
                Rider.Queue.QueueManagerFactories.Add("DEFAULT", Rider.Queue.QueueManagerFactories[name]);

            return this;
        }
    }
}