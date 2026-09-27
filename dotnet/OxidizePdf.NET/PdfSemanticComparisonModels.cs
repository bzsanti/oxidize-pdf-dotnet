using System.Text.Json.Serialization;

namespace OxidizePdf.NET;

/// <summary>Resource bounds applied independently to each compared PDF.</summary>
public sealed class PdfSemanticComparisonLimits
{
    /// <summary>Maximum reachable indirect objects.</summary>
    [JsonPropertyName("max_objects")] public uint MaxObjects { get; set; } = 100000;
    /// <summary>Maximum direct and indirect nesting depth.</summary>
    [JsonPropertyName("max_depth")] public uint MaxDepth { get; set; } = 256;
    /// <summary>Maximum total decoded stream bytes.</summary>
    [JsonPropertyName("max_decoded_stream_bytes")] public ulong MaxDecodedStreamBytes { get; set; } = 256 * 1024 * 1024;
    /// <summary>Maximum total canonical representation bytes.</summary>
    [JsonPropertyName("max_canonical_bytes")] public ulong MaxCanonicalBytes { get; set; } = 256 * 1024 * 1024;
    /// <summary>Maximum logical text bytes.</summary>
    [JsonPropertyName("max_extracted_text_bytes")] public ulong MaxExtractedTextBytes { get; set; } = 64 * 1024 * 1024;
    /// <summary>Maximum latest in-use objects outside Root and Info.</summary>
    [JsonPropertyName("max_unreachable_objects")] public uint MaxUnreachableObjects { get; set; } = 10000;
    /// <summary>Maximum physical revisions.</summary>
    [JsonPropertyName("max_revisions")] public uint MaxRevisions { get; set; } = 1024;
}

/// <summary>Comparison of supported semantic domains and physical revision histories.</summary>
public sealed class PdfSemanticComparisonResult
{
    /// <summary>True if only serialization differs or all supported domains match.</summary>
    [JsonPropertyName("semantically_equal")] public bool SemanticallyEqual { get; set; }
    /// <summary>Differences sorted by stable logical path.</summary>
    [JsonPropertyName("differences")] public PdfSemanticDifference[] Differences { get; set; } = [];
    /// <summary>Left revisions, oldest first.</summary>
    [JsonPropertyName("left_revisions")] public PdfRevisionSummary[] LeftRevisions { get; set; } = [];
    /// <summary>Right revisions, oldest first.</summary>
    [JsonPropertyName("right_revisions")] public PdfRevisionSummary[] RightRevisions { get; set; } = [];
    /// <summary>Left latest in-use unreachable objects.</summary>
    [JsonPropertyName("left_unreachable_objects")] public PdfComparisonObjectId[] LeftUnreachableObjects { get; set; } = [];
    /// <summary>Right latest in-use unreachable objects.</summary>
    [JsonPropertyName("right_unreachable_objects")] public PdfComparisonObjectId[] RightUnreachableObjects { get; set; } = [];
    /// <summary>Objects paired by equivalent normalized semantics.</summary>
    [JsonPropertyName("equivalent_objects")] public PdfEquivalentObjectPair[] EquivalentObjects { get; set; } = [];
}

/// <summary>One observed semantic difference.</summary>
public sealed class PdfSemanticDifference
{
    /// <summary>Logical path independent of object numbering.</summary>
    [JsonPropertyName("path")] public string Path { get; set; } = "";
    /// <summary>Visual, Textual, Structural, Metadata, Security, or SerializationOnly.</summary>
    [JsonPropertyName("class")] public string Class { get; set; } = "";
    /// <summary>Upstream explanation of the change.</summary>
    [JsonPropertyName("description")] public string Description { get; set; } = "";
}

/// <summary>One physical cross-reference revision.</summary>
public sealed class PdfRevisionSummary
{
    /// <summary>Zero-based chronological index.</summary>
    [JsonPropertyName("index")] public uint Index { get; set; }
    /// <summary>Byte offset of the xref table or stream.</summary>
    [JsonPropertyName("xref_offset")] public ulong XrefOffset { get; set; }
    /// <summary>Lowercase hexadecimal SHA-256 of normalized document state.</summary>
    [JsonPropertyName("semantic_fingerprint")] public string SemanticFingerprint { get; set; } = "";
    /// <summary>Changes attributed to this revision.</summary>
    [JsonPropertyName("object_changes")] public PdfRevisionObjectChange[] ObjectChanges { get; set; } = [];
}

/// <summary>An indirect object state transition.</summary>
public sealed class PdfRevisionObjectChange
{
    /// <summary>Physical object identity.</summary>
    [JsonPropertyName("id")] public PdfComparisonObjectId Id { get; set; }
    /// <summary>Added, Replaced, or Freed.</summary>
    [JsonPropertyName("kind")] public string Kind { get; set; } = "";
}

/// <summary>A pair of equivalent objects across documents.</summary>
public sealed class PdfEquivalentObjectPair
{
    /// <summary>Left object identity.</summary>
    [JsonPropertyName("left")] public PdfComparisonObjectId Left { get; set; }
    /// <summary>Right object identity.</summary>
    [JsonPropertyName("right")] public PdfComparisonObjectId Right { get; set; }
}

/// <summary>Physical PDF indirect-object identity.</summary>
public readonly record struct PdfComparisonObjectId(
    [property: JsonPropertyName("object_number")] uint ObjectNumber,
    [property: JsonPropertyName("generation")] ushort Generation);
