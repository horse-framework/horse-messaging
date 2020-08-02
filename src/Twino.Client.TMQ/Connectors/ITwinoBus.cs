using System.IO;
using System.Threading.Tasks;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Connectors
{
    /// <summary>
    /// Implementation for sending messages to Twino MQ
    /// </summary>
    public interface ITwinoBus
    {
        /// <summary>
        /// Sends a raw message
        /// </summary>
        Task<TwinoResult> SendAsync(TmqMessage message);

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
        /// Sends a raw message and waits for it's response
        /// </summary>
        /// <param name="message">Raw message</param>
        /// <returns>Response message</returns>
        Task<TmqMessage> RequestAsync(TmqMessage message);

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

        /// <summary>
        /// Pushes a message into a queue
        /// </summary>
        /// <param name="channel">Target channel name</param>
        /// <param name="queueId">Target Queue Id</param>
        /// <param name="content">Message content</param>
        /// <param name="waitAcknowledge">If true, Task awaits until acknowledge received from server</param>
        /// <returns></returns>
        Task<TwinoResult> Push(string channel, ushort queueId, MemoryStream content, bool waitAcknowledge = false);

        /// <summary>
        /// Pushes a JSON message into a queue
        /// </summary>
        /// <param name="jsonObject">The object that will be serialized to JSON string</param>
        /// <param name="waitAcknowledge">If true, Task awaits until acknowledge received from server</param>
        /// <returns></returns>
        Task<TwinoResult> PushJson(object jsonObject, bool waitAcknowledge = false);

        /// <summary>
        /// Pushes a JSON message into a specified queue
        /// </summary>
        /// <param name="channel">Target channel name</param>
        /// <param name="queueId">Target Queue Id</param>
        /// <param name="jsonObject">The object that will be serialized to JSON string</param>
        /// <param name="waitAcknowledge">If true, Task awaits until acknowledge received from server</param>
        /// <returns></returns>
        Task<TwinoResult> PushJson(string channel, ushort queueId, object jsonObject, bool waitAcknowledge = false);

        /// <summary>
        /// Publish a message to a router
        /// </summary>
        /// <param name="routerName">Router name</param>
        /// <param name="content">Message content</param>
        /// <param name="waitAcknowledge">If true, Task awaits until acknowledge received from server</param>
        /// <returns></returns>
        Task<TwinoResult> Publish(string routerName, MemoryStream content, bool waitAcknowledge = false);

        /// <summary>
        /// Publish a JSON message to a router
        /// </summary>
        /// <param name="jsonObject">The object that will be serialized to JSON string</param>
        /// <param name="waitAcknowledge">If true, Task awaits until acknowledge received from server</param>
        /// <returns></returns>
        Task<TwinoResult> PublishJson(object jsonObject, bool waitAcknowledge = false);

        /// <summary>
        /// Publish a JSON message to a router
        /// </summary>
        /// <param name="routerName">Router name</param>
        /// <param name="jsonObject">The object that will be serialized to JSON string</param>
        /// <param name="waitAcknowledge">If true, Task awaits until acknowledge received from server</param>
        /// <returns></returns>
        Task<TwinoResult> PublishJson(string routerName, object jsonObject, bool waitAcknowledge = false);

        /// <summary>
        /// Publish a string message to a router and waits for a response message
        /// </summary>
        /// <param name="routerName">Router name</param>
        /// <param name="message"></param>
        /// <param name="contentType"></param>
        /// <returns></returns>
        Task<TmqMessage> PublishRequest(string routerName, string message, ushort contentType = 0);

        /// <summary>
        /// Publish a JSON message to a router and waits for a response message
        /// </summary>
        /// <param name="request">Request model</param>
        /// <typeparam name="TRequest">Request model type</typeparam>
        /// <typeparam name="TResponse">Response model type</typeparam>
        Task<TwinoResult<TResponse>> PublishRequestJson<TRequest, TResponse>(TRequest request);

        /// <summary>
        /// Publish a JSON message to a router and waits for a response message
        /// </summary>
        /// <param name="routerName">Target router name</param>
        /// <param name="request">Request model</param>
        /// <param name="contentType">Message content type</param>
        /// <typeparam name="TRequest">Request model type</typeparam>
        /// <typeparam name="TResponse">Response model type</typeparam>
        /// <returns></returns>
        Task<TwinoResult<TResponse>> PublishRequestJson<TRequest, TResponse>(string routerName, TRequest request, ushort? contentType = null);

        /// <summary>
        /// Gets connected client object
        /// </summary>
        TmqClient GetClient();
    }
}