using System;
using System.Threading.Tasks;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ
{
    /// <summary>
    /// Handles Twino Requests
    /// </summary>
    /// <typeparam name="TRequest">Request Model</typeparam>
    /// <typeparam name="TResponse">Response Model</typeparam>
    public interface ITwinoRequestHandler<in TRequest, TResponse>
    {
        /// <summary>
        /// Handles JSON TMQ request and sends JSON response
        /// </summary>
        /// <param name="request">Request model</param>
        /// <param name="rawMessage">Raw TMQ messsage</param>
        /// <param name="client">Twino MQ connection client</param>
        /// <returns></returns>
        Task<TResponse> Handle(TRequest request, TwinoMessage rawMessage, TmqClient client);

        /// <summary>
        /// Executed if an exception is thrown in Handle method
        /// </summary>
        /// <param name="exception">Thrown exception</param>
        /// <param name="request">Request model</param>
        /// <param name="rawMessage">Raw TMQ messsage</param>
        /// <param name="client">Twino MQ connection client</param>
        /// <returns></returns>
        Task<ErrorResponse> OnError(Exception exception, TRequest request, TwinoMessage rawMessage, TmqClient client);
    }
}