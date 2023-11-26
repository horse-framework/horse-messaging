using System;

namespace Horse.Messaging.Protocol;

/// <summary>
/// Message content serializer implementation for HorseMessage objects
/// </summary>
public interface IMessageContentSerializer
{
    /// <summary>
    /// Serializes message content and converts the result to byte array
    /// </summary>
    /// <param name="model">Model that will be serialized into the horse message</param>
    /// <param name="message">Message</param>
    void Serialize(HorseMessage message, object model);

    /// <summary>
    /// Deserializes message content and returns the object
    /// </summary>
    /// <param name="type">Model type</param>
    /// <param name="message">Message</param>
    object Deserialize(HorseMessage message, Type type);
}