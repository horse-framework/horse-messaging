using System;

namespace Twino.Mvc.Controllers.Parameters
{
    /// <summary>
    /// Body parameter content types.
    /// Tells us how we deserialize the serialized data
    /// </summary>
    public enum BodyType
    {
        /// <summary>
        /// JSON Message
        /// </summary>
        Json = 0,
        
        /// <summary>
        /// XML Message
        /// </summary>
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

        /// <summary>
        /// Creates new from body attribute for reading JSON messages
        /// </summary>
        public FromBodyAttribute() : this(BodyType.Json)
        {
        }
        
        /// <summary>
        /// Creates new from body attribute for reading specified body type
        /// </summary>
        public FromBodyAttribute(BodyType type): base(null)
        {
            Type = type;
            Source = ParameterSource.Body;
        }
    }

}
