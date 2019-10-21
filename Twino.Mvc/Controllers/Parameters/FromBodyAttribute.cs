using System;

namespace Twino.Mvc.Controllers.Parameters
{
    /// <summary>
    /// Body parameter content types.
    /// Tells us how we deserialize the serialized data
    /// </summary>
    public enum BodyType
    {
        Json = 0,
        Xml = 1
    }

    /// <summary>
    /// Attribute for parameters will be read from the content body
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class FromBodyAttribute : ParameterSourceAttribute
    {
        /// <summary>
        /// Object serialization method
        /// </summary>
        public BodyType Type { get; }

        public FromBodyAttribute() : this(BodyType.Json)
        {
        }

        public FromBodyAttribute(BodyType type): base(null)
        {
            Type = type;
            Source = ParameterSource.Body;
        }
    }

}
