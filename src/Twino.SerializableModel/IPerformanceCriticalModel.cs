using Twino.JsonModel.Serialization;

namespace Twino.JsonModel
{
    /// <summary>
    /// Used by Model Reader and Writer classes.
    /// When serialization and deserialization performance is critical,
    /// This interface provides you to manage serialization manually.
    /// If your model is implemented from IJsonModel and IPerformanceCritical model,
    /// Model Reader and Writer classes are called Serialize and Deserialize methods.
    /// </summary>
    public interface IPerformanceCriticalModel : ISerializableModel
    {
        /// <summary>
        /// Serializes the object as JSON
        /// </summary>
        void Serialize(LightJsonWriter writer);

        /// <summary>
        /// Deserializes the object from JSON
        /// </summary>
        void Deserialize(LightJsonReader reader);
    }
}