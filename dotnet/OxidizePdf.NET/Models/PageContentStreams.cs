namespace OxidizePdf.NET.Models;

/// <summary>
/// Decoded content streams from a specific page of a PDF document.
/// </summary>
public class PageContentStreams
{
    /// <summary>The decoded content stream byte arrays.</summary>
    public IReadOnlyList<byte[]> Streams { get; }

    /// <summary>Number of content streams on the page.</summary>
    public int Count => Streams.Count;

    /// <summary>Whether the page has no content streams.</summary>
    public bool IsEmpty => Streams.Count == 0;

    /// <summary>
    /// Creates a new instance wrapping the given content streams.
    /// </summary>
    public PageContentStreams(IReadOnlyList<byte[]> streams)
    {
        Streams = streams ?? Array.Empty<byte[]>();
    }
}
