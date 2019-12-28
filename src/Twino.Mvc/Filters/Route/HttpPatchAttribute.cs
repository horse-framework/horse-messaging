using System;

namespace Twino.Mvc.Filters.Route
{
    /// <summary>
    /// Attribute for action methods routed with HTTP PATCH method
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class HttpPatchAttribute : HttpMethodAttribute
    {

        public HttpPatchAttribute() : this(null)
        {
        }

        public HttpPatchAttribute(string pattern) : base("PATCH", pattern)
        {
        }

    }
}
