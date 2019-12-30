using System;

namespace Twino.Mvc.Filters.Route
{
    /// <summary>
    /// Attribute for action methods routed with HTTP PUT method
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class HttpPutAttribute : HttpMethodAttribute
    {

        public HttpPutAttribute() : this(null)
        {
        }

        public HttpPutAttribute(string pattern) : base("PUT", pattern)
        {
        }

    }
}
