namespace Horse.Mq.Client.Bus
{
    /// <summary>
    /// Base Horse Connection implementation
    /// </summary>
    public interface IHorseConnection
    {
        /// <summary>
        /// Gets connected client object
        /// </summary>
        HorseClient GetClient();
    }
}