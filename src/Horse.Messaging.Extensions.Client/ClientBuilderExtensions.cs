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
    /// <summary>
    /// Uses graceful shutdown via IHostedService.
    /// </summary>
    /// <param name="builder">Horse Client Builder</param>
    /// <param name="minWait">Minimum wait time. The host will wait at least min wait time, even everything is done</param>
    /// <param name="maxWait">Maximum wait time. If consumers still trying to consume the message they received before graceful called, their operations will be canceled.</param>
    /// <returns></returns>
    public static HorseClientBuilder UseGracefulShutdownHostedService(this HorseClientBuilder builder, TimeSpan minWait, TimeSpan maxWait)
    {
        IServiceCollection collection = builder.Services;
        collection.AddHostedService(prov => new GracefulShutdownService(prov, minWait, maxWait, null));
        return builder;
    }

    /// <summary>
    /// Uses graceful shutdown via IHostedService.
    /// </summary>
    /// <param name="builder">Horse Client Builder</param>
    /// <param name="minWait">Minimum wait time. The host will wait at least min wait time, even everything is done</param>
    /// <param name="maxWait">Maximum wait time. If consumers still trying to consume the message they received before graceful called, their operations will be canceled.</param>
    /// <param name="shuttingDownAction">
    /// That method is called before start to wait for graceful shutdown. It's recommended to change your system status as shutting down in that method.
    /// </param>
    /// <returns></returns>
    public static HorseClientBuilder UseGracefulShutdownHostedService(this HorseClientBuilder builder, TimeSpan minWait, TimeSpan maxWait, Func<Task> shuttingDownAction)
    {
        IServiceCollection collection = builder.Services;
        collection.AddHostedService(prov => new GracefulShutdownService(prov, minWait, maxWait, null));
        return builder;
    }
}