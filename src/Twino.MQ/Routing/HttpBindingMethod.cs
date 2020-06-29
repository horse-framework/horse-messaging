namespace Twino.MQ.Routing
{
    /// <summary>
    /// HTTP Methods for HTTP Binding
    /// </summary>
    public enum HttpBindingMethod : ushort
    {
        /// <summary>
        /// "GET"
        /// </summary>
        Get,

        /// <summary>
        /// "POST"
        /// </summary>
        Post,

        /// <summary>
        /// "PUT"
        /// </summary>
        Put,

        /// <summary>
        /// "PATCH"
        /// </summary>
        Patch,

        /// <summary>
        /// "DELETE"
        /// </summary>
        Delete
    }
}