using System.Text.Json;
using System.Text.Json.Serialization;

namespace OxidizePdf.NET;

/// <summary>A typed PDF value for signature and tagged-structure entries.</summary>
public sealed class PdfObjectValue
{
    /// <summary>Null, Boolean, Integer, Real, Text, Name, Bytes, Array, Dictionary, or Reference.</summary>
    [JsonPropertyName("type")] public string Type { get; }
    /// <summary>Payload matching the type. Bytes use base64; Reference uses [object number, generation].</summary>
    [JsonPropertyName("value"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Value { get; }
    /// <summary>Creates a typed value. Native validation rejects invalid types or payloads.</summary>
    public PdfObjectValue(string type, object? value = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        Type = type;
        Value = value is null ? null : JsonSerializer.SerializeToElement(value);
    }
}
