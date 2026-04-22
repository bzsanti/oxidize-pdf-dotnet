using System.Text.Json;
using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Pipeline;

/// <summary>
/// Discriminant tag for <see cref="ReadingOrderStrategy"/>.
/// </summary>
public enum ReadingOrderKind
{
    /// <summary>Simple top-to-bottom, left-to-right ordering.</summary>
    Simple,
    /// <summary>Preserve raw PDF fragment order (no reordering).</summary>
    None,
    /// <summary>XY-Cut recursive algorithm for multi-column layouts.</summary>
    XyCut,
}

/// <summary>
/// Strategy for ordering text fragments before classification. Mirrors
/// <c>oxidize_pdf::pipeline::ReadingOrderStrategy</c> (a Rust enum with one
/// payload-carrying variant). JSON shape matches serde's default tagged
/// representation: <c>"Simple"</c>, <c>"None"</c>, or <c>{"XYCut":{"min_gap":20.0}}</c>.
/// </summary>
/// <remarks>
/// Both this converter and <c>serde_json</c> accept integer and decimal numeric
/// tokens for <c>min_gap</c> interchangeably. <c>System.Text.Json</c> emits integer
/// tokens (<c>20</c>) while <c>serde_json</c> emits decimals (<c>20.0</c>); the
/// difference is textual only and does not affect the deserialized value.
/// </remarks>
[JsonConverter(typeof(ReadingOrderStrategyJsonConverter))]
public sealed class ReadingOrderStrategy : IEquatable<ReadingOrderStrategy>
{
    /// <summary>Top-to-bottom, left-to-right ordering (default on the Rust side).</summary>
    public static readonly ReadingOrderStrategy Simple = new(ReadingOrderKind.Simple, 0.0);

    /// <summary>Preserve the raw PDF fragment order.</summary>
    public static readonly ReadingOrderStrategy None = new(ReadingOrderKind.None, 0.0);

    /// <summary>Discriminant tag identifying which variant this instance represents.</summary>
    public ReadingOrderKind Kind { get; }

    /// <summary>Minimum gap parameter for the XY-Cut algorithm. Unused for <see cref="Simple"/> and <see cref="None"/>.</summary>
    public double MinGap { get; }

    private ReadingOrderStrategy(ReadingOrderKind kind, double minGap)
    {
        Kind = kind;
        MinGap = minGap;
    }

    /// <summary>
    /// Create an XY-Cut reading-order strategy with the given minimum gap (in PDF points).
    /// </summary>
    /// <param name="minGap">Minimum vertical whitespace gap, in PDF points, that triggers a region split. Must be a finite non-negative number.</param>
    public static ReadingOrderStrategy XyCut(double minGap)
    {
        if (minGap < 0.0 || double.IsNaN(minGap) || double.IsInfinity(minGap))
            throw new ArgumentOutOfRangeException(nameof(minGap), "minGap must be a finite non-negative number");
        return new ReadingOrderStrategy(ReadingOrderKind.XyCut, minGap);
    }

    /// <inheritdoc/>
    public bool Equals(ReadingOrderStrategy? other) =>
        other is not null && Kind == other.Kind && MinGap.Equals(other.MinGap);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as ReadingOrderStrategy);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Kind, MinGap);
}

internal sealed class ReadingOrderStrategyJsonConverter : JsonConverter<ReadingOrderStrategy>
{
    /// <inheritdoc/>
    public override ReadingOrderStrategy Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var tag = reader.GetString();
            return tag switch
            {
                "Simple" => ReadingOrderStrategy.Simple,
                "None" => ReadingOrderStrategy.None,
                _ => throw new JsonException($"Unknown ReadingOrderStrategy tag: {tag}"),
            };
        }

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected string or object");

        reader.Read();
        if (reader.TokenType != JsonTokenType.PropertyName)
            throw new JsonException("Expected property name");
        if (reader.GetString() != "XYCut")
            throw new JsonException("Expected property 'XYCut'");

        reader.Read();
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected object after XYCut");

        reader.Read();
        if (reader.TokenType != JsonTokenType.PropertyName)
            throw new JsonException("Expected 'min_gap'");
        if (reader.GetString() != "min_gap")
            throw new JsonException("Expected 'min_gap'");

        reader.Read();
        if (reader.TokenType != JsonTokenType.Number)
            throw new JsonException("Expected numeric min_gap");
        var minGap = reader.GetDouble();

        // Consume any trailing fields inside the inner object.
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            reader.Skip();
        }

        // Now consume the outer EndObject.
        reader.Read();
        if (reader.TokenType != JsonTokenType.EndObject)
            throw new JsonException("Expected closing object for ReadingOrderStrategy");

        try
        {
            return ReadingOrderStrategy.XyCut(minGap);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new JsonException(ex.Message, ex);
        }
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, ReadingOrderStrategy value, JsonSerializerOptions options)
    {
        switch (value.Kind)
        {
            case ReadingOrderKind.Simple:
                writer.WriteStringValue("Simple");
                break;
            case ReadingOrderKind.None:
                writer.WriteStringValue("None");
                break;
            case ReadingOrderKind.XyCut:
                writer.WriteStartObject();
                writer.WriteStartObject("XYCut");
                writer.WriteNumber("min_gap", value.MinGap);
                writer.WriteEndObject();
                writer.WriteEndObject();
                break;
        }
    }
}
