namespace Twino.MQ
{
    public interface IMessageProcessFlow
    {
        //received from sender

        //after proceed by exchange (queue found)

        //before queue (if queue has listener or not, how many listeners)

        //after queue

        //message sent

        //delivery received

        //response received
        //response sent
    }
}