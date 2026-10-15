using System.Text.Json.Serialization;

namespace OxidizePdf.NET;

/// <summary>Explicit preparation before tagged editing. At least one step must be
/// selected. Recovery runs before language preparation; neither invents author metadata.</summary>
public sealed class PdfTaggedPreparationOptions
{
    /// <summary>Opt in to unambiguous derived-index/page-key recovery and retirement
    /// of unreachable zero-offset xref entries. Default false.</summary>
    [JsonPropertyName("recover_indexes")]
    public bool RecoverIndexes { get; init; }

    /// <summary>Supply missing catalog language. Null leaves it unchanged; blank or
    /// existing nonempty language is rejected. The value is not inferred or normalized.</summary>
    [JsonPropertyName("document_language")]
    public string? DocumentLanguage { get; init; }
}

/// <summary>Preparation inventory and subsequent mutation plan, without publishing bytes.</summary>
public sealed class PdfTaggedPreparationPlan
{
    /// <summary>Exact identities changed by preparation, including xref stream objects
    /// and original identities of retired entries. Sorted and deduplicated by core.</summary>
    [JsonPropertyName("preparation_objects")]
    public PdfTaggedObjectId[] PreparationObjects { get; set; } = [];

    /// <summary>Mutation plan relative to the prepared baseline. Does not include
    /// preparation objects and does not certify final validity.</summary>
    [JsonPropertyName("mutation_plan")]
    public PdfTaggedEditPlan MutationPlan { get; set; } = new();
}

/// <summary>Validated prepared output. Preparation inventory remains separate from Edit.Plan.</summary>
public sealed class PdfTaggedPreparedEditResult
{
    /// <summary>Complete object inventory for preparation, including retired identities.</summary>
    [JsonPropertyName("preparation_objects")]
    public PdfTaggedObjectId[] PreparationObjects { get; set; } = [];

    /// <summary>Owned output bytes and mutation report. ValidationBefore is the prepared
    /// baseline. ValidationAfter must be valid according to core structural checks;
    /// this is not full PDF/UA certification.</summary>
    [JsonPropertyName("edit")]
    public PdfTaggedEditResult Edit { get; set; } = new();
}

/// <summary>Failure categories for preflight and prepared tagged operations.</summary>
public enum PdfTaggedError
{
    /// <summary>Invalid request, ambiguous structure, limits, or final validation failure.</summary>
    InvalidInputOrStructure,
    /// <summary>Source PDF parsing failure.</summary>
    InvalidPdf,
    /// <summary>Encryption or certification policy prohibits the operation.</summary>
    PermissionDenied,
    /// <summary>Native I/O failure.</summary>
    Io,
    /// <summary>Output serialization or text encoding failure.</summary>
    Serialization,
    /// <summary>Unexpected native failure.</summary>
    NativeFailure,
}

/// <summary>A typed failure from preflight or prepared editing. Ordinary APIs retain
/// their existing PdfExtractionException behavior.</summary>
public sealed class PdfTaggedException : PdfExtractionException
{
    /// <summary>Failure category; Message retains the core diagnostic.</summary>
    public PdfTaggedError Error { get; }
    internal PdfTaggedException(PdfTaggedError error, string message) : base(message) => Error = error;
}
