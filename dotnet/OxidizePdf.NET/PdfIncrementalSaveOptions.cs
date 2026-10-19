using System.Text.Json;

namespace OxidizePdf.NET;

/// <summary>How supplied pages are applied to the base PDF.</summary>
public enum PdfIncrementalPageMode
{
    /// <summary>Keep all base pages, then append the supplied pages.</summary>
    AppendPages,
    /// <summary>Replace the first N base pages with the N supplied pages; retain
    /// the remaining base pages. Additional supplied pages extend the document.
    /// This replaces content, rather than drawing an overlay.</summary>
    ReplaceLeadingPages,
}

/// <summary>Policy for the Info dictionary in an incremental revision.</summary>
public enum PdfIncrementalInfoPolicy
{
    /// <summary>Keep the original Info reference, including absence.</summary>
    Preserve,
    /// <summary>Write the supplied document's metadata and build-identification
    /// policy. Original custom Info fields are not copied. XMP is unchanged.</summary>
    Replace,
}

/// <summary>Configuration for a real base-document incremental write.</summary>
public sealed class PdfIncrementalSaveOptions
{
    /// <summary>Append by default.</summary>
    public PdfIncrementalPageMode PageMode { get; init; }
    /// <summary>Preserve the original Info dictionary by default.</summary>
    public PdfIncrementalInfoPolicy InfoPolicy { get; init; }
    /// <summary>Core writer settings, default Legacy. Xref/object streams must
    /// both be false. Compression controls newly written streams. PdfVersion is
    /// passed to the core but cannot change the preserved base header/version.
    /// The fifth core field, incremental_update, is always true for this operation.</summary>
    public PdfSaveOptions Writer { get; init; } = PdfSaveOptions.Legacy();

    internal string ToJson()
    {
        if (!Enum.IsDefined(PageMode)) throw new ArgumentException("Invalid page mode", nameof(PageMode));
        if (!Enum.IsDefined(InfoPolicy)) throw new ArgumentException("Invalid Info policy", nameof(InfoPolicy));
        ArgumentNullException.ThrowIfNull(Writer);
        if (Writer.UseXrefStreams || Writer.UseObjectStreams)
            throw new ArgumentException("Incremental output requires classic xrefs without object streams", nameof(Writer));
        if (Writer.PdfVersion is not ("1.0" or "1.1" or "1.2" or "1.3" or "1.4" or "1.5" or "1.6" or "1.7" or "2.0"))
            throw new ArgumentException("Invalid PDF version", nameof(Writer));
        return JsonSerializer.Serialize(new
        {
            mode = PageMode.ToString(),
            info_policy = InfoPolicy.ToString(),
            use_xref_streams = Writer.UseXrefStreams,
            use_object_streams = Writer.UseObjectStreams,
            pdf_version = Writer.PdfVersion,
            compress_streams = Writer.CompressStreams,
            incremental_update = true,
        });
    }
}

/// <summary>Failure category from the incremental writer.</summary>
public enum PdfIncrementalSaveError
{
    /// <summary>Invalid native options or input.</summary>
    InvalidInput,
    /// <summary>Base PDF cannot be parsed.</summary>
    InvalidPdf,
    /// <summary>Encrypted bases are unsupported.</summary>
    EncryptedBase,
    /// <summary>Temporary storage failed.</summary>
    Io,
    /// <summary>Core rejected the document structure or could not write the revision.</summary>
    Serialization,
    /// <summary>Other native failure.</summary>
    NativeFailure,
}

/// <summary>Typed native incremental-writing failure.</summary>
public sealed class PdfIncrementalSaveException : PdfExtractionException
{
    /// <summary>Native failure category.</summary>
    public PdfIncrementalSaveError Error { get; }
    internal PdfIncrementalSaveException(PdfIncrementalSaveError error, string message) : base(message) => Error = error;
}
