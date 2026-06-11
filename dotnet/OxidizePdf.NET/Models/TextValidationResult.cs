using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Models;

/// <summary>
/// TXT-016 — A single match found by <see cref="TextValidation"/> within a text
/// string, with its classified type and position.
/// </summary>
public sealed class TextMatch
{
    /// <summary>The matched substring.</summary>
    [JsonPropertyName("text")] public string Text { get; init; } = "";

    /// <summary>Zero-based character offset of the match in the input text.</summary>
    [JsonPropertyName("position")] public int Position { get; init; }

    /// <summary>Length of the matched substring.</summary>
    [JsonPropertyName("length")] public int Length { get; init; }

    /// <summary>Match confidence in 0.0..1.0.</summary>
    [JsonPropertyName("confidence")] public double Confidence { get; init; }

    /// <summary>
    /// Classified match type: "date", "contractNumber", "partyName",
    /// "monetaryAmount", "location", or "custom:&lt;name&gt;".
    /// </summary>
    [JsonPropertyName("match_type")] public string MatchType { get; init; } = "";
}

/// <summary>
/// TXT-016 — Result of validating or searching a text string, returning the
/// classified matches found within it.
/// </summary>
public sealed class TextValidationResult
{
    /// <summary>Whether any match was found.</summary>
    [JsonPropertyName("found")] public bool Found { get; init; }

    /// <summary>Aggregate confidence in 0.0..1.0.</summary>
    [JsonPropertyName("confidence")] public double Confidence { get; init; }

    /// <summary>The classified matches found in the text.</summary>
    [JsonPropertyName("matches")] public List<TextMatch> Matches { get; init; } = new();

    /// <summary>Additional metadata produced by the validator.</summary>
    [JsonPropertyName("metadata")] public Dictionary<string, string> Metadata { get; init; } = new();
}
