using System;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Handlers;
using Horse.Messaging.Server.Network;
using Horse.Messaging.Server.Options;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Messaging.Server.Security;
using Horse.Server;

namespace Horse.Messaging.Server
{
    /// <summary>
    /// Extension Methods for Horse.Messaging.Server
    /// </summary>
    public static class MqExtensions
    {
        #region Use Horse

        /// <summary>
        /// Uses Horse.Messaging.Server Messaging Queue server
        /// </summary>
        public static HorseServer UseHorseMq(this HorseServer server, HorseMq horseMq)
        {
            HmqNetworkHandler handler = new HmqNetworkHandler(horseMq);
            horseMq.Server = server;

            horseMq.NodeManager.ConnectionHandler = new NodeConnectionHandler(horseMq.NodeManager, handler);
            server.UseHmq(handler);

            if (horseMq.NodeManager != null)
                horseMq.NodeManager.SubscribeStartStop(server);

            return server;
        }

        /// <summary>
        /// Uses Horse.Messaging.Server Messaging Queue server
        /// </summary>
        public static HorseMq UseHorseMq(this HorseServer server, Action<HorseMqBuilder> cfg)
        {
            HorseMq mq = new HorseMq();
            HmqNetworkHandler handler = new HmqNetworkHandler(mq);
            mq.Server = server;

            mq.NodeManager.ConnectionHandler = new NodeConnectionHandler(mq.NodeManager, handler);
            server.UseHmq(handler);

            if (mq.NodeManager != null)
                mq.NodeManager.SubscribeStartStop(server);

            HorseMqBuilder builder = new HorseMqBuilder();
            builder.Server = mq;

            cfg(builder);
            return mq;
        }

        #endregion

        #region Options

        /// <summary>
        /// Sets Horse MQ Options
        /// </summary>
        public static HorseMqBuilder AddOptions(this HorseMqBuilder builder, HorseMqOptions options)
        {
            builder.Server.Options = options;
            return builder;
        }

        /// <summary>
        /// Sets Horse MQ Options
        /// </summary>
        public static HorseMqBuilder AddOptions(this HorseMqBuilder builder, Action<HorseMqOptions> options)
        {
            if (builder.Server.Options == null)
                builder.Server.Options = new HorseMqOptions();

            options(builder.Server.Options);
            return builder;
        }

        /// <summary>
        /// Uses Custom Client Id Generator.
        /// Default is DefaultUniqueIdGenerator.
        /// </summary>
        public static HorseMqBuilder UseClientIdGenerator<TUniqueIdGenerator>(this HorseMqBuilder builder)
            where TUniqueIdGenerator : IUniqueIdGenerator, new()
        {
            builder.Server.ClientIdGenerator = new TUniqueIdGenerator();
            return builder;
        }

        /// <summary>
        /// Uses Custom Client Id Generator.
        /// Default is DefaultUniqueIdGenerator.
        /// </summary>
        public static HorseMqBuilder UseClientIdGenerator(this HorseMqBuilder builder, IUniqueIdGenerator generator)
        {
            builder.Server.ClientIdGenerator = generator;
            return builder;
        }

        /// <summary>
        /// Uses Custom Message Id Generator.
        /// Default is DefaultUniqueIdGenerator.
        /// </summary>
        public static HorseMqBuilder UseMessageIdGenerator<TUniqueIdGenerator>(this HorseMqBuilder builder)
            where TUniqueIdGenerator : IUniqueIdGenerator, new()
        {
            builder.Server.MessageIdGenerator = new TUniqueIdGenerator();
            return builder;
        }

        /// <summary>
        /// Uses Custom Message Id Generator.
        /// Default is DefaultUniqueIdGenerator.
        /// </summary>
        public static HorseMqBuilder UseMessageIdGenerator(this HorseMqBuilder builder, IUniqueIdGenerator generator)
        {
            builder.Server.MessageIdGenerator = generator;
            return builder;
        }

        #endregion

        #region Auth

        /// <summary>
        /// Uses client authentication
        /// </summary>
        public static HorseMqBuilder AddClientAuthenticator<TClientAuthenticator>(this HorseMqBuilder builder)
            where TClientAuthenticator : IClientAuthenticator, new()
        {
            builder.Server.AddClientAuthenticator(new TClientAuthenticator());
            return builder;
        }

        /// <summary>
        /// Uses client authentication
        /// </summary>
        public static HorseMqBuilder AddClientAuthenticator(this HorseMqBuilder builder, IClientAuthenticator authenticator)
        {
            builder.Server.AddClientAuthenticator(authenticator);
            return builder;
        }

        /// <summary>
        /// Uses client authorization
        /// </summary>
        public static HorseMqBuilder AddClientAuthorization<TClientAuthorization>(this HorseMqBuilder builder)
            where TClientAuthorization : IClientAuthorization, new()
        {
            builder.Server.AddClientAuthorization(new TClientAuthorization());
            return builder;
        }

        /// <summary>
        /// Uses client authorization
        /// </summary>
        public static HorseMqBuilder AddClientAuthorization(this HorseMqBuilder builder, IClientAuthorization authorization)
        {
            builder.Server.AddClientAuthorization(authorization);
            return builder;
        }

        /// <summary>
        /// Uses queue authentication
        /// </summary>
        public static HorseMqBuilder AddQueueAuthentication<TQueueAuthenticator>(this HorseMqBuilder builder)
            where TQueueAuthenticator : IQueueAuthenticator, new()
        {
            builder.Server.AddQueueAuthenticator(new TQueueAuthenticator());
            return builder;
        }

        /// <summary>
        /// Uses queue authentication
        /// </summary>
        public static HorseMqBuilder AddQueueAuthentication(this HorseMqBuilder builder, IQueueAuthenticator authenticator)
        {
            builder.Server.AddQueueAuthenticator(authenticator);
            return builder;
        }

        /// <summary>
        /// Activates admin operations and uses admin authorization
        /// </summary>
        public static HorseMqBuilder AddAdminAuthorization<TAdminAuthorization>(this HorseMqBuilder builder)
            where TAdminAuthorization : IAdminAuthorization, new()
        {
            builder.Server.AddAdminAuthorization(new TAdminAuthorization());
            return builder;
        }

        /// <summary>
        /// Activates admin operations and uses admin authorization
        /// </summary>
        public static HorseMqBuilder AddAdminAuthorization(this HorseMqBuilder builder, IAdminAuthorization authorization)
        {
            builder.Server.AddAdminAuthorization(authorization);
            return builder;
        }

        #endregion

        #region Events

        /// <summary>
        /// Adds queue event handler
        /// </summary>
        public static HorseMqBuilder AddQueueEventHandler<TQueueAuthenticator>(this HorseMqBuilder builder)
            where TQueueAuthenticator : IQueueEventHandler, new()
        {
            builder.Server.AddQueueEventHandler(new TQueueAuthenticator());
            return builder;
        }

        /// <summary>
        /// Adds queue event handler
        /// </summary>
        public static HorseMqBuilder AddQueueEventHandler(this HorseMqBuilder builder, IQueueEventHandler queueEventHandler)
        {
            builder.Server.AddQueueEventHandler(queueEventHandler);
            return builder;
        }

        /// <summary>
        /// Adds client event handler
        /// </summary>
        public static HorseMqBuilder AddClientHandler<TClientHandler>(this HorseMqBuilder builder)
            where TClientHandler : IClientHandler, new()
        {
            builder.Server.AddClientHandler(new TClientHandler());
            return builder;
        }

        /// <summary>
        /// Adds client event handler
        /// </summary>
        public static HorseMqBuilder AddClientHandler(this HorseMqBuilder builder, IClientHandler clientHandler)
        {
            builder.Server.AddClientHandler(clientHandler);
            return builder;
        }

        /// <summary>
        /// Adds error handler
        /// </summary>
        public static HorseMqBuilder AddErrorHandler<TErrorHandler>(this HorseMqBuilder builder)
            where TErrorHandler : IErrorHandler, new()
        {
            builder.Server.AddErrorHandler(new TErrorHandler());
            return builder;
        }

        /// <summary>
        /// Adds error handler
        /// </summary>
        public static HorseMqBuilder AddErrorHandler(this HorseMqBuilder builder, IErrorHandler errorHandler)
        {
            builder.Server.AddErrorHandler(errorHandler);
            return builder;
        }

        /// <summary>
        /// Uses server type message event handler
        /// </summary>
        public static HorseMqBuilder AddServerMessageHandler<TServerMessageHandler>(this HorseMqBuilder builder)
            where TServerMessageHandler : IServerMessageHandler, new()
        {
            builder.Server.AddMessageHandler(new TServerMessageHandler());
            return builder;
        }

        /// <summary>
        /// Uses a custom server message handler
        /// </summary>
        [Obsolete("This method adds new handler, use AddServerMessageHandler instead")]
        public static HorseMqBuilder UseServerMessageHandler(this HorseMqBuilder builder, IServerMessageHandler messageHandler)
        {
            return AddServerMessageHandler(builder, messageHandler);
        }
        
        /// <summary>
        /// Uses a custom server message handler
        /// </summary>
        public static HorseMqBuilder AddServerMessageHandler(this HorseMqBuilder builder, IServerMessageHandler messageHandler)
        {
            builder.Server.AddMessageHandler(messageHandler);
            return builder;
        }

        #endregion

        #region Delivery

        /// <summary>
        /// Implements a message delivery handler factory
        /// </summary>
        public static HorseMqBuilder UseDeliveryHandler(this HorseMqBuilder builder, Func<DeliveryHandlerBuilder, Task<IMessageDeliveryHandler>> deliveryHandler)
        {
            builder.Server.DeliveryHandlerFactory = deliveryHandler;
            return builder;
        }

        /// <summary>
        /// Implements non-durable basic delivery handler
        /// </summary>
        public static HorseMqBuilder UseJustAllowDeliveryHandler(this HorseMqBuilder builder)
        {
            builder.Server.DeliveryHandlerFactory = d => Task.FromResult<IMessageDeliveryHandler>(new JustAllowDeliveryHandler());
            return builder;
        }

        /// <summary>
        /// Implements non-durable basic delivery handler with ack
        /// </summary>
        /// <param name="builder">Horse MQ Builder</param>
        /// <param name="producerAck">Decision, when producer will receive acknowledge (or confirm)</param>
        /// <param name="consumerAckFail">Decision, what will be done if consumer sends nack or doesn't send ack in time</param>
        public static HorseMqBuilder UseAckDeliveryHandler(this HorseMqBuilder builder, AcknowledgeWhen producerAck, PutBackDecision consumerAckFail)
        {
            builder.Server.DeliveryHandlerFactory = d => Task.FromResult<IMessageDeliveryHandler>(new AckDeliveryHandler(producerAck, consumerAckFail));
            return builder;
        }

        #endregion
    }
}