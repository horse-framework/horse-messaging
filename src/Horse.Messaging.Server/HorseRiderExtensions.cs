using System;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Cache;
using Horse.Messaging.Server.Channels;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Direct;
using Horse.Messaging.Server.Network;
using Horse.Messaging.Server.Options;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Routing;
using Horse.Server;

namespace Horse.Messaging.Server
{
    /// <summary>
    /// Extension Methods for Horse.Messaging.Server
    /// </summary>
    public static class HorseRiderExtensions
    {
        #region Use Horse

        /// <summary>
        /// Uses Horse.Messaging.Server Messaging Queue server
        /// </summary>
        public static HorseServer UseRider(this HorseServer server, HorseRider horseRider)
        {
            HorseNetworkHandler handler = new HorseNetworkHandler(horseRider);
            horseRider.Server = server;

            horseRider.NodeManager.ConnectionHandler = new NodeConnectionHandler(horseRider.NodeManager, handler);
            server.UseHorseProtocol(handler);

            if (horseRider.NodeManager != null)
                horseRider.NodeManager.SubscribeStartStop(server);

            return server;
        }

        /// <summary>
        /// Uses Horse.Messaging.Server Messaging Queue server
        /// </summary>
        public static HorseRider UseRider(this HorseServer server, Action<HorseRiderBuilder> cfg)
        {
            HorseRider rider = new HorseRider();
            HorseNetworkHandler handler = new HorseNetworkHandler(rider);
            rider.Server = server;

            rider.NodeManager.ConnectionHandler = new NodeConnectionHandler(rider.NodeManager, handler);
            server.UseHorseProtocol(handler);

            if (rider.NodeManager != null)
                rider.NodeManager.SubscribeStartStop(server);

            HorseRiderBuilder builder = new HorseRiderBuilder();
            builder.Rider = rider;

            cfg(builder);
            
            rider.Initialize();
            
            return rider;
        }

        #endregion

        #region Options

        /// <summary>
        /// Sets Horse MQ Options
        /// </summary>
        public static HorseRiderBuilder ConfigureOptions(this HorseRiderBuilder builder, Action<HorseRiderOptions> options)
        {
            if (builder.Rider.Options == null)
                builder.Rider.Options = new HorseRiderOptions();

            options(builder.Rider.Options);
            return builder;
        }

        /// <summary>
        /// Uses Custom Client Id Generator.
        /// Default is DefaultUniqueIdGenerator.
        /// </summary>
        public static HorseRiderBuilder UseClientIdGenerator<TUniqueIdGenerator>(this HorseRiderBuilder builder)
            where TUniqueIdGenerator : IUniqueIdGenerator, new()
        {
            builder.Rider.Client.ClientIdGenerator = new TUniqueIdGenerator();
            return builder;
        }

        /// <summary>
        /// Uses Custom Client Id Generator.
        /// Default is DefaultUniqueIdGenerator.
        /// </summary>
        public static HorseRiderBuilder UseClientIdGenerator(this HorseRiderBuilder builder, IUniqueIdGenerator generator)
        {
            builder.Rider.Client.ClientIdGenerator = generator;
            return builder;
        }

        /// <summary>
        /// Uses Custom Message Id Generator.
        /// Default is DefaultUniqueIdGenerator.
        /// </summary>
        public static HorseRiderBuilder UseMessageIdGenerator<TUniqueIdGenerator>(this HorseRiderBuilder builder)
            where TUniqueIdGenerator : IUniqueIdGenerator, new()
        {
            builder.Rider.MessageIdGenerator = new TUniqueIdGenerator();
            return builder;
        }

        /// <summary>
        /// Uses Custom Message Id Generator.
        /// Default is DefaultUniqueIdGenerator.
        /// </summary>
        public static HorseRiderBuilder UseMessageIdGenerator(this HorseRiderBuilder builder, IUniqueIdGenerator generator)
        {
            builder.Rider.MessageIdGenerator = generator;
            return builder;
        }

        #endregion

        /// <summary>
        /// Configure clients
        /// </summary>
        public static HorseRiderBuilder ConfigureClients(this HorseRiderBuilder builder, Action<HorseClientConfigurator> cfg)
        {
            HorseClientConfigurator configurator = new HorseClientConfigurator(builder.Rider);
            cfg(configurator);
            return builder;
        }

        /// <summary>
        /// Configure cache
        /// </summary>
        public static HorseRiderBuilder ConfigureCache(this HorseRiderBuilder builder, Action<HorseCacheConfigurator> cfg)
        {
            HorseCacheConfigurator configurator = new HorseCacheConfigurator(builder.Rider);
            cfg(configurator);
            return builder;
        }

        /// <summary>
        /// Configure channels
        /// </summary>
        public static HorseRiderBuilder ConfigureChannels(this HorseRiderBuilder builder, Action<HorseChannelConfigurator> cfg)
        {
            HorseChannelConfigurator configurator = new HorseChannelConfigurator(builder.Rider);
            cfg(configurator);
            return builder;
        }

        /// <summary>
        /// Configure routers
        /// </summary>
        public static HorseRiderBuilder ConfigureRouters(this HorseRiderBuilder builder, Action<HorseRouterConfigurator> cfg)
        {
            HorseRouterConfigurator configurator = new HorseRouterConfigurator(builder.Rider);
            cfg(configurator);
            return builder;
        }

        /// <summary>
        /// Configure queues
        /// </summary>
        public static HorseRiderBuilder ConfigureQueues(this HorseRiderBuilder builder, Action<HorseQueueConfigurator> cfg)
        {
            HorseQueueConfigurator configurator = new HorseQueueConfigurator(builder.Rider);
            cfg(configurator);
            return builder;
        }

        /// <summary>
        /// Configure direct messages
        /// </summary>
        public static HorseRiderBuilder ConfigureDirect(this HorseRiderBuilder builder, Action<HorseDirectConfigurator> cfg)
        {
            HorseDirectConfigurator configurator = new HorseDirectConfigurator(builder.Rider);
            cfg(configurator);
            return builder;
        }

        #region Events

        /// <summary>
        /// Adds error handler
        /// </summary>
        public static HorseRiderBuilder AddErrorHandler<TErrorHandler>(this HorseRiderBuilder builder)
            where TErrorHandler : IErrorHandler, new()
        {
            builder.Rider.ErrorHandlers.Add(new TErrorHandler());
            return builder;
        }

        /// <summary>
        /// Adds error handler
        /// </summary>
        public static HorseRiderBuilder AddErrorHandler(this HorseRiderBuilder builder, IErrorHandler errorHandler)
        {
            builder.Rider.ErrorHandlers.Add(errorHandler);
            return builder;
        }

        /// <summary>
        /// Uses server type message event handler
        /// </summary>
        public static HorseRiderBuilder AddServerMessageHandler<TServerMessageHandler>(this HorseRiderBuilder builder)
            where TServerMessageHandler : IServerMessageHandler, new()
        {
            builder.Rider.ServerMessageHandlers.Add(new TServerMessageHandler());
            return builder;
        }

        /// <summary>
        /// Uses a custom server message handler
        /// </summary>
        [Obsolete("This method adds new handler, use AddServerMessageHandler instead")]
        public static HorseRiderBuilder UseServerMessageHandler(this HorseRiderBuilder builder, IServerMessageHandler messageHandler)
        {
            return AddServerMessageHandler(builder, messageHandler);
        }

        /// <summary>
        /// Uses a custom server message handler
        /// </summary>
        public static HorseRiderBuilder AddServerMessageHandler(this HorseRiderBuilder builder, IServerMessageHandler messageHandler)
        {
            builder.Rider.ServerMessageHandlers.Add(messageHandler);
            return builder;
        }

        #endregion
    }
}