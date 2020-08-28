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
            NetworkMessageHandler handler = new NetworkMessageHandler(twinoMq);
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
            NetworkMessageHandler handler = new NetworkMessageHandler(mq);
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
        public static TwinoMqBuilder UseAuthentication<TClientAuthenticator>(this TwinoMqBuilder builder)
            where TClientAuthenticator : IClientAuthenticator, new()
        {
            builder.Server.Authenticator = new TClientAuthenticator();
            return builder;
        }

        /// <summary>
        /// Uses client authentication
        /// </summary>
        public static TwinoMqBuilder UseAuthentication(this TwinoMqBuilder builder, IClientAuthenticator authenticator)
        {
            builder.Server.Authenticator = authenticator;
            return builder;
        }

        /// <summary>
        /// Uses client authorization
        /// </summary>
        public static TwinoMqBuilder UseAuthorization<TClientAuthorization>(this TwinoMqBuilder builder)
            where TClientAuthorization : IClientAuthorization, new()
        {
            builder.Server.Authorization = new TClientAuthorization();
            return builder;
        }

        /// <summary>
        /// Uses client authorization
        /// </summary>
        public static TwinoMqBuilder UseAuthorization(this TwinoMqBuilder builder, IClientAuthorization authorization)
        {
            builder.Server.Authorization = authorization;
            return builder;
        }

        /// <summary>
        /// Uses queue authentication
        /// </summary>
        public static TwinoMqBuilder UseQueueAuthentication<TQueueAuthenticator>(this TwinoMqBuilder builder)
            where TQueueAuthenticator : IQueueAuthenticator, new()
        {
            builder.Server.QueueAuthenticator = new TQueueAuthenticator();
            return builder;
        }

        /// <summary>
        /// Uses queue authentication
        /// </summary>
        public static TwinoMqBuilder UseQueueAuthentication(this TwinoMqBuilder builder, IQueueAuthenticator authenticator)
        {
            builder.Server.QueueAuthenticator = authenticator;
            return builder;
        }

        /// <summary>
        /// Activates admin operations and uses admin authorization
        /// </summary>
        public static TwinoMqBuilder UseAdminAuthorization<TAdminAuthorization>(this TwinoMqBuilder builder)
            where TAdminAuthorization : IAdminAuthorization, new()
        {
            builder.Server.AdminAuthorization = new TAdminAuthorization();
            return builder;
        }

        /// <summary>
        /// Activates admin operations and uses admin authorization
        /// </summary>
        public static TwinoMqBuilder UseAdminAuthorization(this TwinoMqBuilder builder, IAdminAuthorization authorization)
        {
            builder.Server.AdminAuthorization = authorization;
            return builder;
        }

        #endregion

        #region Events

        /// <summary>
        /// Uses queue event handler
        /// </summary>
        public static TwinoMqBuilder UseQueueEventHandler<TQueueAuthenticator>(this TwinoMqBuilder builder)
            where TQueueAuthenticator : IQueueEventHandler, new()
        {
            builder.Server.QueueEventHandler = new TQueueAuthenticator();
            return builder;
        }

        /// <summary>
        /// Uses queue event handler
        /// </summary>
        public static TwinoMqBuilder UseQueueEventHandler(this TwinoMqBuilder builder, IQueueEventHandler queueEventHandler)
        {
            builder.Server.QueueEventHandler = queueEventHandler;
            return builder;
        }

        /// <summary>
        /// Uses client event handler
        /// </summary>
        public static TwinoMqBuilder UseClientHandler<TClientHandler>(this TwinoMqBuilder builder)
            where TClientHandler : IClientHandler, new()
        {
            builder.Server.ClientHandler = new TClientHandler();
            return builder;
        }

        /// <summary>
        /// Uses client event handler
        /// </summary>
        public static TwinoMqBuilder UseClientHandler(this TwinoMqBuilder builder, IClientHandler clientHandler)
        {
            builder.Server.ClientHandler = clientHandler;
            return builder;
        }

        /// <summary>
        /// Uses server type message event handler
        /// </summary>
        public static TwinoMqBuilder UseServerMessageHandler<TServerMessageHandler>(this TwinoMqBuilder builder)
            where TServerMessageHandler : IServerMessageHandler, new()
        {
            builder.Server.ServerMessageHandler = new TServerMessageHandler();
            return builder;
        }

        /// <summary>
        /// Uses a custom server message handler
        /// </summary>
        public static TwinoMqBuilder UseServerMessageHandler(this TwinoMqBuilder builder, IServerMessageHandler messageHandler)
        {
            builder.Server.ServerMessageHandler = messageHandler;
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