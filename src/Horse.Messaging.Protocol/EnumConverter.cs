using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using EnumsNET;

namespace Horse.Messaging.Protocol;

/// <inheritdoc />
public class EnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    /// <inheritdoc />
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return Enums.Parse<T>(reader.GetString(), true, EnumFormat.Description);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.AsString(EnumFormat.Description));
    }
}