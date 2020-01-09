using System;

namespace Twino.Mvc.Filters.Route
{
    /// <summary>
    /// Attribute for action methods routed with HTTP OPTIONS method
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class HttpOptionsAttribute : HttpMethodAttribute
    {
        /// <summary>
        /// Creates new HTTP Method OPTIONS attribute
        /// </summary>
        public HttpOptionsAttribute() : this(null)
        {
        }

        /// <summary>
        /// Creates new HTTP Method OPTIONS attribute with specified route pattern
        /// </summary>
        public HttpOptionsAttribute(string pattern) : base("OPTIONS", pattern)
        {
        }
    }
}