using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Direct
{
    /// <inheritdoc />
    public interface IHorseDirectBus<TIdentifier> : IHorseDirectBus
    {
    }
    
    /// <summary>
    /// Implementation for direct messages and requests
    /// </summary>
    public interface IHorseDirectBus : IHorseConnection
    {
        /// <summary>
        /// Sends a message to a direct target
        /// </summary>
        /// <param name="target">Target Id</param>
        /// <param name="contentType">Message content type</param>
        /// <param name="content">Message content</param>
        /// <param name="waitForCommit">If true, awaitable waits for commit</param>
        /// <param name="messageHeaders">Additional message headers</param>
        /// <returns></returns>
        Task<HorseResult> SendAsync(string target,
                                    ushort contentType,
                                    MemoryStream content,
                                    bool waitForCommit,
                                    IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

        /// <summary>
        /// Sends a message to receivers with specified name
        /// </summary>
        /// <param name="name">Receiver client name</param>
        /// <param name="contentType">Message content type</param>
        /// <param name="content">Message content</param>
        /// <param name="waitForCommit">If true, awaitable waits for commit</param>
        /// <param name="messageHeaders">Additional message headers</param>
        /// <returns></returns>
        Task<HorseResult> SendByName(string name,
                                     ushort contentType,
                                     MemoryStream content,
                                     bool waitForCommit,
                                     IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

        /// <summary>
        /// Sends a message to receivers with specified type
        /// </summary>
        /// <param name="type">Receiver client type</param>
        /// <param name="contentType">Message content type</param>
        /// <param name="content">Message content</param>
        /// <param name="waitAcknowledge">If true, awaitable waits for acknowledge</param>
        /// <param name="messageHeaders">Additional message headers</param>
        /// <returns></returns>
        Task<HorseResult> SendByType(string type,
                                     ushort contentType,
                                     MemoryStream content,
                                     bool waitAcknowledge,
                                     IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

        /// <summary>
        /// Sends a message to a receiver by id
        /// </summary>
        /// <param name="id">Receiver client id</param>
        /// <param name="contentType">Message content type</param>
        /// <param name="content">Message content</param>
        /// <param name="waitAcknowledge">If true, awaitable waits for acknowledge</param>
        /// <param name="messageHeaders">Additional message headers</param>
        /// <returns></returns>
        Task<HorseResult> SendById(string id,
                                   ushort contentType,
                                   MemoryStream content,
                                   bool waitAcknowledge,
                                   IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

        /// <summary>
        /// Sends a JSON message to targets by name
        /// </summary>
        /// <param name="name">Receiver name</param>
        /// <param name="contentType">Message content type</param>
        /// <param name="model">Message model</param>
        /// <param name="waitAcknowledge">If true, Task awaits until acknowledge received from server</param>
        /// <param name="messageHeaders">Additional message headers</param>
        /// <returns></returns>
        Task<HorseResult> SendJsonByName<T>(string name, ushort contentType, T model, bool waitAcknowledge,
                                            IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

        /// <summary>
        /// Sends a JSON message to targets by target
        /// </summary>
        /// <param name="type">Receiver type</param>
        /// <param name="contentType">Message content type</param>
        /// <param name="model">Message model</param>
        /// <param name="waitAcknowledge">If true, Task awaits until acknowledge received from server</param>
        /// <param name="messageHeaders">Additional message headers</param>
        /// <returns></returns>
        Task<HorseResult> SendJsonByType<T>(string type, ushort contentType, T model, bool waitAcknowledge,
                                            IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

        /// <summary>
        /// Sends a JSON message to a target
        /// </summary>
        /// <param name="id">Receiver</param>
        /// <param name="contentType">Message content type</param>
        /// <param name="model">Message model</param>
        /// <param name="waitAcknowledge">If true, Task awaits until acknowledge received from server</param>
        /// <param name="messageHeaders">Additional message headers</param>
        /// <returns></returns>
        Task<HorseResult> SendJsonById<T>(string id, ushort contentType, T model, bool waitAcknowledge,
                                          IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

        /// <summary>
        /// Sends a JSON message to a direct receiver
        /// </summary>
        /// <param name="model">Model that will be serialized to JSON string</param>
        /// <param name="waitForAcknowledge">If true, Task awaits until acknowledge received from server</param>
        /// <param name="messageHeaders">Additional message headers</param>
        /// <returns></returns>
        Task<HorseResult> SendJson(object model,
                                   bool waitForAcknowledge = false,
                                   IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

        /// <summary>
        /// Sends a request to a target
        /// </summary>
        /// <param name="target">Receiver</param>
        /// <param name="contentType">Message content type</param>
        /// <param name="content">Message content</param>
        /// <param name="messageHeaders">Additional message headers</param>
        /// <returns></returns>
        Task<HorseMessage> Request(string target,
                                   ushort contentType,
                                   MemoryStream content,
                                   IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

        /// <summary>
        /// Sends a request to a target
        /// </summary>
        /// <param name="target">Receiver</param>
        /// <param name="contentType">Message content type</param>
        /// <param name="content">Message content</param>
        /// <param name="messageHeaders">Additional message headers</param>
        /// <returns></returns>
        Task<HorseMessage> Request(string target,
                                   ushort contentType,
                                   string content,
                                   IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

        /// <summary>
        /// Sends an empty request to a target
        /// </summary>
        /// <param name="target">Receiver</param>
        /// <param name="contentType">Message content type</param>
        /// <param name="messageHeaders">Additional message headers</param>
        /// <returns></returns>
        Task<HorseMessage> Request(string target,
                                   ushort contentType,
                                   IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

        /// <summary>
        /// Sends a JSON message to a specified direct receiver
        /// </summary>
        /// <param name="target">Receiver</param>
        /// <param name="contentType">Message content type</param>
        /// <param name="model">Model that will be serialized to JSON string</param>
        /// <param name="waitForAcknowledge">If true, Task awaits until acknowledge received from server</param>
        /// <param name="messageHeaders">Additional message headers</param>
        /// <returns></returns>
        Task<HorseResult> SendDirectJsonAsync<T>(string target,
                                                 ushort contentType,
                                                 T model,
                                                 bool waitForAcknowledge = false,
                                                 IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

        /// <summary>
        /// Sends a JSON request and waits for it's response
        /// </summary>
        /// <param name="request">Request model</param>
        /// <typeparam name="TResponse">Response model</typeparam>
        /// <param name="messageHeaders">Additional message headers</param>
        /// <returns>Response message</returns>
        Task<HorseResult<TResponse>> RequestJsonAsync<TResponse>(object request,
                                                                 IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

        /// <summary>
        /// Sends a JSON request to a specific target and waits for it's response
        /// </summary>
        /// <param name="target">Receiver target</param>
        /// <param name="contentType">Message content type</param>
        /// <param name="request">Request model</param>
        /// <typeparam name="TRequest">Should be a class. Primitive types are not supported</typeparam>
        /// <typeparam name="TResponse">Response model</typeparam>
        /// <param name="messageHeaders">Additional message headers</param>
        /// <returns></returns>
        Task<HorseResult<TResponse>> RequestJsonAsync<TRequest, TResponse>(string target,
                                                                           ushort contentType,
                                                                           TRequest request,
                                                                           IEnumerable<KeyValuePair<string, string>> messageHeaders = null);
    }
}