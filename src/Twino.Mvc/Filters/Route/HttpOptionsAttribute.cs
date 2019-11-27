using System;

namespace Twino.Mvc.Filters.Route
{
    /// <summary>
    /// Attribute for action methods routed with HTTP OPTIONS method
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class HttpOptionsAttribute : HttpMethodAttribute
    {

        public HttpOptionsAttribute() : this(null)
        {
        }

        public HttpOptionsAttribute(string pattern) : base("OPTIONS", pattern)
        {
        }

    }
}
