namespace OxidizePdf.NET;

/// <summary>A split PDF and its suggested filename. The caller owns the byte array
/// and decides whether and where to persist it.</summary>
public sealed class PdfSplitResult
{
    /// <summary>Suggested filename, never an instruction to write a file.</summary>
    public required string FileName { get; init; }

    /// <summary>Owned PDF bytes, independent of input and other results.</summary>
    public required byte[] Bytes { get; init; }

    /// <summary>Always false with core 5.4.2, whose split optimization flag is a no-op.</summary>
    public bool OptimizationApplied => false;
}

/// <summary>Failure category for a native split operation.</summary>
public enum PdfSplitError
{
    /// <summary>Invalid options or page selection.</summary>
    InvalidInput,
    /// <summary>Source PDF cannot be parsed.</summary>
    InvalidPdf,
    /// <summary>Temporary storage failure.</summary>
    Io,
    /// <summary>Result encoding failed.</summary>
    Serialization,
    /// <summary>Other native failure.</summary>
    NativeFailure,
}

/// <summary>Typed native split failure, compatible with PdfExtractionException handlers.</summary>
public sealed class PdfSplitException : PdfExtractionException
{
    /// <summary>Failure category.</summary>
    public PdfSplitError Error { get; }
    internal PdfSplitException(PdfSplitError error, string message) : base(message) => Error = error;
}
