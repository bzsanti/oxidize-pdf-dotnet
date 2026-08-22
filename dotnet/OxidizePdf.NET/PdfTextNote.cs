namespace OxidizePdf.NET;

/// <summary>Stable identity of an indirect PDF text-note annotation.</summary>
public readonly record struct PdfTextNoteId(uint ObjectNumber, ushort GenerationNumber);

/// <summary>A standard PDF text-note annotation in an existing document.</summary>
public sealed record PdfTextNote(PdfTextNoteId Id, uint PageIndex, double X, double Y, string Contents);

/// <summary>A mutation applied atomically to text-note annotations.</summary>
public abstract record PdfTextNoteMutation
{
    private PdfTextNoteMutation() { }
    /// <summary>Adds a 20×20 point note to a zero-based page.</summary>
    /// <param name="PageIndex">Zero-based page index.</param>
    /// <param name="X">Lower-left X coordinate.</param>
    /// <param name="Y">Lower-left Y coordinate.</param>
    /// <param name="Contents">Non-empty note contents.</param>
    public sealed record Add(uint PageIndex, double X, double Y, string Contents) : PdfTextNoteMutation;
    /// <summary>Moves and edits a note while preserving its other dictionary keys.</summary>
    /// <param name="Id">Stable note identity.</param>
    /// <param name="X">New lower-left X coordinate.</param>
    /// <param name="Y">New lower-left Y coordinate.</param>
    /// <param name="Contents">New non-empty contents.</param>
    public sealed record Update(PdfTextNoteId Id, double X, double Y, string Contents) : PdfTextNoteMutation;
    /// <summary>Removes an existing note from its page.</summary>
    /// <param name="Id">Stable note identity.</param>
    public sealed record Remove(PdfTextNoteId Id) : PdfTextNoteMutation;
}

/// <summary>Result of one incremental text-note edit revision.</summary>
public sealed record PdfTextNoteUpdate(byte[] PdfBytes, IReadOnlyList<PdfTextNote> AddedNotes);
