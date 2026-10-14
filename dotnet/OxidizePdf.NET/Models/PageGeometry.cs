using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Models;

/// <summary>Existing-page geometry as resolved by the Rust parser.</summary>
public sealed class PageGeometry
{
    /// <summary>One-based page number.</summary>
    [JsonPropertyName("page_number")] public int PageNumber { get; init; }
    /// <summary>Clockwise rotation from the page or nearest ancestor; defaults to zero. Raw core value, not normalized.</summary>
    [JsonPropertyName("rotation")] public int Rotation { get; init; }
    /// <summary>Resolved MediaBox in unrotated PDF user-space coordinates.</summary>
    [JsonPropertyName("media_box")] public PageGeometryBox MediaBox { get; init; } = new();
    /// <summary>Resolved CropBox, or null when absent. Absence means MediaBox is the effective crop.</summary>
    [JsonPropertyName("crop_box")] public PageGeometryBox? CropBox { get; init; }
    /// <summary>MediaBox width after the core's rotation handling; matches GetPageDimensionsAsync.</summary>
    [JsonPropertyName("width")] public double Width { get; init; }
    /// <summary>MediaBox height after the core's rotation handling; matches GetPageDimensionsAsync.</summary>
    [JsonPropertyName("height")] public double Height { get; init; }
}

/// <summary>A rectangle in unrotated PDF user space (normally points); coordinates retain their original origin.</summary>
public sealed class PageGeometryBox
{
    /// <summary>Lower-left horizontal coordinate.</summary>
    [JsonPropertyName("left")] public double Left { get; init; }
    /// <summary>Lower-left vertical coordinate.</summary>
    [JsonPropertyName("bottom")] public double Bottom { get; init; }
    /// <summary>Upper-right horizontal coordinate.</summary>
    [JsonPropertyName("right")] public double Right { get; init; }
    /// <summary>Upper-right vertical coordinate.</summary>
    [JsonPropertyName("top")] public double Top { get; init; }
}
