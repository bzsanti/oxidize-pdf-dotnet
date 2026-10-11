namespace OxidizePdf.NET;

/// <summary>Controls emission of oxidize build-identification metadata.</summary>
public enum BuildIdentification : byte
{
    /// <summary>Emit core build, edition and feature identifiers (default).</summary>
    Enabled,
    /// <summary>Omit those identifiers while preserving application metadata.</summary>
    Disabled
}
