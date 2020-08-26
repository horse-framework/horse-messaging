using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Bus
{
    /// <summary>
    /// Implementation for route messages and requests
    /// </summary>
    public interface ITwinoRouteBus : ITwinoConnection
    {
        /// <summary>
        /// Publish a message to a router
        /// </summary>
        /// <param name="routerName">Router name</param>
        /// <param name="content">Message content</param>
        /// <param name="waitAcknowledge">If true, Task awaits until acknowledge received from server</param>
        /// <param name="messageHeaders">Additional message headers</param>
        /// <returns></returns>
        Task<TwinoResult> Publish(string routerName, string content, bool waitAcknowledge = false,
                                  IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

        /// <summary>
        /// Publish a message to a router
        /// </summary>
        /// <param name="routerName">Router name</param>
        /// <param name="content">Message content</param>
        /// <param name="waitAcknowledge">If true, Task awaits until acknowledge received from server</param>
        /// <param name="messageHeaders">Additional message headers</param>
        /// <returns></returns>
        Task<TwinoResult> Publish(string routerName, MemoryStream content, bool waitAcknowledge = false,
                                  IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

        /// <summary>
        /// Publish a JSON message to a router
        /// </summary>
        /// <param name="jsonObject">The object that will be serialized to JSON string</param>
        /// <param name="waitAcknowledge">If true, Task awaits until acknowledge received from server</param>
        /// <param name="messageHeaders">Additional message headers</param>
        /// <returns></returns>
        Task<TwinoResult> PublishJson(object jsonObject, bool waitAcknowledge = false,
                                      IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

        /// <summary>
        /// Publish a JSON message to a router
        /// </summary>
        /// <param name="routerName">Router name</param>
        /// <param name="jsonObject">The object that will be serialized to JSON string</param>
        /// <param name="waitAcknowledge">If true, Task awaits until acknowledge received from server</param>
        /// <param name="messageHeaders">Additional message headers</param>
        /// <returns></returns>
        Task<TwinoResult> PublishJson(string routerName, object jsonObject, bool waitAcknowledge = false,
                                      IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

        /// <summary>
        /// Publish a JSON message to a router
        /// </summary>
        /// <param name="routerName">Router name</param>
        /// <param name="jsonObject">The object that will be serialized to JSON string</param>
        /// <param name="contentType">Message content type</param>
        /// <param name="waitAcknowledge">If true, Task awaits until acknowledge received from server</param>
        /// <param name="messageHeaders">Additional message headers</param>
        /// <returns></returns>
        Task<TwinoResult> PublishJson(string routerName, object jsonObject, ushort? contentType = null,
                                      bool waitAcknowledge = false,
                                      IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

        /// <summary>
        /// Publish a string message to a router and waits for a response message
        /// </summary>
        /// <param name="routerName">Router name</param>
        /// <param name="message"></param>
        /// <param name="contentType">Message content type</param>
        /// <param name="messageHeaders">Additional message headers</param>
        /// <returns></returns>
        Task<TwinoMessage> PublishRequest(string routerName, string message, ushort contentType = 0,
                                        IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

        /// <summary>
        /// Publish a JSON message to a router and waits for a response message
        /// </summary>
        /// <param name="request">Request model</param>
        /// <typeparam name="TRequest">Request model type</typeparam>
        /// <typeparam name="TResponse">Response model type</typeparam>
        /// <param name="messageHeaders">Additional message headers</param>
        Task<TwinoResult<TResponse>> PublishRequestJson<TRequest, TResponse>(TRequest request,
                                                                             IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

        /// <summary>
        /// Publish a JSON message to a router and waits for a response message
        /// </summary>
        /// <param name="routerName">Target router name</param>
        /// <param name="request">Request model</param>
        /// <param name="contentType">Message content type</param>
        /// <typeparam name="TRequest">Request model type</typeparam>
        /// <typeparam name="TResponse">Response model type</typeparam>
        /// <param name="messageHeaders">Additional message headers</param>
        /// <returns></returns>
        Task<TwinoResult<TResponse>> PublishRequestJson<TRequest, TResponse>(string routerName, TRequest request, ushort? contentType = null,
                                                                             IEnumerable<KeyValuePair<string, string>> messageHeaders = null);
    }
}