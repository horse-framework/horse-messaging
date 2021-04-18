using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Horse.Messaging.Client.Models;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Bus
{
    /// <summary>
    /// Implementation for queue messages and requests
    /// </summary>
    public interface IHorseQueueBus : IHorseConnection
    {
        /// <summary>
        /// Pushes a message into a queue
        /// </summary>
        /// <param name="queue">Target queue name</param>
        /// <param name="content">Message content</param>
        /// <param name="waitAcknowledge">If true, Task awaits until acknowledge received from server</param>
        /// <param name="messageHeaders">Additional message headers</param>
        /// <returns></returns>
        Task<HorseResult> Push(string queue, MemoryStream content, bool waitAcknowledge = false,
                               IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

        /// <summary>
        /// Pushes a message into a queue
        /// </summary>
        /// <param name="queue">Target queue name</param>
        /// <param name="content">Message content</param>
        /// <param name="waitAcknowledge">If true, Task awaits until acknowledge received from server</param>
        /// <param name="messageHeaders">Additional message headers</param>
        /// <returns></returns>
        Task<HorseResult> Push(string queue, string content, bool waitAcknowledge = false,
                               IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

        /// <summary>
        /// Pushes a message into a queue
        /// </summary>
        /// <param name="queue">Target queue name</param>
        /// <param name="content">Message content</param>
        /// <param name="messageId">Message Id string</param>
        /// <param name="waitAcknowledge">If true, Task awaits until acknowledge received from server</param>
        /// <param name="messageHeaders">Additional message headers</param>
        /// <returns></returns>
        Task<HorseResult> Push(string queue, MemoryStream content, string messageId, bool waitAcknowledge = false,
                               IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

        /// <summary>
        /// Pushes a message into a queue
        /// </summary>
        /// <param name="queue">Target queue name</param>
        /// <param name="content">Message content</param>
        /// <param name="messageId">Message Id string</param>
        /// <param name="waitAcknowledge">If true, Task awaits until acknowledge received from server</param>
        /// <param name="messageHeaders">Additional message headers</param>
        /// <returns></returns>
        Task<HorseResult> Push(string queue, string content, string messageId, bool waitAcknowledge = false,
                               IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

        /// <summary>
        /// Pushes a JSON message into a queue
        /// </summary>
        /// <param name="jsonObject">The object that will be serialized to JSON string</param>
        /// <param name="waitAcknowledge">If true, Task awaits until acknowledge received from server</param>
        /// <param name="messageHeaders">Additional message headers</param>
        /// <returns></returns>
        Task<HorseResult> PushJson(object jsonObject, bool waitAcknowledge = false,
                                   IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

        /// <summary>
        /// Pushes a JSON message into a specified queue
        /// </summary>
        /// <param name="queue">Target queue name</param>
        /// <param name="jsonObject">The object that will be serialized to JSON string</param>
        /// <param name="waitAcknowledge">If true, Task awaits until acknowledge received from server</param>
        /// <param name="messageHeaders">Additional message headers</param>
        /// <returns></returns>
        Task<HorseResult> PushJson(string queue, object jsonObject, bool waitAcknowledge = false,
                                   IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

        /// <summary>
        /// Pushes a JSON message into a queue
        /// </summary>
        /// <param name="jsonObject">The object that will be serialized to JSON string</param>
        /// <param name="messageId">Message Id string</param>
        /// <param name="waitAcknowledge">If true, Task awaits until acknowledge received from server</param>
        /// <param name="messageHeaders">Additional message headers</param>
        /// <returns></returns>
        Task<HorseResult> PushJson(object jsonObject, string messageId, bool waitAcknowledge = false,
                                   IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

        /// <summary>
        /// Pushes a JSON message into a specified queue
        /// </summary>
        /// <param name="queue">Target queue name</param>
        /// <param name="messageId">Message Id string</param>
        /// <param name="jsonObject">The object that will be serialized to JSON string</param>
        /// <param name="waitAcknowledge">If true, Task awaits until acknowledge received from server</param>
        /// <param name="messageHeaders">Additional message headers</param>
        /// <returns></returns>
        Task<HorseResult> PushJson(string queue, object jsonObject, string messageId, bool waitAcknowledge = false,
                                   IEnumerable<KeyValuePair<string, string>> messageHeaders = null);

        /// <summary>
        /// Request a pull request
        /// </summary>
        /// <param name="request">Pull request object</param>
        /// <param name="actionForEachMessage">Action for each pulled messages</param>
        /// <returns></returns>
        Task<PullContainer> Pull(PullRequest request, Func<int, HorseMessage, Task> actionForEachMessage = null);
    }
}