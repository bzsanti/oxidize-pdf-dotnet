// Verifies the legacy character-based ChunkOptions has been marked
// [Obsolete] with a migration message pointing callers at the
// token-aware HybridChunkConfig / SemanticChunkConfig replacements.
//
// File-level pragma is required because referencing the obsolete type
// itself triggers CS0618 under TreatWarningsAsErrors=true.

#pragma warning disable CS0618 // Type or member is obsolete

using OxidizePdf.NET.Models;

namespace OxidizePdf.NET.Tests.Pipeline;

public class ChunkOptionsObsoleteTests
{
    [Fact]
    public void ChunkOptions_carries_ObsoleteAttribute()
    {
        var attrs = typeof(ChunkOptions).GetCustomAttributes(typeof(ObsoleteAttribute), inherit: false);
        Assert.Single(attrs);
    }

    [Fact]
    public void ChunkOptions_obsolete_message_mentions_replacements()
    {
        var attr = (ObsoleteAttribute)typeof(ChunkOptions)
            .GetCustomAttributes(typeof(ObsoleteAttribute), inherit: false)[0];

        Assert.False(string.IsNullOrEmpty(attr.Message));
        Assert.Contains("HybridChunkConfig", attr.Message);
        Assert.Contains("SemanticChunkConfig", attr.Message);
    }

    [Fact]
    public void ChunkOptions_obsolete_is_not_an_error_yet()
    {
        // We're warning users for one minor release before removing — the
        // overload they're calling must still compile and run.
        var attr = (ObsoleteAttribute)typeof(ChunkOptions)
            .GetCustomAttributes(typeof(ObsoleteAttribute), inherit: false)[0];

        Assert.False(attr.IsError, "ChunkOptions must remain callable for one minor release; flip IsError only on the removal release");
    }
}

#pragma warning restore CS0618
