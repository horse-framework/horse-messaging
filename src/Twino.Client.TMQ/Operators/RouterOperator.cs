using System.IO;
using System.Text;
using System.Threading.Tasks;
using Twino.Client.TMQ.Annotations.Resolvers;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ.Operators
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


        /// <summary>
        /// Publishes a string message to a router
        /// </summary>
        public async Task<TwinoResult> Publish(string routerName, string message, bool waitForAcknowledge = false, ushort contentType = 0)
        {
            TmqMessage msg = new TmqMessage(MessageType.Router, routerName, contentType);
            msg.PendingAcknowledge = waitForAcknowledge;
            msg.SetMessageId(_client.UniqueIdGenerator.Create());
            msg.Content = new MemoryStream(Encoding.UTF8.GetBytes(message));

            return await _client.SendAndWaitForAcknowledge(msg, waitForAcknowledge);
        }

        /// <summary>
        /// Publishes a byte array data to a router
        /// </summary>
        public async Task<TwinoResult> Publish(string routerName, byte[] data, bool waitForAcknowledge = false, ushort contentType = 0)
        {
            TmqMessage msg = new TmqMessage(MessageType.Router, routerName, contentType);
            msg.PendingAcknowledge = waitForAcknowledge;
            msg.SetMessageId(_client.UniqueIdGenerator.Create());
            msg.Content = new MemoryStream(data);

            return await _client.SendAndWaitForAcknowledge(msg, waitForAcknowledge);
        }

        /// <summary>
        /// Publishes a JSON object to a router
        /// </summary>
        public Task<TwinoResult> PublishJson<TModel>(TModel model, bool waitForAcknowledge = false)
        {
            return PublishJson(null, model, waitForAcknowledge);
        }

        /// <summary>
        /// Publishes a JSON object to a router
        /// </summary>
        public async Task<TwinoResult> PublishJson<TModel>(string routerName, TModel model, bool waitForAcknowledge = false, ushort? contentType = null)
        {
            TypeDeliveryDescriptor descriptor = _client.DeliveryContainer.GetDescriptor<TModel>();
            TmqMessage message = descriptor.CreateMessage(MessageType.Router, routerName, contentType);
            
            message.PendingAcknowledge = waitForAcknowledge;
            message.SetMessageId(_client.UniqueIdGenerator.Create());

            message.Content = new MemoryStream();
            await System.Text.Json.JsonSerializer.SerializeAsync(message.Content, model);

            return await _client.SendAndWaitForAcknowledge(message, waitForAcknowledge);
        }

        /// <summary>
        /// Sends a string request to router.
        /// Waits response from at least one binding.
        /// </summary>
        public async Task<TmqMessage> PublishRequest(string routerName, string message, ushort contentType = 0)
        {
            TmqMessage msg = new TmqMessage(MessageType.Router, routerName, contentType);
            msg.PendingResponse = true;
            msg.Content = new MemoryStream(Encoding.UTF8.GetBytes(message));
            return await _client.Request(msg);
        }

        /// <summary>
        /// Sends a request to router.
        /// Waits response from at least one binding.
        /// </summary>
        public Task<TmqMessage> PublishRequestJson<TModel>(TModel model)
        {
            return PublishRequestJson(null, model);
        }
        

        /// <summary>
        /// Sends a request to router.
        /// Waits response from at least one binding.
        /// </summary>
        public async Task<TmqMessage> PublishRequestJson<TModel>(string routerName, TModel model, ushort? contentType = null)
        {
            TypeDeliveryDescriptor descriptor = _client.DeliveryContainer.GetDescriptor<TModel>();
            TmqMessage message = descriptor.CreateMessage(MessageType.Router, routerName, contentType);
            message.PendingResponse = true;
            await message.SetJsonContent(model);
            return await _client.Request(message);
        }
    }
}