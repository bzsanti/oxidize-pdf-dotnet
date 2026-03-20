using System.Text.Json;
using System.Text.Json.Serialization;

namespace OxidizePdf.NET;

/// <summary>
/// Specifies which pages of a PDF to include in a merge operation.
/// </summary>
public abstract class PdfPageRange
{
    private PdfPageRange() { }

    /// <summary>Include all pages.</summary>
    public sealed class All : PdfPageRange { }

    /// <summary>Include a single page by 0-based index.</summary>
    public sealed class Single : PdfPageRange
    {
        /// <summary>0-based page index.</summary>
        public int Index { get; }

        /// <param name="index">0-based page index.</param>
        public Single(int index)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            Index = index;
        }
    }

    /// <summary>Include a contiguous range of pages (0-based, both ends inclusive).</summary>
    public sealed class Range : PdfPageRange
    {
        /// <summary>0-based index of the first page in the range.</summary>
        public int From { get; }

        /// <summary>0-based index of the last page in the range (inclusive).</summary>
        public int To { get; }

        /// <param name="from">0-based index of the first page.</param>
        /// <param name="to">0-based index of the last page (inclusive).</param>
        public Range(int from, int to)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(from);
            ArgumentOutOfRangeException.ThrowIfNegative(to);
            if (from > to)
                throw new ArgumentException($"'from' ({from}) must not be greater than 'to' ({to}).");
            From = from;
            To = to;
        }
    }

    /// <summary>Include an arbitrary list of pages by 0-based indices.</summary>
    public sealed class List : PdfPageRange
    {
        /// <summary>0-based page indices to include.</summary>
        public IReadOnlyList<int> Indices { get; }

        /// <param name="indices">0-based page indices to include.</param>
        public List(IReadOnlyList<int> indices)
        {
            ArgumentNullException.ThrowIfNull(indices);
            if (indices.Count == 0)
                throw new ArgumentException("At least one page index is required.", nameof(indices));
            foreach (var idx in indices)
                ArgumentOutOfRangeException.ThrowIfNegative(idx);
            Indices = indices;
        }
    }

    /// <summary>Serialises the page range to the JSON format expected by the native FFI.</summary>
    internal object ToJsonObject() => this switch
    {
        All => new { kind = "All" },
        Single s => (object)new { kind = "Single", index = s.Index },
        Range r => new { kind = "Range", from = r.From, to = r.To },
        List l => new { kind = "List", indices = l.Indices },
        _ => throw new InvalidOperationException($"Unknown PdfPageRange type: {GetType()}"),
    };
}

/// <summary>
/// Represents a single PDF input (with an optional page range) for use with
/// <see cref="PdfOperations.MergeAsync(System.Collections.Generic.IReadOnlyList{PdfMergeInput}, CancellationToken)"/>.
/// </summary>
public sealed class PdfMergeInput
{
    /// <summary>The PDF content as a byte array.</summary>
    public byte[] PdfBytes { get; }

    /// <summary>
    /// Optional page range. When <c>null</c>, all pages are included.
    /// </summary>
    public PdfPageRange? Pages { get; }

    /// <param name="pdfBytes">The PDF content. Must not be null or empty.</param>
    /// <param name="pages">Pages to include, or <c>null</c> for all pages.</param>
    public PdfMergeInput(byte[] pdfBytes, PdfPageRange? pages = null)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty.", nameof(pdfBytes));
        PdfBytes = pdfBytes;
        Pages = pages;
    }

    /// <summary>Serialises the input to the anonymous object expected by the FFI JSON array.</summary>
    internal object ToJsonObject()
    {
        var b64 = Convert.ToBase64String(PdfBytes);
        object? pagesObj = Pages?.ToJsonObject();
        return new { pdf = b64, pages = pagesObj };
    }
}
