namespace Twino.Client.Connectors
{
    /// <summary>
    /// Only connects to the server, when Send is called.
    /// After send the message, disconnected from the server.
    /// </summary>
    public class SingleMessageConnector : NecessityConnector
    {
        /// <summary>
        /// Connects to the server, sends the message and disconnects after message is sent.
        /// </summary>
        public override bool Send(byte[] preparedData)
        {
            bool sent = base.Send(preparedData);

            Disconnect();
            return sent;
        }
    }
}