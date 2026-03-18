using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Models;

/// <summary>
/// Raw content streams from a PDF page, base64-encoded.
/// </summary>
internal class ContentStreamResult
{
    [JsonPropertyName("streams")]
    public List<string> Streams { get; set; } = new();
}
