namespace OxidizePdf.NET;

/// <summary>Resource bounds for in-memory image extraction.</summary>
public sealed class ImageExtractionOptions
{
    /// <summary>Maximum images returned by one extraction.</summary>
    public int MaxImages { get; set; } = 1_000;
    /// <summary>Maximum encoded bytes retained for one image.</summary>
    public long MaxEncodedBytesPerImage { get; set; } = 64 * 1024 * 1024;
    /// <summary>Maximum encoded bytes retained across all images.</summary>
    public long MaxTotalEncodedBytes { get; set; } = 256 * 1024 * 1024;
    /// <summary>Maximum width-times-height pixel count for one decoded image.</summary>
    public long MaxDecodedPixelsPerImage { get; set; } = 100_000_000;

    internal void Validate()
    {
        if (MaxImages <= 0) throw new ArgumentOutOfRangeException(nameof(MaxImages));
        if (MaxEncodedBytesPerImage <= 0) throw new ArgumentOutOfRangeException(nameof(MaxEncodedBytesPerImage));
        if (MaxTotalEncodedBytes <= 0) throw new ArgumentOutOfRangeException(nameof(MaxTotalEncodedBytes));
        if (MaxDecodedPixelsPerImage <= 0) throw new ArgumentOutOfRangeException(nameof(MaxDecodedPixelsPerImage));
    }
}
