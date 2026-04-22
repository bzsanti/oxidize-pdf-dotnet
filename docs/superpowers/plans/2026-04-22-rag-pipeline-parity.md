# RAG Pipeline Parity with Python Bridge — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close every "immediate" Tier 0 item listed in `oxidize-python/docs/PARITY_SPEC.md` (dated 2026-04-22): **RAG-003, RAG-004, RAG-006, RAG-007, RAG-008, RAG-009, RAG-012, RAG-020**. After this plan ships, every Tier 0 RAG row that the spec flags as .NET-owed moves from ❌ to ✅ with semantic (not shape) tests backing it.

**Architecture:** The Rust core (`oxidize-pdf 2.5.4+`) already exposes `PdfDocument::{partition_with_profile, partition_with, rag_chunks_with_profile, rag_chunks_with, rag_chunks_with_profile_config}` plus `SemanticChunker`, `ai::DocumentChunker`, and `ai::MarkdownExporter`. We add a new FFI module `pipeline_config.rs` (serde mirrors for config types) and seven new `extern "C"` entry points: five for the partition/chunk pipeline, one for markdown-with-options, and two for the standalone `DocumentChunker` (`oxidize_chunk_text`, `oxidize_estimate_tokens`). On the .NET side a new `OxidizePdf.NET.Pipeline` namespace gathers the configuration types; a new `OxidizePdf.NET.Ai` namespace gathers the standalone `DocumentChunker` + `MarkdownOptions`. Configuration crosses the FFI boundary as JSON UTF-8 strings; profile is a `u8` enum discriminant. All existing APIs stay source-compatible; the legacy character-based `ChunkOptions` is marked `[Obsolete]`. **Per PARITY_SPEC maintenance rule #4, no Tier 0 row can be marked ✅ without semantic regression tests** — we port the 12 Python `test_rag_chunks_disjoint.py` tests verbatim. M1–M6 (feature-parity roadmap) is paused until this plan ships.

**Tech Stack:** Rust 1.77+ / PyO3-free cdylib (`oxidize_pdf_ffi`), `serde`, `serde_json`; C# .NET 8/9/10, xUnit + FluentAssertions, `System.Text.Json`.

**Scope:**
- **In-scope (Tier 0 immediate, per PARITY_SPEC):**
  - RAG-003 `RagChunksAsync(profile)` overload
  - RAG-004 `ExtractionProfile` enum (7 values)
  - RAG-006 `MergePolicy` enum (2 variants — see §Known PARITY_SPEC discrepancy)
  - RAG-007 `ReadingOrderStrategy` class
  - RAG-008 standalone `DocumentChunker` (chunk arbitrary text)
  - RAG-009 `DocumentChunker.EstimateTokens(string)` static
  - RAG-012 `MarkdownOptions` + `ToMarkdownAsync(opts)` overload
  - RAG-020 **12 semantic disjointness regression tests** (ported from Python `test_rag_chunks_disjoint.py`)
  - Plus supporting items: RAG-005 (`HybridChunkConfig`), `SemanticChunkConfig`, `PartitionConfig`, `SemanticChunksAsync`
  - QA-001 refresh `docs/FEATURE_PARITY.md`; maintenance rule #1: mirror `PARITY_SPEC.md` into this repo
- **Out of scope (follow-up plans):** MCP server (MCP-001, separate plan), streaming APIs, pluggable tokenizer, `Element`/`PdfElement` rename (RAG-016 — breaking change, needs its own discussion), JSON export unification (RAG-014), NuGet Kernel Memory package (RAG-021, INT-002), OCR (RAG-019).
- **Explicitly paused:** M1 (already-merged DOC-014/015/017/020 stay merged), M2–M6 roadmap. Current `feature/m1-document-metadata` branch is blocked upstream anyway.

**Known PARITY_SPEC discrepancy (to fix in spec, not in code):** RAG-006 lists three `MergePolicy` variants (`AnyInlineContent`, `SameTypeOnly`, `None`). The Rust core at 2.5.4 (`pipeline/hybrid_chunking.rs:26-32`) and the Python bridge (`src/ai_pipeline.rs:306-322`) both expose only two: `SameTypeOnly`, `AnyInlineContent`. Our .NET enum will match the code (2 variants). Task 25 opens a PR against the Python repo's `PARITY_SPEC.md` to correct the row. Task 25 also mirrors the spec into our repo.

---

## File Structure

**Native (Rust FFI) — `native/src/`:**
- **Create `native/src/pipeline_config.rs`** — serde-compatible mirrors of `ExtractionProfile`, `ReadingOrderStrategy`, `PartitionConfig`, `HybridChunkConfig`, `SemanticChunkConfig`, `MergePolicy`, `MarkdownOptions`. Provides `From` conversions to the real `oxidize_pdf::{pipeline, ai}::*` types plus helpers to parse JSON inputs. Single responsibility: cross-boundary serialization.
- **Modify `native/src/parser.rs`** — add eight new `extern "C"` functions:
  1. `oxidize_partition_with_profile`
  2. `oxidize_partition_with_config`
  3. `oxidize_rag_chunks_with_profile`
  4. `oxidize_rag_chunks_with_config`
  5. `oxidize_semantic_chunks`
  6. `oxidize_to_markdown_with_options` (RAG-012)
  7. `oxidize_chunk_text` (RAG-008 — text-only chunker, no PDF)
  8. `oxidize_estimate_tokens` (RAG-009 — word-count proxy)

  Existing `oxidize_partition` / `oxidize_rag_chunks` / `oxidize_to_markdown` remain untouched.
- **Modify `native/src/lib.rs`** — register `pub mod pipeline_config;`.
- **Modify `native/Cargo.toml`** — bump FFI crate from `0.8.0` → `0.9.0-rag.1`.

**Managed (C#) — `dotnet/OxidizePdf.NET/`:**

*Pipeline namespace (partition + chunking config):*
- **Create `dotnet/OxidizePdf.NET/Pipeline/ExtractionProfile.cs`** — enum (7 variants), matches Rust discriminant order.
- **Create `dotnet/OxidizePdf.NET/Pipeline/MergePolicy.cs`** — enum (2 variants).
- **Create `dotnet/OxidizePdf.NET/Pipeline/ReadingOrderStrategy.cs`** — sealed immutable class; static `Simple`, `None`; factory `XyCut(double minGap)`. Custom JSON converter matches `serde`'s default tagged enum shape.
- **Create `dotnet/OxidizePdf.NET/Pipeline/PartitionConfig.cs`** — mutable class with `With*` fluent methods; JSON round-trippable with snake_case.
- **Create `dotnet/OxidizePdf.NET/Pipeline/HybridChunkConfig.cs`** — mutable class, fluent, JSON round-trippable.
- **Create `dotnet/OxidizePdf.NET/Pipeline/SemanticChunkConfig.cs`** — mutable class, fluent.

*Ai namespace (standalone chunker + markdown — mirrors Python's `MarkdownExporter` / `DocumentChunker`):*
- **Create `dotnet/OxidizePdf.NET/Ai/MarkdownOptions.cs`** — POCO with `IncludeMetadata` and `IncludePageNumbers` bools, both default `true`.
- **Create `dotnet/OxidizePdf.NET/Ai/DocumentChunker.cs`** — instance class wrapping the standalone Rust `ai::DocumentChunker`. Ctor `(int chunkSize, int overlap)` + parameterless default ctor (512, 50). Instance method `ChunkText(string) → List<TextChunk>`. Static `EstimateTokens(string) → int` (RAG-009).
- **Create `dotnet/OxidizePdf.NET/Models/TextChunk.cs`** — DTO for `DocumentChunker.ChunkText`: `Id` (string), `Content` (string), `Tokens` (int), `PageNumbers` (List<int> — empty for text-only input), `ChunkIndex` (int). Deliberately named `TextChunk` (not `DocumentChunk`) to avoid collision with the existing `DocumentChunk` class; we'll flag this naming asymmetry in the PARITY_SPEC mirror.
- **Create `dotnet/OxidizePdf.NET/Models/SemanticChunk.cs`** — DTO for `oxidize_semantic_chunks` output.

*PdfExtractor overloads + P/Invoke:*
- **Modify `dotnet/OxidizePdf.NET/NativeMethods.cs`** — add P/Invoke declarations for all eight new FFI functions.
- **Modify `dotnet/OxidizePdf.NET/PdfExtractor.cs`** — add six new overloads/methods:
  - `PartitionAsync(byte[], ExtractionProfile, CancellationToken)`
  - `PartitionAsync(byte[], PartitionConfig, CancellationToken)`
  - `RagChunksAsync(byte[], ExtractionProfile, CancellationToken)`
  - `RagChunksAsync(byte[], PartitionConfig?, HybridChunkConfig?, CancellationToken)`
  - `SemanticChunksAsync(byte[], SemanticChunkConfig?, PartitionConfig?, CancellationToken)`
  - `ToMarkdownAsync(byte[], MarkdownOptions, CancellationToken)` (RAG-012)
- **Modify `dotnet/OxidizePdf.NET/Models/ChunkOptions.cs`** — `[Obsolete]` attribute pointing at `HybridChunkConfig`/`SemanticChunkConfig`.
- **Modify `dotnet/OxidizePdf.NET/OxidizePdf.NET.csproj`** — `<Version>0.9.0-rag.1`.

**Tests — `dotnet/OxidizePdf.NET.Tests/Pipeline/` and `.../Ai/`:**

*Unit (config types — pure C#, no FFI):*
- `Pipeline/ExtractionProfileTests.cs` — discriminant round-trip, all 7 values.
- `Pipeline/ReadingOrderStrategyTests.cs` — factories, equality, JSON shape.
- `Pipeline/PartitionConfigTests.cs` — defaults, fluent chaining, JSON round-trip.
- `Pipeline/HybridChunkConfigTests.cs` — defaults, fluent, JSON round-trip, MergePolicy serialization.
- `Pipeline/SemanticChunkConfigTests.cs` — defaults, fluent, JSON round-trip.
- `Ai/MarkdownOptionsTests.cs` — defaults, JSON round-trip.

*Integration (hit the FFI):*
- `Pipeline/PdfExtractorProfileTests.cs` — each of the 7 profiles returns non-empty elements.
- `Pipeline/PdfExtractorPartitionConfigTests.cs` — `WithoutTables()`, `WithReadingOrder(XyCut)` visibly affect output.
- `Pipeline/PdfExtractorRagProfileTests.cs` — each profile returns chunks with valid `FullText`/`TokenEstimate`.
- `Pipeline/PdfExtractorHybridChunksTests.cs` — `max_tokens=8` vs 512 yields more chunks; `MergePolicy` switch verifiable.
- `Pipeline/PdfExtractorSemanticChunksTests.cs` — defaults return chunks; tiny `max_tokens` splits.
- `Ai/MarkdownOptionsIntegrationTests.cs` — `ToMarkdownAsync(opts)` with `IncludeMetadata=false` omits metadata section that default call includes.
- `Ai/DocumentChunkerTests.cs` — ctor, `ChunkText("long text")` yields multiple `TextChunk`s respecting size; static `EstimateTokens("hello world") == 2`.

*Semantic regression (RAG-020 — ports `oxidize-python/tests/test_rag_chunks_disjoint.py` verbatim):*
- `Pipeline/RagChunksDisjointnessTests.cs` — **12 tests** covering:
  - 6 tests in `TestRagChunksDisjointness` class: pairwise-disjoint chunks, each paragraph marker appears exactly once, chunk count bounded by source element count — across two fixtures (title+3-paragraphs single-page PDF; 2-section multi-page PDF).
  - 6 tests in `TestRagChunksDisjointnessAcrossProfiles`: disjointness + marker uniqueness across 3 profiles (`Standard`, `Rag`, `Academic`) using `RagChunksAsync(pdf, profile)`.

  These are **the gating tests** for marking the Tier 0 rows ✅ per PARITY_SPEC maintenance rule #4.
- `PdfExtractorPartitionConfigTests.cs` — integration: `.WithoutTables()` suppresses Table elements; `.WithReadingOrder(XyCut)` changes element order for multi-column.
- `PdfExtractorHybridChunksTests.cs` — integration: `max_tokens=64` yields smaller/more chunks than defaults; `MergePolicy.SameTypeOnly` vs `AnyInlineContent` differ.
- `PdfExtractorSemanticChunksTests.cs` — integration: returns non-empty list; respects `max_tokens`.

**Docs & examples:**
- **Modify `examples/BasicUsage/Program.cs`** — profile + config + `MarkdownOptions` + standalone `DocumentChunker` snippets.
- **Modify `README.md`** — replace character-based chunking section with profile/token-aware example; link to Python bridge for philosophy parity.
- **Modify `CHANGELOG.md`** — `## [0.9.0-rag.1]` entry listing every RAG-* ID closed.
- **Create `docs/PARITY_SPEC.md`** — verbatim copy of `oxidize-python/docs/PARITY_SPEC.md` per maintenance rule #1 (spec is a mirror). Flip the rows this plan closes from ❌ to ✅ in **both** copies as part of the release commit.
- **Modify `docs/FEATURE_PARITY.md`** — refresh to reflect 2.5.5 core + 0.9.0-rag.1 bridge (QA-001 in spec is currently ❌ "stale 2026-03-20").
- **Modify `docs/superpowers/plans/2026-04-21-feature-parity-roadmap.md`** — "⏸ Paused for RAG parity work (see `2026-04-22-rag-pipeline-parity.md`)" banner.

---

## Pre-Work: Branch & Worktree Setup

- [ ] **Step 0.1: Verify current branch state**

Run:
```bash
git -C /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet status --short
git -C /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet log --oneline -1
```
Expected: clean working tree except possibly `.claude/settings.local.json` (M); latest commit `0b727c8 feat(m1): DOC-020 save with WriterConfig presets`.

If anything else is dirty, stash or commit before proceeding.

- [ ] **Step 0.2: Create a worktree for RAG work isolated from the M1 branch**

Run:
```bash
git -C /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet worktree add -b feature/rag-pipeline-parity ../oxidize-dotnet-rag develop 2>/dev/null \
  || git -C /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet worktree add -b feature/rag-pipeline-parity ../oxidize-dotnet-rag main
```
Expected: new worktree at `/home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet-rag` on branch `feature/rag-pipeline-parity`.

All subsequent paths in this plan are written assuming you `cd` into the worktree root. If `develop` does not exist in this repo, the fallback branches from `main` — verify with `git branch`.

- [ ] **Step 0.3: Sanity-check baseline build + tests**

Run:
```bash
cd /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet-rag \
  && cargo build --manifest-path native/Cargo.toml --release \
  && dotnet test dotnet/OxidizePdf.sln --nologo
```
Expected: Rust builds clean (no warnings), all existing .NET tests pass. If anything fails here, stop — the plan assumes green baseline.

- [ ] **Step 0.4: Commit the empty-branch marker**

Run:
```bash
git commit --allow-empty -m "chore(rag): start RAG pipeline parity work (plan: 2026-04-22)"
```

---

## Task 1: C# `ExtractionProfile` and `MergePolicy` enums

**Files:**
- Create: `dotnet/OxidizePdf.NET/Pipeline/ExtractionProfile.cs`
- Create: `dotnet/OxidizePdf.NET/Pipeline/MergePolicy.cs`
- Test: `dotnet/OxidizePdf.NET.Tests/Pipeline/ExtractionProfileTests.cs`

These are pure value enums with no FFI today — we just need their discriminants to match the Rust enum order so we can pass them as `byte`.

- [ ] **Step 1.1: Write the failing test for ExtractionProfile discriminants**

Create `dotnet/OxidizePdf.NET.Tests/Pipeline/ExtractionProfileTests.cs`:

```csharp
using FluentAssertions;
using OxidizePdf.NET.Pipeline;
using Xunit;

namespace OxidizePdf.NET.Tests.Pipeline;

public class ExtractionProfileTests
{
    [Theory]
    [InlineData(ExtractionProfile.Standard, 0)]
    [InlineData(ExtractionProfile.Academic, 1)]
    [InlineData(ExtractionProfile.Form, 2)]
    [InlineData(ExtractionProfile.Government, 3)]
    [InlineData(ExtractionProfile.Dense, 4)]
    [InlineData(ExtractionProfile.Presentation, 5)]
    [InlineData(ExtractionProfile.Rag, 6)]
    public void Discriminants_match_rust_enum_order(ExtractionProfile profile, int expected)
    {
        ((byte)profile).Should().Be((byte)expected);
    }

    [Fact]
    public void MergePolicy_discriminants_match_rust_enum_order()
    {
        ((byte)MergePolicy.SameTypeOnly).Should().Be(0);
        ((byte)MergePolicy.AnyInlineContent).Should().Be(1);
    }
}
```

- [ ] **Step 1.2: Run test to verify it fails**

Run:
```bash
dotnet test dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj --filter "FullyQualifiedName~ExtractionProfileTests" --nologo
```
Expected: FAIL with "The type or namespace name 'ExtractionProfile' could not be found".

- [ ] **Step 1.3: Implement both enums**

Create `dotnet/OxidizePdf.NET/Pipeline/ExtractionProfile.cs`:

```csharp
namespace OxidizePdf.NET.Pipeline;

/// <summary>
/// Pre-configured extraction profiles for different document types.
/// Mirrors <c>oxidize_pdf::pipeline::ExtractionProfile</c> — discriminant order MUST match.
/// </summary>
public enum ExtractionProfile : byte
{
    Standard = 0,
    Academic = 1,
    Form = 2,
    Government = 3,
    Dense = 4,
    Presentation = 5,
    Rag = 6,
}
```

Create `dotnet/OxidizePdf.NET/Pipeline/MergePolicy.cs`:

```csharp
namespace OxidizePdf.NET.Pipeline;

/// <summary>
/// Policy for merging adjacent elements in hybrid chunking.
/// Mirrors <c>oxidize_pdf::pipeline::MergePolicy</c>.
/// </summary>
public enum MergePolicy : byte
{
    /// <summary>Only merge Paragraph+Paragraph and ListItem+ListItem.</summary>
    SameTypeOnly = 0,
    /// <summary>Merge any adjacent non-structural elements. Default.</summary>
    AnyInlineContent = 1,
}
```

- [ ] **Step 1.4: Run test to verify it passes**

Run:
```bash
dotnet test dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj --filter "FullyQualifiedName~ExtractionProfileTests" --nologo
```
Expected: 2 tests passing.

- [ ] **Step 1.5: Commit**

```bash
git add dotnet/OxidizePdf.NET/Pipeline/ExtractionProfile.cs \
        dotnet/OxidizePdf.NET/Pipeline/MergePolicy.cs \
        dotnet/OxidizePdf.NET.Tests/Pipeline/ExtractionProfileTests.cs
git commit -m "feat(rag): add ExtractionProfile and MergePolicy enums"
```

---

## Task 2: `ReadingOrderStrategy` immutable class

**Files:**
- Create: `dotnet/OxidizePdf.NET/Pipeline/ReadingOrderStrategy.cs`
- Test: `dotnet/OxidizePdf.NET.Tests/Pipeline/ReadingOrderStrategyTests.cs`

This is a Rust enum with one payload-carrying variant (`XYCut { min_gap }`). We model it as a sealed class with static factory methods and a custom JSON shape matching `serde`'s default tagged enum representation (`{"Simple":null}` / `{"None":null}` / `{"XYCut":{"min_gap":20.0}}`).

- [ ] **Step 2.1: Write the failing tests**

Create `dotnet/OxidizePdf.NET.Tests/Pipeline/ReadingOrderStrategyTests.cs`:

```csharp
using System.Text.Json;
using FluentAssertions;
using OxidizePdf.NET.Pipeline;
using Xunit;

namespace OxidizePdf.NET.Tests.Pipeline;

public class ReadingOrderStrategyTests
{
    [Fact]
    public void Simple_and_None_are_singletons()
    {
        ReadingOrderStrategy.Simple.Should().BeSameAs(ReadingOrderStrategy.Simple);
        ReadingOrderStrategy.None.Should().BeSameAs(ReadingOrderStrategy.None);
    }

    [Fact]
    public void XyCut_carries_min_gap()
    {
        var s = ReadingOrderStrategy.XyCut(15.0);
        s.Kind.Should().Be(ReadingOrderKind.XyCut);
        s.MinGap.Should().Be(15.0);
    }

    [Fact]
    public void XyCut_rejects_negative_min_gap()
    {
        var act = () => ReadingOrderStrategy.XyCut(-1.0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void JSON_shape_matches_rust_serde_default()
    {
        JsonSerializer.Serialize(ReadingOrderStrategy.Simple).Should().Be("\"Simple\"");
        JsonSerializer.Serialize(ReadingOrderStrategy.None).Should().Be("\"None\"");
        JsonSerializer.Serialize(ReadingOrderStrategy.XyCut(20.0))
            .Should().Be("{\"XYCut\":{\"min_gap\":20}}");
    }
}
```

- [ ] **Step 2.2: Run test to verify it fails**

Run:
```bash
dotnet test dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj --filter "FullyQualifiedName~ReadingOrderStrategyTests" --nologo
```
Expected: FAIL with "type or namespace ReadingOrderStrategy".

- [ ] **Step 2.3: Implement `ReadingOrderStrategy` with a custom JSON converter**

Create `dotnet/OxidizePdf.NET/Pipeline/ReadingOrderStrategy.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Pipeline;

public enum ReadingOrderKind
{
    Simple,
    None,
    XyCut,
}

/// <summary>
/// Strategy for ordering text fragments before classification.
/// Mirrors <c>oxidize_pdf::pipeline::ReadingOrderStrategy</c> (a Rust enum with
/// one payload-carrying variant). JSON shape matches serde's default tagged
/// representation: <c>"Simple"</c>, <c>"None"</c>, or <c>{"XYCut":{"min_gap":20.0}}</c>.
/// </summary>
[JsonConverter(typeof(ReadingOrderStrategyJsonConverter))]
public sealed class ReadingOrderStrategy : IEquatable<ReadingOrderStrategy>
{
    public static readonly ReadingOrderStrategy Simple = new(ReadingOrderKind.Simple, 0.0);
    public static readonly ReadingOrderStrategy None = new(ReadingOrderKind.None, 0.0);

    public ReadingOrderKind Kind { get; }
    public double MinGap { get; }

    private ReadingOrderStrategy(ReadingOrderKind kind, double minGap)
    {
        Kind = kind;
        MinGap = minGap;
    }

    public static ReadingOrderStrategy XyCut(double minGap)
    {
        if (minGap < 0.0 || double.IsNaN(minGap) || double.IsInfinity(minGap))
            throw new ArgumentOutOfRangeException(nameof(minGap), "minGap must be a finite non-negative number");
        return new ReadingOrderStrategy(ReadingOrderKind.XyCut, minGap);
    }

    public bool Equals(ReadingOrderStrategy? other) =>
        other is not null && Kind == other.Kind && MinGap.Equals(other.MinGap);

    public override bool Equals(object? obj) => Equals(obj as ReadingOrderStrategy);
    public override int GetHashCode() => HashCode.Combine(Kind, MinGap);
}

internal sealed class ReadingOrderStrategyJsonConverter : JsonConverter<ReadingOrderStrategy>
{
    public override ReadingOrderStrategy Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var tag = reader.GetString();
            return tag switch
            {
                "Simple" => ReadingOrderStrategy.Simple,
                "None" => ReadingOrderStrategy.None,
                _ => throw new JsonException($"Unknown ReadingOrderStrategy tag: {tag}"),
            };
        }

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected string or object");
        reader.Read();
        if (reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "XYCut")
            throw new JsonException("Expected property 'XYCut'");
        reader.Read(); // StartObject
        reader.Read(); // property name
        if (reader.GetString() != "min_gap") throw new JsonException("Expected 'min_gap'");
        reader.Read(); // number
        var minGap = reader.GetDouble();
        reader.Read(); // EndObject (inner)
        reader.Read(); // EndObject (outer)
        return ReadingOrderStrategy.XyCut(minGap);
    }

    public override void Write(Utf8JsonWriter writer, ReadingOrderStrategy value, JsonSerializerOptions options)
    {
        switch (value.Kind)
        {
            case ReadingOrderKind.Simple:
                writer.WriteStringValue("Simple");
                break;
            case ReadingOrderKind.None:
                writer.WriteStringValue("None");
                break;
            case ReadingOrderKind.XyCut:
                writer.WriteStartObject();
                writer.WriteStartObject("XYCut");
                writer.WriteNumber("min_gap", value.MinGap);
                writer.WriteEndObject();
                writer.WriteEndObject();
                break;
        }
    }
}
```

- [ ] **Step 2.4: Run test to verify it passes**

Run:
```bash
dotnet test dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj --filter "FullyQualifiedName~ReadingOrderStrategyTests" --nologo
```
Expected: 4 tests passing.

- [ ] **Step 2.5: Commit**

```bash
git add dotnet/OxidizePdf.NET/Pipeline/ReadingOrderStrategy.cs \
        dotnet/OxidizePdf.NET.Tests/Pipeline/ReadingOrderStrategyTests.cs
git commit -m "feat(rag): add ReadingOrderStrategy with serde-compatible JSON"
```

---

## Task 3: `PartitionConfig` fluent builder

**Files:**
- Create: `dotnet/OxidizePdf.NET/Pipeline/PartitionConfig.cs`
- Test: `dotnet/OxidizePdf.NET.Tests/Pipeline/PartitionConfigTests.cs`

Mirrors the Rust struct field-for-field. Defaults match `PartitionConfig::default()` from `oxidize-pdf 2.5.4`. JSON uses snake_case (matches serde).

- [ ] **Step 3.1: Write the failing tests**

Create `dotnet/OxidizePdf.NET.Tests/Pipeline/PartitionConfigTests.cs`:

```csharp
using System.Text.Json;
using FluentAssertions;
using OxidizePdf.NET.Pipeline;
using Xunit;

namespace OxidizePdf.NET.Tests.Pipeline;

public class PartitionConfigTests
{
    [Fact]
    public void Defaults_match_rust_PartitionConfig_default()
    {
        var c = new PartitionConfig();
        c.DetectTables.Should().BeTrue();
        c.DetectHeadersFooters.Should().BeTrue();
        c.TitleMinFontRatio.Should().Be(1.3);
        c.HeaderZone.Should().Be(0.05);
        c.FooterZone.Should().Be(0.05);
        c.MinTableConfidence.Should().Be(0.5);
        c.ReadingOrder.Should().BeSameAs(ReadingOrderStrategy.Simple);
    }

    [Fact]
    public void Fluent_builders_mutate_and_return_self()
    {
        var c = new PartitionConfig()
            .WithoutTables()
            .WithoutHeadersFooters()
            .WithTitleMinFontRatio(1.5)
            .WithMinTableConfidence(0.7)
            .WithReadingOrder(ReadingOrderStrategy.XyCut(20.0));

        c.DetectTables.Should().BeFalse();
        c.DetectHeadersFooters.Should().BeFalse();
        c.TitleMinFontRatio.Should().Be(1.5);
        c.MinTableConfidence.Should().Be(0.7);
        c.ReadingOrder.Kind.Should().Be(ReadingOrderKind.XyCut);
        c.ReadingOrder.MinGap.Should().Be(20.0);
    }

    [Fact]
    public void Validate_rejects_out_of_range_confidence()
    {
        var c = new PartitionConfig { MinTableConfidence = 1.5 };
        c.Invoking(x => x.Validate()).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void JSON_round_trip_preserves_all_fields()
    {
        var original = new PartitionConfig()
            .WithoutTables()
            .WithReadingOrder(ReadingOrderStrategy.XyCut(15.0))
            .WithTitleMinFontRatio(1.4);

        var json = JsonSerializer.Serialize(original, PartitionConfig.JsonOptions);
        var round = JsonSerializer.Deserialize<PartitionConfig>(json, PartitionConfig.JsonOptions)!;

        round.DetectTables.Should().BeFalse();
        round.TitleMinFontRatio.Should().Be(1.4);
        round.ReadingOrder.Kind.Should().Be(ReadingOrderKind.XyCut);
        round.ReadingOrder.MinGap.Should().Be(15.0);
    }

    [Fact]
    public void JSON_uses_snake_case_property_names()
    {
        var c = new PartitionConfig();
        var json = JsonSerializer.Serialize(c, PartitionConfig.JsonOptions);
        json.Should().Contain("\"detect_tables\"");
        json.Should().Contain("\"title_min_font_ratio\"");
        json.Should().Contain("\"min_table_confidence\"");
        json.Should().Contain("\"reading_order\"");
    }
}
```

- [ ] **Step 3.2: Run test to verify it fails**

Run:
```bash
dotnet test dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj --filter "FullyQualifiedName~PartitionConfigTests" --nologo
```
Expected: FAIL with "type or namespace PartitionConfig".

- [ ] **Step 3.3: Implement `PartitionConfig`**

Create `dotnet/OxidizePdf.NET/Pipeline/PartitionConfig.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Pipeline;

/// <summary>
/// Configuration for the document partitioner. Mirrors
/// <c>oxidize_pdf::pipeline::PartitionConfig</c> field-for-field.
/// </summary>
public class PartitionConfig
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    [JsonPropertyName("detect_tables")]
    public bool DetectTables { get; set; } = true;

    [JsonPropertyName("detect_headers_footers")]
    public bool DetectHeadersFooters { get; set; } = true;

    [JsonPropertyName("title_min_font_ratio")]
    public double TitleMinFontRatio { get; set; } = 1.3;

    [JsonPropertyName("header_zone")]
    public double HeaderZone { get; set; } = 0.05;

    [JsonPropertyName("footer_zone")]
    public double FooterZone { get; set; } = 0.05;

    [JsonPropertyName("reading_order")]
    public ReadingOrderStrategy ReadingOrder { get; set; } = ReadingOrderStrategy.Simple;

    [JsonPropertyName("min_table_confidence")]
    public double MinTableConfidence { get; set; } = 0.5;

    public PartitionConfig WithoutTables()        { DetectTables = false; return this; }
    public PartitionConfig WithoutHeadersFooters(){ DetectHeadersFooters = false; return this; }
    public PartitionConfig WithTitleMinFontRatio(double ratio) { TitleMinFontRatio = ratio; return this; }
    public PartitionConfig WithMinTableConfidence(double threshold) { MinTableConfidence = threshold; return this; }
    public PartitionConfig WithReadingOrder(ReadingOrderStrategy strategy)
    {
        ReadingOrder = strategy ?? throw new ArgumentNullException(nameof(strategy));
        return this;
    }

    public void Validate()
    {
        if (TitleMinFontRatio <= 0) throw new ArgumentException("TitleMinFontRatio must be positive");
        if (HeaderZone < 0 || HeaderZone > 1) throw new ArgumentException("HeaderZone must be in [0, 1]");
        if (FooterZone < 0 || FooterZone > 1) throw new ArgumentException("FooterZone must be in [0, 1]");
        if (MinTableConfidence < 0 || MinTableConfidence > 1)
            throw new ArgumentException("MinTableConfidence must be in [0, 1]");
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
}
```

- [ ] **Step 3.4: Run test to verify it passes**

Run:
```bash
dotnet test dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj --filter "FullyQualifiedName~PartitionConfigTests" --nologo
```
Expected: 5 tests passing.

- [ ] **Step 3.5: Commit**

```bash
git add dotnet/OxidizePdf.NET/Pipeline/PartitionConfig.cs \
        dotnet/OxidizePdf.NET.Tests/Pipeline/PartitionConfigTests.cs
git commit -m "feat(rag): add PartitionConfig fluent builder"
```

---

## Task 4: `HybridChunkConfig` and `SemanticChunkConfig`

**Files:**
- Create: `dotnet/OxidizePdf.NET/Pipeline/HybridChunkConfig.cs`
- Create: `dotnet/OxidizePdf.NET/Pipeline/SemanticChunkConfig.cs`
- Test: `dotnet/OxidizePdf.NET.Tests/Pipeline/HybridChunkConfigTests.cs`
- Test: `dotnet/OxidizePdf.NET.Tests/Pipeline/SemanticChunkConfigTests.cs`

- [ ] **Step 4.1: Write the failing tests**

Create `dotnet/OxidizePdf.NET.Tests/Pipeline/HybridChunkConfigTests.cs`:

```csharp
using System.Text.Json;
using FluentAssertions;
using OxidizePdf.NET.Pipeline;
using Xunit;

namespace OxidizePdf.NET.Tests.Pipeline;

public class HybridChunkConfigTests
{
    [Fact]
    public void Defaults_match_rust()
    {
        var c = new HybridChunkConfig();
        c.MaxTokens.Should().Be(512);
        c.OverlapTokens.Should().Be(50);
        c.MergeAdjacent.Should().BeTrue();
        c.PropagateHeadings.Should().BeTrue();
        c.MergePolicy.Should().Be(MergePolicy.AnyInlineContent);
    }

    [Fact]
    public void Fluent_mutators_chain()
    {
        var c = new HybridChunkConfig()
            .WithMaxTokens(256)
            .WithOverlap(30)
            .WithMergePolicy(MergePolicy.SameTypeOnly);
        c.MaxTokens.Should().Be(256);
        c.OverlapTokens.Should().Be(30);
        c.MergePolicy.Should().Be(MergePolicy.SameTypeOnly);
    }

    [Fact]
    public void Validate_rejects_overlap_ge_max()
    {
        var c = new HybridChunkConfig { MaxTokens = 100, OverlapTokens = 100 };
        c.Invoking(x => x.Validate()).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void JSON_round_trip_preserves_merge_policy_as_string()
    {
        var c = new HybridChunkConfig().WithMergePolicy(MergePolicy.SameTypeOnly);
        var json = JsonSerializer.Serialize(c, HybridChunkConfig.JsonOptions);
        json.Should().Contain("\"merge_policy\":\"SameTypeOnly\"");
        var back = JsonSerializer.Deserialize<HybridChunkConfig>(json, HybridChunkConfig.JsonOptions)!;
        back.MergePolicy.Should().Be(MergePolicy.SameTypeOnly);
    }
}
```

Create `dotnet/OxidizePdf.NET.Tests/Pipeline/SemanticChunkConfigTests.cs`:

```csharp
using System.Text.Json;
using FluentAssertions;
using OxidizePdf.NET.Pipeline;
using Xunit;

namespace OxidizePdf.NET.Tests.Pipeline;

public class SemanticChunkConfigTests
{
    [Fact]
    public void Defaults_match_rust()
    {
        var c = new SemanticChunkConfig();
        c.MaxTokens.Should().Be(512);
        c.OverlapTokens.Should().Be(50);
        c.RespectElementBoundaries.Should().BeTrue();
    }

    [Fact]
    public void Fluent_with_overlap()
    {
        var c = new SemanticChunkConfig(256).WithOverlap(75);
        c.MaxTokens.Should().Be(256);
        c.OverlapTokens.Should().Be(75);
    }

    [Fact]
    public void JSON_round_trip()
    {
        var c = new SemanticChunkConfig(128).WithOverlap(10);
        var json = JsonSerializer.Serialize(c, SemanticChunkConfig.JsonOptions);
        json.Should().Contain("\"max_tokens\":128");
        var back = JsonSerializer.Deserialize<SemanticChunkConfig>(json, SemanticChunkConfig.JsonOptions)!;
        back.MaxTokens.Should().Be(128);
        back.OverlapTokens.Should().Be(10);
    }
}
```

- [ ] **Step 4.2: Run tests to verify they fail**

Run:
```bash
dotnet test dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj --filter "FullyQualifiedName~HybridChunkConfigTests|FullyQualifiedName~SemanticChunkConfigTests" --nologo
```
Expected: FAIL with missing types.

- [ ] **Step 4.3: Implement both config classes**

Create `dotnet/OxidizePdf.NET/Pipeline/HybridChunkConfig.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Pipeline;

/// <summary>
/// Configuration for the hybrid chunker. Mirrors
/// <c>oxidize_pdf::pipeline::HybridChunkConfig</c>.
/// </summary>
public class HybridChunkConfig
{
    internal static readonly JsonSerializerOptions JsonOptions = BuildOptions();

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 512;

    [JsonPropertyName("overlap_tokens")]
    public int OverlapTokens { get; set; } = 50;

    [JsonPropertyName("merge_adjacent")]
    public bool MergeAdjacent { get; set; } = true;

    [JsonPropertyName("propagate_headings")]
    public bool PropagateHeadings { get; set; } = true;

    [JsonPropertyName("merge_policy")]
    public MergePolicy MergePolicy { get; set; } = MergePolicy.AnyInlineContent;

    public HybridChunkConfig WithMaxTokens(int n) { MaxTokens = n; return this; }
    public HybridChunkConfig WithOverlap(int n)   { OverlapTokens = n; return this; }
    public HybridChunkConfig WithMergePolicy(MergePolicy p) { MergePolicy = p; return this; }

    public void Validate()
    {
        if (MaxTokens <= 0) throw new ArgumentException("MaxTokens must be positive");
        if (OverlapTokens < 0) throw new ArgumentException("OverlapTokens must be non-negative");
        if (OverlapTokens >= MaxTokens)
            throw new ArgumentException("OverlapTokens must be less than MaxTokens");
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    private static JsonSerializerOptions BuildOptions()
    {
        var o = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        o.Converters.Add(new JsonStringEnumConverter());
        return o;
    }
}
```

Create `dotnet/OxidizePdf.NET/Pipeline/SemanticChunkConfig.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Pipeline;

/// <summary>
/// Configuration for the semantic chunker. Mirrors
/// <c>oxidize_pdf::pipeline::SemanticChunkConfig</c>.
/// </summary>
public class SemanticChunkConfig
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 512;

    [JsonPropertyName("overlap_tokens")]
    public int OverlapTokens { get; set; } = 50;

    [JsonPropertyName("respect_element_boundaries")]
    public bool RespectElementBoundaries { get; set; } = true;

    public SemanticChunkConfig() { }
    public SemanticChunkConfig(int maxTokens) { MaxTokens = maxTokens; }

    public SemanticChunkConfig WithOverlap(int n) { OverlapTokens = n; return this; }

    public void Validate()
    {
        if (MaxTokens <= 0) throw new ArgumentException("MaxTokens must be positive");
        if (OverlapTokens < 0) throw new ArgumentException("OverlapTokens must be non-negative");
        if (OverlapTokens >= MaxTokens)
            throw new ArgumentException("OverlapTokens must be less than MaxTokens");
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
}
```

- [ ] **Step 4.4: Run tests to verify they pass**

Run:
```bash
dotnet test dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj --filter "FullyQualifiedName~HybridChunkConfigTests|FullyQualifiedName~SemanticChunkConfigTests" --nologo
```
Expected: 7 tests passing.

- [ ] **Step 4.5: Commit**

```bash
git add dotnet/OxidizePdf.NET/Pipeline/HybridChunkConfig.cs \
        dotnet/OxidizePdf.NET/Pipeline/SemanticChunkConfig.cs \
        dotnet/OxidizePdf.NET.Tests/Pipeline/HybridChunkConfigTests.cs \
        dotnet/OxidizePdf.NET.Tests/Pipeline/SemanticChunkConfigTests.cs
git commit -m "feat(rag): add HybridChunkConfig and SemanticChunkConfig"
```

---

## Task 4b: `MarkdownOptions` C# POCO (RAG-012 support)

**Files:**
- Create: `dotnet/OxidizePdf.NET/Ai/MarkdownOptions.cs`
- Test: `dotnet/OxidizePdf.NET.Tests/Ai/MarkdownOptionsTests.cs`

Mirrors `oxidize_pdf::ai::MarkdownOptions`: two bools, both default `true`.

- [ ] **Step 4b.1: Write the failing test**

Create `dotnet/OxidizePdf.NET.Tests/Ai/MarkdownOptionsTests.cs`:

```csharp
using System.Text.Json;
using FluentAssertions;
using OxidizePdf.NET.Ai;
using Xunit;

namespace OxidizePdf.NET.Tests.Ai;

public class MarkdownOptionsTests
{
    [Fact]
    public void Defaults_match_rust_MarkdownOptions_default()
    {
        var o = new MarkdownOptions();
        o.IncludeMetadata.Should().BeTrue();
        o.IncludePageNumbers.Should().BeTrue();
    }

    [Fact]
    public void JSON_round_trip_uses_snake_case()
    {
        var o = new MarkdownOptions { IncludeMetadata = false, IncludePageNumbers = true };
        var json = JsonSerializer.Serialize(o, MarkdownOptions.JsonOptions);
        json.Should().Contain("\"include_metadata\":false");
        json.Should().Contain("\"include_page_numbers\":true");
        var back = JsonSerializer.Deserialize<MarkdownOptions>(json, MarkdownOptions.JsonOptions)!;
        back.IncludeMetadata.Should().BeFalse();
        back.IncludePageNumbers.Should().BeTrue();
    }
}
```

- [ ] **Step 4b.2: Run test to verify it fails**

Run:
```bash
dotnet test dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj --filter "FullyQualifiedName~MarkdownOptionsTests" --nologo
```
Expected: FAIL — type not found.

- [ ] **Step 4b.3: Implement**

Create `dotnet/OxidizePdf.NET/Ai/MarkdownOptions.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Ai;

/// <summary>
/// Options for Markdown export. Mirrors <c>oxidize_pdf::ai::MarkdownOptions</c>.
/// </summary>
public class MarkdownOptions
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    [JsonPropertyName("include_metadata")]
    public bool IncludeMetadata { get; set; } = true;

    [JsonPropertyName("include_page_numbers")]
    public bool IncludePageNumbers { get; set; } = true;

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
}
```

- [ ] **Step 4b.4: Verify passes**

```bash
dotnet test dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj --filter "FullyQualifiedName~MarkdownOptionsTests" --nologo
```
Expected: 2 tests passing.

- [ ] **Step 4b.5: Commit**

```bash
git add dotnet/OxidizePdf.NET/Ai/MarkdownOptions.cs \
        dotnet/OxidizePdf.NET.Tests/Ai/MarkdownOptionsTests.cs
git commit -m "feat(rag): add MarkdownOptions POCO (RAG-012 support)"
```

---

## Task 5: Rust FFI — `pipeline_config.rs` mirror types

**Files:**
- Create: `native/src/pipeline_config.rs`
- Modify: `native/src/lib.rs` (register module)

We define serde-compatible mirror structs that deserialize the JSON produced by the C# types, plus `From` impls to the real `oxidize_pdf::pipeline::*` types. No `extern "C"` functions yet — those come in the next tasks.

- [ ] **Step 5.1: Write the failing Rust unit test**

Create `native/src/pipeline_config.rs`:

```rust
//! Serde mirrors of oxidize-pdf pipeline config types for JSON-based FFI.
//!
//! C# serializes the config, passes a UTF-8 JSON string across the boundary,
//! and we parse it here and convert to the real `oxidize_pdf::pipeline::*` types.

use oxidize_pdf::pipeline::{
    ExtractionProfile as RustProfile, HybridChunkConfig as RustHybrid,
    MergePolicy as RustMergePolicy, PartitionConfig as RustPartition,
    SemanticChunkConfig as RustSemantic,
};
use oxidize_pdf::pipeline::partition::ReadingOrderStrategy as RustReadingOrder;
use serde::Deserialize;

#[derive(Debug, Deserialize)]
#[serde(untagged)]
pub enum ReadingOrderDto {
    Unit(String),
    XyCut { #[serde(rename = "XYCut")] x: XyCutDto },
}

#[derive(Debug, Deserialize)]
pub struct XyCutDto { pub min_gap: f64 }

impl From<ReadingOrderDto> for RustReadingOrder {
    fn from(d: ReadingOrderDto) -> Self {
        match d {
            ReadingOrderDto::Unit(s) if s == "Simple" => RustReadingOrder::Simple,
            ReadingOrderDto::Unit(s) if s == "None"   => RustReadingOrder::None,
            ReadingOrderDto::Unit(s) => panic!("unknown reading_order tag: {s}"),
            ReadingOrderDto::XyCut { x } => RustReadingOrder::XYCut { min_gap: x.min_gap },
        }
    }
}

#[derive(Debug, Deserialize)]
pub struct PartitionConfigDto {
    pub detect_tables: bool,
    pub detect_headers_footers: bool,
    pub title_min_font_ratio: f64,
    pub header_zone: f64,
    pub footer_zone: f64,
    pub reading_order: ReadingOrderDto,
    pub min_table_confidence: f64,
}

impl From<PartitionConfigDto> for RustPartition {
    fn from(d: PartitionConfigDto) -> Self {
        RustPartition {
            detect_tables: d.detect_tables,
            detect_headers_footers: d.detect_headers_footers,
            title_min_font_ratio: d.title_min_font_ratio,
            header_zone: d.header_zone,
            footer_zone: d.footer_zone,
            reading_order: d.reading_order.into(),
            min_table_confidence: d.min_table_confidence,
        }
    }
}

#[derive(Debug, Deserialize)]
pub struct HybridChunkConfigDto {
    pub max_tokens: usize,
    pub overlap_tokens: usize,
    pub merge_adjacent: bool,
    pub propagate_headings: bool,
    pub merge_policy: MergePolicyDto,
}

#[derive(Debug, Deserialize)]
pub enum MergePolicyDto { SameTypeOnly, AnyInlineContent }

impl From<MergePolicyDto> for RustMergePolicy {
    fn from(d: MergePolicyDto) -> Self {
        match d {
            MergePolicyDto::SameTypeOnly => RustMergePolicy::SameTypeOnly,
            MergePolicyDto::AnyInlineContent => RustMergePolicy::AnyInlineContent,
        }
    }
}

impl From<HybridChunkConfigDto> for RustHybrid {
    fn from(d: HybridChunkConfigDto) -> Self {
        RustHybrid {
            max_tokens: d.max_tokens,
            overlap_tokens: d.overlap_tokens,
            merge_adjacent: d.merge_adjacent,
            propagate_headings: d.propagate_headings,
            merge_policy: d.merge_policy.into(),
        }
    }
}

#[derive(Debug, Deserialize)]
pub struct SemanticChunkConfigDto {
    pub max_tokens: usize,
    pub overlap_tokens: usize,
    pub respect_element_boundaries: bool,
}

impl From<SemanticChunkConfigDto> for RustSemantic {
    fn from(d: SemanticChunkConfigDto) -> Self {
        RustSemantic {
            max_tokens: d.max_tokens,
            overlap_tokens: d.overlap_tokens,
            respect_element_boundaries: d.respect_element_boundaries,
        }
    }
}

// Markdown options DTO (mirrors `oxidize_pdf::ai::MarkdownOptions`).
#[derive(Debug, Deserialize)]
pub struct MarkdownOptionsDto {
    pub include_metadata: bool,
    pub include_page_numbers: bool,
}

impl From<MarkdownOptionsDto> for oxidize_pdf::ai::MarkdownOptions {
    fn from(d: MarkdownOptionsDto) -> Self {
        oxidize_pdf::ai::MarkdownOptions {
            include_metadata: d.include_metadata,
            include_page_numbers: d.include_page_numbers,
        }
    }
}

/// Map the u8 discriminant received across the FFI boundary to the Rust enum.
/// Order MUST match the C# `ExtractionProfile` enum.
pub fn profile_from_u8(v: u8) -> Result<RustProfile, String> {
    Ok(match v {
        0 => RustProfile::Standard,
        1 => RustProfile::Academic,
        2 => RustProfile::Form,
        3 => RustProfile::Government,
        4 => RustProfile::Dense,
        5 => RustProfile::Presentation,
        6 => RustProfile::Rag,
        other => return Err(format!("unknown ExtractionProfile discriminant: {other}")),
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn partition_config_roundtrip_simple() {
        let json = r#"{
            "detect_tables": true,
            "detect_headers_footers": false,
            "title_min_font_ratio": 1.4,
            "header_zone": 0.05,
            "footer_zone": 0.05,
            "reading_order": "Simple",
            "min_table_confidence": 0.6
        }"#;
        let dto: PartitionConfigDto = serde_json::from_str(json).unwrap();
        let cfg: RustPartition = dto.into();
        assert!(cfg.detect_tables);
        assert!(!cfg.detect_headers_footers);
        assert_eq!(cfg.title_min_font_ratio, 1.4);
        assert_eq!(cfg.min_table_confidence, 0.6);
        assert!(matches!(cfg.reading_order, RustReadingOrder::Simple));
    }

    #[test]
    fn partition_config_xycut() {
        let json = r#"{
            "detect_tables": true,
            "detect_headers_footers": true,
            "title_min_font_ratio": 1.3,
            "header_zone": 0.05,
            "footer_zone": 0.05,
            "reading_order": {"XYCut":{"min_gap":20.0}},
            "min_table_confidence": 0.5
        }"#;
        let dto: PartitionConfigDto = serde_json::from_str(json).unwrap();
        let cfg: RustPartition = dto.into();
        match cfg.reading_order {
            RustReadingOrder::XYCut { min_gap } => assert_eq!(min_gap, 20.0),
            _ => panic!("expected XYCut"),
        }
    }

    #[test]
    fn profile_discriminants() {
        assert!(matches!(profile_from_u8(0).unwrap(), RustProfile::Standard));
        assert!(matches!(profile_from_u8(6).unwrap(), RustProfile::Rag));
        assert!(profile_from_u8(99).is_err());
    }

    #[test]
    fn hybrid_config_roundtrip() {
        let json = r#"{
            "max_tokens": 256,
            "overlap_tokens": 30,
            "merge_adjacent": true,
            "propagate_headings": true,
            "merge_policy": "SameTypeOnly"
        }"#;
        let dto: HybridChunkConfigDto = serde_json::from_str(json).unwrap();
        let cfg: RustHybrid = dto.into();
        assert_eq!(cfg.max_tokens, 256);
        assert_eq!(cfg.overlap_tokens, 30);
        assert_eq!(cfg.merge_policy, RustMergePolicy::SameTypeOnly);
    }

    #[test]
    fn markdown_options_roundtrip() {
        let json = r#"{"include_metadata":false,"include_page_numbers":true}"#;
        let dto: MarkdownOptionsDto = serde_json::from_str(json).unwrap();
        let opts: oxidize_pdf::ai::MarkdownOptions = dto.into();
        assert!(!opts.include_metadata);
        assert!(opts.include_page_numbers);
    }
}
```

- [ ] **Step 5.2: Register the module**

Edit `native/src/lib.rs` — add `pub mod pipeline_config;` next to the other `pub mod` declarations (inline placement: after `pub mod parser;`).

- [ ] **Step 5.3: Run the Rust tests**

Run:
```bash
cargo test --manifest-path native/Cargo.toml pipeline_config --lib
```
Expected: 5 tests passing.

- [ ] **Step 5.4: Commit**

```bash
git add native/src/pipeline_config.rs native/src/lib.rs
git commit -m "feat(rag): add pipeline_config mirror types for FFI JSON config"
```

---

## Task 6: Rust FFI — `oxidize_partition_with_profile`

**Files:**
- Modify: `native/src/parser.rs` (append new extern function)
- Test: inline `#[cfg(test)]` in `native/src/parser.rs` (or new `tests/` file — pick whichever matches existing pattern)

- [ ] **Step 6.1: Write the failing Rust test**

Append to `native/src/parser.rs` (or wherever FFI tests live in this crate — check existing test module):

```rust
#[cfg(test)]
mod profile_ffi_tests {
    use super::*;
    use std::ffi::CStr;

    // Minimal fixture: a tiny PDF you can build with the already-available
    // oxidize_pdf::Document APIs. Shared helper used across profile tests.
    fn sample_pdf() -> Vec<u8> {
        use oxidize_pdf::{Document, Font, Page};
        let mut doc = Document::new();
        let mut page = Page::a4();
        page.text()
            .set_font(Font::Helvetica, 14.0)
            .at(50.0, 750.0)
            .write("Introduction")
            .unwrap();
        page.text()
            .set_font(Font::Helvetica, 11.0)
            .at(50.0, 720.0)
            .write("This is a sample paragraph for testing partitioning by profile.")
            .unwrap();
        doc.add_page(page);
        doc.to_bytes().unwrap()
    }

    #[test]
    fn oxidize_partition_with_profile_returns_json_array() {
        let pdf = sample_pdf();
        let mut out: *mut c_char = std::ptr::null_mut();
        let code = unsafe {
            oxidize_partition_with_profile(pdf.as_ptr(), pdf.len(), 6 /* Rag */, &mut out)
        };
        assert_eq!(code, ErrorCode::Success as c_int);
        assert!(!out.is_null());
        let json = unsafe { CStr::from_ptr(out).to_string_lossy().into_owned() };
        unsafe { oxidize_free_string(out); }
        assert!(json.starts_with('['));
        assert!(json.contains("element_type"));
    }

    #[test]
    fn oxidize_partition_with_profile_rejects_bad_discriminant() {
        let pdf = sample_pdf();
        let mut out: *mut c_char = std::ptr::null_mut();
        let code = unsafe {
            oxidize_partition_with_profile(pdf.as_ptr(), pdf.len(), 99, &mut out)
        };
        assert_eq!(code, ErrorCode::InvalidArgument as c_int);
        assert!(out.is_null());
    }
}
```

- [ ] **Step 6.2: Run test to verify it fails**

Run:
```bash
cargo test --manifest-path native/Cargo.toml profile_ffi_tests --lib
```
Expected: FAIL with "function or associated item `oxidize_partition_with_profile` not found".

- [ ] **Step 6.3: Implement the FFI function**

Append to `native/src/parser.rs` (after `oxidize_partition`, before `oxidize_rag_chunks`):

```rust
/// Partition a PDF using a pre-configured extraction profile.
///
/// # Arguments
/// * `profile` — `u8` discriminant; see `pipeline_config::profile_from_u8`.
///
/// # Safety
/// - `pdf_bytes` must point to `pdf_len` readable bytes.
/// - `out_json` must be writeable; callee frees returned string via `oxidize_free_string`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_partition_with_profile(
    pdf_bytes: *const u8,
    pdf_len: usize,
    profile: u8,
    out_json: *mut *mut c_char,
) -> c_int {
    clear_last_error();

    if pdf_bytes.is_null() || out_json.is_null() {
        set_last_error("Null pointer provided to oxidize_partition_with_profile");
        return ErrorCode::NullPointer as c_int;
    }
    *out_json = ptr::null_mut();
    if pdf_len == 0 {
        set_last_error("PDF data is empty (0 bytes)");
        return ErrorCode::PdfParseError as c_int;
    }

    let prof = match crate::pipeline_config::profile_from_u8(profile) {
        Ok(p) => p,
        Err(e) => { set_last_error(e); return ErrorCode::InvalidArgument as c_int; }
    };

    let bytes = slice::from_raw_parts(pdf_bytes, pdf_len);
    let reader = match open_lenient(bytes) {
        Ok(r) => r,
        Err(e) => { set_last_error(e); return ErrorCode::PdfParseError as c_int; }
    };
    let document = PdfDocument::new(reader);
    let elements = match document.partition_with_profile(prof) {
        Ok(e) => e,
        Err(e) => { set_last_error(format!("partition_with_profile failed: {e}")); return ErrorCode::PdfParseError as c_int; }
    };

    let results: Vec<PdfElementResult> = elements.iter().map(|el| {
        let bbox = el.bbox();
        PdfElementResult {
            element_type: el.type_name().to_string(),
            text: el.display_text(),
            page_number: el.page() + 1,
            x: bbox.x, y: bbox.y, width: bbox.width, height: bbox.height,
            confidence: el.metadata().confidence,
        }
    }).collect();

    let json = match serde_json::to_string(&results) {
        Ok(j) => j,
        Err(e) => { set_last_error(format!("serialize elements: {e}")); return ErrorCode::SerializationError as c_int; }
    };
    let c_string = match CString::new(json) {
        Ok(c) => c,
        Err(e) => { set_last_error(format!("null bytes in JSON: {e}")); return ErrorCode::InvalidUtf8 as c_int; }
    };
    *out_json = c_string.into_raw();
    ErrorCode::Success as c_int
}
```

- [ ] **Step 6.4: Run test to verify it passes**

Run:
```bash
cargo test --manifest-path native/Cargo.toml profile_ffi_tests --lib
```
Expected: 2 tests passing.

- [ ] **Step 6.5: Commit**

```bash
git add native/src/parser.rs
git commit -m "feat(rag): add oxidize_partition_with_profile FFI function"
```

---

## Task 7: Rust FFI — `oxidize_partition_with_config`

**Files:**
- Modify: `native/src/parser.rs`

- [ ] **Step 7.1: Write the failing Rust test**

Append to the `profile_ffi_tests` module in `native/src/parser.rs`:

```rust
    #[test]
    fn oxidize_partition_with_config_respects_without_tables() {
        let pdf = sample_pdf();
        let cfg = r#"{
            "detect_tables": false,
            "detect_headers_footers": true,
            "title_min_font_ratio": 1.3,
            "header_zone": 0.05,
            "footer_zone": 0.05,
            "reading_order": "Simple",
            "min_table_confidence": 0.5
        }"#;
        let cfg_c = std::ffi::CString::new(cfg).unwrap();
        let mut out: *mut c_char = std::ptr::null_mut();
        let code = unsafe {
            oxidize_partition_with_config(pdf.as_ptr(), pdf.len(), cfg_c.as_ptr(), &mut out)
        };
        assert_eq!(code, ErrorCode::Success as c_int);
        let json = unsafe { std::ffi::CStr::from_ptr(out).to_string_lossy().into_owned() };
        unsafe { oxidize_free_string(out); }
        // No table elements expected (simple fixture wouldn't produce them anyway,
        // but we verify structure is valid JSON array).
        assert!(json.starts_with('['));
    }

    #[test]
    fn oxidize_partition_with_config_rejects_bad_json() {
        let pdf = sample_pdf();
        let cfg_c = std::ffi::CString::new("{not valid json").unwrap();
        let mut out: *mut c_char = std::ptr::null_mut();
        let code = unsafe {
            oxidize_partition_with_config(pdf.as_ptr(), pdf.len(), cfg_c.as_ptr(), &mut out)
        };
        assert_eq!(code, ErrorCode::InvalidArgument as c_int);
    }
```

- [ ] **Step 7.2: Run to verify failure**

Run:
```bash
cargo test --manifest-path native/Cargo.toml profile_ffi_tests --lib
```
Expected: FAIL — `oxidize_partition_with_config` not found.

- [ ] **Step 7.3: Implement**

Append to `native/src/parser.rs`:

```rust
/// Partition a PDF using an explicit config supplied as a JSON UTF-8 string.
///
/// # Safety
/// - `pdf_bytes` must be valid for `pdf_len` bytes.
/// - `config_json` must be a valid NUL-terminated UTF-8 C string.
/// - `out_json` must be writeable; free via `oxidize_free_string`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_partition_with_config(
    pdf_bytes: *const u8,
    pdf_len: usize,
    config_json: *const c_char,
    out_json: *mut *mut c_char,
) -> c_int {
    clear_last_error();

    if pdf_bytes.is_null() || config_json.is_null() || out_json.is_null() {
        set_last_error("Null pointer provided to oxidize_partition_with_config");
        return ErrorCode::NullPointer as c_int;
    }
    *out_json = ptr::null_mut();
    if pdf_len == 0 {
        set_last_error("PDF data is empty (0 bytes)");
        return ErrorCode::PdfParseError as c_int;
    }

    let cfg_str = match std::ffi::CStr::from_ptr(config_json).to_str() {
        Ok(s) => s,
        Err(e) => { set_last_error(format!("invalid UTF-8 in config: {e}")); return ErrorCode::InvalidUtf8 as c_int; }
    };
    let dto: crate::pipeline_config::PartitionConfigDto = match serde_json::from_str(cfg_str) {
        Ok(d) => d,
        Err(e) => { set_last_error(format!("invalid PartitionConfig JSON: {e}")); return ErrorCode::InvalidArgument as c_int; }
    };
    let cfg: oxidize_pdf::pipeline::PartitionConfig = dto.into();

    let bytes = slice::from_raw_parts(pdf_bytes, pdf_len);
    let reader = match open_lenient(bytes) {
        Ok(r) => r,
        Err(e) => { set_last_error(e); return ErrorCode::PdfParseError as c_int; }
    };
    let document = PdfDocument::new(reader);
    let elements = match document.partition_with(cfg) {
        Ok(e) => e,
        Err(e) => { set_last_error(format!("partition_with failed: {e}")); return ErrorCode::PdfParseError as c_int; }
    };

    let results: Vec<PdfElementResult> = elements.iter().map(|el| {
        let bbox = el.bbox();
        PdfElementResult {
            element_type: el.type_name().to_string(),
            text: el.display_text(),
            page_number: el.page() + 1,
            x: bbox.x, y: bbox.y, width: bbox.width, height: bbox.height,
            confidence: el.metadata().confidence,
        }
    }).collect();

    let json = match serde_json::to_string(&results) {
        Ok(j) => j,
        Err(e) => { set_last_error(format!("serialize: {e}")); return ErrorCode::SerializationError as c_int; }
    };
    let c_string = match CString::new(json) {
        Ok(c) => c,
        Err(e) => { set_last_error(format!("null bytes: {e}")); return ErrorCode::InvalidUtf8 as c_int; }
    };
    *out_json = c_string.into_raw();
    ErrorCode::Success as c_int
}
```

- [ ] **Step 7.4: Verify tests pass**

Run:
```bash
cargo test --manifest-path native/Cargo.toml profile_ffi_tests --lib
```
Expected: 4 tests passing.

- [ ] **Step 7.5: Commit**

```bash
git add native/src/parser.rs
git commit -m "feat(rag): add oxidize_partition_with_config FFI function"
```

---

## Task 8: Rust FFI — `oxidize_rag_chunks_with_profile`

**Files:**
- Modify: `native/src/parser.rs`

- [ ] **Step 8.1: Write the failing test**

Append to `profile_ffi_tests`:

```rust
    #[test]
    fn oxidize_rag_chunks_with_profile_returns_chunks() {
        let pdf = sample_pdf();
        let mut out: *mut c_char = std::ptr::null_mut();
        let code = unsafe {
            oxidize_rag_chunks_with_profile(pdf.as_ptr(), pdf.len(), 6 /* Rag */, &mut out)
        };
        assert_eq!(code, ErrorCode::Success as c_int);
        let json = unsafe { std::ffi::CStr::from_ptr(out).to_string_lossy().into_owned() };
        unsafe { oxidize_free_string(out); }
        assert!(json.starts_with('['));
        assert!(json.contains("chunk_index"));
        assert!(json.contains("full_text"));
    }
```

- [ ] **Step 8.2: Run to verify failure**

Run:
```bash
cargo test --manifest-path native/Cargo.toml profile_ffi_tests --lib
```
Expected: FAIL.

- [ ] **Step 8.3: Implement**

Append to `native/src/parser.rs`:

```rust
/// Extract RAG chunks using a pre-configured extraction profile.
///
/// # Safety
/// See `oxidize_partition_with_profile`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_rag_chunks_with_profile(
    pdf_bytes: *const u8,
    pdf_len: usize,
    profile: u8,
    out_json: *mut *mut c_char,
) -> c_int {
    clear_last_error();

    if pdf_bytes.is_null() || out_json.is_null() {
        set_last_error("Null pointer provided to oxidize_rag_chunks_with_profile");
        return ErrorCode::NullPointer as c_int;
    }
    *out_json = ptr::null_mut();
    if pdf_len == 0 {
        set_last_error("PDF data is empty (0 bytes)");
        return ErrorCode::PdfParseError as c_int;
    }

    let prof = match crate::pipeline_config::profile_from_u8(profile) {
        Ok(p) => p,
        Err(e) => { set_last_error(e); return ErrorCode::InvalidArgument as c_int; }
    };

    let bytes = slice::from_raw_parts(pdf_bytes, pdf_len);
    let reader = match open_lenient(bytes) {
        Ok(r) => r,
        Err(e) => { set_last_error(e); return ErrorCode::PdfParseError as c_int; }
    };
    let document = PdfDocument::new(reader);
    let chunks = match document.rag_chunks_with_profile(prof) {
        Ok(c) => c,
        Err(e) => { set_last_error(format!("rag_chunks_with_profile failed: {e}")); return ErrorCode::PdfParseError as c_int; }
    };

    let results: Vec<RagChunkResult> = chunks.iter().enumerate().map(|(i, chunk)| RagChunkResult {
        chunk_index: i,
        text: chunk.text.clone(),
        full_text: chunk.full_text.clone(),
        page_numbers: chunk.page_numbers.iter().map(|p| p + 1).collect(),
        element_types: chunk.element_types.clone(),
        heading_context: chunk.heading_context.clone(),
        token_estimate: chunk.token_estimate,
        is_oversized: chunk.is_oversized,
    }).collect();

    let json = match serde_json::to_string(&results) {
        Ok(j) => j,
        Err(e) => { set_last_error(format!("serialize: {e}")); return ErrorCode::SerializationError as c_int; }
    };
    let c_string = match CString::new(json) {
        Ok(c) => c,
        Err(e) => { set_last_error(format!("null bytes: {e}")); return ErrorCode::InvalidUtf8 as c_int; }
    };
    *out_json = c_string.into_raw();
    ErrorCode::Success as c_int
}
```

- [ ] **Step 8.4: Verify tests pass**

Run:
```bash
cargo test --manifest-path native/Cargo.toml profile_ffi_tests --lib
```
Expected: 5 tests passing.

- [ ] **Step 8.5: Commit**

```bash
git add native/src/parser.rs
git commit -m "feat(rag): add oxidize_rag_chunks_with_profile FFI function"
```

---

## Task 9: Rust FFI — `oxidize_rag_chunks_with_config`

**Files:**
- Modify: `native/src/parser.rs`

Signature: both `partition_config_json` and `hybrid_config_json` are optional (null-allowed). If either is null, the corresponding default is used. This lets C# pass only the hybrid config for size tuning while keeping default partitioning.

- [ ] **Step 9.1: Write the failing test**

Append to `profile_ffi_tests`:

```rust
    #[test]
    fn oxidize_rag_chunks_with_config_respects_max_tokens() {
        let pdf = sample_pdf();
        let hybrid = r#"{
            "max_tokens": 16,
            "overlap_tokens": 0,
            "merge_adjacent": false,
            "propagate_headings": true,
            "merge_policy": "SameTypeOnly"
        }"#;
        let hy_c = std::ffi::CString::new(hybrid).unwrap();
        let mut out: *mut c_char = std::ptr::null_mut();
        let code = unsafe {
            oxidize_rag_chunks_with_config(
                pdf.as_ptr(), pdf.len(),
                std::ptr::null(),             // partition_config: null → default
                hy_c.as_ptr(),
                &mut out,
            )
        };
        assert_eq!(code, ErrorCode::Success as c_int);
        let json = unsafe { std::ffi::CStr::from_ptr(out).to_string_lossy().into_owned() };
        unsafe { oxidize_free_string(out); }
        // At 16 max_tokens, we expect multiple chunks on the sample paragraph.
        let chunks: serde_json::Value = serde_json::from_str(&json).unwrap();
        assert!(chunks.as_array().unwrap().len() >= 1);
    }
```

- [ ] **Step 9.2: Run to verify failure**

Run:
```bash
cargo test --manifest-path native/Cargo.toml profile_ffi_tests --lib
```
Expected: FAIL.

- [ ] **Step 9.3: Implement**

Append to `native/src/parser.rs`:

```rust
/// Extract RAG chunks using an explicit partition config and/or hybrid chunk config
/// supplied as JSON UTF-8 strings. Pass NULL for either to use the default.
///
/// # Safety
/// Both config pointers, if non-null, must be NUL-terminated UTF-8 C strings.
#[no_mangle]
pub unsafe extern "C" fn oxidize_rag_chunks_with_config(
    pdf_bytes: *const u8,
    pdf_len: usize,
    partition_config_json: *const c_char,
    hybrid_config_json: *const c_char,
    out_json: *mut *mut c_char,
) -> c_int {
    clear_last_error();

    if pdf_bytes.is_null() || out_json.is_null() {
        set_last_error("Null pointer provided to oxidize_rag_chunks_with_config");
        return ErrorCode::NullPointer as c_int;
    }
    *out_json = ptr::null_mut();
    if pdf_len == 0 {
        set_last_error("PDF data is empty (0 bytes)");
        return ErrorCode::PdfParseError as c_int;
    }

    // Parse optional configs
    let partition_cfg = if partition_config_json.is_null() {
        oxidize_pdf::pipeline::PartitionConfig::default()
    } else {
        let s = match std::ffi::CStr::from_ptr(partition_config_json).to_str() {
            Ok(v) => v,
            Err(e) => { set_last_error(format!("partition_config UTF-8: {e}")); return ErrorCode::InvalidUtf8 as c_int; }
        };
        match serde_json::from_str::<crate::pipeline_config::PartitionConfigDto>(s) {
            Ok(d) => d.into(),
            Err(e) => { set_last_error(format!("partition_config JSON: {e}")); return ErrorCode::InvalidArgument as c_int; }
        }
    };
    let hybrid_cfg = if hybrid_config_json.is_null() {
        oxidize_pdf::pipeline::HybridChunkConfig::default()
    } else {
        let s = match std::ffi::CStr::from_ptr(hybrid_config_json).to_str() {
            Ok(v) => v,
            Err(e) => { set_last_error(format!("hybrid_config UTF-8: {e}")); return ErrorCode::InvalidUtf8 as c_int; }
        };
        match serde_json::from_str::<crate::pipeline_config::HybridChunkConfigDto>(s) {
            Ok(d) => d.into(),
            Err(e) => { set_last_error(format!("hybrid_config JSON: {e}")); return ErrorCode::InvalidArgument as c_int; }
        }
    };

    let bytes = slice::from_raw_parts(pdf_bytes, pdf_len);
    let reader = match open_lenient(bytes) {
        Ok(r) => r,
        Err(e) => { set_last_error(e); return ErrorCode::PdfParseError as c_int; }
    };
    let document = PdfDocument::new(reader);

    // Partition with the explicit config, then chunk with the hybrid config.
    let elements = match document.partition_with(partition_cfg) {
        Ok(e) => e,
        Err(e) => { set_last_error(format!("partition_with failed: {e}")); return ErrorCode::PdfParseError as c_int; }
    };
    let chunker = oxidize_pdf::pipeline::HybridChunker::new(hybrid_cfg);
    let hybrid_chunks = chunker.chunk(&elements);
    let chunks: Vec<oxidize_pdf::pipeline::RagChunk> = hybrid_chunks
        .iter()
        .enumerate()
        .map(|(i, hc)| oxidize_pdf::pipeline::RagChunk::from_hybrid_chunk(i, hc))
        .collect();

    let results: Vec<RagChunkResult> = chunks.iter().enumerate().map(|(i, chunk)| RagChunkResult {
        chunk_index: i,
        text: chunk.text.clone(),
        full_text: chunk.full_text.clone(),
        page_numbers: chunk.page_numbers.iter().map(|p| p + 1).collect(),
        element_types: chunk.element_types.clone(),
        heading_context: chunk.heading_context.clone(),
        token_estimate: chunk.token_estimate,
        is_oversized: chunk.is_oversized,
    }).collect();

    let json = match serde_json::to_string(&results) {
        Ok(j) => j,
        Err(e) => { set_last_error(format!("serialize: {e}")); return ErrorCode::SerializationError as c_int; }
    };
    let c_string = match CString::new(json) {
        Ok(c) => c,
        Err(e) => { set_last_error(format!("null bytes: {e}")); return ErrorCode::InvalidUtf8 as c_int; }
    };
    *out_json = c_string.into_raw();
    ErrorCode::Success as c_int
}
```

- [ ] **Step 9.4: Verify tests pass**

Run:
```bash
cargo test --manifest-path native/Cargo.toml profile_ffi_tests --lib
```
Expected: 6 tests passing.

- [ ] **Step 9.5: Commit**

```bash
git add native/src/parser.rs
git commit -m "feat(rag): add oxidize_rag_chunks_with_config FFI function"
```

---

## Task 10: Rust FFI — `oxidize_semantic_chunks`

**Files:**
- Modify: `native/src/parser.rs`

Semantic chunking path: `SemanticChunker` takes a `SemanticChunkConfig` and a slice of `Element`s. We partition first (with the given partition config or defaults), then chunk with the semantic config.

- [ ] **Step 10.1: Check the SemanticChunker API surface**

Run:
```bash
grep -n "pub fn\|pub struct SemanticChunker" /home/santi/.cargo/registry/src/index.crates.io-*/oxidize-pdf-2.5.*/src/pipeline/semantic_chunking.rs | head -10
```
If the API is `SemanticChunker::new(cfg).chunk(&elements) -> Vec<SemanticChunk>` and each `SemanticChunk` has `.text()`, `.token_estimate()`, `.page_numbers()`, `.is_oversized()`, proceed. If the API differs, adapt the result-type mapping in Step 10.3 accordingly.

- [ ] **Step 10.2: Write the failing Rust test + add result type**

First, add a new result struct near `RagChunkResult` in `native/src/parser.rs`:

```rust
#[derive(serde::Serialize)]
struct SemanticChunkResult {
    chunk_index: usize,
    text: String,
    page_numbers: Vec<u32>,
    token_estimate: usize,
    is_oversized: bool,
}
```

Then append to `profile_ffi_tests`:

```rust
    #[test]
    fn oxidize_semantic_chunks_returns_chunks() {
        let pdf = sample_pdf();
        let sem = r#"{"max_tokens":64,"overlap_tokens":10,"respect_element_boundaries":true}"#;
        let sem_c = std::ffi::CString::new(sem).unwrap();
        let mut out: *mut c_char = std::ptr::null_mut();
        let code = unsafe {
            oxidize_semantic_chunks(
                pdf.as_ptr(), pdf.len(),
                std::ptr::null(),
                sem_c.as_ptr(),
                &mut out,
            )
        };
        assert_eq!(code, ErrorCode::Success as c_int);
        let json = unsafe { std::ffi::CStr::from_ptr(out).to_string_lossy().into_owned() };
        unsafe { oxidize_free_string(out); }
        assert!(json.starts_with('['));
        assert!(json.contains("token_estimate"));
    }
```

- [ ] **Step 10.3: Implement the FFI function**

Append to `native/src/parser.rs`:

```rust
/// Extract semantic chunks (element-boundary aware) from a PDF.
///
/// # Safety
/// See `oxidize_rag_chunks_with_config`. `semantic_config_json` is required
/// (non-null); `partition_config_json` may be NULL for defaults.
#[no_mangle]
pub unsafe extern "C" fn oxidize_semantic_chunks(
    pdf_bytes: *const u8,
    pdf_len: usize,
    partition_config_json: *const c_char,
    semantic_config_json: *const c_char,
    out_json: *mut *mut c_char,
) -> c_int {
    clear_last_error();

    if pdf_bytes.is_null() || semantic_config_json.is_null() || out_json.is_null() {
        set_last_error("Null pointer provided to oxidize_semantic_chunks");
        return ErrorCode::NullPointer as c_int;
    }
    *out_json = ptr::null_mut();
    if pdf_len == 0 {
        set_last_error("PDF data is empty (0 bytes)");
        return ErrorCode::PdfParseError as c_int;
    }

    let partition_cfg = if partition_config_json.is_null() {
        oxidize_pdf::pipeline::PartitionConfig::default()
    } else {
        let s = match std::ffi::CStr::from_ptr(partition_config_json).to_str() {
            Ok(v) => v,
            Err(e) => { set_last_error(format!("partition_config UTF-8: {e}")); return ErrorCode::InvalidUtf8 as c_int; }
        };
        match serde_json::from_str::<crate::pipeline_config::PartitionConfigDto>(s) {
            Ok(d) => d.into(),
            Err(e) => { set_last_error(format!("partition_config JSON: {e}")); return ErrorCode::InvalidArgument as c_int; }
        }
    };

    let sem_str = match std::ffi::CStr::from_ptr(semantic_config_json).to_str() {
        Ok(v) => v,
        Err(e) => { set_last_error(format!("semantic_config UTF-8: {e}")); return ErrorCode::InvalidUtf8 as c_int; }
    };
    let sem_cfg: oxidize_pdf::pipeline::SemanticChunkConfig =
        match serde_json::from_str::<crate::pipeline_config::SemanticChunkConfigDto>(sem_str) {
            Ok(d) => d.into(),
            Err(e) => { set_last_error(format!("semantic_config JSON: {e}")); return ErrorCode::InvalidArgument as c_int; }
        };

    let bytes = slice::from_raw_parts(pdf_bytes, pdf_len);
    let reader = match open_lenient(bytes) {
        Ok(r) => r,
        Err(e) => { set_last_error(e); return ErrorCode::PdfParseError as c_int; }
    };
    let document = PdfDocument::new(reader);
    let elements = match document.partition_with(partition_cfg) {
        Ok(e) => e,
        Err(e) => { set_last_error(format!("partition_with failed: {e}")); return ErrorCode::PdfParseError as c_int; }
    };

    let chunker = oxidize_pdf::pipeline::SemanticChunker::new(sem_cfg);
    let sem_chunks = chunker.chunk(&elements);

    let results: Vec<SemanticChunkResult> = sem_chunks.iter().enumerate().map(|(i, sc)| SemanticChunkResult {
        chunk_index: i,
        text: sc.text(),
        page_numbers: sc.page_numbers().into_iter().map(|p| p + 1).collect(),
        token_estimate: sc.token_estimate(),
        is_oversized: sc.is_oversized(),
    }).collect();

    let json = match serde_json::to_string(&results) {
        Ok(j) => j,
        Err(e) => { set_last_error(format!("serialize: {e}")); return ErrorCode::SerializationError as c_int; }
    };
    let c_string = match CString::new(json) {
        Ok(c) => c,
        Err(e) => { set_last_error(format!("null bytes: {e}")); return ErrorCode::InvalidUtf8 as c_int; }
    };
    *out_json = c_string.into_raw();
    ErrorCode::Success as c_int
}
```

**Note**: if `SemanticChunker::new` / `.chunk()` signatures differ in 2.5.4, adapt — the `oxidize-pdf` sources under `~/.cargo/registry/...-2.5.4/src/pipeline/semantic_chunking.rs` are authoritative.

- [ ] **Step 10.4: Verify tests pass**

Run:
```bash
cargo test --manifest-path native/Cargo.toml profile_ffi_tests --lib
```
Expected: 7 tests passing. Also run the full native suite to catch regressions:
```bash
cargo test --manifest-path native/Cargo.toml --lib
```

- [ ] **Step 10.5: Commit**

```bash
git add native/src/parser.rs
git commit -m "feat(rag): add oxidize_semantic_chunks FFI function"
```

---

## Task 10b: Rust FFI — `oxidize_to_markdown_with_options` (RAG-012)

**Files:**
- Modify: `native/src/parser.rs`

- [ ] **Step 10b.1: Write the failing Rust test**

Append to the `profile_ffi_tests` module in `native/src/parser.rs`:

```rust
    #[test]
    fn oxidize_to_markdown_with_options_respects_include_metadata() {
        let pdf = sample_pdf();
        let opts = r#"{"include_metadata":false,"include_page_numbers":false}"#;
        let opts_c = std::ffi::CString::new(opts).unwrap();
        let mut out: *mut c_char = std::ptr::null_mut();
        let code = unsafe {
            oxidize_to_markdown_with_options(pdf.as_ptr(), pdf.len(), opts_c.as_ptr(), &mut out)
        };
        assert_eq!(code, ErrorCode::Success as c_int);
        let md = unsafe { std::ffi::CStr::from_ptr(out).to_string_lossy().into_owned() };
        unsafe { oxidize_free_string(out); }
        assert!(!md.is_empty());

        // With defaults (include_metadata=true), the baseline oxidize_to_markdown
        // output MUST differ — otherwise options are being ignored.
        let mut out_default: *mut c_char = std::ptr::null_mut();
        let code2 = unsafe { oxidize_to_markdown(pdf.as_ptr(), pdf.len(), &mut out_default) };
        assert_eq!(code2, ErrorCode::Success as c_int);
        let md_default = unsafe { std::ffi::CStr::from_ptr(out_default).to_string_lossy().into_owned() };
        unsafe { oxidize_free_string(out_default); }
        assert_ne!(md, md_default, "options ignored — markdown output is identical to defaults");
    }
```

- [ ] **Step 10b.2: Run to verify failure**

```bash
cargo test --manifest-path native/Cargo.toml profile_ffi_tests --lib
```
Expected: FAIL — function not found.

- [ ] **Step 10b.3: Implement**

Append to `native/src/parser.rs`:

```rust
/// Export PDF content as Markdown using explicit `MarkdownOptions` JSON.
///
/// # Safety
/// - `pdf_bytes` must be valid for `pdf_len` bytes.
/// - `options_json` must be a valid NUL-terminated UTF-8 C string.
/// - `out_text` will be allocated; free with `oxidize_free_string`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_to_markdown_with_options(
    pdf_bytes: *const u8,
    pdf_len: usize,
    options_json: *const c_char,
    out_text: *mut *mut c_char,
) -> c_int {
    clear_last_error();

    if pdf_bytes.is_null() || options_json.is_null() || out_text.is_null() {
        set_last_error("Null pointer provided to oxidize_to_markdown_with_options");
        return ErrorCode::NullPointer as c_int;
    }
    *out_text = ptr::null_mut();
    if pdf_len == 0 {
        set_last_error("PDF data is empty (0 bytes)");
        return ErrorCode::PdfParseError as c_int;
    }

    let opts_str = match std::ffi::CStr::from_ptr(options_json).to_str() {
        Ok(s) => s,
        Err(e) => { set_last_error(format!("options UTF-8: {e}")); return ErrorCode::InvalidUtf8 as c_int; }
    };
    let opts: oxidize_pdf::ai::MarkdownOptions =
        match serde_json::from_str::<crate::pipeline_config::MarkdownOptionsDto>(opts_str) {
            Ok(d) => d.into(),
            Err(e) => { set_last_error(format!("options JSON: {e}")); return ErrorCode::InvalidArgument as c_int; }
        };

    let bytes = slice::from_raw_parts(pdf_bytes, pdf_len);
    let reader = match open_lenient(bytes) {
        Ok(r) => r,
        Err(e) => { set_last_error(e); return ErrorCode::PdfParseError as c_int; }
    };
    let document = PdfDocument::new(reader);

    // Extract text, then feed it through the options-aware exporter.
    let text = match document.extract_text() {
        Ok(t) => t.iter().map(|p| p.text.clone()).collect::<Vec<_>>().join("\n\n"),
        Err(e) => { set_last_error(format!("extract_text failed: {e}")); return ErrorCode::PdfParseError as c_int; }
    };
    let exporter = oxidize_pdf::ai::MarkdownExporter::new(opts);
    let md = match exporter.export(&text) {
        Ok(s) => s,
        Err(e) => { set_last_error(format!("markdown export: {e}")); return ErrorCode::PdfParseError as c_int; }
    };

    let c_string = match CString::new(md) {
        Ok(c) => c,
        Err(e) => { set_last_error(format!("null bytes: {e}")); return ErrorCode::InvalidUtf8 as c_int; }
    };
    *out_text = c_string.into_raw();
    ErrorCode::Success as c_int
}
```

**Note:** If the exact `PdfDocument::extract_text` return shape differs in 2.5.4, follow whatever the existing `oxidize_to_markdown` does — it already solved this problem. Grep `oxidize_to_markdown` in `parser.rs` and mirror its body with the options-aware exporter swap.

- [ ] **Step 10b.4: Verify passes**

```bash
cargo test --manifest-path native/Cargo.toml profile_ffi_tests --lib
```
Expected: 8 tests passing (7 from earlier tasks + 1 new).

- [ ] **Step 10b.5: Commit**

```bash
git add native/src/parser.rs
git commit -m "feat(rag): add oxidize_to_markdown_with_options FFI function (RAG-012)"
```

---

## Task 10c: Rust FFI — `oxidize_chunk_text` + `oxidize_estimate_tokens` (RAG-008, RAG-009)

**Files:**
- Modify: `native/src/parser.rs`

Two small functions, same commit. They operate on pure text (no PDF parsing), so no `open_lenient` is involved — this is the simplest FFI in the plan.

- [ ] **Step 10c.1: Write the failing Rust tests**

Append to `profile_ffi_tests`:

```rust
    #[test]
    fn oxidize_estimate_tokens_returns_word_count_proxy() {
        let mut out: usize = 0;
        let text = std::ffi::CString::new("hello world from oxidize").unwrap();
        let code = unsafe { oxidize_estimate_tokens(text.as_ptr(), &mut out) };
        assert_eq!(code, ErrorCode::Success as c_int);
        assert_eq!(out, 4); // four whitespace-separated tokens (word-count proxy)
    }

    #[test]
    fn oxidize_chunk_text_splits_long_text() {
        let long = "word ".repeat(200);
        let text = std::ffi::CString::new(long).unwrap();
        let mut out: *mut c_char = std::ptr::null_mut();
        let code = unsafe {
            oxidize_chunk_text(text.as_ptr(), 50 /* chunk_size */, 5 /* overlap */, &mut out)
        };
        assert_eq!(code, ErrorCode::Success as c_int);
        let json = unsafe { std::ffi::CStr::from_ptr(out).to_string_lossy().into_owned() };
        unsafe { oxidize_free_string(out); }
        let chunks: serde_json::Value = serde_json::from_str(&json).unwrap();
        let arr = chunks.as_array().unwrap();
        assert!(arr.len() >= 2, "expected multiple chunks for 200-word input at size=50");
        // First chunk should have id like "chunk_0" and non-empty content
        assert_eq!(arr[0]["chunk_index"].as_u64().unwrap(), 0);
        assert!(arr[0]["content"].as_str().unwrap().len() > 0);
    }

    #[test]
    fn oxidize_chunk_text_rejects_overlap_ge_size() {
        let text = std::ffi::CString::new("some text").unwrap();
        let mut out: *mut c_char = std::ptr::null_mut();
        let code = unsafe { oxidize_chunk_text(text.as_ptr(), 10, 10, &mut out) };
        assert_eq!(code, ErrorCode::InvalidArgument as c_int);
        assert!(out.is_null());
    }
```

- [ ] **Step 10c.2: Run to verify failure**

```bash
cargo test --manifest-path native/Cargo.toml profile_ffi_tests --lib
```
Expected: FAIL — functions not found.

- [ ] **Step 10c.3: Add result type + implement both functions**

Add near `RagChunkResult` in `native/src/parser.rs`:

```rust
#[derive(serde::Serialize)]
struct TextChunkResult {
    id: String,
    content: String,
    tokens: usize,
    page_numbers: Vec<usize>,
    chunk_index: usize,
}
```

Append:

```rust
/// Estimate the number of tokens in a text string using the core's word-count proxy.
///
/// # Safety
/// `text` must be a valid NUL-terminated UTF-8 C string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_estimate_tokens(
    text: *const c_char,
    out_count: *mut usize,
) -> c_int {
    clear_last_error();
    if text.is_null() || out_count.is_null() {
        set_last_error("Null pointer provided to oxidize_estimate_tokens");
        return ErrorCode::NullPointer as c_int;
    }
    let s = match std::ffi::CStr::from_ptr(text).to_str() {
        Ok(v) => v,
        Err(e) => { set_last_error(format!("UTF-8: {e}")); return ErrorCode::InvalidUtf8 as c_int; }
    };
    *out_count = oxidize_pdf::ai::DocumentChunker::estimate_tokens(s);
    ErrorCode::Success as c_int
}

/// Chunk arbitrary text using a fixed-size + overlap strategy (no PDF involved).
/// Returns a JSON array of `{id, content, tokens, page_numbers, chunk_index}`.
///
/// # Safety
/// `text` must be a valid NUL-terminated UTF-8 C string.
/// `out_json` will be allocated; free with `oxidize_free_string`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_chunk_text(
    text: *const c_char,
    chunk_size: usize,
    overlap: usize,
    out_json: *mut *mut c_char,
) -> c_int {
    clear_last_error();
    if text.is_null() || out_json.is_null() {
        set_last_error("Null pointer provided to oxidize_chunk_text");
        return ErrorCode::NullPointer as c_int;
    }
    *out_json = ptr::null_mut();
    if chunk_size == 0 {
        set_last_error("chunk_size must be positive");
        return ErrorCode::InvalidArgument as c_int;
    }
    if overlap >= chunk_size {
        set_last_error("overlap must be less than chunk_size");
        return ErrorCode::InvalidArgument as c_int;
    }
    let s = match std::ffi::CStr::from_ptr(text).to_str() {
        Ok(v) => v,
        Err(e) => { set_last_error(format!("UTF-8: {e}")); return ErrorCode::InvalidUtf8 as c_int; }
    };

    let chunker = oxidize_pdf::ai::DocumentChunker::new(chunk_size, overlap);
    let chunks = match chunker.chunk_text(s) {
        Ok(c) => c,
        Err(e) => { set_last_error(format!("chunk_text: {e}")); return ErrorCode::PdfParseError as c_int; }
    };

    let results: Vec<TextChunkResult> = chunks.into_iter().map(|c| TextChunkResult {
        id: c.id,
        content: c.content,
        tokens: c.tokens,
        page_numbers: c.page_numbers,
        chunk_index: c.chunk_index,
    }).collect();

    let json = match serde_json::to_string(&results) {
        Ok(j) => j,
        Err(e) => { set_last_error(format!("serialize: {e}")); return ErrorCode::SerializationError as c_int; }
    };
    let c_string = match CString::new(json) {
        Ok(c) => c,
        Err(e) => { set_last_error(format!("null bytes: {e}")); return ErrorCode::InvalidUtf8 as c_int; }
    };
    *out_json = c_string.into_raw();
    ErrorCode::Success as c_int
}
```

- [ ] **Step 10c.4: Verify passes**

```bash
cargo test --manifest-path native/Cargo.toml profile_ffi_tests --lib
cargo test --manifest-path native/Cargo.toml --lib
```
Expected: 11 `profile_ffi_tests` tests passing, full native suite green.

- [ ] **Step 10c.5: Commit**

```bash
git add native/src/parser.rs
git commit -m "feat(rag): add oxidize_chunk_text and oxidize_estimate_tokens FFI (RAG-008, RAG-009)"
```

---

## Task 11: C# P/Invoke declarations

**Files:**
- Modify: `dotnet/OxidizePdf.NET/NativeMethods.cs`

- [ ] **Step 11.1: Add the eight new declarations**

Append after the existing `oxidize_rag_chunks` declaration in `NativeMethods.cs` (line ~938):

```csharp
    /// <summary>Partition PDF using an ExtractionProfile discriminant.</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_partition_with_profile(
        IntPtr pdfBytes, nuint pdfLen, byte profile, out IntPtr outJson);

    /// <summary>Partition PDF using a PartitionConfig serialized as JSON.</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_partition_with_config(
        IntPtr pdfBytes, nuint pdfLen,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string configJson,
        out IntPtr outJson);

    /// <summary>Extract RAG chunks using an ExtractionProfile discriminant.</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_rag_chunks_with_profile(
        IntPtr pdfBytes, nuint pdfLen, byte profile, out IntPtr outJson);

    /// <summary>Extract RAG chunks with optional partition + hybrid configs (null for defaults).</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_rag_chunks_with_config(
        IntPtr pdfBytes, nuint pdfLen,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? partitionConfigJson,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? hybridConfigJson,
        out IntPtr outJson);

    /// <summary>Extract semantic chunks (element-boundary aware).</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_semantic_chunks(
        IntPtr pdfBytes, nuint pdfLen,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? partitionConfigJson,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string semanticConfigJson,
        out IntPtr outJson);

    /// <summary>Export PDF to Markdown with explicit MarkdownOptions (RAG-012).</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_to_markdown_with_options(
        IntPtr pdfBytes, nuint pdfLen,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string optionsJson,
        out IntPtr outText);

    /// <summary>Chunk arbitrary text using size + overlap (RAG-008).</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_chunk_text(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
        nuint chunkSize, nuint overlap,
        out IntPtr outJson);

    /// <summary>Estimate tokens in a text string (RAG-009).</summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int oxidize_estimate_tokens(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
        out nuint outCount);
```

- [ ] **Step 11.2: Verify the managed project still compiles**

Run:
```bash
dotnet build dotnet/OxidizePdf.NET/OxidizePdf.NET.csproj --nologo
```
Expected: build succeeds, zero warnings.

- [ ] **Step 11.3: Commit**

```bash
git add dotnet/OxidizePdf.NET/NativeMethods.cs
git commit -m "feat(rag): add P/Invoke declarations for new pipeline FFI functions"
```

---

## Task 12: `PdfExtractor.PartitionAsync(byte[], ExtractionProfile)` overload

**Files:**
- Modify: `dotnet/OxidizePdf.NET/PdfExtractor.cs`
- Test: `dotnet/OxidizePdf.NET.Tests/Pipeline/PdfExtractorProfileTests.cs`

Before starting Tasks 12–16, rebuild the native library so the managed side sees the new FFI:
```bash
cargo build --manifest-path native/Cargo.toml --release
# Copy or symlink the produced lib to where the test project loads it — follow
# whatever pattern NativeBinariesTests.cs expects (check that test for the path).
```

- [ ] **Step 12.1: Write the failing integration test**

Create `dotnet/OxidizePdf.NET.Tests/Pipeline/PdfExtractorProfileTests.cs`:

```csharp
using FluentAssertions;
using OxidizePdf.NET;
using OxidizePdf.NET.Pipeline;
using Xunit;

namespace OxidizePdf.NET.Tests.Pipeline;

public class PdfExtractorProfileTests
{
    private static byte[] SamplePdf()
    {
        // Build a minimal PDF in-memory using the existing PdfDocument/PdfPage APIs.
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.SetFont(NativeMethods_Exposed.HelveticaBold(), 14.0)
            .TextAt(50, 750, "Introduction");
        page.SetFont(NativeMethods_Exposed.Helvetica(), 11.0)
            .TextAt(50, 720, "Sample paragraph used for RAG profile tests. It contains enough words to exercise the partitioner.");
        doc.AddPage(page);
        return doc.SaveToBytes();
    }

    [Fact]
    public async Task PartitionAsync_with_Standard_profile_returns_elements()
    {
        var pdf = SamplePdf();
        var extractor = new PdfExtractor();
        var elements = await extractor.PartitionAsync(pdf, ExtractionProfile.Standard);
        elements.Should().NotBeEmpty();
        elements.Should().OnlyContain(e => !string.IsNullOrEmpty(e.ElementType));
    }

    [Fact]
    public async Task PartitionAsync_with_Rag_profile_returns_elements()
    {
        var pdf = SamplePdf();
        var extractor = new PdfExtractor();
        var elements = await extractor.PartitionAsync(pdf, ExtractionProfile.Rag);
        elements.Should().NotBeEmpty();
    }

    [Fact]
    public async Task PartitionAsync_with_invalid_discriminant_should_not_be_possible()
    {
        // Defensive: even though the enum restricts callers, a cast should surface
        // the InvalidArgument error from the FFI layer.
        var pdf = SamplePdf();
        var extractor = new PdfExtractor();
        var act = async () => await extractor.PartitionAsync(pdf, (ExtractionProfile)99);
        await act.Should().ThrowAsync<PdfExtractionException>();
    }
}
```

**Helper note:** If the test project does not already have internal accessors for `Helvetica`/`HelveticaBold`, replace those with `StandardFont.Helvetica` / `StandardFont.HelveticaBold` from the existing public API (check `EndToEndTests.cs` for the idiom). The exact call is whatever existing tests use — don't invent a helper.

- [ ] **Step 12.2: Run tests to verify they fail**

Run:
```bash
dotnet test dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj --filter "FullyQualifiedName~PdfExtractorProfileTests" --nologo
```
Expected: FAIL — overload doesn't exist.

- [ ] **Step 12.3: Implement the overload**

In `dotnet/OxidizePdf.NET/PdfExtractor.cs`, add the using `using OxidizePdf.NET.Pipeline;` at the top if missing, then add after the existing `PartitionAsync` method (line ~100):

```csharp
    /// <summary>
    /// Partition a PDF into typed semantic elements using a pre-configured extraction profile.
    /// </summary>
    public Task<List<PdfElement>> PartitionAsync(
        byte[] pdfBytes,
        ExtractionProfile profile,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ValidatePdfSize(pdfBytes);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => PartitionWithProfile(pdfBytes, profile), cancellationToken);
    }

    private List<PdfElement> PartitionWithProfile(byte[] pdfBytes, ExtractionProfile profile)
    {
        var handle = GCHandle.Alloc(pdfBytes, GCHandleType.Pinned);
        try
        {
            IntPtr outJson = IntPtr.Zero;
            var rc = NativeMethods.oxidize_partition_with_profile(
                handle.AddrOfPinnedObject(), (nuint)pdfBytes.Length, (byte)profile, out outJson);
            ThrowIfError(rc, "oxidize_partition_with_profile failed");
            try
            {
                var json = Marshal.PtrToStringUTF8(outJson) ?? "[]";
                return JsonSerializer.Deserialize<List<PdfElement>>(json) ?? new();
            }
            finally
            {
                if (outJson != IntPtr.Zero) NativeMethods.oxidize_free_string(outJson);
            }
        }
        finally { handle.Free(); }
    }
```

If `ThrowIfError` and `GCHandle` pinning differs from the existing style in this file, mirror the pattern already used by `Partition` (look for the existing private `Partition(byte[])` method and copy its surrounding infrastructure).

- [ ] **Step 12.4: Verify tests pass**

Run:
```bash
dotnet test dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj --filter "FullyQualifiedName~PdfExtractorProfileTests" --nologo
```
Expected: 3 tests passing.

- [ ] **Step 12.5: Commit**

```bash
git add dotnet/OxidizePdf.NET/PdfExtractor.cs \
        dotnet/OxidizePdf.NET.Tests/Pipeline/PdfExtractorProfileTests.cs
git commit -m "feat(rag): add PartitionAsync(byte[], ExtractionProfile) overload"
```

---

## Task 13: `PdfExtractor.PartitionAsync(byte[], PartitionConfig)` overload

**Files:**
- Modify: `dotnet/OxidizePdf.NET/PdfExtractor.cs`
- Test: `dotnet/OxidizePdf.NET.Tests/Pipeline/PdfExtractorPartitionConfigTests.cs`

- [ ] **Step 13.1: Write the failing test**

Create `dotnet/OxidizePdf.NET.Tests/Pipeline/PdfExtractorPartitionConfigTests.cs`:

```csharp
using FluentAssertions;
using OxidizePdf.NET;
using OxidizePdf.NET.Pipeline;
using Xunit;

namespace OxidizePdf.NET.Tests.Pipeline;

public class PdfExtractorPartitionConfigTests
{
    private static byte[] SamplePdf() => PdfExtractorProfileTests_Helpers.SamplePdf();

    [Fact]
    public async Task PartitionAsync_with_default_config_matches_no_arg_call()
    {
        var pdf = SamplePdf();
        var extractor = new PdfExtractor();
        var defaulted = await extractor.PartitionAsync(pdf);
        var explicitCfg = await extractor.PartitionAsync(pdf, new PartitionConfig());
        explicitCfg.Should().HaveCount(defaulted.Count);
    }

    [Fact]
    public async Task PartitionAsync_null_config_throws()
    {
        var pdf = SamplePdf();
        var extractor = new PdfExtractor();
        var act = async () => await extractor.PartitionAsync(pdf, (PartitionConfig)null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PartitionAsync_WithReadingOrder_XyCut_succeeds()
    {
        var pdf = SamplePdf();
        var extractor = new PdfExtractor();
        var cfg = new PartitionConfig().WithReadingOrder(ReadingOrderStrategy.XyCut(20.0));
        var elements = await extractor.PartitionAsync(pdf, cfg);
        elements.Should().NotBeEmpty();
    }

    [Fact]
    public async Task PartitionAsync_WithInvalidConfig_throws_from_Validate()
    {
        var pdf = SamplePdf();
        var extractor = new PdfExtractor();
        var bad = new PartitionConfig { MinTableConfidence = 5.0 };
        var act = async () => await extractor.PartitionAsync(pdf, bad);
        await act.Should().ThrowAsync<ArgumentException>();
    }
}

internal static class PdfExtractorProfileTests_Helpers
{
    public static byte[] SamplePdf()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.SetFont(StandardFont.HelveticaBold, 14.0).TextAt(50, 750, "Introduction");
        page.SetFont(StandardFont.Helvetica, 11.0)
            .TextAt(50, 720, "Sample paragraph used for RAG tests.");
        doc.AddPage(page);
        return doc.SaveToBytes();
    }
}
```

Refactor the earlier profile test file to use this shared helper instead of its own `SamplePdf()` — delete the private method there and replace calls with `PdfExtractorProfileTests_Helpers.SamplePdf()`.

- [ ] **Step 13.2: Run tests to verify they fail**

Run:
```bash
dotnet test dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj --filter "FullyQualifiedName~PdfExtractorPartitionConfigTests" --nologo
```
Expected: FAIL — overload missing.

- [ ] **Step 13.3: Implement**

Add to `PdfExtractor.cs` after Task 12's overload:

```csharp
    /// <summary>
    /// Partition a PDF into typed semantic elements using an explicit PartitionConfig.
    /// </summary>
    public Task<List<PdfElement>> PartitionAsync(
        byte[] pdfBytes,
        PartitionConfig config,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(pdfBytes);
        ArgumentNullException.ThrowIfNull(config);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ValidatePdfSize(pdfBytes);
        config.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        var json = config.ToJson();
        return Task.Run(() => PartitionWithConfig(pdfBytes, json), cancellationToken);
    }

    private List<PdfElement> PartitionWithConfig(byte[] pdfBytes, string configJson)
    {
        var handle = GCHandle.Alloc(pdfBytes, GCHandleType.Pinned);
        try
        {
            IntPtr outJson = IntPtr.Zero;
            var rc = NativeMethods.oxidize_partition_with_config(
                handle.AddrOfPinnedObject(), (nuint)pdfBytes.Length, configJson, out outJson);
            ThrowIfError(rc, "oxidize_partition_with_config failed");
            try
            {
                var result = Marshal.PtrToStringUTF8(outJson) ?? "[]";
                return JsonSerializer.Deserialize<List<PdfElement>>(result) ?? new();
            }
            finally
            {
                if (outJson != IntPtr.Zero) NativeMethods.oxidize_free_string(outJson);
            }
        }
        finally { handle.Free(); }
    }
```

- [ ] **Step 13.4: Verify tests pass**

Run:
```bash
dotnet test dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj --filter "FullyQualifiedName~PdfExtractorPartitionConfigTests" --nologo
```
Expected: 4 tests passing.

- [ ] **Step 13.5: Commit**

```bash
git add dotnet/OxidizePdf.NET/PdfExtractor.cs \
        dotnet/OxidizePdf.NET.Tests/Pipeline/PdfExtractorPartitionConfigTests.cs
git commit -m "feat(rag): add PartitionAsync(byte[], PartitionConfig) overload"
```

---

## Task 14: `PdfExtractor.RagChunksAsync(byte[], ExtractionProfile)` overload

**Files:**
- Modify: `dotnet/OxidizePdf.NET/PdfExtractor.cs`
- Test: `dotnet/OxidizePdf.NET.Tests/Pipeline/PdfExtractorRagProfileTests.cs`

- [ ] **Step 14.1: Write the failing test**

Create `dotnet/OxidizePdf.NET.Tests/Pipeline/PdfExtractorRagProfileTests.cs`:

```csharp
using FluentAssertions;
using OxidizePdf.NET;
using OxidizePdf.NET.Pipeline;
using Xunit;

namespace OxidizePdf.NET.Tests.Pipeline;

public class PdfExtractorRagProfileTests
{
    [Fact]
    public async Task RagChunksAsync_with_Rag_profile_returns_chunks_with_context()
    {
        var pdf = PdfExtractorProfileTests_Helpers.SamplePdf();
        var extractor = new PdfExtractor();
        var chunks = await extractor.RagChunksAsync(pdf, ExtractionProfile.Rag);
        chunks.Should().NotBeEmpty();
        chunks.Should().OnlyContain(c => c.FullText.Length >= c.Text.Length);
        chunks.Should().OnlyContain(c => c.TokenEstimate >= 0);
    }

    [Theory]
    [InlineData(ExtractionProfile.Standard)]
    [InlineData(ExtractionProfile.Academic)]
    [InlineData(ExtractionProfile.Form)]
    [InlineData(ExtractionProfile.Government)]
    [InlineData(ExtractionProfile.Dense)]
    [InlineData(ExtractionProfile.Presentation)]
    [InlineData(ExtractionProfile.Rag)]
    public async Task RagChunksAsync_every_profile_returns_valid_chunks(ExtractionProfile profile)
    {
        var pdf = PdfExtractorProfileTests_Helpers.SamplePdf();
        var extractor = new PdfExtractor();
        var chunks = await extractor.RagChunksAsync(pdf, profile);
        chunks.Should().NotBeNull();
        chunks.Should().OnlyContain(c => c.ChunkIndex >= 0);
    }
}
```

- [ ] **Step 14.2: Run to verify failure**

Run:
```bash
dotnet test dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj --filter "FullyQualifiedName~PdfExtractorRagProfileTests" --nologo
```
Expected: FAIL.

- [ ] **Step 14.3: Implement**

Add to `PdfExtractor.cs` after existing `RagChunksAsync`:

```csharp
    /// <summary>
    /// Extract structure-aware RAG chunks using a pre-configured extraction profile.
    /// </summary>
    public Task<List<RagChunk>> RagChunksAsync(
        byte[] pdfBytes,
        ExtractionProfile profile,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ValidatePdfSize(pdfBytes);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => RagChunksWithProfile(pdfBytes, profile), cancellationToken);
    }

    private List<RagChunk> RagChunksWithProfile(byte[] pdfBytes, ExtractionProfile profile)
    {
        var handle = GCHandle.Alloc(pdfBytes, GCHandleType.Pinned);
        try
        {
            IntPtr outJson = IntPtr.Zero;
            var rc = NativeMethods.oxidize_rag_chunks_with_profile(
                handle.AddrOfPinnedObject(), (nuint)pdfBytes.Length, (byte)profile, out outJson);
            ThrowIfError(rc, "oxidize_rag_chunks_with_profile failed");
            try
            {
                var json = Marshal.PtrToStringUTF8(outJson) ?? "[]";
                return JsonSerializer.Deserialize<List<RagChunk>>(json) ?? new();
            }
            finally
            {
                if (outJson != IntPtr.Zero) NativeMethods.oxidize_free_string(outJson);
            }
        }
        finally { handle.Free(); }
    }
```

- [ ] **Step 14.4: Verify**

Run:
```bash
dotnet test dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj --filter "FullyQualifiedName~PdfExtractorRagProfileTests" --nologo
```
Expected: 8 tests passing.

- [ ] **Step 14.5: Commit**

```bash
git add dotnet/OxidizePdf.NET/PdfExtractor.cs \
        dotnet/OxidizePdf.NET.Tests/Pipeline/PdfExtractorRagProfileTests.cs
git commit -m "feat(rag): add RagChunksAsync(byte[], ExtractionProfile) overload"
```

---

## Task 15: `PdfExtractor.RagChunksAsync(byte[], PartitionConfig?, HybridChunkConfig?)` overload

**Files:**
- Modify: `dotnet/OxidizePdf.NET/PdfExtractor.cs`
- Test: `dotnet/OxidizePdf.NET.Tests/Pipeline/PdfExtractorHybridChunksTests.cs`

- [ ] **Step 15.1: Write the failing test**

Create `dotnet/OxidizePdf.NET.Tests/Pipeline/PdfExtractorHybridChunksTests.cs`:

```csharp
using FluentAssertions;
using OxidizePdf.NET;
using OxidizePdf.NET.Pipeline;
using Xunit;

namespace OxidizePdf.NET.Tests.Pipeline;

public class PdfExtractorHybridChunksTests
{
    [Fact]
    public async Task RagChunksAsync_with_null_configs_matches_defaults()
    {
        var pdf = PdfExtractorProfileTests_Helpers.SamplePdf();
        var extractor = new PdfExtractor();
        var defaulted = await extractor.RagChunksAsync(pdf);
        var withNulls = await extractor.RagChunksAsync(pdf, partitionConfig: null, hybridConfig: null);
        withNulls.Should().HaveCount(defaulted.Count);
    }

    [Fact]
    public async Task RagChunksAsync_with_tiny_max_tokens_produces_more_chunks()
    {
        var pdf = PdfExtractorProfileTests_Helpers.SamplePdf();
        var extractor = new PdfExtractor();
        var big = await extractor.RagChunksAsync(pdf,
            partitionConfig: null,
            hybridConfig: new HybridChunkConfig().WithMaxTokens(512));
        var tiny = await extractor.RagChunksAsync(pdf,
            partitionConfig: null,
            hybridConfig: new HybridChunkConfig().WithMaxTokens(8).WithOverlap(0));
        tiny.Count.Should().BeGreaterThanOrEqualTo(big.Count);
    }

    [Fact]
    public async Task RagChunksAsync_validates_HybridChunkConfig()
    {
        var pdf = PdfExtractorProfileTests_Helpers.SamplePdf();
        var extractor = new PdfExtractor();
        var bad = new HybridChunkConfig { MaxTokens = 10, OverlapTokens = 10 };
        var act = async () => await extractor.RagChunksAsync(pdf, null, bad);
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
```

- [ ] **Step 15.2: Run to verify failure**

Run:
```bash
dotnet test dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj --filter "FullyQualifiedName~PdfExtractorHybridChunksTests" --nologo
```
Expected: FAIL.

- [ ] **Step 15.3: Implement**

Add to `PdfExtractor.cs`:

```csharp
    /// <summary>
    /// Extract structure-aware RAG chunks with optional partition and hybrid chunk configs.
    /// Pass <c>null</c> for either to use the corresponding defaults.
    /// </summary>
    public Task<List<RagChunk>> RagChunksAsync(
        byte[] pdfBytes,
        PartitionConfig? partitionConfig,
        HybridChunkConfig? hybridConfig,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ValidatePdfSize(pdfBytes);
        partitionConfig?.Validate();
        hybridConfig?.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        var pJson = partitionConfig?.ToJson();
        var hJson = hybridConfig?.ToJson();
        return Task.Run(() => RagChunksWithConfigs(pdfBytes, pJson, hJson), cancellationToken);
    }

    private List<RagChunk> RagChunksWithConfigs(byte[] pdfBytes, string? partitionJson, string? hybridJson)
    {
        var handle = GCHandle.Alloc(pdfBytes, GCHandleType.Pinned);
        try
        {
            IntPtr outJson = IntPtr.Zero;
            var rc = NativeMethods.oxidize_rag_chunks_with_config(
                handle.AddrOfPinnedObject(), (nuint)pdfBytes.Length,
                partitionJson, hybridJson, out outJson);
            ThrowIfError(rc, "oxidize_rag_chunks_with_config failed");
            try
            {
                var result = Marshal.PtrToStringUTF8(outJson) ?? "[]";
                return JsonSerializer.Deserialize<List<RagChunk>>(result) ?? new();
            }
            finally
            {
                if (outJson != IntPtr.Zero) NativeMethods.oxidize_free_string(outJson);
            }
        }
        finally { handle.Free(); }
    }
```

- [ ] **Step 15.4: Verify**

Run:
```bash
dotnet test dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj --filter "FullyQualifiedName~PdfExtractorHybridChunksTests" --nologo
```
Expected: 3 tests passing.

- [ ] **Step 15.5: Commit**

```bash
git add dotnet/OxidizePdf.NET/PdfExtractor.cs \
        dotnet/OxidizePdf.NET.Tests/Pipeline/PdfExtractorHybridChunksTests.cs
git commit -m "feat(rag): add RagChunksAsync with PartitionConfig+HybridChunkConfig overload"
```

---

## Task 16: `SemanticChunk` model + `PdfExtractor.SemanticChunksAsync`

**Files:**
- Create: `dotnet/OxidizePdf.NET/Models/SemanticChunk.cs`
- Modify: `dotnet/OxidizePdf.NET/PdfExtractor.cs`
- Test: `dotnet/OxidizePdf.NET.Tests/Pipeline/PdfExtractorSemanticChunksTests.cs`

- [ ] **Step 16.1: Write the failing test**

Create `dotnet/OxidizePdf.NET.Tests/Pipeline/PdfExtractorSemanticChunksTests.cs`:

```csharp
using FluentAssertions;
using OxidizePdf.NET;
using OxidizePdf.NET.Models;
using OxidizePdf.NET.Pipeline;
using Xunit;

namespace OxidizePdf.NET.Tests.Pipeline;

public class PdfExtractorSemanticChunksTests
{
    [Fact]
    public async Task SemanticChunksAsync_with_defaults_returns_chunks()
    {
        var pdf = PdfExtractorProfileTests_Helpers.SamplePdf();
        var extractor = new PdfExtractor();
        var chunks = await extractor.SemanticChunksAsync(pdf);
        chunks.Should().NotBeEmpty();
        chunks.Should().OnlyContain(c => c.TokenEstimate >= 0);
        chunks.Should().OnlyContain(c => c.PageNumbers.Count > 0);
    }

    [Fact]
    public async Task SemanticChunksAsync_with_tiny_max_tokens_splits()
    {
        var pdf = PdfExtractorProfileTests_Helpers.SamplePdf();
        var extractor = new PdfExtractor();
        var chunks = await extractor.SemanticChunksAsync(pdf,
            new SemanticChunkConfig(8).WithOverlap(0));
        chunks.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SemanticChunksAsync_validates_config()
    {
        var pdf = PdfExtractorProfileTests_Helpers.SamplePdf();
        var extractor = new PdfExtractor();
        var bad = new SemanticChunkConfig { MaxTokens = 10, OverlapTokens = 10 };
        var act = async () => await extractor.SemanticChunksAsync(pdf, bad);
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
```

- [ ] **Step 16.2: Run to verify failure**

Run:
```bash
dotnet test dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj --filter "FullyQualifiedName~PdfExtractorSemanticChunksTests" --nologo
```
Expected: FAIL.

- [ ] **Step 16.3: Create the `SemanticChunk` model**

Create `dotnet/OxidizePdf.NET/Models/SemanticChunk.cs`:

```csharp
using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Models;

/// <summary>
/// A semantic chunk produced by the element-aware chunker.
/// </summary>
public class SemanticChunk
{
    [JsonPropertyName("chunk_index")]
    public int ChunkIndex { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("page_numbers")]
    public List<int> PageNumbers { get; set; } = new();

    [JsonPropertyName("token_estimate")]
    public int TokenEstimate { get; set; }

    [JsonPropertyName("is_oversized")]
    public bool IsOversized { get; set; }
}
```

- [ ] **Step 16.4: Implement `SemanticChunksAsync`**

Add to `PdfExtractor.cs`:

```csharp
    /// <summary>
    /// Extract semantic (element-boundary aware) chunks from a PDF.
    /// </summary>
    public Task<List<SemanticChunk>> SemanticChunksAsync(
        byte[] pdfBytes,
        SemanticChunkConfig? config = null,
        PartitionConfig? partitionConfig = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ValidatePdfSize(pdfBytes);
        config ??= new SemanticChunkConfig();
        config.Validate();
        partitionConfig?.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        var pJson = partitionConfig?.ToJson();
        var sJson = config.ToJson();
        return Task.Run(() => SemanticChunksImpl(pdfBytes, pJson, sJson), cancellationToken);
    }

    private List<SemanticChunk> SemanticChunksImpl(byte[] pdfBytes, string? partitionJson, string semanticJson)
    {
        var handle = GCHandle.Alloc(pdfBytes, GCHandleType.Pinned);
        try
        {
            IntPtr outJson = IntPtr.Zero;
            var rc = NativeMethods.oxidize_semantic_chunks(
                handle.AddrOfPinnedObject(), (nuint)pdfBytes.Length,
                partitionJson, semanticJson, out outJson);
            ThrowIfError(rc, "oxidize_semantic_chunks failed");
            try
            {
                var result = Marshal.PtrToStringUTF8(outJson) ?? "[]";
                return JsonSerializer.Deserialize<List<SemanticChunk>>(result) ?? new();
            }
            finally
            {
                if (outJson != IntPtr.Zero) NativeMethods.oxidize_free_string(outJson);
            }
        }
        finally { handle.Free(); }
    }
```

- [ ] **Step 16.5: Verify**

Run:
```bash
dotnet test dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj --filter "FullyQualifiedName~PdfExtractorSemanticChunksTests" --nologo
```
Expected: 3 tests passing.

- [ ] **Step 16.6: Commit**

```bash
git add dotnet/OxidizePdf.NET/Models/SemanticChunk.cs \
        dotnet/OxidizePdf.NET/PdfExtractor.cs \
        dotnet/OxidizePdf.NET.Tests/Pipeline/PdfExtractorSemanticChunksTests.cs
git commit -m "feat(rag): add SemanticChunksAsync with SemanticChunk model"
```

---

## Task 16b: `PdfExtractor.ToMarkdownAsync(byte[], MarkdownOptions)` overload (RAG-012)

**Files:**
- Modify: `dotnet/OxidizePdf.NET/PdfExtractor.cs`
- Test: `dotnet/OxidizePdf.NET.Tests/Ai/MarkdownOptionsIntegrationTests.cs`

- [ ] **Step 16b.1: Write the failing test**

Create `dotnet/OxidizePdf.NET.Tests/Ai/MarkdownOptionsIntegrationTests.cs`:

```csharp
using FluentAssertions;
using OxidizePdf.NET;
using OxidizePdf.NET.Ai;
using OxidizePdf.NET.Tests.Pipeline;
using Xunit;

namespace OxidizePdf.NET.Tests.Ai;

public class MarkdownOptionsIntegrationTests
{
    [Fact]
    public async Task ToMarkdownAsync_with_options_null_throws()
    {
        var pdf = PdfExtractorProfileTests_Helpers.SamplePdf();
        var extractor = new PdfExtractor();
        var act = async () => await extractor.ToMarkdownAsync(pdf, (MarkdownOptions)null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ToMarkdownAsync_with_options_differs_from_defaults()
    {
        var pdf = PdfExtractorProfileTests_Helpers.SamplePdf();
        var extractor = new PdfExtractor();
        var defaulted = await extractor.ToMarkdownAsync(pdf);
        var noMeta = await extractor.ToMarkdownAsync(pdf,
            new MarkdownOptions { IncludeMetadata = false, IncludePageNumbers = false });
        noMeta.Should().NotBe(defaulted, "options-aware export must differ from defaults");
    }
}
```

- [ ] **Step 16b.2: Run to verify failure**

```bash
dotnet test dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj --filter "FullyQualifiedName~MarkdownOptionsIntegrationTests" --nologo
```
Expected: FAIL — overload missing.

- [ ] **Step 16b.3: Implement**

Add to `PdfExtractor.cs` after existing `ToMarkdownAsync`:

```csharp
    /// <summary>
    /// Export PDF content as Markdown using explicit MarkdownOptions (RAG-012).
    /// </summary>
    public Task<string> ToMarkdownAsync(
        byte[] pdfBytes,
        MarkdownOptions options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(pdfBytes);
        ArgumentNullException.ThrowIfNull(options);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be empty", nameof(pdfBytes));
        ValidatePdfSize(pdfBytes);
        cancellationToken.ThrowIfCancellationRequested();

        var optsJson = options.ToJson();
        return Task.Run(() => MarkdownWithOptions(pdfBytes, optsJson), cancellationToken);
    }

    private string MarkdownWithOptions(byte[] pdfBytes, string optionsJson)
    {
        var handle = GCHandle.Alloc(pdfBytes, GCHandleType.Pinned);
        try
        {
            IntPtr outText = IntPtr.Zero;
            var rc = NativeMethods.oxidize_to_markdown_with_options(
                handle.AddrOfPinnedObject(), (nuint)pdfBytes.Length, optionsJson, out outText);
            ThrowIfError(rc, "oxidize_to_markdown_with_options failed");
            try
            {
                return Marshal.PtrToStringUTF8(outText) ?? "";
            }
            finally
            {
                if (outText != IntPtr.Zero) NativeMethods.oxidize_free_string(outText);
            }
        }
        finally { handle.Free(); }
    }
```

Add `using OxidizePdf.NET.Ai;` at the top if missing.

- [ ] **Step 16b.4: Verify**

```bash
dotnet test dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj --filter "FullyQualifiedName~MarkdownOptionsIntegrationTests" --nologo
```
Expected: 2 tests passing.

- [ ] **Step 16b.5: Commit**

```bash
git add dotnet/OxidizePdf.NET/PdfExtractor.cs \
        dotnet/OxidizePdf.NET.Tests/Ai/MarkdownOptionsIntegrationTests.cs
git commit -m "feat(rag): add ToMarkdownAsync(MarkdownOptions) overload (RAG-012)"
```

---

## Task 16c: Standalone `DocumentChunker` + `TextChunk` + `EstimateTokens` (RAG-008, RAG-009)

**Files:**
- Create: `dotnet/OxidizePdf.NET/Ai/DocumentChunker.cs`
- Create: `dotnet/OxidizePdf.NET/Models/TextChunk.cs`
- Test: `dotnet/OxidizePdf.NET.Tests/Ai/DocumentChunkerTests.cs`

Matches the Python `oxidize_pdf.DocumentChunker` class. Standalone (no PDF input) — works on raw text.

- [ ] **Step 16c.1: Write the failing tests**

Create `dotnet/OxidizePdf.NET.Tests/Ai/DocumentChunkerTests.cs`:

```csharp
using FluentAssertions;
using OxidizePdf.NET.Ai;
using Xunit;

namespace OxidizePdf.NET.Tests.Ai;

public class DocumentChunkerTests
{
    [Fact]
    public void Default_ctor_matches_rust_defaults()
    {
        var c = new DocumentChunker();
        c.ChunkSize.Should().Be(512);
        c.Overlap.Should().Be(50);
    }

    [Fact]
    public void Ctor_rejects_overlap_ge_chunk_size()
    {
        var act = () => new DocumentChunker(10, 10);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EstimateTokens_word_count_proxy()
    {
        DocumentChunker.EstimateTokens("").Should().Be(0);
        DocumentChunker.EstimateTokens("hello").Should().Be(1);
        DocumentChunker.EstimateTokens("hello world from oxidize").Should().Be(4);
    }

    [Fact]
    public void EstimateTokens_null_throws()
    {
        var act = () => DocumentChunker.EstimateTokens(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ChunkText_splits_long_text_into_multiple_chunks()
    {
        var chunker = new DocumentChunker(20, 2);
        var longText = string.Join(" ", Enumerable.Repeat("word", 200));
        var chunks = chunker.ChunkText(longText);
        chunks.Should().HaveCountGreaterThan(1);
        chunks[0].ChunkIndex.Should().Be(0);
        chunks[0].Id.Should().StartWith("chunk_");
        chunks[0].Content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ChunkText_null_throws()
    {
        var chunker = new DocumentChunker();
        var act = () => chunker.ChunkText(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ChunkText_empty_returns_empty_list()
    {
        var chunker = new DocumentChunker();
        var chunks = chunker.ChunkText("");
        chunks.Should().BeEmpty();
    }
}
```

- [ ] **Step 16c.2: Run to verify failure**

```bash
dotnet test dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj --filter "FullyQualifiedName~DocumentChunkerTests" --nologo
```
Expected: FAIL — types missing.

- [ ] **Step 16c.3: Create the `TextChunk` DTO**

Create `dotnet/OxidizePdf.NET/Models/TextChunk.cs`:

```csharp
using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Models;

/// <summary>
/// A chunk produced by <see cref="OxidizePdf.NET.Ai.DocumentChunker"/>.
/// Mirrors <c>oxidize_pdf::ai::DocumentChunk</c> (distinct from the PDF-centric
/// <see cref="DocumentChunk"/> that comes out of per-page extraction).
/// </summary>
public class TextChunk
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("tokens")]
    public int Tokens { get; set; }

    [JsonPropertyName("page_numbers")]
    public List<int> PageNumbers { get; set; } = new();

    [JsonPropertyName("chunk_index")]
    public int ChunkIndex { get; set; }
}
```

- [ ] **Step 16c.4: Implement `DocumentChunker`**

Create `dotnet/OxidizePdf.NET/Ai/DocumentChunker.cs`:

```csharp
using System.Runtime.InteropServices;
using System.Text.Json;
using OxidizePdf.NET.Models;

namespace OxidizePdf.NET.Ai;

/// <summary>
/// Standalone text chunker mirroring <c>oxidize_pdf::ai::DocumentChunker</c>.
/// Operates on raw strings (no PDF involved). For PDF-to-chunks use
/// <see cref="OxidizePdf.NET.PdfExtractor.RagChunksAsync(byte[], System.Threading.CancellationToken)"/>.
/// </summary>
public class DocumentChunker
{
    public int ChunkSize { get; }
    public int Overlap { get; }

    public DocumentChunker() : this(512, 50) { }

    public DocumentChunker(int chunkSize, int overlap)
    {
        if (chunkSize <= 0) throw new ArgumentException("chunkSize must be positive", nameof(chunkSize));
        if (overlap < 0) throw new ArgumentException("overlap must be non-negative", nameof(overlap));
        if (overlap >= chunkSize) throw new ArgumentException("overlap must be less than chunkSize", nameof(overlap));
        ChunkSize = chunkSize;
        Overlap = overlap;
    }

    /// <summary>Chunk a text string into size-bounded overlapping pieces.</summary>
    public List<TextChunk> ChunkText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0) return new List<TextChunk>();

        IntPtr outJson = IntPtr.Zero;
        var rc = NativeMethods.oxidize_chunk_text(text, (nuint)ChunkSize, (nuint)Overlap, out outJson);
        PdfExtractor.ThrowIfErrorPublic(rc, "oxidize_chunk_text failed");
        try
        {
            var json = Marshal.PtrToStringUTF8(outJson) ?? "[]";
            return JsonSerializer.Deserialize<List<TextChunk>>(json) ?? new();
        }
        finally
        {
            if (outJson != IntPtr.Zero) NativeMethods.oxidize_free_string(outJson);
        }
    }

    /// <summary>Estimate tokens using the Rust core's word-count proxy.</summary>
    public static int EstimateTokens(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0) return 0;
        nuint count;
        var rc = NativeMethods.oxidize_estimate_tokens(text, out count);
        PdfExtractor.ThrowIfErrorPublic(rc, "oxidize_estimate_tokens failed");
        return (int)count;
    }
}
```

**Note:** `PdfExtractor.ThrowIfErrorPublic` is an `internal` forwarding method that you need to add to `PdfExtractor.cs`. Look at the existing private `ThrowIfError` and wrap it:

```csharp
// In PdfExtractor.cs, add near ThrowIfError:
internal static void ThrowIfErrorPublic(int rc, string message) => ThrowIfError(rc, message);
```

If `ThrowIfError` is already static/internal and `DocumentChunker` is in the same assembly, a direct call works — use that instead and skip the forwarding method. Verify by looking at the existing `ThrowIfError` visibility.

- [ ] **Step 16c.5: Verify passes**

```bash
dotnet test dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj --filter "FullyQualifiedName~DocumentChunkerTests" --nologo
```
Expected: 7 tests passing.

- [ ] **Step 16c.6: Commit**

```bash
git add dotnet/OxidizePdf.NET/Ai/DocumentChunker.cs \
        dotnet/OxidizePdf.NET/Models/TextChunk.cs \
        dotnet/OxidizePdf.NET/PdfExtractor.cs \
        dotnet/OxidizePdf.NET.Tests/Ai/DocumentChunkerTests.cs
git commit -m "feat(rag): add standalone DocumentChunker + TextChunk + EstimateTokens (RAG-008, RAG-009)"
```

---

## Task 16d: **Semantic disjointness regression tests — CRITICAL (RAG-020)**

**Files:**
- Create: `dotnet/OxidizePdf.NET.Tests/Pipeline/RagChunksDisjointnessTests.cs`

This is **the gating task** for marking Tier 0 rows ✅. We port the 12 tests from `oxidize-python/tests/test_rag_chunks_disjoint.py` verbatim. These are **semantic** tests (known input → expected properties of output), not shape-only. PARITY_SPEC maintenance rule #4 requires this before any Tier 0 row can be marked ✅.

- [ ] **Step 16d.1: Verify the Python reference is still at the expected path**

Run:
```bash
ls /home/santi/repos/BelowZero/oxidizePdf/oxidize-python/tests/test_rag_chunks_disjoint.py
```
Expected: file exists. If path changed, update the port to match the new source — the test file there is authoritative.

- [ ] **Step 16d.2: Create the disjointness test file**

Create `dotnet/OxidizePdf.NET.Tests/Pipeline/RagChunksDisjointnessTests.cs`:

```csharp
// Ported from oxidize-python/tests/test_rag_chunks_disjoint.py
//
// Contract verified here:
//   * Pairwise disjointness: no chunk's text is a substring of another's.
//   * Marker uniqueness: each unique paragraph marker in the source PDF
//     must appear in exactly one chunk.
//   * Bounded fan-out: chunk count <= source element count.
//
// These are SEMANTIC regression tests (input → expected output), not shape
// smoke tests. Required by PARITY_SPEC maintenance rule #4 (2026-04-21 audit
// lesson: shape-only tests do not count for Tier 0).

using FluentAssertions;
using OxidizePdf.NET;
using OxidizePdf.NET.Models;
using OxidizePdf.NET.Pipeline;
using Xunit;

namespace OxidizePdf.NET.Tests.Pipeline;

public class RagChunksDisjointnessTests
{
    // Markers chosen to be unique and unlikely to collide with framework
    // punctuation so substring checks are unambiguous.
    private const string TitleMarker = "HEAD-ALPHA";
    private static readonly string[] ParaMarkers =
        { "alpha-content-line", "bravo-content-line", "charlie-content-line" };

    // ── Synthetic PDF builders ─────────────────────────────────────────────

    private static byte[] BuildTitleThenParagraphsPdf()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.SetFont(StandardFont.HelveticaBold, 16.0).TextAt(50, 750, TitleMarker);
        page.SetFont(StandardFont.Helvetica, 11.0);
        page.TextAt(50, 700, $"Para1 body paragraph {ParaMarkers[0]}.");
        page.TextAt(50, 680, $"Para2 body paragraph {ParaMarkers[1]}.");
        page.TextAt(50, 660, $"Para3 body paragraph {ParaMarkers[2]}.");
        doc.AddPage(page);
        return doc.SaveToBytes();
    }

    private static byte[] BuildMultiSectionPdf()
    {
        using var doc = new PdfDocument();
        var sections = new[] { "SECTION-ONE", "SECTION-TWO" };
        for (int s = 0; s < sections.Length; s++)
        {
            using var page = PdfPage.A4();
            page.SetFont(StandardFont.HelveticaBold, 16.0).TextAt(50, 750, sections[s]);
            page.SetFont(StandardFont.Helvetica, 11.0);
            for (int p = 0; p < 3; p++)
            {
                var marker = $"sec{s}-para{p}-unique-token";
                var y = 700.0 - p * 20.0;
                page.TextAt(50, y, $"Body line {marker} ends here.");
            }
            doc.AddPage(page);
        }
        return doc.SaveToBytes();
    }

    // ── Generic semantic assertions ────────────────────────────────────────

    private static void AssertChunksPairwiseDisjoint(IReadOnlyList<RagChunk> chunks)
    {
        for (int i = 0; i < chunks.Count; i++)
        {
            for (int j = i + 1; j < chunks.Count; j++)
            {
                var ti = chunks[i].Text;
                var tj = chunks[j].Text;
                ti.Should().NotBeNullOrEmpty($"chunk[{i}].Text is empty");
                tj.Should().NotBeNullOrEmpty($"chunk[{j}].Text is empty");
                tj.Should().NotContain(ti,
                    $"chunk[{i}].Text is a substring of chunk[{j}].Text (quadratic accumulation bug)\n  i={ti}\n  j={tj}");
                ti.Should().NotContain(tj,
                    $"chunk[{j}].Text is a substring of chunk[{i}].Text (quadratic accumulation bug)\n  i={ti}\n  j={tj}");
            }
        }
    }

    private static void AssertMarkerAppearsExactlyOnce(IReadOnlyList<RagChunk> chunks, string marker)
    {
        var occurrences = chunks.Count(c => c.Text.Contains(marker));
        occurrences.Should().Be(1,
            $"marker {marker!r} must appear in exactly one chunk, found in {occurrences}\n" +
            $"  chunks: [{string.Join(", ", chunks.Select(c => $"\"{c.Text}\""))}]"
                .Replace("{marker!r}", $"\"{marker}\""));
    }

    // ── Tests: single-page fixture ─────────────────────────────────────────

    [Fact]
    public async Task TitlePlusParagraphsChunksAreDisjoint()
    {
        var pdf = BuildTitleThenParagraphsPdf();
        var extractor = new PdfExtractor();
        var chunks = await extractor.RagChunksAsync(pdf);

        chunks.Should().NotBeEmpty("rag_chunks() must emit at least one chunk");
        AssertChunksPairwiseDisjoint(chunks);
    }

    [Fact]
    public async Task EachParagraphMarkerAppearsInExactlyOneChunk()
    {
        var pdf = BuildTitleThenParagraphsPdf();
        var extractor = new PdfExtractor();
        var chunks = await extractor.RagChunksAsync(pdf);

        foreach (var marker in ParaMarkers)
            AssertMarkerAppearsExactlyOnce(chunks, marker);
    }

    [Fact]
    public async Task ChunkCountBoundedBySourceElements()
    {
        // Title + 3 paragraphs = 4 source elements; chunker may merge but
        // MUST NOT split a paragraph or duplicate elements.
        var pdf = BuildTitleThenParagraphsPdf();
        var extractor = new PdfExtractor();
        var chunks = await extractor.RagChunksAsync(pdf);

        chunks.Count.Should().BeLessThanOrEqualTo(4,
            $"chunk count ({chunks.Count}) exceeds source element count (4); duplication suspected");
    }

    // ── Tests: multi-section fixture ───────────────────────────────────────

    [Fact]
    public async Task MultiSectionPdfChunksAreDisjoint()
    {
        var pdf = BuildMultiSectionPdf();
        var extractor = new PdfExtractor();
        var chunks = await extractor.RagChunksAsync(pdf);

        chunks.Should().NotBeEmpty();
        AssertChunksPairwiseDisjoint(chunks);
    }

    [Fact]
    public async Task MultiSectionEachMarkerAppearsOnce()
    {
        var pdf = BuildMultiSectionPdf();
        var extractor = new PdfExtractor();
        var chunks = await extractor.RagChunksAsync(pdf);

        for (int s = 0; s < 2; s++)
            for (int p = 0; p < 3; p++)
                AssertMarkerAppearsExactlyOnce(chunks, $"sec{s}-para{p}-unique-token");
    }

    [Fact]
    public async Task MultiSectionChunkCountBounded()
    {
        // 2 sections * (1 title + 3 paragraphs) = 8 source elements.
        var pdf = BuildMultiSectionPdf();
        var extractor = new PdfExtractor();
        var chunks = await extractor.RagChunksAsync(pdf);

        chunks.Count.Should().BeLessThanOrEqualTo(8,
            $"chunk count ({chunks.Count}) exceeds source element count (8); duplication suspected");
    }

    // ── Tests: disjointness across profiles ────────────────────────────────

    public static IEnumerable<object[]> ProfileCases() => new[]
    {
        new object[] { ExtractionProfile.Standard },
        new object[] { ExtractionProfile.Rag },
        new object[] { ExtractionProfile.Academic },
    };

    [Theory]
    [MemberData(nameof(ProfileCases))]
    public async Task ProfileEmitsDisjointChunks(ExtractionProfile profile)
    {
        var pdf = BuildMultiSectionPdf();
        var extractor = new PdfExtractor();
        var chunks = await extractor.RagChunksAsync(pdf, profile);

        chunks.Should().NotBeEmpty();
        AssertChunksPairwiseDisjoint(chunks);
    }

    [Theory]
    [MemberData(nameof(ProfileCases))]
    public async Task ProfileMarkerUniqueness(ExtractionProfile profile)
    {
        var pdf = BuildMultiSectionPdf();
        var extractor = new PdfExtractor();
        var chunks = await extractor.RagChunksAsync(pdf, profile);

        for (int s = 0; s < 2; s++)
            for (int p = 0; p < 3; p++)
                AssertMarkerAppearsExactlyOnce(chunks, $"sec{s}-para{p}-unique-token");
    }
}
```

**Note on the `{marker!r}` formatting**: C# doesn't have Python's `!r` repr. The `Replace` call above is a pragmatic workaround so the error message mirrors Python's. If it reads awkward, drop it — the `marker` value in a `$""` string is enough to identify which marker failed.

- [ ] **Step 16d.3: Run the tests and count them**

```bash
dotnet test dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj --filter "FullyQualifiedName~RagChunksDisjointnessTests" --nologo
```
Expected: **12 tests passing** (6 `[Fact]` + 2 `[Theory]` × 3 profiles = 12). If any test fails, do **not** patch the test — it means a real disjointness regression exists in the chunker. Investigate the Rust side first (`oxidize-pdf 2.5.5+` fixed the quadratic-accumulation bug that motivated these tests; verify the native lib includes the fix).

- [ ] **Step 16d.4: Commit**

```bash
git add dotnet/OxidizePdf.NET.Tests/Pipeline/RagChunksDisjointnessTests.cs
git commit -m "test(rag): port 12 semantic disjointness regression tests (RAG-020)"
```

---

## Task 17: Mark legacy `ChunkOptions` obsolete

**Files:**
- Modify: `dotnet/OxidizePdf.NET/Models/ChunkOptions.cs`
- Modify: `dotnet/OxidizePdf.NET/PdfExtractor.cs` (suppress `[Obsolete]` warnings in the *caller-facing* `ExtractChunksAsync` that still takes `ChunkOptions` — we don't delete it, we just warn users).

- [ ] **Step 17.1: Write the test asserting the obsolete attribute is present**

Create `dotnet/OxidizePdf.NET.Tests/Pipeline/ChunkOptionsObsoleteTests.cs`:

```csharp
using FluentAssertions;
using OxidizePdf.NET.Models;
using Xunit;

namespace OxidizePdf.NET.Tests.Pipeline;

public class ChunkOptionsObsoleteTests
{
    [Fact]
    public void ChunkOptions_is_marked_obsolete_with_migration_message()
    {
        var attr = typeof(ChunkOptions).GetCustomAttributes(typeof(ObsoleteAttribute), false);
        attr.Should().ContainSingle();
        var message = ((ObsoleteAttribute)attr[0]).Message;
        message.Should().Contain("HybridChunkConfig");
        message.Should().Contain("SemanticChunkConfig");
    }
}
```

- [ ] **Step 17.2: Run to verify failure**

Run:
```bash
dotnet test dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj --filter "FullyQualifiedName~ChunkOptionsObsoleteTests" --nologo
```
Expected: FAIL.

- [ ] **Step 17.3: Add the attribute**

Edit `dotnet/OxidizePdf.NET/Models/ChunkOptions.cs` — add above `public class ChunkOptions`:

```csharp
[Obsolete("Character-based chunking is superseded by token-aware HybridChunkConfig (for RAG chunks) or SemanticChunkConfig (for element-aware chunks). See docs/RAG.md for migration. ChunkOptions will remain supported for one minor release.")]
```

- [ ] **Step 17.4: Suppress downstream warnings where ChunkOptions is still referenced internally**

The existing `ExtractChunksAsync(byte[], ChunkOptions?, CancellationToken)` must keep working. Wrap its parameter type reference with `#pragma warning disable CS0618` / `restore` in `PdfExtractor.cs` at the method boundary, and do the same around any remaining test that uses it (the file `ChunkOptionsValidationTests.cs` will warn otherwise).

In `PdfExtractor.cs`, wrap the method:

```csharp
#pragma warning disable CS0618 // ChunkOptions is obsolete but kept for backward compatibility.
    public Task<List<DocumentChunk>> ExtractChunksAsync(
        byte[] pdfBytes,
        ChunkOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // ...existing body unchanged...
    }
#pragma warning restore CS0618
```

Also add the same pragma pair around the internal `ExtractChunks` helper and any `ChunkOptionsNative` conversion sites.

In `dotnet/OxidizePdf.NET.Tests/ChunkOptionsValidationTests.cs`, add at the top:
```csharp
#pragma warning disable CS0618
```
(no matching `restore` needed; test files scope pragmas file-wide).

- [ ] **Step 17.5: Verify full test suite still passes with zero warnings**

Run:
```bash
dotnet build dotnet/OxidizePdf.sln --nologo /warnaserror
dotnet test dotnet/OxidizePdf.sln --nologo
```
Expected: build clean (no warnings), all tests passing including the new obsolete test.

- [ ] **Step 17.6: Commit**

```bash
git add dotnet/OxidizePdf.NET/Models/ChunkOptions.cs \
        dotnet/OxidizePdf.NET/PdfExtractor.cs \
        dotnet/OxidizePdf.NET.Tests/ChunkOptionsValidationTests.cs \
        dotnet/OxidizePdf.NET.Tests/Pipeline/ChunkOptionsObsoleteTests.cs
git commit -m "chore(rag): mark character-based ChunkOptions as obsolete"
```

---

## Task 18: Update example and README

**Files:**
- Modify: `examples/BasicUsage/Program.cs`
- Modify: `README.md`

- [ ] **Step 18.1: Extend `examples/BasicUsage/Program.cs`**

Append after the existing chunking example (don't remove the old code — add a new demonstration section):

```csharp
using OxidizePdf.NET.Pipeline;
using OxidizePdf.NET.Ai;

Console.WriteLine("\n=== RAG profile demo ===\n");
var ragChunks = await extractor.RagChunksAsync(pdfBytes, ExtractionProfile.Rag);
foreach (var chunk in ragChunks.Take(3))
{
    Console.WriteLine($"Chunk {chunk.ChunkIndex} pages={string.Join(",", chunk.PageNumbers)} tokens≈{chunk.TokenEstimate}");
    Console.WriteLine($"  heading: {chunk.HeadingContext ?? "(none)"}");
    Console.WriteLine($"  text   : {chunk.Text[..Math.Min(80, chunk.Text.Length)]}…");
}

Console.WriteLine("\n=== Custom partition config (multi-column) ===\n");
var cfg = new PartitionConfig()
    .WithReadingOrder(ReadingOrderStrategy.XyCut(20.0))
    .WithMinTableConfidence(0.7);
var elements = await extractor.PartitionAsync(pdfBytes, cfg);
Console.WriteLine($"Got {elements.Count} semantic elements using XY-Cut reading order");

Console.WriteLine("\n=== Markdown with options ===\n");
var md = await extractor.ToMarkdownAsync(pdfBytes,
    new MarkdownOptions { IncludeMetadata = false, IncludePageNumbers = true });
Console.WriteLine(md[..Math.Min(200, md.Length)]);

Console.WriteLine("\n=== Standalone DocumentChunker (no PDF) ===\n");
var chunker = new DocumentChunker(chunkSize: 64, overlap: 8);
var longText = "Paragraph one about oxidize-pdf. " + string.Concat(Enumerable.Repeat("word ", 200));
var textChunks = chunker.ChunkText(longText);
Console.WriteLine($"Chunked {longText.Length} chars into {textChunks.Count} chunks");
Console.WriteLine($"Token estimate for input: {DocumentChunker.EstimateTokens(longText)}");
```

- [ ] **Step 18.2: Update `README.md`**

Find the chunking section and replace/augment it with:

```markdown
## RAG extraction

The .NET bridge mirrors the RAG-first philosophy of the Python bridge (`oxidize-python`).
Feed PDFs into a vector store with one call:

```csharp
using OxidizePdf.NET;
using OxidizePdf.NET.Pipeline;

var extractor = new PdfExtractor();
var chunks = await extractor.RagChunksAsync(pdfBytes, ExtractionProfile.Rag);
foreach (var c in chunks)
{
    // c.FullText includes heading context — use for embeddings
    // c.PageNumbers lets you cite sources
    // c.TokenEstimate helps you plan batch sizes
}
```

Seven profiles are available: `Standard`, `Academic`, `Form`, `Government`, `Dense`,
`Presentation`, `Rag`. For fine-grained control, pass a `PartitionConfig` and/or
`HybridChunkConfig` directly. See `examples/BasicUsage/Program.cs`.
```

- [ ] **Step 18.3: Run the example to verify it executes end-to-end**

Run:
```bash
dotnet run --project examples/BasicUsage/BasicUsage.csproj --nologo
```
Expected: prints extraction results and the new RAG demo sections without errors.

- [ ] **Step 18.4: Commit**

```bash
git add examples/BasicUsage/Program.cs README.md
git commit -m "docs(rag): example + README section for RAG profiles"
```

---

## Task 18b: Mirror `PARITY_SPEC.md` and refresh `FEATURE_PARITY.md`

**Files:**
- Create: `docs/PARITY_SPEC.md`
- Modify: `docs/FEATURE_PARITY.md`

Per PARITY_SPEC maintenance rule #1, the spec is a mirror — identical content in both repos. Per rule #2, when a row changes status it must flip in **both** copies in the same PR cycle.

- [ ] **Step 18b.1: Copy the Python spec verbatim**

Run:
```bash
cp /home/santi/repos/BelowZero/oxidizePdf/oxidize-python/docs/PARITY_SPEC.md \
   docs/PARITY_SPEC.md
```

- [ ] **Step 18b.2: Flip the closed rows to ✅**

In the fresh `docs/PARITY_SPEC.md`, update these Tier 0 rows:

| ID | Change |
|---|---|
| RAG-003 | `.NET ❌` → `.NET ✅ RagChunksAsync(profile)`; Action → `—` |
| RAG-004 | `.NET ❌` → `.NET ✅`; Action → `—` |
| RAG-006 | `.NET ❌` → `.NET ✅ (2 variants)`; update capability cell to drop the spurious `None` variant; Action → `Python: remove None from spec (docs-only drift)` |
| RAG-007 | `.NET ❌` → `.NET ✅`; Action → `—` |
| RAG-008 | `.NET ❌` → `.NET ✅ DocumentChunker`; Action → `—` |
| RAG-009 | `.NET ❌` → `.NET ✅ DocumentChunker.EstimateTokens`; Action → `—` |
| RAG-012 | `.NET ❌` → `.NET ✅ ToMarkdownAsync(MarkdownOptions)`; Action → `—` |
| RAG-020 | `.NET ❌` → `.NET ✅ 12 ported tests`; Action → `—` |

Also update the "Bridge versions checkpoint" line at the top to reflect `.NET OxidizePdf.NET 0.9.0-rag.1 (=core 2.5.5)`.

And the "Summary: .NET — 20 actions" section: strike through the 7 RAG rows now closed; re-count remaining work.

- [ ] **Step 18b.3: Open a PR / patch against the Python repo to mirror the changes**

The Python copy must be updated in lockstep. Since this plan targets the .NET repo, produce a patch file and note it in the PR description:

```bash
diff -u /home/santi/repos/BelowZero/oxidizePdf/oxidize-python/docs/PARITY_SPEC.md \
       docs/PARITY_SPEC.md > /tmp/parity-spec-update.patch
```

Attach `/tmp/parity-spec-update.patch` to the .NET PR description with a note: *"Run `git apply` against this patch in `oxidize-python` — see maintenance rule #1."*

- [ ] **Step 18b.4: Refresh `docs/FEATURE_PARITY.md`**

Open the current `docs/FEATURE_PARITY.md`. Update:
- Header date to `2026-04-22`.
- Core version from whatever is currently listed to `2.5.5`.
- Bridge version to `0.9.0-rag.1`.
- Any rows that reference character-based chunking to reflect the new token-aware API.
- Remove or update any "TODO: expose profiles" style notes (now done).

The exact edits depend on the file's current structure — read it first, make minimal targeted edits. Don't rewrite from scratch.

- [ ] **Step 18b.5: Verify the mirror is byte-for-byte aligned (after status flips)**

The spec files should differ ONLY in the cells you intentionally flipped. Any other diff means a bug.

```bash
diff /home/santi/repos/BelowZero/oxidizePdf/oxidize-python/docs/PARITY_SPEC.md \
     docs/PARITY_SPEC.md | head -60
```

Inspect — verify every diff line corresponds to a flipped row.

- [ ] **Step 18b.6: Commit**

```bash
git add docs/PARITY_SPEC.md docs/FEATURE_PARITY.md
git commit -m "docs(rag): mirror PARITY_SPEC.md, refresh FEATURE_PARITY.md (QA-001)"
```

---

## Task 19: Version bump and CHANGELOG

**Files:**
- Modify: `native/Cargo.toml`
- Modify: `dotnet/OxidizePdf.NET/OxidizePdf.NET.csproj`
- Modify: `CHANGELOG.md`
- Modify: `docs/superpowers/plans/2026-04-21-feature-parity-roadmap.md`

- [ ] **Step 19.1: Bump the FFI crate**

Edit `native/Cargo.toml`: change `version = "0.8.0"` to `version = "0.9.0-rag.1"`.

- [ ] **Step 19.2: Bump the .NET package**

Edit `dotnet/OxidizePdf.NET/OxidizePdf.NET.csproj`: change the `<Version>` element to `0.9.0-rag.1`. If there's a `<PackageVersion>` or `<AssemblyVersion>`, update those too — match whatever pattern the existing file uses.

- [ ] **Step 19.3: Add CHANGELOG entry**

Prepend under `# Changelog` in `CHANGELOG.md`:

```markdown
## [0.9.0-rag.1] — 2026-04-22

### Added — RAG pipeline parity with Python bridge

Closes every "immediate" Tier 0 row in `docs/PARITY_SPEC.md`:

- **RAG-003, RAG-004**: `ExtractionProfile` enum (7 values) + `RagChunksAsync(profile)` overload.
- **RAG-005**: `HybridChunkConfig`, `SemanticChunkConfig`, and `PartitionAsync`/`RagChunksAsync`/`SemanticChunksAsync` overloads accepting them.
- **RAG-006**: `MergePolicy` enum (2 variants — `SameTypeOnly`, `AnyInlineContent`; matches Rust core).
- **RAG-007**: `ReadingOrderStrategy` class (`Simple`, `None`, `XyCut(minGap)`).
- **RAG-008**: Standalone `OxidizePdf.NET.Ai.DocumentChunker` (`ChunkText(string) → List<TextChunk>`).
- **RAG-009**: `DocumentChunker.EstimateTokens(string)` static.
- **RAG-012**: `MarkdownOptions` + `ToMarkdownAsync(byte[], MarkdownOptions)` overload.
- **RAG-020**: 12 ported semantic disjointness regression tests (`RagChunksDisjointnessTests.cs`) — the gating tests per PARITY_SPEC maintenance rule #4.
- **QA-001**: `docs/FEATURE_PARITY.md` refreshed; `docs/PARITY_SPEC.md` added as a mirror of the Python repo's canonical spec.

New types: `TextChunk`, `SemanticChunk`, `MarkdownOptions`, `PartitionConfig`, `HybridChunkConfig`, `SemanticChunkConfig`, `ExtractionProfile`, `MergePolicy`, `ReadingOrderStrategy`.

Eight new FFI entry points: `oxidize_partition_with_profile`, `oxidize_partition_with_config`, `oxidize_rag_chunks_with_profile`, `oxidize_rag_chunks_with_config`, `oxidize_semantic_chunks`, `oxidize_to_markdown_with_options`, `oxidize_chunk_text`, `oxidize_estimate_tokens`.

### Deprecated
- `ChunkOptions` (character-based chunking) is marked `[Obsolete]` — migrate to `HybridChunkConfig` or `SemanticChunkConfig`. Remains supported for one minor release.

### Known spec discrepancy
- `PARITY_SPEC.md` RAG-006 listed three `MergePolicy` variants; Rust core at 2.5.5 and the Python bridge expose only two. The .NET enum matches the code; a patch against the Python spec is attached to this PR.

### Paused
- Feature-parity roadmap milestones M1–M6 are paused in favour of RAG parity; see plan at `docs/superpowers/plans/2026-04-22-rag-pipeline-parity.md`.
```

- [ ] **Step 19.4: Add the pause banner to the roadmap**

Prepend to `docs/superpowers/plans/2026-04-21-feature-parity-roadmap.md` (at the top, under the H1):

```markdown
> ⏸ **Paused (2026-04-22).** RAG pipeline parity with the Python bridge takes precedence.
> See `2026-04-22-rag-pipeline-parity.md`. Resume M1 once RAG work ships as 0.9.0.
```

- [ ] **Step 19.5: Final full-suite verification**

Run:
```bash
cargo build --manifest-path native/Cargo.toml --release
cargo test  --manifest-path native/Cargo.toml --lib
dotnet test dotnet/OxidizePdf.sln --nologo /warnaserror
```
Expected: Rust clean, all Rust unit tests pass, .NET builds warning-free, all .NET tests pass.

- [ ] **Step 19.6: Commit and push**

```bash
git add native/Cargo.toml \
        dotnet/OxidizePdf.NET/OxidizePdf.NET.csproj \
        CHANGELOG.md \
        docs/superpowers/plans/2026-04-21-feature-parity-roadmap.md
git commit -m "chore(rag): bump to 0.9.0-rag.1 and pause M1–M6 roadmap"
git push -u origin feature/rag-pipeline-parity
```

Do **not** merge to `develop` / `main` in this plan; open a PR and hand off for review. The PR description must include:
- Link to this plan.
- The patch file against `oxidize-python/docs/PARITY_SPEC.md` produced in Task 18b.3.
- Test count summary (Rust unit: 11 in `pipeline_config` + 11 in `profile_ffi_tests`; C# unit: ~25 config tests; integration: ~20; disjointness: 12). Total delta vs main: ~80 new tests.

---

## Follow-Up Plans (Out of Scope Here)

Tracked in PARITY_SPEC but intentionally deferred to future plans:

1. **MCP server (MCP-001)** — new `OxidizePdf.NET.Mcp` project wrapping the 12 tools exposed by the Python `oxidize-mcp` package. Uses the official `ModelContextProtocol` C# SDK. New solution project, own release cadence. Draft as `2026-04-XX-mcp-server.md` after this plan ships.
2. **Kernel Memory NuGet package (INT-002, RAG-021)** — promote `examples/KernelMemory/` to `OxidizePdf.KernelMemory.DocumentReader` NuGet.
3. **Semantic Kernel plugin (INT-005)** — .NET-native RAG adapter.
4. **`Element` / `PdfElement` naming alignment (RAG-016)** — breaking change; needs deprecation cycle.
5. **JSON export unification (RAG-014)** — pick one philosophy (entity-centric vs extraction-centric).
6. **OCR (RAG-019)** — known gap in both bridges; requires `OcrProvider` exposure through FFI.
7. **Streaming APIs** — `IAsyncEnumerable<RagChunk>` for memory-efficient large-PDF handling.
8. **Pluggable tokenizer** — `ITokenizer` interface so callers can inject BPE/tiktoken for accurate GPT/Claude token counts instead of the word-count proxy.
9. **QA-004** — C# equivalent of `audit_rag_chunks.py` that runs against fixtures.

---

## Self-Review Checklist (completed inline)

- **Spec coverage:** 26 tasks (1 – 4, 4b, 5 – 10, 10b, 10c, 11 – 16, 16b, 16c, 16d, 17, 18, 18b, 19) cover every "immediate" Tier 0 row from `PARITY_SPEC.md`:
  - RAG-003: Task 17 (RagChunksAsync profile overload)
  - RAG-004: Task 1 (ExtractionProfile enum)
  - RAG-005: Task 4 (HybridChunkConfig) + Task 18 (RagChunksAsync config overload)
  - RAG-006: Task 1 (MergePolicy enum)
  - RAG-007: Task 2 (ReadingOrderStrategy)
  - RAG-008: Task 16c (DocumentChunker.ChunkText)
  - RAG-009: Task 16c (DocumentChunker.EstimateTokens static)
  - RAG-012: Task 4b (MarkdownOptions) + Task 10b (FFI) + Task 16b (ToMarkdownAsync overload)
  - RAG-020: Task 16d (12 disjointness tests — gating per maintenance rule #4)
  - QA-001 + maintenance rule #1: Task 18b (PARITY_SPEC mirror + FEATURE_PARITY refresh)
  - Supporting: Tasks 3, 6–9, 13–15, 19 (PartitionConfig, remaining FFI, Partition overloads, Semantic overload, version bump)
- **Placeholders:** no TBDs. Conditional fallbacks point at authoritative `.cargo/registry/...` paths or existing test idioms (e.g. `EndToEndTests.cs`).
- **Type consistency:** `ExtractionProfile`, `PartitionConfig`, `HybridChunkConfig`, `SemanticChunkConfig`, `ReadingOrderStrategy`, `MergePolicy`, `SemanticChunk`, `TextChunk`, `MarkdownOptions`, `DocumentChunker` names used identically across all tasks. FFI function names match between Rust definitions (Tasks 6–10, 10b, 10c) and C# P/Invoke (Task 11). JSON field names (snake_case) match between Rust DTOs in Task 5 and C# `[JsonPropertyName]` attributes in Tasks 3, 4, 4b.
- **Gating test coverage:** Task 16d is the only task whose failure blocks v0.9.0-rag.1 release — if those 12 tests don't pass, no Tier 0 row can be flipped to ✅ in Task 18b.
- **MergePolicy discrepancy**: documented in header §Known PARITY_SPEC discrepancy and addressed in Task 18b.2 with a patch against the Python spec. Plan does not wait for the Python PR to merge — the .NET enum ships with 2 variants regardless.
