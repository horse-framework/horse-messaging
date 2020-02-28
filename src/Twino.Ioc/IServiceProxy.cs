namespace Twino.Ioc
{
    /// <summary>
    /// Service proxy for registered service
    /// </summary>
    public interface IServiceProxy
    {
        /// <summary>
        /// Your proxy method.
        /// </summary>
        object Proxy(object decorated);
    }
}
