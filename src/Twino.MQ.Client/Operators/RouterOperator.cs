using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Twino.MQ.Client.Annotations.Resolvers;
using Twino.MQ.Client.Models;
using Twino.Protocols.TMQ;
using Twino.Protocols.TMQ.Models;

namespace Twino.MQ.Client.Operators
{
    /// <summary>
    /// Router manager object for tmq client
    /// </summary>
    public class RouterOperator
    {
        private readonly TmqClient _client;

        internal RouterOperator(TmqClient client)
        {
            _client = client;
        }

        #region Actions

        /// <summary>
        /// Creates new router.
        /// Returns success result if router already exists.
        /// </summary>
        public async Task<TwinoResult> Create(string name, RouteMethod method)
        {
            TwinoMessage message = new TwinoMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.CreateRouter;
            message.SetTarget(name);
            message.WaitResponse = true;
            message.AddHeader(TwinoHeaders.ROUTE_METHOD, Convert.ToInt32(method).ToString());
            message.SetMessageId(_client.UniqueIdGenerator.Create());
            return await _client.WaitResponse(message, true);
        }

        /// <summary>
        /// Gets information of all routers in server
        /// </summary>
        public async Task<TmqModelResult<List<RouterInformation>>> List()
        {
            TwinoMessage message = new TwinoMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.ListRouters;
            return await _client.SendAndGetJson<List<RouterInformation>>(message);
        }

        /// <summary>
        /// Removes a router.
        /// Returns success result if router doesn't exists.
        /// </summary>
        public async Task<TwinoResult> Remove(string name)
        {
            TwinoMessage message = new TwinoMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.RemoveRouter;
            message.SetTarget(name);
            message.WaitResponse = true;
            message.SetMessageId(_client.UniqueIdGenerator.Create());
            return await _client.WaitResponse(message, true);
        }

        /// <summary>
        /// Adds new binding to a router
        /// </summary>
        /// <param name="routerName">Router name of the binding</param>
        /// <param name="type">Binding type</param>
        /// <param name="name">Binding name</param>
        /// <param name="target">Binding target. Queue name, tag name, direct receiver id, name, type, etc.</param>
        /// <param name="interaction">Binding interaction</param>
        /// <param name="bindingMethod">Binding method is used when multiple receivers available in same binding. It's used for Direct and Tag bindings.</param>
        /// <param name="contentType">Overwritten content type if specified</param>
        /// <param name="priority">Binding priority</param>
        /// <returns></returns>
        public async Task<TwinoResult> AddBinding(string routerName,
                                                  BindingType type,
                                                  string name,
                                                  string target,
                                                  BindingInteraction interaction,
                                                  RouteMethod bindingMethod = RouteMethod.Distribute,
                                                  ushort? contentType = null,
                                                  int priority = 1)
        {
            TwinoMessage message = new TwinoMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.AddBinding;
            message.SetTarget(routerName);
            message.WaitResponse = true;
            message.SetMessageId(_client.UniqueIdGenerator.Create());
            BindingInformation info = new BindingInformation
                                      {
                                          Name = name,
                                          Target = target,
                                          Interaction = interaction,
                                          ContentType = contentType,
                                          Priority = priority,
                                          BindingType = type,
                                          Method = bindingMethod
                                      };
            message.Serialize(info, new NewtonsoftContentSerializer());
            return await _client.WaitResponse(message, true);
        }

        /// <summary>
        /// Gets all bindings of a router
        /// </summary>
        public async Task<TmqModelResult<List<BindingInformation>>> GetBindings(string routerName)
        {
            TwinoMessage message = new TwinoMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.ListBindings;
            message.SetTarget(routerName);
            return await _client.SendAndGetJson<List<BindingInformation>>(message);
        }

        /// <summary>
        /// Remove a binding from a router
        /// </summary>
        public async Task<TwinoResult> RemoveBinding(string routerName, string bindingName)
        {
            TwinoMessage message = new TwinoMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.RemoveBinding;
            message.SetTarget(routerName);
            message.WaitResponse = true;
            message.SetMessageId(_client.UniqueIdGenerator.Create());
            message.AddHeader(TwinoHeaders.BINDING_NAME, bindingName);
            return await _client.WaitResponse(message, true);
        }

        #endregion

        #region Publish

        /// <summary>
        /// Publishes a string message to a router
        /// </summary>
        public async Task<TwinoResult> Publish(string routerName,
                                               string message,
                                               bool waitForAcknowledge = false,
                                               ushort contentType = 0,
                                               IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TwinoMessage msg = new TwinoMessage(MessageType.Router, routerName, contentType);
            msg.WaitResponse = waitForAcknowledge;
            msg.SetMessageId(_client.UniqueIdGenerator.Create());
            msg.Content = new MemoryStream(Encoding.UTF8.GetBytes(message));

            if (messageHeaders != null)
                foreach (KeyValuePair<string, string> pair in messageHeaders)
                    msg.AddHeader(pair.Key, pair.Value);

            return await _client.WaitResponse(msg, waitForAcknowledge);
        }

        /// <summary>
        /// Publishes a byte array data to a router
        /// </summary>
        public async Task<TwinoResult> Publish(string routerName,
                                               byte[] data,
                                               bool waitForAcknowledge = false,
                                               ushort contentType = 0,
                                               IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TwinoMessage msg = new TwinoMessage(MessageType.Router, routerName, contentType);
            msg.WaitResponse = waitForAcknowledge;
            msg.SetMessageId(_client.UniqueIdGenerator.Create());
            msg.Content = new MemoryStream(data);

            if (messageHeaders != null)
                foreach (KeyValuePair<string, string> pair in messageHeaders)
                    msg.AddHeader(pair.Key, pair.Value);

            return await _client.WaitResponse(msg, waitForAcknowledge);
        }

        /// <summary>
        /// Publishes a JSON object to a router
        /// </summary>
        public Task<TwinoResult> PublishJson(object model, bool waitForAcknowledge = false,
                                             IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return PublishJson(null, model, waitForAcknowledge, null, messageHeaders);
        }

        /// <summary>
        /// Publishes a JSON object to a router
        /// </summary>
        public async Task<TwinoResult> PublishJson(string routerName,
                                                   object model,
                                                   bool waitForAcknowledge = false,
                                                   ushort? contentType = null,
                                                   IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TypeDeliveryDescriptor descriptor = _client.DeliveryContainer.GetDescriptor(model.GetType());
            TwinoMessage message = descriptor.CreateMessage(MessageType.Router, routerName, contentType);

            message.WaitResponse = waitForAcknowledge;
            message.SetMessageId(_client.UniqueIdGenerator.Create());
            message.Serialize(model, _client.JsonSerializer);

            if (messageHeaders != null)
                foreach (KeyValuePair<string, string> pair in messageHeaders)
                    message.AddHeader(pair.Key, pair.Value);

            return await _client.WaitResponse(message, waitForAcknowledge);
        }

        /// <summary>
        /// Sends a string request to router.
        /// Waits response from at least one binding.
        /// </summary>
        public async Task<TwinoMessage> PublishRequest(string routerName, string message, ushort contentType = 0,
                                                     IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TwinoMessage msg = new TwinoMessage(MessageType.Router, routerName, contentType);
            msg.WaitResponse = true;
            msg.Content = new MemoryStream(Encoding.UTF8.GetBytes(message));

            if (messageHeaders != null)
                foreach (KeyValuePair<string, string> pair in messageHeaders)
                    msg.AddHeader(pair.Key, pair.Value);

            return await _client.Request(msg);
        }

        /// <summary>
        /// Sends a request to router.
        /// Waits response from at least one binding.
        /// </summary>
        public Task<TwinoResult<TResponse>> PublishRequestJson<TRequest, TResponse>(TRequest request,
                                                                                    IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return PublishRequestJson<TRequest, TResponse>(null, request, null, messageHeaders);
        }

        /// <summary>
        /// Sends a request to router.
        /// Waits response from at least one binding.
        /// </summary>
        public async Task<TwinoResult<TResponse>> PublishRequestJson<TRequest, TResponse>(string routerName, TRequest request, ushort? contentType = null,
                                                                                          IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            TypeDeliveryDescriptor descriptor = _client.DeliveryContainer.GetDescriptor(request.GetType());
            TwinoMessage message = descriptor.CreateMessage(MessageType.Router, routerName, contentType);
            message.WaitResponse = true;
            message.Serialize(request, _client.JsonSerializer);

            if (messageHeaders != null)
                foreach (KeyValuePair<string, string> pair in messageHeaders)
                    message.AddHeader(pair.Key, pair.Value);

            TwinoMessage responseMessage = await _client.Request(message);
            if (responseMessage.ContentType == 0)
            {
                TResponse response = responseMessage.Deserialize<TResponse>(_client.JsonSerializer);
                return new TwinoResult<TResponse>(response, message, TwinoResultCode.Ok);
            }

            return new TwinoResult<TResponse>(default, responseMessage, (TwinoResultCode) responseMessage.ContentType);
        }

        #endregion
    }
}