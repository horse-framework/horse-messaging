using System;

namespace Horse.Messaging.Client.Channels
{
    public class HorseChannelException : Exception
    {
        public HorseChannelException(string message) : base(message)
        {
        }
    }
}