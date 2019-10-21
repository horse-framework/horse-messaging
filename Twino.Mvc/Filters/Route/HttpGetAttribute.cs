using System;

namespace Twino.Mvc.Filters.Route
{
    /// <summary>
    /// Attribute for action methods routed with HTTP GET method
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class HttpGetAttribute : HttpMethodAttribute
    {

        public HttpGetAttribute() : this(null)
        {
        }

        public HttpGetAttribute(string pattern) : base("GET", pattern)
        {
        }

    }
}
