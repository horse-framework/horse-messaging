using System;

namespace Twino.Mvc.Filters.Route
{
    /// <summary>
    /// Attribute for action methods routed with HTTP DELETE method
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class HttpDeleteAttribute : HttpMethodAttribute
    {
        /// <summary>
        /// Creates new HTTP Method DELETE attribute
        /// </summary>
        public HttpDeleteAttribute() : this(null)
        {
        }

        /// <summary>
        /// Creates new HTTP Method DELETE attribute with specified route pattern
        /// </summary>
        public HttpDeleteAttribute(string pattern) : base("DELETE", pattern)
        {
        }
    }
}