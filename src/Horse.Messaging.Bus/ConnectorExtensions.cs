using System;
using System.Collections.Generic;
using Horse.Messaging.Bus.Internal;
using Horse.Mq.Client.Connectors;
using Microsoft.Extensions.DependencyInjection;

namespace Horse.Messaging.Bus
{
    /// <summary>
    /// Horse Connector implementations
    /// </summary>
    public static class HorseConnectorExtensions
    {
        /// <summary>
        /// Adds Horse connector with configuration
        /// </summary>
        public static IServiceCollection AddHorseBus(this IServiceCollection services, Action<HorseConnectorBuilder> config)
        {
            HorseConnectorBuilder builder = new HorseConnectorBuilder(services);
            config(builder);

            HmqStickyConnector connector = builder.Build();

            AddConsumersMicrosoftDI(services, connector, builder);
            services.AddSingleton(connector);
            services.AddSingleton(connector.Bus);
            services.AddSingleton(connector.Bus.Direct);
            services.AddSingleton(connector.Bus.Queue);
            services.AddSingleton(connector.Bus.Route);

            return services;
        }

        /// <summary>
        /// Adds Horse connector with configuration
        /// </summary>
        public static IServiceCollection AddHorseBus<TIdentifier>(this IServiceCollection services, Action<HorseConnectorBuilder> config)
        {
            HorseConnectorBuilder builder = new HorseConnectorBuilder(services);
            config(builder);

            HmqStickyConnector<TIdentifier> connector = builder.Build<TIdentifier>();

            AddConsumersMicrosoftDI(services, connector, builder);
            services.AddSingleton(connector);

            HorseBus<TIdentifier> bus = (HorseBus<TIdentifier>) connector.Bus;
            services.AddSingleton<IHorseBus<TIdentifier>>(bus);
            services.AddSingleton<IHorseDirectBus<TIdentifier>>((HorseDirectBus<TIdentifier>) bus.Direct);
            services.AddSingleton<IHorseQueueBus<TIdentifier>>((HorseQueueBus<TIdentifier>) bus.Queue);
            services.AddSingleton<IHorseRouteBus<TIdentifier>>((HorseRouteBus<TIdentifier>) bus.Route);

            return services;
        }

        private static void AddConsumersMicrosoftDI(IServiceCollection services, HmqStickyConnector connector, HorseConnectorBuilder builder)
        {
            foreach (Tuple<ServiceLifetime, Type> pair in builder.AssembyConsumers)
            {
                IEnumerable<Type> types = connector.Observer
                                                   .RegisterAssemblyConsumers(() => new MicrosoftDependencyConsumerFactory(pair.Item1),
                                                                              pair.Item2);

                foreach (Type type in types)
                    AddConsumerIntoCollection(services, pair.Item1, type);
            }

            foreach (Tuple<ServiceLifetime, Type> pair in builder.IndividualConsumers)
            {
                connector.Observer.RegisterConsumer(pair.Item2, () => new MicrosoftDependencyConsumerFactory(pair.Item1));
                AddConsumerIntoCollection(services, pair.Item1, pair.Item2);
            }
        }

        private static void AddConsumerIntoCollection(IServiceCollection services, ServiceLifetime lifetime, Type consumerType)
        {
            switch (lifetime)
            {
                case ServiceLifetime.Transient:
                    services.AddTransient(consumerType, consumerType);
                    break;

                case ServiceLifetime.Scoped:
                    services.AddScoped(consumerType, consumerType);
                    break;

                case ServiceLifetime.Singleton:
                    services.AddSingleton(consumerType, consumerType);
                    break;
            }
        }

        /// <summary>
        /// Uses horse bus and connects to the server
        /// </summary>
        public static IServiceProvider UseHorseBus(this IServiceProvider provider)
        {
            MicrosoftDependencyConsumerFactory.Provider = provider;
            HmqStickyConnector connector = provider.GetService<HmqStickyConnector>();
            connector.Run();
            return provider;
        }

        /// <summary>
        /// Uses horse bus and connects to the server
        /// </summary>
        public static IServiceProvider UseHorseBus<TIdentifier>(this IServiceProvider provider)
        {
            MicrosoftDependencyConsumerFactory.Provider = provider;
            HmqStickyConnector<TIdentifier> connector = provider.GetService<HmqStickyConnector<TIdentifier>>();
            connector.Run();
            return provider;
        }
    }
}