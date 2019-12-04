namespace Test.Mq
{
    public class ChannelMessageTest
    {
        //todo: without queuing when there are available clients
        //todo: without queuing when there are not any available clients (they will subscribe after messages are sent)
        
        //todo: with queuing when there are available clients
        //todo: with queuing when there are not any available clients (they will subscribe after messages are sent)
        
        //todo: SendOnlyFirstAcquirer option when there are multiple receivers
        //todo: SendOnlyFirstAcquirer option when there are no receivers on first try (they will subscribe after first try)
        
        //todo: use message id option
        
        //todo: wait acknowledge when there are no receivers
        //todo: wait acknowledge when there is one receiver (true case and receiver doesn't send ack case)
        //todo: wait acknowledge when there are multiple receivers (true case and receiver doesn't send ack case)
    }
}