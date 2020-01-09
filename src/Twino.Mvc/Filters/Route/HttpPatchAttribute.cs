using System;

namespace Twino.Mvc.Filters.Route
{
    /// <summary>
    /// Attribute for action methods routed with HTTP PATCH method
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class HttpPatchAttribute : HttpMethodAttribute
    {
        /// <summary>
        /// Creates new HTTP Method PATCH attribute
        /// </summary>
        public HttpPatchAttribute() : this(null)
        {
        }

        /// <summary>
        /// Creates new HTTP Method PATCH attribute with specified route pattern
        /// </summary>
        public HttpPatchAttribute(string pattern) : base("PATCH", pattern)
        {
        }
    }
}