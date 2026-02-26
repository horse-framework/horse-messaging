using System;
using Horse.Messaging.Server.Clients;

namespace Horse.Messaging.Server.Stores;

public class StoreSubscription
{
    public MessagingClient Client { get; set; }
    public StorePartition Partition { get; set; }
    
    public long CurrentOffset { get; set; }
    
    public DateTime? SubscriptionDate { get; set; }
    public DateTime? ConsumeExpiration { get; set; }

    public StoreSubscription(StorePartition partition, long offset)
    {
        Partition = partition;
        CurrentOffset = offset;
    }
}