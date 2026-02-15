using System;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Horse.Messaging.Extensions.Client;

/// <summary>
/// Horse Client Builder Hosted Service Extensions
/// </summary>
public static class ClientBuilderExtensions
{
    /// <param name="builder">Horse Client Builder</param>
    extension(HorseClientBuilder builder)
    {
        /// <summary>
        /// With this method queues and channels will be unsubscribed when the host is stopping.
        /// The host will wait until all consume operations are completed or max wait time is elapsed.
        /// It's recommended to use that method for graceful shutdown of your host.
        /// </summary>
        /// <param name="minWait">Minimum wait time. The host will wait at least min wait time, even everything is done</param>
        /// <param name="maxWait">Maximum wait time. If consumers still trying to consume the message they received before graceful called, their operations will be canceled.</param>
        /// <returns></returns>
        public HorseClientBuilder WithGracefulShutdown(TimeSpan minWait, TimeSpan maxWait)
        {
            builder.Services.AddHostedService(prov => new GracefulShutdownService(prov, minWait, maxWait, null));
            return builder;
        }

        /// <summary>
        /// With this method queues and channels will be unsubscribed when the host is stopping.
        /// The host will wait until all consume operations are completed or max wait time is elapsed.
        /// It's recommended to use that method for graceful shutdown of your host.
        /// </summary>
        /// <param name="minWait">Minimum wait time. The host will wait at least min wait time, even everything is done</param>
        /// <param name="maxWait">Maximum wait time. If consumers still trying to consume the message they received before graceful called, their operations will be canceled.</param>
        /// <param name="shuttingDownAction">
        /// That method is called before start to wait for graceful shutdown. It's recommended to change your system status as shutting down in that method.
        /// </param>
        /// <returns></returns>
        public HorseClientBuilder WithGracefulShutdown(TimeSpan minWait, TimeSpan maxWait, Func<IServiceProvider, Task> shuttingDownAction)
        {
            builder.Services.AddHostedService(prov => new GracefulShutdownService(prov, minWait, maxWait, shuttingDownAction));
            return builder;
        }
    }
}