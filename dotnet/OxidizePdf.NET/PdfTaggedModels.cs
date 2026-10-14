using System.Text.Json;
using System.Text.Json.Serialization;

namespace OxidizePdf.NET;

/// <summary>Resource limits for tagged structure inspection and editing.</summary>
public sealed class PdfTaggedLimits
{
    /// <summary>Maximum visited indirect objects.</summary>
    [JsonPropertyName("max_objects")] public uint MaxObjects { get; set; } = 100000;
    /// <summary>Maximum structure and number-tree depth.</summary>
    [JsonPropertyName("max_depth")] public uint MaxDepth { get; set; } = 256;
    /// <summary>Maximum children and number-tree entries.</summary>
    [JsonPropertyName("max_entries")] public uint MaxEntries { get; set; } = 1000000;
    /// <summary>Maximum decoded content inspected for MCIDs.</summary>
    [JsonPropertyName("max_decoded_content_bytes")] public ulong MaxDecodedContentBytes { get; set; } = 256 * 1024 * 1024;
}

/// <summary>Bounded tagged-structure inspection; not a complete PDF/UA conformance certificate.</summary>
public sealed class PdfTaggedValidation
{
    /// <summary>Whether a structure tree is present.</summary>
    [JsonPropertyName("tagged")] public bool Tagged { get; set; }
    /// <summary>Whether upstream found any structural errors.</summary>
    [JsonPropertyName("valid")] public bool Valid { get; set; }
    /// <summary>Root object identity.</summary>
    [JsonPropertyName("structure_tree_root")] public PdfTaggedObjectId? StructureTreeRoot { get; set; }
    /// <summary>Custom-to-standard role mapping.</summary>
    [JsonPropertyName("role_map")] public Dictionary<string,string> RoleMap { get; set; } = [];
    /// <summary>Declared attribute classes.</summary>
    [JsonPropertyName("class_names")] public string[] ClassNames { get; set; } = [];
    /// <summary>Class values as typed PDF value JSON.</summary>
    [JsonPropertyName("class_map")] public Dictionary<string,JsonElement> ClassMap { get; set; } = [];
    /// <summary>Marked artifacts in content.</summary>
    [JsonPropertyName("artifacts")] public PdfTaggedArtifact[] Artifacts { get; set; } = [];
    /// <summary>Inspected structure elements.</summary>
    [JsonPropertyName("elements")] public PdfTaggedElement[] Elements { get; set; } = [];
    /// <summary>Parent-tree entry count.</summary>
    [JsonPropertyName("parent_tree_entries")] public uint ParentTreeEntries { get; set; }
    /// <summary>Parent-tree keys and typed PDF values.</summary>
    [JsonPropertyName("parent_tree")] public PdfTaggedParentEntry[] ParentTree { get; set; } = [];
    /// <summary>Observed MCIDs by content context.</summary>
    [JsonPropertyName("content_mcids")] public PdfTaggedContentMcids[] ContentMcids { get; set; } = [];
    /// <summary>Stable validation findings.</summary>
    [JsonPropertyName("findings")] public PdfTaggedFinding[] Findings { get; set; } = [];
}

/// <summary>An existing indirect structure element.</summary>
public sealed class PdfTaggedElement
{
    /// <summary>Object identity.</summary>
    [JsonPropertyName("object")] public PdfTaggedObjectId Object { get; set; }
    /// <summary>PDF structure type.</summary>
    [JsonPropertyName("structure_type")] public string? StructureType { get; set; }
    /// <summary>Parent identity.</summary>
    [JsonPropertyName("parent")] public PdfTaggedObjectId? Parent { get; set; }
    /// <summary>Child count.</summary>
    [JsonPropertyName("child_count")] public uint ChildCount { get; set; }
    /// <summary>Marked-content association count.</summary>
    [JsonPropertyName("marked_content_count")] public uint MarkedContentCount { get; set; }
    /// <summary>Language tag.</summary>
    [JsonPropertyName("language")] public string? Language { get; set; }
    /// <summary>Alternate text.</summary>
    [JsonPropertyName("alternate_text")] public string? AlternateText { get; set; }
    /// <summary>Replacement text.</summary>
    [JsonPropertyName("actual_text")] public string? ActualText { get; set; }
    /// <summary>Element title.</summary>
    [JsonPropertyName("title")] public string? Title { get; set; }
    /// <summary>Original dictionary keys.</summary>
    [JsonPropertyName("dictionary_keys")] public string[] DictionaryKeys { get; set; } = [];
    /// <summary>Original values as typed PDF value JSON; strings retain raw bytes.</summary>
    [JsonPropertyName("dictionary")] public Dictionary<string,JsonElement> Dictionary { get; set; } = [];
}

/// <summary>One marked-content artifact.</summary>
public sealed class PdfTaggedArtifact
{
    /// <summary>Content context identity.</summary>
    [JsonPropertyName("context")] public PdfTaggedObjectId Context { get; set; }
    /// <summary>Content operator index.</summary>
    [JsonPropertyName("operation_index")] public uint OperationIndex { get; set; }
    /// <summary>Artifact subtype.</summary>
    [JsonPropertyName("subtype")] public string? Subtype { get; set; }
}

/// <summary>Parent-tree mapping.</summary>
public sealed class PdfTaggedParentEntry
{
    /// <summary>Number-tree key.</summary>
    [JsonPropertyName("key")] public long Key { get; set; }
    /// <summary>Typed PDF value JSON.</summary>
    [JsonPropertyName("value")] public JsonElement Value { get; set; }
}

/// <summary>Marked content observed in a page or form.</summary>
public sealed class PdfTaggedContentMcids
{
    /// <summary>Content context identity.</summary>
    [JsonPropertyName("context")] public PdfTaggedObjectId Context { get; set; }
    /// <summary>Observed marked-content IDs.</summary>
    [JsonPropertyName("mcids")] public long[] Mcids { get; set; } = [];
}

/// <summary>A stable upstream tagged-structure diagnostic.</summary>
public sealed class PdfTaggedFinding
{
    /// <summary>Error or Warning.</summary>
    [JsonPropertyName("severity")] public string Severity { get; set; } = "";
    /// <summary>Stable upstream finding identifier.</summary>
    [JsonPropertyName("code")] public string Code { get; set; } = "";
    /// <summary>Logical location.</summary>
    [JsonPropertyName("path")] public string Path { get; set; } = "";
    /// <summary>Diagnostic explanation.</summary>
    [JsonPropertyName("message")] public string Message { get; set; } = "";
    /// <summary>Associated object, when available.</summary>
    [JsonPropertyName("object")] public PdfTaggedObjectId? Object { get; set; }
}

/// <summary>Exact objects affected by the requested atomic edit.</summary>
public sealed class PdfTaggedEditPlan
{
    /// <summary>Objects that would change.</summary>
    [JsonPropertyName("changed_objects")] public PdfTaggedChangedObject[] ChangedObjects { get; set; } = [];
    /// <summary>Normalized mutation descriptions with typed PDF values.</summary>
    [JsonPropertyName("mutations")] public JsonElement[] Mutations { get; set; } = [];
}

/// <summary>One affected indirect object.</summary>
public sealed class PdfTaggedChangedObject
{
    /// <summary>Object identity.</summary>
    [JsonPropertyName("object")] public PdfTaggedObjectId Object { get; set; }
    /// <summary>StructureElement, StructureTreeRoot, ParentTree, or CrossReference.</summary>
    [JsonPropertyName("kind")] public string Kind { get; set; } = "";
}

/// <summary>Incremental output and validation before and after editing.</summary>
public sealed class PdfTaggedEditResult
{
    /// <summary>Updated PDF bytes, preserving the input prefix.</summary>
    [JsonPropertyName("pdf_bytes")] public byte[] PdfBytes { get; set; } = [];
    /// <summary>Applied plan.</summary>
    [JsonPropertyName("plan")] public PdfTaggedEditPlan Plan { get; set; } = new();
    /// <summary>Input inspection.</summary>
    [JsonPropertyName("validation_before")] public PdfTaggedValidation ValidationBefore { get; set; } = new();
    /// <summary>Output inspection.</summary>
    [JsonPropertyName("validation_after")] public PdfTaggedValidation ValidationAfter { get; set; } = new();
}

/// <summary>Indirect object identity in an existing tagged PDF.</summary>
public readonly record struct PdfTaggedObjectId(
    [property: JsonPropertyName("object_number")] uint ObjectNumber,
    [property: JsonPropertyName("generation")] ushort Generation);

/// <summary>A controlled mutation of existing tagged structure.</summary>
public abstract record PdfTaggedMutation
{
    private PdfTaggedMutation() { }
    /// <summary>Creates an element under an existing parent, optionally at a child index.</summary>
    public sealed record CreateElement(PdfTaggedObjectId Parent, string StructureType,
        Dictionary<string, PdfObjectValue>? Attributes = null, uint? Index = null) : PdfTaggedMutation;
    /// <summary>Sets an attribute; null removes it. Structural keys are protected by upstream.</summary>
    public sealed record SetElementAttribute(PdfTaggedObjectId Element, string Key, PdfObjectValue? Value) : PdfTaggedMutation;
    /// <summary>Moves an element under a new parent, optionally at a child index.</summary>
    public sealed record ReparentElement(PdfTaggedObjectId Element, PdfTaggedObjectId NewParent, uint? Index = null) : PdfTaggedMutation;
    /// <summary>Associates an MCID already present in page content.</summary>
    public sealed record AssociateMcid(PdfTaggedObjectId Element, PdfTaggedObjectId Page, uint Mcid) : PdfTaggedMutation;
    /// <summary>Sets a ParentTree entry; null removes it.</summary>
    public sealed record SetParentTreeEntry(long Key, PdfObjectValue? Value) : PdfTaggedMutation;
}
