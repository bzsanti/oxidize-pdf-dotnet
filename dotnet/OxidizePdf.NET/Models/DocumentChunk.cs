using System.Text.Json;
using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Models;

/// <summary>
/// Represents a text chunk extracted from a PDF, optimized for RAG/LLM pipelines
/// </summary>
[JsonConverter(typeof(DocumentChunkJsonConverter))]
public class DocumentChunk
{
    /// <summary>
    /// Chunk index in the document (0-based)
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Source page number (1-based)
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// Extracted text content
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Extraction confidence score (0.0 - 1.0)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Bounding box of the chunk on the page
    /// </summary>
    public BoundingBox BoundingBox { get; set; } = new();
}

/// <summary>
/// Bounding box coordinates for spatial positioning
/// </summary>
public class BoundingBox
{
    /// <summary>
    /// X coordinate (left edge)
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Y coordinate (bottom edge in PDF coordinates)
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// Width of the box
    /// </summary>
    public double Width { get; set; }

    /// <summary>
    /// Height of the box
    /// </summary>
    public double Height { get; set; }
}

/// <summary>
/// Custom JSON converter for DocumentChunk that handles flat bounding box fields from Rust FFI
/// </summary>
internal class DocumentChunkJsonConverter : JsonConverter<DocumentChunk>
{
    public override DocumentChunk Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected start of object");

        var chunk = new DocumentChunk();
        double x = 0, y = 0, width = 0, height = 0;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                chunk.BoundingBox = new BoundingBox { X = x, Y = y, Width = width, Height = height };
                return chunk;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected property name");

            var propertyName = reader.GetString();
            reader.Read();

            switch (propertyName)
            {
                case "index":
                    chunk.Index = reader.GetInt32();
                    break;
                case "page_number":
                    chunk.PageNumber = reader.GetInt32();
                    break;
                case "text":
                    chunk.Text = reader.GetString() ?? string.Empty;
                    break;
                case "confidence":
                    chunk.Confidence = reader.GetDouble();
                    break;
                case "x":
                    x = reader.GetDouble();
                    break;
                case "y":
                    y = reader.GetDouble();
                    break;
                case "width":
                    width = reader.GetDouble();
                    break;
                case "height":
                    height = reader.GetDouble();
                    break;
            }
        }

        throw new JsonException("Unexpected end of JSON");
    }

    public override void Write(Utf8JsonWriter writer, DocumentChunk value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("index", value.Index);
        writer.WriteNumber("page_number", value.PageNumber);
        writer.WriteString("text", value.Text);
        writer.WriteNumber("confidence", value.Confidence);
        writer.WriteNumber("x", value.BoundingBox.X);
        writer.WriteNumber("y", value.BoundingBox.Y);
        writer.WriteNumber("width", value.BoundingBox.Width);
        writer.WriteNumber("height", value.BoundingBox.Height);
        writer.WriteEndObject();
    }
}
