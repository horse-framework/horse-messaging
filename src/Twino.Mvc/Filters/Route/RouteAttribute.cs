using System;

namespace Twino.Mvc.Filters.Route
{
    /// <summary>
    /// Route attribute for the controller classes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class RouteAttribute : Attribute
    {
        /// <summary>
        /// Controller route pattern.
        /// For controller name type "[controller]"
        /// </summary>
        public string Pattern { get; }

        public RouteAttribute()
        {
            Pattern = "[controller]";
        }
        
        public RouteAttribute(string pattern)
        {
            Pattern = pattern;
        }
    }
}