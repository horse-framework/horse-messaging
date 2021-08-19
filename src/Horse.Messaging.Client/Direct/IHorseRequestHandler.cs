using System;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Direct
{
    /// <summary>
    /// Handles Horse Requests
    /// </summary>
    /// <typeparam name="TRequest">Request Model</typeparam>
    /// <typeparam name="TResponse">Response Model</typeparam>
    public interface IHorseRequestHandler<in TRequest, TResponse>
    {
        /// <summary>
        /// Handles JSON Horse request and sends JSON response
        /// </summary>
        /// <param name="request">Request model</param>
        /// <param name="rawMessage">Raw Horse messsage</param>
        /// <param name="client">Horse MQ connection client</param>
        /// <returns></returns>
        Task<TResponse> Handle(TRequest request, HorseMessage rawMessage, HorseClient client);

        /// <summary>
        /// Executed if an exception is thrown in Handle method
        /// </summary>
        /// <param name="exception">Thrown exception</param>
        /// <param name="request">Request model</param>
        /// <param name="rawMessage">Raw Horse messsage</param>
        /// <param name="client">Horse MQ connection client</param>
        /// <returns></returns>
        Task<ErrorResponse> OnError(Exception exception, TRequest request, HorseMessage rawMessage, HorseClient client);
    }
}