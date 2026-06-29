using Microsoft.KernelMemory.DataFormats;

namespace Microsoft.KernelMemory;

/// <summary>
/// Kernel Memory builder extensions for the oxidize-pdf content decoder.
/// </summary>
public static class OxidizePdfKernelMemoryBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="OxidizePdf.NET.KernelMemory.OxidizePdfDecoder"/> as an
    /// <see cref="IContentDecoder"/>, so Kernel Memory uses oxidize-pdf's
    /// structure-aware chunking for <c>application/pdf</c> documents.
    /// </summary>
    /// <param name="builder">The Kernel Memory builder.</param>
    /// <param name="options">Optional chunking options (null = RAG profile defaults).</param>
    public static IKernelMemoryBuilder WithOxidizePdf(
        this IKernelMemoryBuilder builder,
        OxidizePdf.NET.KernelMemory.OxidizePdfDecoderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddSingleton<IContentDecoder>(
            new OxidizePdf.NET.KernelMemory.OxidizePdfDecoder(options));
        return builder;
    }
}
