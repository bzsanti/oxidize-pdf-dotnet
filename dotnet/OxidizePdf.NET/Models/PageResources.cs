using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Models;

/// <summary>
/// Resources associated with a specific page of a PDF document.
/// </summary>
public class PageResources
{
    /// <summary>Font names used on this page.</summary>
    [JsonPropertyName("font_names")]
    public List<string> FontNames { get; set; } = new();

    /// <summary>Whether the page contains any XObjects (Forms, Images, etc.).</summary>
    [JsonPropertyName("has_xobjects")]
    public bool HasXObjects { get; set; }

    /// <summary>Top-level resource category keys (e.g., "Font", "XObject", "ExtGState").</summary>
    [JsonPropertyName("resource_keys")]
    public List<string> ResourceKeys { get; set; } = new();
}
