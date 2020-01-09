using System;

namespace Twino.Mvc.Filters.Route
{
    /// <summary>
    /// Attribute for action methods routed with HTTP GET method
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class HttpGetAttribute : HttpMethodAttribute
    {
        /// <summary>
        /// Creates new HTTP Method GET attribute
        /// </summary>
        public HttpGetAttribute() : this(null)
        {
        }

        /// <summary>
        /// Creates new HTTP Method GET attribute with specified route pattern
        /// </summary>
        public HttpGetAttribute(string pattern) : base("GET", pattern)
        {
        }
    }
}