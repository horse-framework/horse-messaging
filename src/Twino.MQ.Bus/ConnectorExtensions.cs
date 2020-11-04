using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Twino.MQ.Bus.Internal;
using Twino.MQ.Client.Connectors;

namespace Twino.MQ.Bus
{
    /// <summary>
    /// Twino Connector implementations
    /// </summary>
    public static class TwinoIocConnectorExtensions
    {
        /// <summary>
        /// Adds Twino connector with configuration
        /// </summary>
        public static IServiceCollection AddTwinoBus(this IServiceCollection services, Action<TwinoConnectorBuilder> config)
        {
            TwinoConnectorBuilder builder = new TwinoConnectorBuilder(services);
            config(builder);

            TmqStickyConnector connector = builder.Build();

            AddConsumersMicrosoftDI(services, connector, builder);
            services.AddSingleton(connector);
            services.AddSingleton(connector.Bus);
            services.AddSingleton(connector.Bus.Direct);
            services.AddSingleton(connector.Bus.Queue);
            services.AddSingleton(connector.Bus.Route);

            return services;
        }

        /// <summary>
        /// Adds Twino connector with configuration
        /// </summary>
        public static IServiceCollection AddTwinoBus<TIdentifier>(this IServiceCollection services, Action<TwinoConnectorBuilder> config)
        {
            TwinoConnectorBuilder builder = new TwinoConnectorBuilder(services);
            config(builder);

            TmqStickyConnector<TIdentifier> connector = builder.Build<TIdentifier>();

            AddConsumersMicrosoftDI(services, connector, builder);
            services.AddSingleton(connector);

            TwinoBus<TIdentifier> bus = (TwinoBus<TIdentifier>) connector.Bus;
            services.AddSingleton<ITwinoBus<TIdentifier>>(bus);
            services.AddSingleton<ITwinoDirectBus<TIdentifier>>((TwinoDirectBus<TIdentifier>) bus.Direct);
            services.AddSingleton<ITwinoQueueBus<TIdentifier>>((TwinoQueueBus<TIdentifier>) bus.Queue);
            services.AddSingleton<ITwinoRouteBus<TIdentifier>>((TwinoRouteBus<TIdentifier>) bus.Route);

            return services;
        }

        private static void AddConsumersMicrosoftDI(IServiceCollection services, TmqStickyConnector connector, TwinoConnectorBuilder builder)
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
        /// Uses twino bus and connects to the server
        /// </summary>
        public static IServiceProvider UseTwinoBus(this IServiceProvider provider)
        {
            MicrosoftDependencyConsumerFactory.Provider = provider;
            TmqStickyConnector connector = provider.GetService<TmqStickyConnector>();
            connector.Run();
            return provider;
        }

        /// <summary>
        /// Uses twino bus and connects to the server
        /// </summary>
        public static IServiceProvider UseTwinoBus<TIdentifier>(this IServiceProvider provider)
        {
            MicrosoftDependencyConsumerFactory.Provider = provider;
            TmqStickyConnector<TIdentifier> connector = provider.GetService<TmqStickyConnector<TIdentifier>>();
            connector.Run();
            return provider;
        }
    }
}