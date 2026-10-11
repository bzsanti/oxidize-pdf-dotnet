using System.Text.Json.Serialization;

namespace OxidizePdf.NET;

/// <summary>Explicit preservation or reconstruction policy. Secondary structures unsupported by preserving merges are rejected.</summary>
public enum PdfExistingDocumentPolicy
{
    /// <summary>Preserve the first PDF as an exact prefix; reject unsupported secondary structures.</summary>
    PreserveBase,
    /// <summary>Preserve the base, explicitly ignoring secondary page labels.</summary>
    PreserveBaseWithFirstInputPageLabels,
    /// <summary>Rebuild page contents and discard document-level semantics, reported as discarded.</summary>
    Reconstruct,
    /// <summary>Reconstruct while copying the first input's document information metadata.</summary>
    ReconstructWithFirstInputMetadata
}

/// <summary>PDF bytes and optional zero-based page selection for a merge.</summary>
public sealed record PdfExistingMergeInput(byte[] PdfBytes, IReadOnlyList<int>? Pages = null);

/// <summary>Sequential page-tree mutation. Indexes refer to the state after preceding mutations.</summary>
public abstract record PdfPageMutation
{
    private PdfPageMutation() { }
    /// <summary>Move one page.</summary>
    public sealed record Move(int From, int To) : PdfPageMutation;
    /// <summary>Rotate clockwise by a multiple of 90 degrees.</summary>
    public sealed record Rotate(int Page, int Degrees) : PdfPageMutation;
    /// <summary>Remove one page.</summary>
    public sealed record Delete(int Page) : PdfPageMutation;
    /// <summary>Insert an independent clone of a page at a position.</summary>
    public sealed record Duplicate(int Page, int At) : PdfPageMutation;
    /// <summary>Import a page from another PDF at a position.</summary>
    public sealed record Insert(byte[] SourcePdf, int Page, int At) : PdfPageMutation;
}

/// <summary>Indirect PDF object identity.</summary>
public readonly record struct PdfIndirectObjectId(
    [property: JsonPropertyName("object_number")] uint ObjectNumber,
    [property: JsonPropertyName("generation_number")] ushort GenerationNumber);

/// <summary>Objects affected by a planned or completed incremental page mutation.</summary>
public sealed class PdfPageMutationReport
{
    /// <summary>Output page count.</summary>
    [JsonPropertyName("page_count")] public int PageCount { get; set; }
    /// <summary>Existing objects receiving new definitions.</summary>
    [JsonPropertyName("replaced_objects")] public List<PdfIndirectObjectId> ReplacedObjects { get; set; } = [];
    /// <summary>Newly allocated objects.</summary>
    [JsonPropertyName("added_objects")] public List<PdfIndirectObjectId> AddedObjects { get; set; } = [];
    /// <summary>Objects no longer reachable from the new catalog; original bytes still remain.</summary>
    [JsonPropertyName("unreachable_objects")] public List<PdfIndirectObjectId> UnreachableObjects { get; set; } = [];
}

/// <summary>One detected semantic structure and its selected treatment.</summary>
public sealed class PdfStructureReport
{
    /// <summary>Upstream structure name, such as Outlines, Forms, Annotations or DocumentInfo.</summary>
    [JsonPropertyName("structure")] public string Structure { get; set; } = string.Empty;
    /// <summary>Preserved, FirstInputWins, Rejected or Discarded.</summary>
    [JsonPropertyName("disposition")] public string Disposition { get; set; } = string.Empty;
}

/// <summary>Semantic inventory for one input PDF.</summary>
public sealed class PdfInputSemanticReport
{
    /// <summary>Zero-based index in the caller's input collection; temporary paths are not exposed.</summary>
    [JsonPropertyName("input_index")] public int InputIndex { get; set; }
    /// <summary>PreservedBase, ImportedPages or ReconstructedPages.</summary>
    [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
    /// <summary>Selected zero-based source pages.</summary>
    [JsonPropertyName("selected_pages")] public int[] SelectedPages { get; set; } = [];
    /// <summary>Detected structures and their treatment.</summary>
    [JsonPropertyName("structures")] public List<PdfStructureReport> Structures { get; set; } = [];
}

/// <summary>Semantic preservation report returned by both planning and execution.</summary>
public sealed class PdfSemanticPreservationReport
{
    /// <summary>PreserveBase or Reconstruct.</summary>
    [JsonPropertyName("engine")] public string Engine { get; set; } = string.Empty;
    /// <summary>Per-input roles, selections and semantic inventories.</summary>
    [JsonPropertyName("inputs")] public List<PdfInputSemanticReport> Inputs { get; set; } = [];
    /// <summary>Planned or completed output page count.</summary>
    [JsonPropertyName("page_count")] public int PageCount { get; set; }
    /// <summary>Object-level incremental plan; null for reconstruction.</summary>
    [JsonPropertyName("mutation")] public PdfPageMutationReport? Mutation { get; set; }
}

/// <summary>Outputs and reports from a structural operation. Planning produces no PDF outputs.</summary>
public sealed class PdfExistingDocumentResult
{
    /// <summary>Complete output PDFs in requested order; empty for planning.</summary>
    [JsonPropertyName("outputs")] public byte[][] Outputs { get; set; } = [];
    /// <summary>One semantic report per merge, extraction or split output.</summary>
    [JsonPropertyName("reports")] public List<PdfSemanticPreservationReport> Reports { get; set; } = [];
    /// <summary>Object-level report for page mutation and reorder operations.</summary>
    [JsonPropertyName("mutation")] public PdfPageMutationReport? Mutation { get; set; }
}
