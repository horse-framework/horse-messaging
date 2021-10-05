using System;
using System.Data;
using System.Threading.Tasks;
using Horse.Messaging.Server.Containers;
using Horse.Messaging.Server.Options;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Queues.Handlers;
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
        public HorseQueueConfigurator UseDeliveryHandler(Func<DeliveryHandlerBuilder, Task<IMessageDeliveryHandler>> deliveryHandler)
        {
            return UseDeliveryHandler("Default", deliveryHandler);
        }

        /// <summary>
        /// Implements a message delivery handler factory
        /// </summary>
        public HorseQueueConfigurator UseDeliveryHandler(string name, Func<DeliveryHandlerBuilder, Task<IMessageDeliveryHandler>> deliveryHandler)
        {
            if (Rider.Queue.DeliveryHandlerFactories.ContainsKey(name))
                throw new DuplicateNameException($"There is already registered delivery handler with same name: {name}");

            Rider.Queue.DeliveryHandlerFactories.Add(name, deliveryHandler);

            if (!name.Equals("DEFAULT", StringComparison.InvariantCultureIgnoreCase) && !Rider.Queue.DeliveryHandlerFactories.ContainsKey("DEFAULT"))
                Rider.Queue.DeliveryHandlerFactories.Add("DEFAULT", Rider.Queue.DeliveryHandlerFactories[name]);

            return this;
        }

        /// <summary>
        /// Implements non-durable basic delivery handler
        /// </summary>
        /// <param name="name">Delivery handler name</param>
        public HorseQueueConfigurator UseJustAllowDeliveryHandler(string name = "DEFAULT")
        {
            if (Rider.Queue.DeliveryHandlerFactories.ContainsKey(name))
                throw new DuplicateNameException($"There is already registered Just Allow delivery handler with same name: {name}");

            Rider.Queue.DeliveryHandlerFactories.Add(name, _ => Task.FromResult<IMessageDeliveryHandler>(new JustAllowDeliveryHandler()));

            if (!name.Equals("DEFAULT", StringComparison.InvariantCultureIgnoreCase) && !Rider.Queue.DeliveryHandlerFactories.ContainsKey("DEFAULT"))
                Rider.Queue.DeliveryHandlerFactories.Add("DEFAULT", Rider.Queue.DeliveryHandlerFactories[name]);

            return this;
        }

        /// <summary>
        /// Implements non-durable basic delivery handler with ack
        /// </summary>
        /// <param name="producerAck">Decision, when producer will receive acknowledge (or confirm)</param>
        /// <param name="consumerAckFail">Decision, what will be done if consumer sends nack or doesn't send ack in time</param>
        public HorseQueueConfigurator UseAckDeliveryHandler(CommitWhen producerAck, PutBackDecision consumerAckFail)
        {
            return UseAckDeliveryHandler("Default", producerAck, consumerAckFail);
        }

        /// <summary>
        /// Implements non-durable basic delivery handler with ack
        /// </summary>
        /// <param name="name">Delivery handler name</param>
        /// <param name="producerAck">Decision, when producer will receive acknowledge (or confirm)</param>
        /// <param name="consumerAckFail">Decision, what will be done if consumer sends nack or doesn't send ack in time</param>
        public HorseQueueConfigurator UseAckDeliveryHandler(string name, CommitWhen producerAck, PutBackDecision consumerAckFail)
        {
            if (Rider.Queue.DeliveryHandlerFactories.ContainsKey(name))
                throw new DuplicateNameException($"There is already registered Ack delivery handler with same name: {name}");

            Rider.Queue.DeliveryHandlerFactories.Add(name, _ => Task.FromResult<IMessageDeliveryHandler>(new AckDeliveryHandler(producerAck, consumerAckFail)));

            if (!name.Equals("DEFAULT", StringComparison.InvariantCultureIgnoreCase) && !Rider.Queue.DeliveryHandlerFactories.ContainsKey("DEFAULT"))
                Rider.Queue.DeliveryHandlerFactories.Add("DEFAULT", Rider.Queue.DeliveryHandlerFactories[name]);

            return this;
        }
    }
}