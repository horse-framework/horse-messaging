using System.Threading.Tasks;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Connectors
{
    /// <summary>
    /// Implementation for direct messages and requests
    /// </summary>
    public interface ITwinoDirectBus : ITwinoConnection
    {
        /// <summary>
        /// Sends a JSON message to a direct receiver
        /// </summary>
        /// <param name="model">Model that will be serialized to JSON string</param>
        /// <param name="waitForAcknowledge">If true, Task awaits until acknowledge received from server</param>
        /// <typeparam name="T">Model Type</typeparam>
        /// <returns></returns>
        Task<TwinoResult> SendDirectJsonAsync<T>(T model, bool waitForAcknowledge = false);

        /// <summary>
        /// Sends a JSON message to a specified direct receiver
        /// </summary>
        /// <param name="target">Receiver</param>
        /// <param name="contentType">Message content type</param>
        /// <param name="model">Model that will be serialized to JSON string</param>
        /// <param name="waitForAcknowledge">If true, Task awaits until acknowledge received from server</param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        Task<TwinoResult> SendDirectJsonAsync<T>(string target, ushort contentType, T model, bool waitForAcknowledge = false);

        /// <summary>
        /// Sends a JSON request and waits for it's response
        /// </summary>
        /// <param name="request">Request model</param>
        /// <typeparam name="TRequest">Should be a class. Primitive types are not supported</typeparam>
        /// <typeparam name="TResponse">Response model</typeparam>
        /// <returns>Response message</returns>
        Task<TwinoResult<TResponse>> RequestJsonAsync<TRequest, TResponse>(TRequest request);

        /// <summary>
        /// Sends a JSON request to a specific target and waits for it's response
        /// </summary>
        /// <param name="target">Receiver target</param>
        /// <param name="contentType">Message content type</param>
        /// <param name="request">Request model</param>
        /// <typeparam name="TRequest">Should be a class. Primitive types are not supported</typeparam>
        /// <typeparam name="TResponse">Response model</typeparam>
        /// <returns></returns>
        Task<TwinoResult<TResponse>> RequestJsonAsync<TRequest, TResponse>(string target, ushort contentType, TRequest request);
    }
}