namespace Twino.Client.TMQ.Bus
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