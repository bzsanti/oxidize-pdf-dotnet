namespace OxidizePdf.NET;

/// <summary>
/// Represents a single image extracted from a PDF document.
/// </summary>
public sealed class ExtractedImageInfo
{
    /// <summary>0-based page number where the image was found.</summary>
    public int PageNumber { get; init; }

    /// <summary>0-based index of the image within its page.</summary>
    public int ImageIndex { get; init; }

    /// <summary>Image width in pixels.</summary>
    public uint Width { get; init; }

    /// <summary>Image height in pixels.</summary>
    public uint Height { get; init; }

    /// <summary>Image format string: "jpeg", "png", "tiff", or "raw".</summary>
    public string Format { get; init; } = string.Empty;

    /// <summary>Raw image file bytes in the original format.</summary>
    public byte[] ImageData { get; init; } = [];
}
