using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Horse.Messaging.Client.Internal;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Models;

namespace Horse.Messaging.Client.Routers
{
    /// <summary>
    /// Router manager object for Horse client
    /// </summary>
    public class RouterOperator
    {
        private readonly HorseClient _client;
        private readonly TypeDescriptorContainer<RouterTypeDescriptor> _descriptorContainer;

        internal RouterOperator(HorseClient client)
        {
            _client = client;
            _descriptorContainer = new TypeDescriptorContainer<RouterTypeDescriptor>(new RouterTypeResolver());
        }

        #region Actions

        /// <summary>
        /// Creates new router.
        /// Returns success result if router already exists.
        /// </summary>
        public async Task<HorseResult> Create(string name, RouteMethod method)
        {
            HorseMessage message = new HorseMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.CreateRouter;
            message.SetTarget(name);
            message.WaitResponse = true;
            message.AddHeader(HorseHeaders.ROUTE_METHOD, Convert.ToInt32(method).ToString());
            message.SetMessageId(_client.UniqueIdGenerator.Create());
            return await _client.WaitResponse(message, true);
        }

        /// <summary>
        /// Gets information of all routers in server
        /// </summary>
        public async Task<HorseModelResult<List<RouterInformation>>> List()
        {
            HorseMessage message = new HorseMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.ListRouters;
            return await _client.SendAndGetJson<List<RouterInformation>>(message);
        }

        /// <summary>
        /// Removes a router.
        /// Returns success result if router doesn't exists.
        /// </summary>
        public async Task<HorseResult> Remove(string name)
        {
            HorseMessage message = new HorseMessage();
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
        public async Task<HorseResult> AddBinding(string routerName,
                                                  string type,
                                                  string name,
                                                  string target,
                                                  BindingInteraction interaction,
                                                  RouteMethod bindingMethod = RouteMethod.Distribute,
                                                  ushort? contentType = null,
                                                  int priority = 1)
        {
            HorseMessage message = new HorseMessage();
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
        public async Task<HorseModelResult<List<BindingInformation>>> GetBindings(string routerName)
        {
            HorseMessage message = new HorseMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.ListBindings;
            message.SetTarget(routerName);
            return await _client.SendAndGetJson<List<BindingInformation>>(message);
        }

        /// <summary>
        /// Remove a binding from a router
        /// </summary>
        public async Task<HorseResult> RemoveBinding(string routerName, string bindingName)
        {
            HorseMessage message = new HorseMessage();
            message.Type = MessageType.Server;
            message.ContentType = KnownContentTypes.RemoveBinding;
            message.SetTarget(routerName);
            message.WaitResponse = true;
            message.SetMessageId(_client.UniqueIdGenerator.Create());
            message.AddHeader(HorseHeaders.BINDING_NAME, bindingName);
            return await _client.WaitResponse(message, true);
        }

        #endregion

        #region Publish

        /// <summary>
        /// Publishes a string message to a router
        /// </summary>
        public Task<HorseResult> Publish(string routerName,
                                         string message,
                                         bool waitForAcknowledge = false,
                                         ushort contentType = 0,
                                         IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return Publish(routerName, message, null, waitForAcknowledge, contentType, messageHeaders);
        }

        /// <summary>
        /// Publishes a string message to a router
        /// </summary>
        public async Task<HorseResult> Publish(string routerName,
                                               string message,
                                               string messageId = null,
                                               bool waitForAcknowledge = false,
                                               ushort contentType = 0,
                                               IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            HorseMessage msg = new HorseMessage(MessageType.Router, routerName, contentType);
            msg.WaitResponse = waitForAcknowledge;

            if (!string.IsNullOrEmpty(messageId))
                msg.SetMessageId(messageId);
            else
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
        public Task<HorseResult> Publish(string routerName,
                                         byte[] data,
                                         bool waitForAcknowledge = false,
                                         ushort contentType = 0,
                                         IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return Publish(routerName, data, null, waitForAcknowledge, contentType, messageHeaders);
        }

        /// <summary>
        /// Publishes a byte array data to a router
        /// </summary>
        public async Task<HorseResult> Publish(string routerName,
                                               byte[] data,
                                               string messageId = null,
                                               bool waitForAcknowledge = false,
                                               ushort contentType = 0,
                                               IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            HorseMessage msg = new HorseMessage(MessageType.Router, routerName, contentType);

            if (!string.IsNullOrEmpty(messageId))
                msg.SetMessageId(messageId);
            else
                msg.SetMessageId(_client.UniqueIdGenerator.Create());

            msg.WaitResponse = waitForAcknowledge;
            msg.Content = new MemoryStream(data);

            if (messageHeaders != null)
                foreach (KeyValuePair<string, string> pair in messageHeaders)
                    msg.AddHeader(pair.Key, pair.Value);

            return await _client.WaitResponse(msg, waitForAcknowledge);
        }

        /// <summary>
        /// Publishes a JSON object to a router
        /// </summary>
        public Task<HorseResult> PublishJson(object model, bool waitForAcknowledge = false,
                                             IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return PublishJson(null, model, null, waitForAcknowledge, null, messageHeaders);
        }

        /// <summary>
        /// Publishes a JSON object to a router
        /// </summary>
        public Task<HorseResult> PublishJson(string routerName, object model, bool waitForAcknowledge = false,
                                             IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return PublishJson(routerName, model, null, waitForAcknowledge, null, messageHeaders);
        }

        /// <summary>
        /// Publishes a JSON object to a router
        /// </summary>
        public async Task<HorseResult> PublishJson(string routerName,
                                                   object model,
                                                   string messageId = null,
                                                   bool waitForAcknowledge = false,
                                                   ushort? contentType = null,
                                                   IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {

            RouterTypeDescriptor descriptor = _descriptorContainer.GetDescriptor(model.GetType());

            if (!string.IsNullOrEmpty(routerName))
                descriptor.RouterName = routerName;

            if (contentType.HasValue)
                descriptor.ContentType = contentType.Value;

            HorseMessage message = descriptor.CreateMessage();

            if (!string.IsNullOrEmpty(messageId))
                message.SetMessageId(messageId);
            else
                message.SetMessageId(_client.UniqueIdGenerator.Create());

            message.WaitResponse = waitForAcknowledge;
            message.Serialize(model, _client.MessageSerializer);

            if (messageHeaders != null)
                foreach (KeyValuePair<string, string> pair in messageHeaders)
                    message.AddHeader(pair.Key, pair.Value);

            return await _client.WaitResponse(message, waitForAcknowledge);
        }

        /// <summary>
        /// Sends a string request to router.
        /// Waits response from at least one binding.
        /// </summary>
        public async Task<HorseMessage> PublishRequest(string routerName, string message, ushort contentType = 0,
                                                       IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            HorseMessage msg = new HorseMessage(MessageType.Router, routerName, contentType);
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
        public Task<HorseResult<TResponse>> PublishRequestJson<TRequest, TResponse>(TRequest request,
                                                                                    IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            return PublishRequestJson<TRequest, TResponse>(null, request, null, messageHeaders);
        }

        /// <summary>
        /// Sends a request to router.
        /// Waits response from at least one binding.
        /// </summary>
        public async Task<HorseResult<TResponse>> PublishRequestJson<TRequest, TResponse>(string routerName, TRequest request, ushort? contentType = null,
                                                                                          IEnumerable<KeyValuePair<string, string>> messageHeaders = null)
        {
            RouterTypeDescriptor descriptor = _descriptorContainer.GetDescriptor(request.GetType());

            if (!string.IsNullOrEmpty(routerName))
                descriptor.RouterName = routerName;

            if (contentType.HasValue)
                descriptor.ContentType = contentType.Value;

            HorseMessage message = descriptor.CreateMessage();
            message.WaitResponse = true;
            message.Serialize(request, _client.MessageSerializer);

            if (messageHeaders != null)
                foreach (KeyValuePair<string, string> pair in messageHeaders)
                    message.AddHeader(pair.Key, pair.Value);

            HorseMessage responseMessage = await _client.Request(message);
            if (responseMessage.ContentType == 0)
            {
                TResponse response = responseMessage.Deserialize<TResponse>(_client.MessageSerializer);
                return new HorseResult<TResponse>(response, message, HorseResultCode.Ok);
            }

            return new HorseResult<TResponse>(default, responseMessage, (HorseResultCode) responseMessage.ContentType);
        }

        #endregion
    }
}