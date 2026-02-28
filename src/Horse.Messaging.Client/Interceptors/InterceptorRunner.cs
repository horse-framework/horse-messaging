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
    /// Run before interceptors
    /// </summary>
    /// <param name="message"></param>
    /// <param name="client"></param>
    /// <param name="handlerFactory"></param>
    internal async Task RunBeforeInterceptors(HorseMessage message, HorseClient client,
        IHandlerFactory handlerFactory = null, CancellationToken cancellationToken = default)
    {
        await EvalInterceptors(message, client, handlerFactory, true, cancellationToken);
    }

    /// <summary>
    /// Run after interceptors
    /// </summary>
    /// <param name="message"></param>
    /// <param name="client"></param>
    /// <param name="handlerFactory"></param>
    internal async Task RunAfterInterceptors(HorseMessage message, HorseClient client,
        IHandlerFactory handlerFactory = null, CancellationToken cancellationToken = default)
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