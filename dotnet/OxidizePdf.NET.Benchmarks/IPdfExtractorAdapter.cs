namespace OxidizePdf.NET.Benchmarks;

/// <summary>The result of extracting all-pages plain text from one PDF.</summary>
public sealed record ExtractResult(int PageCount, string Text);

/// <summary>
/// Common contract every PDF library is driven through. The identical
/// operation for every adapter — concatenated plain text from every page —
/// is what makes the comparison apples-to-apples.
/// </summary>
public interface IPdfExtractorAdapter
{
    /// <summary>Display name, e.g. "OxidizePdf.NET".</summary>
    string Name { get; }

    /// <summary>License label, e.g. "MIT" or "AGPL".</summary>
    string License { get; }

    /// <summary>The underlying library's package version.</summary>
    string Version { get; }

    /// <summary>Extract plain text from EVERY page, concatenated.</summary>
    ExtractResult Extract(byte[] pdfBytes);
}
