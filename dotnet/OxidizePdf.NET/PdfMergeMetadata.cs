using System.Runtime.InteropServices;
using System.Text.Json;

namespace OxidizePdf.NET;

/// <summary>Source of Title, Author, Subject and Keywords in a reconstructing merge.</summary>
public enum PdfMergeMetadataMode
{
    /// <summary>Copy supported Info fields from the first input (default).</summary>
    FromFirst,
    /// <summary>Copy supported Info fields from the zero-based SourceIndex input.</summary>
    FromDocument,
    /// <summary>Use the supplied custom fields.</summary>
    Custom,
    /// <summary>Copy no source fields. Writer-generated Info may still be present.</summary>
    None
}

/// <summary>Four Info fields supported by the core merge. Null omits a field; empty is a value.</summary>
public sealed class PdfMergeMetadata
{
    /// <summary>Document title.</summary>
    public string? Title { get; set; }
    /// <summary>Document author.</summary>
    public string? Author { get; set; }
    /// <summary>Document subject.</summary>
    public string? Subject { get; set; }
    /// <summary>Document keywords as a single string.</summary>
    public string? Keywords { get; set; }
}

/// <summary>Metadata selection only; does not enable structural or XMP preservation.</summary>
public sealed class PdfMergeMetadataOptions
{
    /// <summary>Defaults to FromFirst, matching existing merge methods.</summary>
    public PdfMergeMetadataMode Mode { get; set; } = PdfMergeMetadataMode.FromFirst;
    /// <summary>Required only for FromDocument. Index in the original input list, not a page index.</summary>
    public int? SourceIndex { get; set; }
    /// <summary>Required only for Custom. All-null fields are allowed.</summary>
    public PdfMergeMetadata? Custom { get; set; }

    internal object Snapshot(int inputCount)
    {
        if (!Enum.IsDefined(Mode)) throw new ArgumentOutOfRangeException(nameof(Mode));
        if (Mode != PdfMergeMetadataMode.FromDocument && SourceIndex is not null)
            throw new ArgumentException("SourceIndex is only valid for FromDocument.");
        if (Mode != PdfMergeMetadataMode.Custom && Custom is not null)
            throw new ArgumentException("Custom fields are only valid for Custom mode.");
        if (Mode == PdfMergeMetadataMode.FromDocument)
        {
            if (SourceIndex is null || SourceIndex < 0 || SourceIndex >= inputCount)
                throw new ArgumentOutOfRangeException(nameof(SourceIndex), "Metadata source must index an input document.");
            return new { mode = "FromDocument", index = SourceIndex.Value };
        }
        if (Mode == PdfMergeMetadataMode.Custom)
        {
            if (Custom is null) throw new ArgumentException("Custom metadata is required.");
            return new { mode = "Custom", title = Custom.Title, author = Custom.Author, subject = Custom.Subject, keywords = Custom.Keywords };
        }
        return new { mode = Mode.ToString() };
    }
}

/// <summary>Failure category for a native merge with metadata.</summary>
public enum PdfMergeError
{
    /// <summary>Invalid request or page selection.</summary>
    InvalidInput,
    /// <summary>Input PDF could not be parsed or reconstructed.</summary>
    InvalidPdf,
    /// <summary>Internal temporary-file access failed.</summary>
    Io,
    /// <summary>Output serialization failed.</summary>
    Serialization,
    /// <summary>Unexpected native failure, including a caught panic.</summary>
    NativeFailure
}

/// <summary>A native metadata merge failure. Managed argument validation uses argument exceptions.</summary>
public sealed class PdfMergeException : PdfExtractionException
{
    /// <summary>Structured failure category.</summary>
    public PdfMergeError Error { get; }
    internal PdfMergeException(PdfMergeError error, string message) : base(message) => Error = error;
}

public static partial class PdfOperations
{
    /// <summary>Reconstructs selected pages and selects their output Info metadata.</summary>
    /// <remarks>
    /// Copies only Title, Author, Subject and Keywords; never preserves XMP, bookmarks,
    /// forms or arbitrary Info keys. Use PdfExistingDocumentOperations for structural
    /// preservation policies. Inputs/options are snapshotted before scheduling. Callers
    /// retain inputs and own the returned byte array. Internal temporary inputs are
    /// removed on return; no caller-specified files are written. Cancellation affects
    /// queued work only, not a running native operation. Core metadata reading is
    /// best-effort: unavailable source fields are omitted.
    /// </remarks>
    /// <param name="inputs">Nonempty ordered inputs with optional page selections.</param>
    /// <param name="options">Metadata policy, or null for FromFirst.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An owned merged PDF byte array.</returns>
    /// <exception cref="ArgumentException">Invalid inputs, selection or option combination.</exception>
    /// <exception cref="PdfMergeException">Native parsing, selection, I/O or serialization failed.</exception>
    /// <exception cref="OperationCanceledException">Work was cancelled before execution.</exception>
    public static Task<byte[]> MergeWithMetadataAsync(IReadOnlyList<PdfMergeInput> inputs,
        PdfMergeMetadataOptions? options = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(inputs);
        if (inputs.Count == 0) throw new ArgumentException("At least one PDF is required.", nameof(inputs));
        var metadata = (options ?? new PdfMergeMetadataOptions()).Snapshot(inputs.Count);
        var snapshots = new object[inputs.Count];
        for (int i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i] ?? throw new ArgumentException("Merge inputs cannot contain null.", nameof(inputs));
            if (input.Pages is PdfPageRange.List list && (list.Indices.Count == 0 || list.Indices.Any(index => index < 0)))
                throw new ArgumentException("Page lists must contain nonnegative indices.", nameof(inputs));
            snapshots[i] = input.ToJsonObject();
        }
        var json = JsonSerializer.Serialize(new { inputs = snapshots, metadata });
        return Task.Run(() => MergeMetadata(json), ct);
    }

    private static byte[] MergeMetadata(string json)
    {
        IntPtr output = IntPtr.Zero;
        nuint length = 0;
        try
        {
            int code = NativeMethods.oxidize_merge_pdfs_with_metadata(json, out output, out length);
            if (code != 0)
            {
                var message = NativeMethods.GetLastError() ?? "Native metadata merge failed.";
                var error = (NativeMethods.ErrorCode)code switch
                {
                    NativeMethods.ErrorCode.InvalidArgument => PdfMergeError.InvalidInput,
                    NativeMethods.ErrorCode.PdfParseError => PdfMergeError.InvalidPdf,
                    NativeMethods.ErrorCode.IoError => PdfMergeError.Io,
                    NativeMethods.ErrorCode.SerializationError => PdfMergeError.Serialization,
                    _ => PdfMergeError.NativeFailure
                };
                throw new PdfMergeException(error, message);
            }
            var bytes = new byte[checked((int)length)];
            Marshal.Copy(output, bytes, 0, bytes.Length);
            return bytes;
        }
        finally
        {
            if (output != IntPtr.Zero) NativeMethods.oxidize_free_bytes(output, length);
        }
    }
}
