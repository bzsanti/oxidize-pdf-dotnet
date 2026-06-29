using OxidizePdf.NET.Pipeline;

namespace OxidizePdf.NET.KernelMemory;

/// <summary>
/// Options controlling how <see cref="OxidizePdfDecoder"/> chunks PDFs before
/// handing them to Kernel Memory. Defaults to the RAG profile.
/// </summary>
public sealed class OxidizePdfDecoderOptions
{
    /// <summary>Extraction profile used when no explicit configs are set. Defaults to <see cref="ExtractionProfile.Rag"/>.</summary>
    public ExtractionProfile Profile { get; set; } = ExtractionProfile.Rag;

    /// <summary>Optional explicit partition config. When set (with or without <see cref="Hybrid"/>), it overrides <see cref="Profile"/>.</summary>
    public PartitionConfig? Partition { get; set; }

    /// <summary>Optional explicit hybrid-chunk config. When set (with or without <see cref="Partition"/>), it overrides <see cref="Profile"/>.</summary>
    public HybridChunkConfig? Hybrid { get; set; }
}
