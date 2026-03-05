using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Interceptors;

internal sealed class InterceptorRunner(List<InterceptorTypeDescriptor> descriptors)
{
    /// <summary>
    /// Runs before interceptors.
    /// </summary>
    /// <param name="message">The Horse message.</param>
    /// <param name="client">The Horse client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal Task RunBeforeInterceptors(HorseMessage message, HorseClient client, CancellationToken cancellationToken)
    {
        return RunBeforeInterceptors(message, client, null, cancellationToken);
    }

    internal async Task RunBeforeInterceptors(HorseMessage message, HorseClient client,
        IHandlerFactory handlerFactory, CancellationToken cancellationToken)
    {
        await EvalInterceptors(message, client, handlerFactory, true, cancellationToken);
    }

    internal Task RunAfterInterceptors(HorseMessage message, HorseClient client, CancellationToken cancellationToken)
    {
        return RunAfterInterceptors(message, client, null, cancellationToken);
    }

    internal async Task RunAfterInterceptors(HorseMessage message, HorseClient client,
        IHandlerFactory handlerFactory, CancellationToken cancellationToken)
    {
        await EvalInterceptors(message, client, handlerFactory, false, cancellationToken);
    }

    private async Task EvalInterceptors(HorseMessage message, HorseClient client,
        IHandlerFactory handlerFactory, bool runBefore, CancellationToken cancellationToken)
    {
        if (descriptors.Count == 0) return;
        IEnumerable<InterceptorTypeDescriptor> decriptors = descriptors.Where(m => m.RunBefore == runBefore);
        IEnumerable<IHorseInterceptor> interceptors = handlerFactory is null
            ? decriptors.Select(m => m.Instance)
            : decriptors.Select(m => handlerFactory.CreateInterceptor(m.InterceptorType));
        foreach (IHorseInterceptor interceptor in interceptors.Where(m => m is not null))
            try
            {
                await interceptor.Intercept(message, client, cancellationToken);
            }
            catch (Exception e)
            {
                client.OnException(e, message);
            }
    }
}