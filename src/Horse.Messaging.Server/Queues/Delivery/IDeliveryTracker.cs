using System.Collections.Generic;
using System.Threading.Tasks;
using Horse.Messaging.Server.Clients;

namespace Horse.Messaging.Server.Queues.Delivery
{
    public interface IDeliveryTracker
    {
        void Start();

        void Reset();

        Task Destroy();

        void Track(MessageDelivery delivery);

        List<QueueMessage> GetDeliveringMessages();
        
        MessageDelivery FindDelivery(MessagingClient client, string messageId);

        MessageDelivery FindAndRemoveDelivery(MessagingClient client, string messageId);
        
        void RemoveDelivery(MessageDelivery delivery);
        
        int GetDeliveryCount();
    }
}