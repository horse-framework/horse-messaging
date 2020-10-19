using System;
using System.Threading.Tasks;
using Twino.MQ.Handlers;
using Twino.MQ.Network;
using Twino.MQ.Options;
using Twino.MQ.Security;
using Twino.Protocols.TMQ;
using Twino.Server;

namespace Twino.MQ
{
    /// <summary>
    /// Extension Methods for Twino.MQ
    /// </summary>
    public static class MqExtensions
    {
        #region Use Twino

        /// <summary>
        /// Uses Twino.MQ Messaging Queue server
        /// </summary>
        public static TwinoServer UseTwinoMQ(this TwinoServer server, TwinoMQ twinoMq)
        {
            TmqNetworkHandler handler = new TmqNetworkHandler(twinoMq);
            twinoMq.Server = server;

            twinoMq.NodeManager.ConnectionHandler = new NodeConnectionHandler(twinoMq.NodeManager, handler);
            server.UseTmq(handler);

            if (twinoMq.NodeManager != null)
                twinoMq.NodeManager.SubscribeStartStop(server);

            return server;
        }

        /// <summary>
        /// Uses Twino.MQ Messaging Queue server
        /// </summary>
        public static TwinoMQ UseTwinoMQ(this TwinoServer server, Action<TwinoMqBuilder> cfg)
        {
            TwinoMQ mq = new TwinoMQ();
            TmqNetworkHandler handler = new TmqNetworkHandler(mq);
            mq.Server = server;

            mq.NodeManager.ConnectionHandler = new NodeConnectionHandler(mq.NodeManager, handler);
            server.UseTmq(handler);

            if (mq.NodeManager != null)
                mq.NodeManager.SubscribeStartStop(server);

            TwinoMqBuilder builder = new TwinoMqBuilder();
            builder.Server = mq;

            cfg(builder);
            return mq;
        }

        #endregion

        #region Options

        /// <summary>
        /// Sets Twino MQ Options
        /// </summary>
        public static TwinoMqBuilder AddOptions(this TwinoMqBuilder builder, TwinoMqOptions options)
        {
            builder.Server.Options = options;
            return builder;
        }

        /// <summary>
        /// Sets Twino MQ Options
        /// </summary>
        public static TwinoMqBuilder AddOptions(this TwinoMqBuilder builder, Action<TwinoMqOptions> options)
        {
            if (builder.Server.Options == null)
                builder.Server.Options = new TwinoMqOptions();

            options(builder.Server.Options);
            return builder;
        }

        /// <summary>
        /// Uses Custom Client Id Generator.
        /// Default is DefaultUniqueIdGenerator.
        /// </summary>
        public static TwinoMqBuilder UseClientIdGenerator<TUniqueIdGenerator>(this TwinoMqBuilder builder)
            where TUniqueIdGenerator : IUniqueIdGenerator, new()
        {
            builder.Server.ClientIdGenerator = new TUniqueIdGenerator();
            return builder;
        }

        /// <summary>
        /// Uses Custom Client Id Generator.
        /// Default is DefaultUniqueIdGenerator.
        /// </summary>
        public static TwinoMqBuilder UseClientIdGenerator(this TwinoMqBuilder builder, IUniqueIdGenerator generator)
        {
            builder.Server.ClientIdGenerator = generator;
            return builder;
        }

        /// <summary>
        /// Uses Custom Message Id Generator.
        /// Default is DefaultUniqueIdGenerator.
        /// </summary>
        public static TwinoMqBuilder UseMessageIdGenerator<TUniqueIdGenerator>(this TwinoMqBuilder builder)
            where TUniqueIdGenerator : IUniqueIdGenerator, new()
        {
            builder.Server.MessageIdGenerator = new TUniqueIdGenerator();
            return builder;
        }

        /// <summary>
        /// Uses Custom Message Id Generator.
        /// Default is DefaultUniqueIdGenerator.
        /// </summary>
        public static TwinoMqBuilder UseMessageIdGenerator(this TwinoMqBuilder builder, IUniqueIdGenerator generator)
        {
            builder.Server.MessageIdGenerator = generator;
            return builder;
        }

        #endregion

        #region Auth

        /// <summary>
        /// Uses client authentication
        /// </summary>
        public static TwinoMqBuilder AddClientAuthenticator<TClientAuthenticator>(this TwinoMqBuilder builder)
            where TClientAuthenticator : IClientAuthenticator, new()
        {
            builder.Server.AddClientAuthenticator(new TClientAuthenticator());
            return builder;
        }

        /// <summary>
        /// Uses client authentication
        /// </summary>
        public static TwinoMqBuilder AddClientAuthenticator(this TwinoMqBuilder builder, IClientAuthenticator authenticator)
        {
            builder.Server.AddClientAuthenticator(authenticator);
            return builder;
        }

        /// <summary>
        /// Uses client authorization
        /// </summary>
        public static TwinoMqBuilder AddClientAuthorization<TClientAuthorization>(this TwinoMqBuilder builder)
            where TClientAuthorization : IClientAuthorization, new()
        {
            builder.Server.AddClientAuthorization(new TClientAuthorization());
            return builder;
        }

        /// <summary>
        /// Uses client authorization
        /// </summary>
        public static TwinoMqBuilder AddClientAuthorization(this TwinoMqBuilder builder, IClientAuthorization authorization)
        {
            builder.Server.AddClientAuthorization(authorization);
            return builder;
        }

        /// <summary>
        /// Uses queue authentication
        /// </summary>
        public static TwinoMqBuilder AddQueueAuthentication<TQueueAuthenticator>(this TwinoMqBuilder builder)
            where TQueueAuthenticator : IQueueAuthenticator, new()
        {
            builder.Server.AddQueueAuthenticator(new TQueueAuthenticator());
            return builder;
        }

        /// <summary>
        /// Uses queue authentication
        /// </summary>
        public static TwinoMqBuilder AddQueueAuthentication(this TwinoMqBuilder builder, IQueueAuthenticator authenticator)
        {
            builder.Server.AddQueueAuthenticator(authenticator);
            return builder;
        }

        /// <summary>
        /// Activates admin operations and uses admin authorization
        /// </summary>
        public static TwinoMqBuilder AddAdminAuthorization<TAdminAuthorization>(this TwinoMqBuilder builder)
            where TAdminAuthorization : IAdminAuthorization, new()
        {
            builder.Server.AddAdminAuthorization(new TAdminAuthorization());
            return builder;
        }

        /// <summary>
        /// Activates admin operations and uses admin authorization
        /// </summary>
        public static TwinoMqBuilder AddAdminAuthorization(this TwinoMqBuilder builder, IAdminAuthorization authorization)
        {
            builder.Server.AddAdminAuthorization(authorization);
            return builder;
        }

        #endregion

        #region Events

        /// <summary>
        /// Adds queue event handler
        /// </summary>
        public static TwinoMqBuilder AddQueueEventHandler<TQueueAuthenticator>(this TwinoMqBuilder builder)
            where TQueueAuthenticator : IQueueEventHandler, new()
        {
            builder.Server.AddQueueEventHandler(new TQueueAuthenticator());
            return builder;
        }

        /// <summary>
        /// Adds queue event handler
        /// </summary>
        public static TwinoMqBuilder AddQueueEventHandler(this TwinoMqBuilder builder, IQueueEventHandler queueEventHandler)
        {
            builder.Server.AddQueueEventHandler(queueEventHandler);
            return builder;
        }

        /// <summary>
        /// Adds client event handler
        /// </summary>
        public static TwinoMqBuilder AddClientHandler<TClientHandler>(this TwinoMqBuilder builder)
            where TClientHandler : IClientHandler, new()
        {
            builder.Server.AddClientHandler(new TClientHandler());
            return builder;
        }

        /// <summary>
        /// Adds client event handler
        /// </summary>
        public static TwinoMqBuilder AddClientHandler(this TwinoMqBuilder builder, IClientHandler clientHandler)
        {
            builder.Server.AddClientHandler(clientHandler);
            return builder;
        }

        /// <summary>
        /// Adds error handler
        /// </summary>
        public static TwinoMqBuilder AddErrorHandler<TErrorHandler>(this TwinoMqBuilder builder)
            where TErrorHandler : IErrorHandler, new()
        {
            builder.Server.AddErrorHandler(new TErrorHandler());
            return builder;
        }

        /// <summary>
        /// Adds error handler
        /// </summary>
        public static TwinoMqBuilder AddErrorHandler(this TwinoMqBuilder builder, IErrorHandler errorHandler)
        {
            builder.Server.AddErrorHandler(errorHandler);
            return builder;
        }

        /// <summary>
        /// Uses server type message event handler
        /// </summary>
        public static TwinoMqBuilder AddServerMessageHandler<TServerMessageHandler>(this TwinoMqBuilder builder)
            where TServerMessageHandler : IServerMessageHandler, new()
        {
            builder.Server.AddMessageHandler(new TServerMessageHandler());
            return builder;
        }

        /// <summary>
        /// Uses a custom server message handler
        /// </summary>
        public static TwinoMqBuilder UseServerMessageHandler(this TwinoMqBuilder builder, IServerMessageHandler messageHandler)
        {
            builder.Server.AddMessageHandler(messageHandler);
            return builder;
        }

        #endregion

        #region Delivery

        /// <summary>
        /// Implements a message delivery handler factory
        /// </summary>
        public static TwinoMqBuilder UseDeliveryHandler(this TwinoMqBuilder builder, Func<DeliveryHandlerBuilder, Task<IMessageDeliveryHandler>> deliveryHandler)
        {
            builder.Server.DeliveryHandlerFactory = deliveryHandler;
            return builder;
        }

        /// <summary>
        /// Implements non-durable basic delivery handler
        /// </summary>
        public static TwinoMqBuilder UseJustAllowDeliveryHandler(this TwinoMqBuilder builder)
        {
            builder.Server.DeliveryHandlerFactory = d => Task.FromResult<IMessageDeliveryHandler>(new JustAllowDeliveryHandler());
            return builder;
        }

        /// <summary>
        /// Implements non-durable basic delivery handler with ack
        /// </summary>
        public static TwinoMqBuilder UseSendAckDeliveryHandler(this TwinoMqBuilder builder, AcknowledgeWhen acknowledgeWhen)
        {
            builder.Server.DeliveryHandlerFactory = d => Task.FromResult<IMessageDeliveryHandler>(new SendAckDeliveryHandler(acknowledgeWhen));
            return builder;
        }

        #endregion
    }
}