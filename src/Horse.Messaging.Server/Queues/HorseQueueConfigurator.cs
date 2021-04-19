using System;
using System.Threading.Tasks;
using Horse.Messaging.Server.Containers;
using Horse.Messaging.Server.Handlers;
using Horse.Messaging.Server.Options;
using Horse.Messaging.Server.Queues.Delivery;
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
            Rider.Queue.DeliveryHandlerFactory = deliveryHandler;
            return this;
        }

        /// <summary>
        /// Implements non-durable basic delivery handler
        /// </summary>
        public HorseQueueConfigurator UseJustAllowDeliveryHandler()
        {
            Rider.Queue.DeliveryHandlerFactory = _ => Task.FromResult<IMessageDeliveryHandler>(new JustAllowDeliveryHandler());
            return this;
        }

        /// <summary>
        /// Implements non-durable basic delivery handler with ack
        /// </summary>
        /// <param name="builder">Horse MQ Builder</param>
        /// <param name="producerAck">Decision, when producer will receive acknowledge (or confirm)</param>
        /// <param name="consumerAckFail">Decision, what will be done if consumer sends nack or doesn't send ack in time</param>
        public HorseQueueConfigurator UseAckDeliveryHandler(AcknowledgeWhen producerAck, PutBackDecision consumerAckFail)
        {
            Rider.Queue.DeliveryHandlerFactory = _ => Task.FromResult<IMessageDeliveryHandler>(new AckDeliveryHandler(producerAck, consumerAckFail));
            return this;
        }
    }
}