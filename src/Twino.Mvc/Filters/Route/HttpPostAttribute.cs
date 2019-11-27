using System;

namespace Twino.Mvc.Filters.Route
{
    /// <summary>
    /// Attribute for action methods routed with HTTP POST method
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class HttpPostAttribute : HttpMethodAttribute
    {

        public HttpPostAttribute() : this(null)
        {
        }

        public HttpPostAttribute(string pattern) : base("POST", pattern)
        {
        }

    }
}
