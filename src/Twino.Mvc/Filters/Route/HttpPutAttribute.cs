using System;

namespace Twino.Mvc.Filters.Route
{
    /// <summary>
    /// Attribute for action methods routed with HTTP PUT method
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class HttpPutAttribute : HttpMethodAttribute
    {
        /// <summary>
        /// Creates new HTTP Method PUT attribute
        /// </summary>
        public HttpPutAttribute() : this(null)
        {
        }

        /// <summary>
        /// Creates new HTTP Method PUT attribute with specified route pattern
        /// </summary>
        public HttpPutAttribute(string pattern) : base("PUT", pattern)
        {
        }
    }
}