# Kernel Memory Connector Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `OxidizePdf.NET.KernelMemory`, a real `IContentDecoder` connector that maps oxidize-pdf's structure-aware RAG chunks 1:1 into Kernel Memory partitions, plus a runnable sample replacing the fake `examples/KernelMemory` demo.

**Architecture:** A new `net8.0;net9.0;net10.0` class library depends only on `Microsoft.KernelMemory.Abstractions` and the existing `OxidizePdf.NET` project. Its `OxidizePdfDecoder` runs `PdfExtractor.RagChunksAsync` and maps each `RagChunk` → one KM `Chunk` (content = `FullText`, metadata = first page + `completeSentences=true`), surfaced via a `WithOxidizePdf` builder extension. Because KM's default `split_text_in_partitions` re-chunks and ignores those sections, an `OxidizeChunkPartitioningHandler : IPipelineStepHandler` replaces that step — reading the structured `ExtractedContent` artifact and emitting one partition per oxidize chunk. A sample and a separate xUnit test project prove the behavior; a handler unit test and an end-to-end test prove KM keeps one partition per oxidize chunk.

**Tech Stack:** C#, .NET 8/9/10, xUnit 2.9.2, Microsoft.KernelMemory (Abstractions for the library, Core for tests/sample).

## Global Constraints

- Library `OxidizePdf.NET.KernelMemory`: `<TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>`, `Nullable` enabled, `TreatWarningsAsErrors=true` (warnings are errors — project rule).
- Library depends on **`Microsoft.KernelMemory.Abstractions` only** (NOT `.Core`) + `ProjectReference` to `..\OxidizePdf.NET\OxidizePdf.NET.csproj`.
- Test project `OxidizePdf.NET.KernelMemory.Tests`: `<TargetFramework>net10.0</TargetFramework>`, references `Microsoft.KernelMemory.Core` (for `KernelMemoryBuilder`, `SimpleVectorDb`) + the connector project.
- Any project implementing `ITextTokenizer`/`ITextEmbeddingGenerator` (the test project) MUST set `<NoWarn>$(NoWarn);KMEXP00</NoWarn>` — those KM interfaces carry `[Experimental("KMEXP00")]` and warnings-as-errors would otherwise break the build.
- Package id `OxidizePdf.NET.KernelMemory`, **version `0.1.0-preview`**, MIT, decoupled from the main package version.
- KM package versions are pinned by running `dotnet add package` in Task 1/Task 5 (records the resolved latest stable); do not hand-write a version guess.
- No smoke tests. Every test asserts real content/behavior (chunk counts, text equality, page numbers), never just "did not throw" or status presence.
- New files added to git; new projects added to `dotnet/OxidizePdf.sln`.
- Branch: `feature/kernel-memory-connector` (already created from `develop`).

---

### Task 1: Scaffold connector + test projects, implement `SupportsMimeType`

**Files:**
- Create: `dotnet/OxidizePdf.NET.KernelMemory/OxidizePdf.NET.KernelMemory.csproj`
- Create: `dotnet/OxidizePdf.NET.KernelMemory/OxidizePdfDecoderOptions.cs`
- Create: `dotnet/OxidizePdf.NET.KernelMemory/OxidizePdfDecoder.cs`
- Create: `dotnet/OxidizePdf.NET.KernelMemory.Tests/OxidizePdf.NET.KernelMemory.Tests.csproj`
- Create: `dotnet/OxidizePdf.NET.KernelMemory.Tests/SupportsMimeTypeTests.cs`
- Modify: `dotnet/OxidizePdf.sln` (add both projects)

**Interfaces:**
- Produces: `OxidizePdf.NET.KernelMemory.OxidizePdfDecoder : Microsoft.KernelMemory.DataFormats.IContentDecoder` with `bool SupportsMimeType(string)`, ctor `OxidizePdfDecoder(OxidizePdfDecoderOptions? options = null)`.
- Produces: `OxidizePdf.NET.KernelMemory.OxidizePdfDecoderOptions { ExtractionProfile Profile = Rag; PartitionConfig? Partition; HybridChunkConfig? Hybrid; }`.

- [ ] **Step 1: Create the library csproj**

`dotnet/OxidizePdf.NET.KernelMemory/OxidizePdf.NET.KernelMemory.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\OxidizePdf.NET\OxidizePdf.NET.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Add the KM Abstractions package (pins the version)**

Run:
```bash
cd dotnet/OxidizePdf.NET.KernelMemory
dotnet add package Microsoft.KernelMemory.Abstractions
cd ../..
```
Expected: a `<PackageReference Include="Microsoft.KernelMemory.Abstractions" Version="X.Y.Z" />` is written. Record the resolved version in the commit message.

- [ ] **Step 3: Write `OxidizePdfDecoderOptions.cs`**

```csharp
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
```

- [ ] **Step 4: Write the decoder skeleton with `SupportsMimeType`**

`dotnet/OxidizePdf.NET.KernelMemory/OxidizePdfDecoder.cs`:
```csharp
using Microsoft.KernelMemory.DataFormats;
using OxidizePdf.NET;

namespace OxidizePdf.NET.KernelMemory;

/// <summary>
/// Kernel Memory <see cref="IContentDecoder"/> backed by oxidize-pdf. Produces
/// one KM <see cref="Chunk"/> per structure-aware oxidize-pdf RAG chunk.
/// </summary>
public sealed class OxidizePdfDecoder : IContentDecoder
{
    private const string PdfMimeType = "application/pdf";

    private readonly OxidizePdfDecoderOptions _options;
    private readonly PdfExtractor _extractor;

    /// <summary>Creates a decoder. <paramref name="options"/> null uses defaults (RAG profile).</summary>
    public OxidizePdfDecoder(OxidizePdfDecoderOptions? options = null)
    {
        _options = options ?? new OxidizePdfDecoderOptions();
        _extractor = new PdfExtractor();
    }

    /// <inheritdoc />
    public bool SupportsMimeType(string mimeType) =>
        mimeType is not null &&
        mimeType.StartsWith(PdfMimeType, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Task<FileContent> DecodeAsync(string filename, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    /// <inheritdoc />
    public Task<FileContent> DecodeAsync(BinaryData data, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    /// <inheritdoc />
    public Task<FileContent> DecodeAsync(Stream data, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
```

- [ ] **Step 5: Create the test csproj**

`dotnet/OxidizePdf.NET.KernelMemory.Tests/OxidizePdf.NET.KernelMemory.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <!-- KM's ITextTokenizer is [Experimental("KMEXP00")]; we implement it in the e2e test. -->
    <NoWarn>$(NoWarn);KMEXP00</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\OxidizePdf.NET.KernelMemory\OxidizePdf.NET.KernelMemory.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 6: Add Microsoft.KernelMemory.Core to the test project (pins version)**

Run:
```bash
cd dotnet/OxidizePdf.NET.KernelMemory.Tests
dotnet add package Microsoft.KernelMemory.Core
cd ../..
```
Expected: `<PackageReference Include="Microsoft.KernelMemory.Core" Version="X.Y.Z" />` written.

- [ ] **Step 7: Write the failing `SupportsMimeType` test**

`dotnet/OxidizePdf.NET.KernelMemory.Tests/SupportsMimeTypeTests.cs`:
```csharp
using OxidizePdf.NET.KernelMemory;

namespace OxidizePdf.NET.KernelMemory.Tests;

public class SupportsMimeTypeTests
{
    [Theory]
    [InlineData("application/pdf", true)]
    [InlineData("application/pdf; charset=binary", true)]
    [InlineData("APPLICATION/PDF", true)]
    [InlineData("text/plain", false)]
    [InlineData("application/json", false)]
    [InlineData("", false)]
    public void SupportsMimeType_matches_only_pdf(string mime, bool expected)
    {
        var decoder = new OxidizePdfDecoder();
        Assert.Equal(expected, decoder.SupportsMimeType(mime));
    }
}
```

- [ ] **Step 8: Add both projects to the solution**

Run:
```bash
dotnet sln dotnet/OxidizePdf.sln add dotnet/OxidizePdf.NET.KernelMemory/OxidizePdf.NET.KernelMemory.csproj
dotnet sln dotnet/OxidizePdf.sln add dotnet/OxidizePdf.NET.KernelMemory.Tests/OxidizePdf.NET.KernelMemory.Tests.csproj
```
Expected: "Project ... added to the solution." twice.

- [ ] **Step 9: Run the test to verify it passes**

Run: `dotnet test dotnet/OxidizePdf.NET.KernelMemory.Tests --filter SupportsMimeTypeTests`
Expected: PASS (6 cases). The skeleton compiles because `DecodeAsync` overloads throw `NotImplementedException` but are not exercised by this test.

- [ ] **Step 10: Commit**

```bash
git add dotnet/OxidizePdf.NET.KernelMemory dotnet/OxidizePdf.NET.KernelMemory.Tests dotnet/OxidizePdf.sln
git commit -m "feat(km): scaffold OxidizePdf.NET.KernelMemory connector + SupportsMimeType"
```

---

### Task 2: Implement `DecodeAsync` — map RagChunks 1:1 to KM Chunks

**Files:**
- Modify: `dotnet/OxidizePdf.NET.KernelMemory/OxidizePdfDecoder.cs`
- Create: `dotnet/OxidizePdf.NET.KernelMemory.Tests/fixtures/sample.pdf` (copy of the existing fixture)
- Modify: `dotnet/OxidizePdf.NET.KernelMemory.Tests/OxidizePdf.NET.KernelMemory.Tests.csproj` (copy fixtures to output)
- Create: `dotnet/OxidizePdf.NET.KernelMemory.Tests/DecodeAsyncTests.cs`

**Interfaces:**
- Consumes: `OxidizePdfDecoder` ctor + `SupportsMimeType` from Task 1; `PdfExtractor.RagChunksAsync(byte[], ExtractionProfile, CancellationToken)` and `RagChunksAsync(byte[], PartitionConfig?, HybridChunkConfig?, CancellationToken)`; `RagChunk { int ChunkIndex; string Text; string FullText; List<int> PageNumbers; }`; `Microsoft.KernelMemory.DataFormats.Chunk` (`Chunk(string?, int, Dictionary<string,string>)`, static `Meta(bool?, int?)`, props `Number`, `Content`, `PageNumber`, `SentencesAreComplete`); `FileContent("application/pdf")` with `List<Chunk> Sections`.
- Produces: working `DecodeAsync(byte[]/Stream/BinaryData/filename)` returning `FileContent` with one `Chunk` per `RagChunk`.

> **NOTE (API reality):** there is no single `RagChunksAsync(byte[], ExtractionProfile, PartitionConfig?, HybridChunkConfig?)` overload. The decoder dispatches: if `Partition` or `Hybrid` is set → `RagChunksAsync(bytes, Partition, Hybrid, ct)`; else → `RagChunksAsync(bytes, Profile, ct)`.

- [ ] **Step 1: Copy the test fixture**

Run:
```bash
mkdir -p dotnet/OxidizePdf.NET.KernelMemory.Tests/fixtures
cp dotnet/OxidizePdf.NET.Tests/fixtures/sample.pdf dotnet/OxidizePdf.NET.KernelMemory.Tests/fixtures/sample.pdf
```
Expected: `sample.pdf` exists in the new fixtures dir.

- [ ] **Step 2: Make the test project copy fixtures to output**

Add to `dotnet/OxidizePdf.NET.KernelMemory.Tests/OxidizePdf.NET.KernelMemory.Tests.csproj` inside a new `<ItemGroup>`:
```xml
  <ItemGroup>
    <Content Include="fixtures\**\*">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
  </ItemGroup>
```

- [ ] **Step 3: Write the failing decode test**

`dotnet/OxidizePdf.NET.KernelMemory.Tests/DecodeAsyncTests.cs`:
```csharp
using OxidizePdf.NET;
using OxidizePdf.NET.KernelMemory;
using OxidizePdf.NET.Pipeline;

namespace OxidizePdf.NET.KernelMemory.Tests;

public class DecodeAsyncTests
{
    private static string FixturePath() =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "sample.pdf");

    [Fact]
    public async Task DecodeAsync_maps_each_rag_chunk_to_one_section()
    {
        var bytes = await File.ReadAllBytesAsync(FixturePath());
        var expected = await new PdfExtractor().RagChunksAsync(bytes, ExtractionProfile.Rag);

        var decoder = new OxidizePdfDecoder();
        var content = await decoder.DecodeAsync(new BinaryData(bytes));

        Assert.Equal("application/pdf", content.MimeType);
        Assert.Equal(expected.Count, content.Sections.Count);
        Assert.NotEmpty(content.Sections);

        for (int i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].FullText, content.Sections[i].Content);
            Assert.Equal(expected[i].ChunkIndex, content.Sections[i].Number);
            Assert.True(content.Sections[i].SentencesAreComplete);
            int expectedPage = expected[i].PageNumbers.Count > 0 ? expected[i].PageNumbers[0] : -1;
            Assert.Equal(expectedPage, content.Sections[i].PageNumber);
        }
    }

    [Fact]
    public async Task DecodeAsync_overloads_produce_identical_output()
    {
        var bytes = await File.ReadAllBytesAsync(FixturePath());
        var decoder = new OxidizePdfDecoder();

        var fromFile = await decoder.DecodeAsync(FixturePath());
        var fromBinary = await decoder.DecodeAsync(new BinaryData(bytes));
        using var stream = new MemoryStream(bytes);
        var fromStream = await decoder.DecodeAsync(stream);

        var fileTexts = fromFile.Sections.Select(s => s.Content).ToList();
        Assert.Equal(fileTexts, fromBinary.Sections.Select(s => s.Content).ToList());
        Assert.Equal(fileTexts, fromStream.Sections.Select(s => s.Content).ToList());
    }
}
```

- [ ] **Step 4: Run to verify it fails**

Run: `dotnet test dotnet/OxidizePdf.NET.KernelMemory.Tests --filter DecodeAsyncTests`
Expected: FAIL — `DecodeAsync` throws `NotImplementedException`.

- [ ] **Step 5: Implement the decode methods**

Replace the three `DecodeAsync` stubs in `OxidizePdfDecoder.cs` and add a private mapper. Add `using OxidizePdf.NET.Models;` for `RagChunk`:
```csharp
    /// <inheritdoc />
    public async Task<FileContent> DecodeAsync(string filename, CancellationToken cancellationToken = default)
    {
        var bytes = await File.ReadAllBytesAsync(filename, cancellationToken).ConfigureAwait(false);
        return await DecodeBytesAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<FileContent> DecodeAsync(BinaryData data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        return DecodeBytesAsync(data.ToArray(), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<FileContent> DecodeAsync(Stream data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        using var ms = new MemoryStream();
        await data.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        return await DecodeBytesAsync(ms.ToArray(), cancellationToken).ConfigureAwait(false);
    }

    private async Task<FileContent> DecodeBytesAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        List<RagChunk> chunks = (_options.Partition is not null || _options.Hybrid is not null)
            ? await _extractor.RagChunksAsync(bytes, _options.Partition, _options.Hybrid, cancellationToken).ConfigureAwait(false)
            : await _extractor.RagChunksAsync(bytes, _options.Profile, cancellationToken).ConfigureAwait(false);

        var content = new FileContent(PdfMimeType);
        foreach (var rc in chunks)
        {
            int page = rc.PageNumbers.Count > 0 ? rc.PageNumbers[0] : -1;
            var meta = Chunk.Meta(sentencesAreComplete: true, pageNumber: page);
            content.Sections.Add(new Chunk(rc.FullText, rc.ChunkIndex, meta));
        }

        return content;
    }
```
Also add `using OxidizePdf.NET.Models;` to the top of the file.

- [ ] **Step 6: Run to verify it passes**

Run: `dotnet test dotnet/OxidizePdf.NET.KernelMemory.Tests --filter DecodeAsyncTests`
Expected: PASS (2 tests).

- [ ] **Step 7: Commit**

```bash
git add dotnet/OxidizePdf.NET.KernelMemory dotnet/OxidizePdf.NET.KernelMemory.Tests
git commit -m "feat(km): DecodeAsync maps oxidize RagChunks 1:1 to KM Chunks"
```

---

### Task 3: Edge-case behavior — empty result and missing file

**Files:**
- Create: `dotnet/OxidizePdf.NET.KernelMemory.Tests/DecodeEdgeCasesTests.cs`
- (No production change expected — this task verifies existing behavior is correct.)

**Interfaces:**
- Consumes: `OxidizePdfDecoder.DecodeAsync` from Task 2.

- [ ] **Step 1: Write the edge-case tests**

`dotnet/OxidizePdf.NET.KernelMemory.Tests/DecodeEdgeCasesTests.cs`:
```csharp
using OxidizePdf.NET.KernelMemory;

namespace OxidizePdf.NET.KernelMemory.Tests;

public class DecodeEdgeCasesTests
{
    [Fact]
    public async Task DecodeAsync_missing_file_throws_FileNotFound()
    {
        var decoder = new OxidizePdfDecoder();
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => decoder.DecodeAsync(Path.Combine(AppContext.BaseDirectory, "does-not-exist.pdf")));
    }

    [Fact]
    public async Task DecodeAsync_empty_bytes_throws_ArgumentException()
    {
        // PdfExtractor.RagChunksAsync rejects empty input with ArgumentException;
        // the decoder must surface it, not swallow it.
        var decoder = new OxidizePdfDecoder();
        await Assert.ThrowsAsync<ArgumentException>(
            () => decoder.DecodeAsync(new BinaryData(Array.Empty<byte>())));
    }
}
```

- [ ] **Step 2: Run to verify behavior**

Run: `dotnet test dotnet/OxidizePdf.NET.KernelMemory.Tests --filter DecodeEdgeCasesTests`
Expected: PASS. If `DecodeAsync_empty_bytes` fails because the extractor wraps the error differently, adjust the asserted exception type to match what `PdfExtractor.RagChunksAsync(empty)` actually throws (verify by reading `PdfExtractor.cs:199-200` — it throws `ArgumentException`). Do not add try/catch to the decoder; surfacing the extractor's exception is the intended contract.

- [ ] **Step 3: Commit**

```bash
git add dotnet/OxidizePdf.NET.KernelMemory.Tests
git commit -m "test(km): decoder surfaces missing-file and empty-input errors"
```

---

### Task 4: `WithOxidizePdf` registration extension

**Files:**
- Create: `dotnet/OxidizePdf.NET.KernelMemory/KernelMemoryBuilderExtensions.cs`
- Create: `dotnet/OxidizePdf.NET.KernelMemory.Tests/WithOxidizePdfTests.cs`

**Interfaces:**
- Consumes: `OxidizePdfDecoder`, `OxidizePdfDecoderOptions` from Tasks 1-2; `Microsoft.KernelMemory.IKernelMemoryBuilder` with `AddSingleton<TService>(TService instance)`; `Microsoft.KernelMemory.DataFormats.IContentDecoder`.
- Produces: `public static IKernelMemoryBuilder WithOxidizePdf(this IKernelMemoryBuilder builder, OxidizePdfDecoderOptions? options = null)`.

- [ ] **Step 1: Write the failing registration test**

`dotnet/OxidizePdf.NET.KernelMemory.Tests/WithOxidizePdfTests.cs`:
```csharp
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.DataFormats;
using OxidizePdf.NET.KernelMemory;

namespace OxidizePdf.NET.KernelMemory.Tests;

public class WithOxidizePdfTests
{
    [Fact]
    public void WithOxidizePdf_registers_a_pdf_content_decoder()
    {
        var builder = new KernelMemoryBuilder();

        var returned = builder.WithOxidizePdf();

        Assert.Same(builder, returned);

        // The registered decoder must be discoverable and claim application/pdf.
        var provider = builder.Services.Build();
        var decoders = provider.GetServices<IContentDecoder>();
        Assert.Contains(decoders, d => d.SupportsMimeType("application/pdf"));
    }
}
```
> If `builder.Services.Build()` / `GetServices` is not the exact accessor on `ServiceCollectionPool`, resolve via the documented API: build the memory with `builder.Build<MemoryServerless>()` and assert it constructs without throwing, AND keep the stronger decoder assertion in the Task 5 e2e test. Prefer the service-collection assertion if available; it is the more direct proof.

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test dotnet/OxidizePdf.NET.KernelMemory.Tests --filter WithOxidizePdfTests`
Expected: FAIL — `WithOxidizePdf` does not exist (compile error).

- [ ] **Step 3: Implement the extension**

`dotnet/OxidizePdf.NET.KernelMemory/KernelMemoryBuilderExtensions.cs`:
```csharp
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
```
> Placing the extension in the `Microsoft.KernelMemory` namespace matches KM's own convention so `WithOxidizePdf` appears without an extra `using`. If `AddSingleton<TService>(instance)` is not the exact member exposed by `IKernelMemoryBuilder`, register through `builder.Services` using the equivalent documented call resolved against the pinned KM version.

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test dotnet/OxidizePdf.NET.KernelMemory.Tests --filter WithOxidizePdfTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add dotnet/OxidizePdf.NET.KernelMemory dotnet/OxidizePdf.NET.KernelMemory.Tests
git commit -m "feat(km): WithOxidizePdf builder extension registers the decoder"
```

---

### Task 5: `OxidizeChunkPartitioningHandler` — emit one KM partition per oxidize chunk

> **Architecture correction (see spec 2026-06-29):** KM's default
> `split_text_in_partitions` re-chunks the flattened text and ignores
> `FileContent.Sections`. KM's `extract_text` writes a second artifact,
> `ExtractedContent` (the structured `FileContent` JSON, sections intact). This
> task adds a handler that replaces the partition step and reads that JSON to
> emit one partition per oxidize chunk.

**Files:**
- Create: `dotnet/OxidizePdf.NET.KernelMemory/OxidizeChunkPartitioningHandler.cs`
- Create: `dotnet/OxidizePdf.NET.KernelMemory.Tests/OxidizeChunkPartitioningHandlerTests.cs`

**Interfaces:**
- Consumes: `Microsoft.KernelMemory.Pipeline.IPipelineStepHandler` (`string StepName { get; }`, `Task<(ReturnType, DataPipeline)> InvokeAsync(DataPipeline, CancellationToken)`); `Microsoft.KernelMemory.Pipeline.IPipelineOrchestrator` (`Task<BinaryData> ReadFileAsync(DataPipeline, string, CancellationToken)`, `Task WriteFileAsync(DataPipeline, string, BinaryData, CancellationToken)`); `DataPipeline` with `Files` → `FileDetails.GeneratedFiles` (`Dictionary<string, GeneratedFileDetails>`), `FileDetails.GetPartitionFileName(int)`, `DataPipeline.ArtifactTypes.{ExtractedContent,TextPartition}`, `GeneratedFileDetails { Name; MimeType; ArtifactType; PartitionNumber; SectionNumber; Tags; ContentSHA256 }`, `pipeline.Tags`; `Microsoft.KernelMemory.DataFormats.{FileContent, Chunk}`; `Microsoft.KernelMemory.MimeTypes.PlainText`; `BinaryData.CalculateSHA256()`.
- Produces: `OxidizePdf.NET.KernelMemory.OxidizeChunkPartitioningHandler` with ctor `(string stepName, IPipelineOrchestrator orchestrator)`.

> **VERIFY against the pinned `0.98.250508.3` before writing** (signatures
> verified against KM `main` 2026-06-28; the package is close but confirm):
> `IPipelineOrchestrator.ReadFileAsync` / `WriteFileAsync` exact signatures;
> the `ReturnType` enum member (`ReturnType.Success`); the `GeneratedFileDetails`
> field set and `GetPartitionFileName`; the `MimeTypes.PlainText` constant; the
> `BinaryData.CalculateSHA256()` extension. If any differs, adapt the code to the
> real member and note it — do not invent members.

- [ ] **Step 1: Write the handler**

`dotnet/OxidizePdf.NET.KernelMemory/OxidizeChunkPartitioningHandler.cs`:
```csharp
using System.Text.Json;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.DataFormats;
using Microsoft.KernelMemory.Pipeline;

namespace OxidizePdf.NET.KernelMemory;

/// <summary>
/// Replaces Kernel Memory's default <c>split_text_in_partitions</c> step. Instead
/// of re-chunking the flattened extracted text, it reads the structured
/// <c>ExtractedContent</c> artifact (the <see cref="FileContent"/> the oxidize-pdf
/// decoder produced) and emits ONE partition per oxidize chunk, preserving heading
/// context (each chunk's <see cref="Chunk.Content"/> is the oxidize RagChunk FullText)
/// and source page.
/// </summary>
public sealed class OxidizeChunkPartitioningHandler : IPipelineStepHandler
{
    private readonly IPipelineOrchestrator _orchestrator;

    public OxidizeChunkPartitioningHandler(string stepName, IPipelineOrchestrator orchestrator)
    {
        this.StepName = stepName;
        this._orchestrator = orchestrator;
    }

    /// <inheritdoc />
    public string StepName { get; }

    /// <inheritdoc />
    public async Task<(ReturnType returnType, DataPipeline updatedPipeline)> InvokeAsync(
        DataPipeline pipeline, CancellationToken cancellationToken = default)
    {
        foreach (DataPipeline.FileDetails uploadedFile in pipeline.Files)
        {
            var newFiles = new Dictionary<string, DataPipeline.GeneratedFileDetails>();

            foreach (DataPipeline.GeneratedFileDetails generated in uploadedFile.GeneratedFiles.Values)
            {
                // Read the STRUCTURED artifact (sections intact), not the flattened ExtractedText.
                if (generated.ArtifactType != DataPipeline.ArtifactTypes.ExtractedContent)
                {
                    continue;
                }

                BinaryData raw = await this._orchestrator
                    .ReadFileAsync(pipeline, generated.Name, cancellationToken)
                    .ConfigureAwait(false);

                FileContent? content = JsonSerializer.Deserialize<FileContent>(raw.ToArray());
                if (content is null) { continue; }

                int partitionNumber = 0;
                foreach (Chunk chunk in content.Sections)
                {
                    string destFile = uploadedFile.GetPartitionFileName(partitionNumber);
                    var data = new BinaryData(chunk.Content);

                    await this._orchestrator
                        .WriteFileAsync(pipeline, destFile, data, cancellationToken)
                        .ConfigureAwait(false);

                    newFiles.Add(destFile, new DataPipeline.GeneratedFileDetails
                    {
                        Name = destFile,
                        MimeType = MimeTypes.PlainText,
                        ArtifactType = DataPipeline.ArtifactTypes.TextPartition,
                        PartitionNumber = partitionNumber,
                        SectionNumber = chunk.PageNumber,
                        Tags = pipeline.Tags,
                        ContentSHA256 = data.CalculateSHA256(),
                    });
                    partitionNumber++;
                }
            }

            foreach (var nf in newFiles) { uploadedFile.GeneratedFiles.Add(nf.Key, nf.Value); }
        }

        return (ReturnType.Success, pipeline);
    }
}
```

- [ ] **Step 2: Write the failing handler unit test**

Use a minimal fake `IPipelineOrchestrator` that serves a known `ExtractedContent`
JSON and captures `WriteFileAsync` calls. The fake implements ONLY the members the
handler calls; for the rest of `IPipelineOrchestrator` throw `NotImplementedException`
(the handler never touches them). **Confirm the interface's member list against the
pinned package and implement the full interface surface** (C# requires all members);
if the interface is too large to fake cleanly, report it — the e2e in Task 6 is the
fallback behavioral proof.

`dotnet/OxidizePdf.NET.KernelMemory.Tests/OxidizeChunkPartitioningHandlerTests.cs`:
```csharp
using System.Text.Json;
using Microsoft.KernelMemory.DataFormats;
using Microsoft.KernelMemory.Pipeline;
using OxidizePdf.NET.KernelMemory;

namespace OxidizePdf.NET.KernelMemory.Tests;

public class OxidizeChunkPartitioningHandlerTests
{
    [Fact]
    public async Task InvokeAsync_emits_one_partition_per_section()
    {
        // Arrange: a FileContent with 3 sections, serialized as the ExtractedContent artifact.
        var content = new FileContent("text/plain");
        content.Sections.Add(new Chunk("Heading A\n\nfirst chunk", 0, Chunk.Meta(true, 1)));
        content.Sections.Add(new Chunk("Heading A\n\nsecond chunk", 1, Chunk.Meta(true, 1)));
        content.Sections.Add(new Chunk("Heading B\n\nthird chunk", 2, Chunk.Meta(true, 2)));
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(content);

        var orchestrator = new FakeOrchestrator(json);
        var pipeline = FakeOrchestrator.PipelineWithExtractedContent("doc.pdf", "extracted.json");

        var handler = new OxidizeChunkPartitioningHandler("split_text_in_partitions", orchestrator);

        // Act
        var (result, _) = await handler.InvokeAsync(pipeline);

        // Assert: one TextPartition per section, content == each chunk's full text, ascending numbers.
        Assert.Equal(ReturnType.Success, result);
        Assert.Equal(3, orchestrator.Written.Count);
        Assert.Equal(
            new[] { "Heading A\n\nfirst chunk", "Heading A\n\nsecond chunk", "Heading B\n\nthird chunk" },
            orchestrator.Written.Select(w => w.content).ToArray());

        var file = pipeline.Files[0];
        var partitions = file.GeneratedFiles.Values
            .Where(f => f.ArtifactType == DataPipeline.ArtifactTypes.TextPartition)
            .OrderBy(f => f.PartitionNumber)
            .ToList();
        Assert.Equal(3, partitions.Count);
        Assert.Equal(new[] { 0, 1, 2 }, partitions.Select(p => p.PartitionNumber).ToArray());
        Assert.Equal(new[] { 1, 1, 2 }, partitions.Select(p => p.SectionNumber).ToArray()); // page numbers
    }
}
```

Implement `FakeOrchestrator` in the same file (or a `TestHelpers/FakeOrchestrator.cs`):
it stores the `byte[]` JSON, returns it from `ReadFileAsync` regardless of name, records
each `WriteFileAsync` as `(name, content-string)` in a public `Written` list, and provides
a static `PipelineWithExtractedContent(fileName, artifactName)` that builds a `DataPipeline`
with one `FileDetails` whose `GeneratedFiles` contains one `ExtractedContent` entry named
`artifactName`. All other `IPipelineOrchestrator` members throw `NotImplementedException`.

- [ ] **Step 3: Run to verify it fails, then passes**

Run: `dotnet test dotnet/OxidizePdf.NET.KernelMemory.Tests --filter OxidizeChunkPartitioningHandlerTests`
Expected: FAIL first (handler/fake not implemented or compile error), then PASS once the handler is written. Do NOT relax the per-section assertions.

- [ ] **Step 4: Commit**

```bash
git add dotnet/OxidizePdf.NET.KernelMemory/OxidizeChunkPartitioningHandler.cs dotnet/OxidizePdf.NET.KernelMemory.Tests/OxidizeChunkPartitioningHandlerTests.cs
git commit -m "feat(km): OxidizeChunkPartitioningHandler emits one partition per oxidize chunk"
```

---

### Task 6: Rework the e2e test — register the handler, prove 1:1 end-to-end

> The e2e test and `RecordingEmbeddingGenerator` already exist on the branch
> (committed by the prior Task 5 attempt, `36dfd78`). The test currently FAILS
> (`Assert.Equal(13, 3)`) because the default partitioner re-chunked. This task
> registers the new handler so the count becomes 1:1, turning the test green
> without weakening the assertion.

**Files:**
- Modify: `dotnet/OxidizePdf.NET.KernelMemory.Tests/PartitionPreservationE2ETests.cs`
- (Keep `RecordingEmbeddingGenerator.cs` as-is.)

**Interfaces:**
- Consumes: `OxidizeChunkPartitioningHandler` (Task 5); `WithOxidizePdf` (Task 4); the existing keyless builder setup (the prior attempt used `AddSingleton<ITextEmbeddingGenerator>(embeddings)` + `WithoutTextGenerator()` + `WithSimpleVectorDb()` because `WithCustomEmbeddingGenerator`/`WithCustomTextPartitioningOptions` do NOT exist in `0.98.250508.3`); `memory.Orchestrator.AddHandler<T>(stepName)`.

- [ ] **Step 1: Read the current test file**

Read `dotnet/OxidizePdf.NET.KernelMemory.Tests/PartitionPreservationE2ETests.cs` to see the
exact keyless builder wiring the prior attempt committed. Preserve that wiring (embedding
generator registration, `WithoutTextGenerator`, `WithSimpleVectorDb`, `Build<MemoryServerless>()`,
the fixture read, and the `oxidizeChunks` baseline).

- [ ] **Step 2: Register the handler after Build, before import**

Immediately after `.Build<MemoryServerless>()` (and before `ImportDocumentAsync`), add:
```csharp
memory.Orchestrator.AddHandler<OxidizeChunkPartitioningHandler>("split_text_in_partitions");
```
> If `AddHandler<T>(stepName)` does not OVERRIDE the existing default for that step
> (e.g. throws on duplicate), switch the builder to `.WithoutDefaultHandlers()` and register
> all four steps explicitly (`extract_text`, `split_text_in_partitions` → the oxidize handler,
> `generate_embeddings`, `save_records` — use KM's default handler types for the three you are
> not replacing). Confirm the exact default step names and handler types against the pinned package.

Keep the assertions exactly as they are:
```csharp
Assert.Equal(oxidizeChunks.Count, embeddings.EmbeddedTexts.Count);
Assert.All(oxidizeChunks, c => Assert.Contains(c.FullText, embeddings.EmbeddedTexts));
```

- [ ] **Step 3: Run to verify it now passes 1:1**

Run: `dotnet test dotnet/OxidizePdf.NET.KernelMemory.Tests --filter PartitionPreservationE2ETests`
Expected: PASS — `embeddings.EmbeddedTexts.Count == oxidizeChunks.Count` (the same count, e.g. 13==13). If it still differs, diagnose the handler registration / step-name override; do NOT weaken the equal-count assertion. Then run the full suite once: `dotnet test dotnet/OxidizePdf.NET.KernelMemory.Tests` — all green.

- [ ] **Step 4: Commit**

```bash
git add dotnet/OxidizePdf.NET.KernelMemory.Tests/PartitionPreservationE2ETests.cs
git commit -m "test(km): e2e proves 1:1 oxidize-chunk preservation via custom partition handler"
```

---

### Task 7: Rewrite `examples/KernelMemory` as a real end-to-end sample

**Files:**
- Modify: `examples/KernelMemory/KernelMemory.csproj`
- Rewrite: `examples/KernelMemory/Program.cs`
- Rewrite: `examples/KernelMemory/README.md`
- Create: `examples/KernelMemory/fixtures/sample.pdf` (copy of the fixture)

**Interfaces:**
- Consumes: `OxidizePdfDecoder`, `WithOxidizePdf` (Tasks 2, 4), `OxidizeChunkPartitioningHandler` (Task 5); KM `KernelMemoryBuilder`, `WithOpenAIDefaults`, `WithSimpleVectorDb`, `MemoryServerless`, `memory.Orchestrator.AddHandler<T>(stepName)`, `Document.AddStream`, `memory.AskAsync`.

- [ ] **Step 1: Repoint the sample csproj to the connector + KM Core**

Replace `examples/KernelMemory/KernelMemory.csproj` with:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\dotnet\OxidizePdf.NET.KernelMemory\OxidizePdf.NET.KernelMemory.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Content Include="fixtures\**\*">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
  </ItemGroup>

</Project>
```
Then add KM Core:
```bash
cd examples/KernelMemory
dotnet add package Microsoft.KernelMemory.Core
cd ../..
cp dotnet/OxidizePdf.NET.Tests/fixtures/sample.pdf examples/KernelMemory/fixtures/sample.pdf
```

- [ ] **Step 2: Rewrite `Program.cs` (keyless decode always; full loop with key)**

`examples/KernelMemory/Program.cs`:
```csharp
using Microsoft.KernelMemory;
using OxidizePdf.NET.KernelMemory;

string pdfPath = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "fixtures", "sample.pdf");
byte[] pdfBytes = await File.ReadAllBytesAsync(pdfPath);

Console.WriteLine("OxidizePdf.NET + Kernel Memory");
Console.WriteLine("==============================\n");

// 1. Always show oxidize-pdf's structure-aware chunks (no API key needed).
var decoder = new OxidizePdfDecoder();
var content = await decoder.DecodeAsync(new BinaryData(pdfBytes));
Console.WriteLine($"oxidize-pdf produced {content.Sections.Count} structure-aware chunks:");
foreach (var c in content.Sections.Take(5))
    Console.WriteLine($"  [page {c.PageNumber}] {Truncate(c.Content, 90)}");
Console.WriteLine();

// 2. With OPENAI_API_KEY, run the full index + semantic query loop.
string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.WriteLine("Set OPENAI_API_KEY to run the full Kernel Memory index + semantic query loop.");
    return;
}

var memory = new KernelMemoryBuilder()
    .WithOpenAIDefaults(apiKey)
    .WithOxidizePdf()
    .WithSimpleVectorDb()
    .Build<MemoryServerless>();

// Replace KM's default text splitter so oxidize-pdf's chunks survive 1:1.
memory.Orchestrator.AddHandler<OxidizeChunkPartitioningHandler>("split_text_in_partitions");

using var stream = new MemoryStream(pdfBytes);
await memory.ImportDocumentAsync(new Document("sample").AddStream(Path.GetFileName(pdfPath), stream));
Console.WriteLine("Indexed. Asking a question...\n");

var answer = await memory.AskAsync("What is this document about?");
Console.WriteLine($"Q: What is this document about?\nA: {answer.Result}");

static string Truncate(string s, int n) => s.Length <= n ? s : string.Concat(s.AsSpan(0, n), "...");
```

- [ ] **Step 3: Rewrite the sample README**

`examples/KernelMemory/README.md`:
```markdown
# OxidizePdf.NET + Kernel Memory

Real end-to-end sample: oxidize-pdf's structure-aware RAG chunks dropped into
Microsoft Kernel Memory via the `OxidizePdf.NET.KernelMemory` connector.

## Run

```bash
# Keyless: prints oxidize-pdf's structure-aware chunks for the bundled PDF.
dotnet run --project examples/KernelMemory

# Full loop: index into Kernel Memory + ask a semantic question.
export OPENAI_API_KEY=sk-...
dotnet run --project examples/KernelMemory path/to/your.pdf
```

## Why the custom partition handler?

`.WithOxidizePdf()` registers a content decoder, and
`memory.Orchestrator.AddHandler<OxidizeChunkPartitioningHandler>("split_text_in_partitions")`
replaces KM's default text splitter. Kernel Memory would normally re-chunk the
extracted text, discarding oxidize-pdf's structure-aware boundaries. The handler
reads the structured `ExtractedContent` artifact and emits **one partition per
oxidize-pdf chunk** — each carrying heading context and source page — so the
chunks you see keyless are exactly what gets embedded and stored.
```

- [ ] **Step 4: Verify the sample builds and runs keyless (real behavior)**

Run: `dotnet run --project examples/KernelMemory`
Expected: prints `oxidize-pdf produced N structure-aware chunks:` with `N >= 1` and at least one `[page X] ...` line, then the "Set OPENAI_API_KEY" message. This confirms the decode path runs without keys against the real bundled PDF.

- [ ] **Step 5: Commit**

```bash
git add examples/KernelMemory
git commit -m "feat(km): rewrite KernelMemory example as a real end-to-end sample"
```

---

### Task 8: Package metadata for the connector

**Files:**
- Modify: `dotnet/OxidizePdf.NET.KernelMemory/OxidizePdf.NET.KernelMemory.csproj`
- Create: `dotnet/OxidizePdf.NET.KernelMemory/README.md` (NuGet package readme)

**Interfaces:** none (packaging only).

- [ ] **Step 1: Add NuGet metadata to the connector csproj**

Add this `<PropertyGroup>` to `OxidizePdf.NET.KernelMemory.csproj`:
```xml
  <PropertyGroup>
    <PackageId>OxidizePdf.NET.KernelMemory</PackageId>
    <Version>0.1.0-preview</Version>
    <Authors>BelowZero</Authors>
    <Description>Kernel Memory content decoder backed by oxidize-pdf: structure-aware, page-cited PDF chunks dropped into your KM RAG pipeline in one call.</Description>
    <PackageTags>kernel-memory;rag;llm;pdf;chunking;embeddings;semantic-kernel;ingestion</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <RepositoryUrl>https://github.com/bzsanti/oxidize-pdf-dotnet</RepositoryUrl>
    <PackageProjectUrl>https://github.com/bzsanti/oxidizePdf</PackageProjectUrl>
  </PropertyGroup>

  <ItemGroup>
    <None Include="README.md" Pack="true" PackagePath="\" />
  </ItemGroup>
```

- [ ] **Step 2: Write the package README**

`dotnet/OxidizePdf.NET.KernelMemory/README.md`:
```markdown
# OxidizePdf.NET.KernelMemory

A Microsoft Kernel Memory `IContentDecoder` backed by [oxidize-pdf](https://github.com/bzsanti/oxidizePdf).
Emits one KM partition per oxidize-pdf structure-aware chunk — heading context and
source page preserved.

```csharp
using Microsoft.KernelMemory;

var memory = new KernelMemoryBuilder()
    .WithOpenAIDefaults(apiKey)
    .WithOxidizePdf()
    .WithSimpleVectorDb()
    .Build<MemoryServerless>();

memory.Orchestrator.AddHandler<OxidizeChunkPartitioningHandler>("split_text_in_partitions");

await memory.ImportDocumentAsync(new Document("doc").AddFile("report.pdf"));
```

The `AddHandler` call replaces KM's default text splitter so oxidize-pdf's chunks
land in the vector store 1:1; without it Kernel Memory re-chunks the text.
```

- [ ] **Step 3: Verify the package builds**

Run: `dotnet pack dotnet/OxidizePdf.NET.KernelMemory -c Release`
Expected: `Successfully created package '...OxidizePdf.NET.KernelMemory.0.1.0-preview.nupkg'`.

- [ ] **Step 4: Commit**

```bash
git add dotnet/OxidizePdf.NET.KernelMemory
git commit -m "build(km): NuGet package metadata for OxidizePdf.NET.KernelMemory 0.1.0-preview"
```

---

## Final verification (after all tasks)

- [ ] Run the full connector test suite: `dotnet test dotnet/OxidizePdf.NET.KernelMemory.Tests` → all PASS.
- [ ] Build the whole solution warnings-clean: `dotnet build dotnet/OxidizePdf.sln -c Release` → no warnings (warnings = errors).
- [ ] Confirm no native binaries or `.bak`/`.orig`/`.tmp` files were staged (`git status` before any add; project rule from error-log #25).

## Out of scope / follow-up

- Wiring `OxidizePdf.NET.KernelMemory` into `release.yml` (pack + publish to NuGet) — separate change after this lands.
- Adoption lever #4 (benchmarks + referral) — separate spec/plan.
