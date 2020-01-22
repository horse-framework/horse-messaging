namespace Twino.MQ.Queues
{
    public enum QueueStatusAction
    {
        Deny,

        Allow,

        DenyAndTrigger,
        
        AllowAndTrigger
    }
}