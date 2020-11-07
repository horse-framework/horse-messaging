namespace Twino.MQ.Client.Bus
{
    /// <summary>
    /// Base Twino Connection implementation
    /// </summary>
    public interface ITwinoConnection
    {
        /// <summary>
        /// Gets connected client object
        /// </summary>
        TmqClient GetClient();
    }
}