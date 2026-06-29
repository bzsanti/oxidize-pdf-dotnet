# PDF Extraction Benchmark Harness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a runnable console tool that benchmarks `OxidizePdf.NET` against PdfPig, iText7 and Docnet.Core over the 802-PDF corpus, reporting speed (on the common-success subset only) and robustness (over the full corpus) as separate, honest metrics.

**Architecture:** A new non-packable `net8.0` console project `dotnet/OxidizePdf.NET.Benchmarks`. Every library is driven through one `IPdfExtractorAdapter` contract (identical "all-pages plain text" operation). A `BenchmarkRunner` produces a `FileResult` per (adapter, pdf) with per-file timeout + exception isolation. A `ResultsAggregator` computes the common-success intersection (speed denominator) and per-adapter robustness percentages. Results are written to `results.json` (raw + aggregates) and `results.md` (speed table + robustness table + hand-authored capability matrix). The competitor packages are dev-only `PackageReference`s in this project; they never ship in the published `OxidizePdf.NET` / connector packages.

**Tech Stack:** C# / .NET 8, xUnit 2.9.2, `UglyToad.PdfPig` 1.7.0-custom-5 (MIT), `itext7` 9.6.0 (AGPL), `Docnet.Core` 2.6.0 (MIT/PDFium BSD), `OxidizePdf.NET` (ProjectReference), `System.Text.Json`.

## Global Constraints

- **Target framework:** `net8.0` for both the console project and its test project (spec §Architecture). The referenced `OxidizePdf.NET` multi-targets `net8.0;net9.0;net10.0`, so `net8.0` resolves.
- **`<IsPackable>false</IsPackable>`** on the console project — it must never be published to NuGet (spec §Architecture).
- **`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`** and **`<Nullable>enable</Nullable>`** on every new project — matches the repo convention (`OxidizePdf.NET.Tests.csproj`) and the user rule "warnings = errors".
- **Competitor versions are pinned exactly** (no floating ranges): PdfPig `1.7.0-custom-5`, iText7 `9.6.0`, Docnet.Core `2.6.0`. PdfPig's `-custom-5` suffix is its normal release tag, not an unstable prerelease; it is the current published release.
- **Native library copy:** consumers of `OxidizePdf.NET` via `ProjectReference` do NOT automatically get `liboxidize_pdf_ffi.*` in their output. Both new projects MUST include the `CopyNativeBinariesToOutput` MSBuild target (provided in Task 1) or the `OxidizeAdapter` throws `DllNotFoundException` at runtime.
- **Speed is reported ONLY over the common-success subset** (PDFs every adapter extracted with status `Ok`); robustness is reported over the full corpus per adapter. These two numbers are never merged. This is the core honesty safeguard (spec §Metrics).
- **Reference page count** for `ms/page` is PdfPig's page count per PDF (a neutral third party), identical across adapters — never oxidize's own.
- **New files must be added to git** (user rule). Each task's commit step stages explicit paths.
- **No smoke tests** (user rule): tests assert real extracted content (known substrings, intersection logic), never just "did not throw" / "file exists".

---

## File Structure

New project `dotnet/OxidizePdf.NET.Benchmarks/`:
- `OxidizePdf.NET.Benchmarks.csproj` — console, net8.0, non-packable, dev-only competitor refs + native-copy target.
- `IPdfExtractorAdapter.cs` — `IPdfExtractorAdapter` interface + `ExtractResult` record.
- `Adapters/OxidizeAdapter.cs` — drives `OxidizePdf.NET.PdfExtractor`.
- `Adapters/PdfPigAdapter.cs` — drives `UglyToad.PdfPig`.
- `Adapters/IText7Adapter.cs` — drives `itext7` (aliased to dodge type-name clashes).
- `Adapters/DocnetAdapter.cs` — drives `Docnet.Core` (PDFium).
- `FileResult.cs` — `FileResult` record + `ExtractStatus` enum.
- `BenchmarkRunner.cs` — per-file timeout + exception isolation, emits `FileResult`s.
- `ResultsAggregator.cs` — `Aggregates`, `SpeedMetric`, `RobustnessMetric` records + intersection/median logic.
- `EnvironmentInfo.cs` — `EnvironmentInfo` + `AdapterInfo` records + capture helper.
- `Reporting/JsonReportWriter.cs` — writes `results.json`.
- `Reporting/MarkdownReportWriter.cs` — writes `results.md` (speed + robustness tables + capability matrix constant).
- `Program.cs` — CLI (`--corpus`, `--timeout`, `--out`), wiring, fail-fast validation.

New project `dotnet/OxidizePdf.NET.Benchmarks.Tests/`:
- `OxidizePdf.NET.Benchmarks.Tests.csproj` — net8.0, xUnit, refs benchmark project + competitor packages + native-copy target.
- `fixtures/sample.pdf` — copied from `OxidizePdf.NET.Tests/fixtures/sample.pdf` (8-page real PDF).
- `AdapterTests.cs` — per-adapter real-extraction tests.
- `BenchmarkRunnerTests.cs` — timeout / error-isolation / empty-detection with fake adapters.
- `ResultsAggregatorTests.cs` — the honesty-safeguard test (intersection, subset speed, full robustness).
- `ReportWriterTests.cs` — JSON round-trips, Markdown contains the expected tables.

Both `.csproj` files are added to `dotnet/OxidizePdf.sln`.

---

### Task 1: Scaffold projects, core contract, and the OxidizeAdapter

**Files:**
- Create: `dotnet/OxidizePdf.NET.Benchmarks/OxidizePdf.NET.Benchmarks.csproj`
- Create: `dotnet/OxidizePdf.NET.Benchmarks/IPdfExtractorAdapter.cs`
- Create: `dotnet/OxidizePdf.NET.Benchmarks/Adapters/OxidizeAdapter.cs`
- Create: `dotnet/OxidizePdf.NET.Benchmarks/Program.cs` (temporary stub, replaced in Task 8)
- Create: `dotnet/OxidizePdf.NET.Benchmarks.Tests/OxidizePdf.NET.Benchmarks.Tests.csproj`
- Create: `dotnet/OxidizePdf.NET.Benchmarks.Tests/fixtures/sample.pdf` (copy)
- Create: `dotnet/OxidizePdf.NET.Benchmarks.Tests/AdapterTests.cs`
- Modify: `dotnet/OxidizePdf.sln`

**Interfaces:**
- Produces:
  - `public sealed record ExtractResult(int PageCount, string Text);`
  - `public interface IPdfExtractorAdapter { string Name { get; } string License { get; } string Version { get; } ExtractResult Extract(byte[] pdfBytes); }`
  - `public sealed class OxidizeAdapter : IPdfExtractorAdapter` — `Name == "OxidizePdf.NET"`, `License == "MIT"`.

- [ ] **Step 1: Create the console project file**

Create `dotnet/OxidizePdf.NET.Benchmarks/OxidizePdf.NET.Benchmarks.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <RootNamespace>OxidizePdf.NET.Benchmarks</RootNamespace>
  </PropertyGroup>

  <!-- Dev-only competitor packages. Never published (IsPackable=false). -->
  <ItemGroup>
    <PackageReference Include="UglyToad.PdfPig" Version="1.7.0-custom-5" />
    <PackageReference Include="itext7" Version="9.6.0" />
    <PackageReference Include="Docnet.Core" Version="2.6.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\OxidizePdf.NET\OxidizePdf.NET.csproj" />
  </ItemGroup>

  <!-- ProjectReference does not flow the native runtimes; copy them so the
       OxidizeAdapter can load liboxidize_pdf_ffi.* at runtime. -->
  <Target Name="CopyNativeBinariesToOutput" AfterTargets="Build">
    <ItemGroup>
      <NativeBinaries Include="..\OxidizePdf.NET\runtimes\**\native\*.*" />
    </ItemGroup>
    <Copy SourceFiles="@(NativeBinaries)" DestinationFolder="$(OutDir)runtimes\%(RecursiveDir)" SkipUnchangedFiles="true" />
  </Target>

</Project>
```

- [ ] **Step 2: Create the contract**

Create `dotnet/OxidizePdf.NET.Benchmarks/IPdfExtractorAdapter.cs`:

```csharp
namespace OxidizePdf.NET.Benchmarks;

/// <summary>The result of extracting all-pages plain text from one PDF.</summary>
public sealed record ExtractResult(int PageCount, string Text);

/// <summary>
/// Common contract every PDF library is driven through. The identical
/// operation for every adapter — concatenated plain text from every page —
/// is what makes the comparison apples-to-apples.
/// </summary>
public interface IPdfExtractorAdapter
{
    /// <summary>Display name, e.g. "OxidizePdf.NET".</summary>
    string Name { get; }

    /// <summary>License label, e.g. "MIT" or "AGPL".</summary>
    string License { get; }

    /// <summary>The underlying library's package version.</summary>
    string Version { get; }

    /// <summary>Extract plain text from EVERY page, concatenated.</summary>
    ExtractResult Extract(byte[] pdfBytes);
}
```

- [ ] **Step 3: Create the temporary Program stub**

Create `dotnet/OxidizePdf.NET.Benchmarks/Program.cs` (replaced in Task 8 — needed now so the Exe project compiles):

```csharp
// Temporary entry point; replaced by the CLI in Task 8.
System.Console.WriteLine("OxidizePdf.NET.Benchmarks — not yet wired. See Task 8.");
```

- [ ] **Step 4: Create the test project file**

Create `dotnet/OxidizePdf.NET.Benchmarks.Tests/OxidizePdf.NET.Benchmarks.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="coverlet.collector" Version="6.0.2" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\OxidizePdf.NET.Benchmarks\OxidizePdf.NET.Benchmarks.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Content Include="fixtures\**\*">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
  </ItemGroup>

  <!-- Native runtimes for the OxidizeAdapter under test. -->
  <Target Name="CopyNativeBinariesToTestOutput" AfterTargets="Build">
    <ItemGroup>
      <NativeTestBinaries Include="..\OxidizePdf.NET\runtimes\**\native\*.*" />
    </ItemGroup>
    <Copy SourceFiles="@(NativeTestBinaries)" DestinationFolder="$(OutDir)runtimes\%(RecursiveDir)" SkipUnchangedFiles="true" />
  </Target>

</Project>
```

- [ ] **Step 5: Copy the sample fixture**

Run:

```bash
mkdir -p dotnet/OxidizePdf.NET.Benchmarks.Tests/fixtures
cp dotnet/OxidizePdf.NET.Tests/fixtures/sample.pdf dotnet/OxidizePdf.NET.Benchmarks.Tests/fixtures/sample.pdf
```

- [ ] **Step 6: Write the failing test for the OxidizeAdapter**

Create `dotnet/OxidizePdf.NET.Benchmarks.Tests/AdapterTests.cs`:

```csharp
using OxidizePdf.NET.Benchmarks;
using OxidizePdf.NET.Benchmarks.Adapters;

namespace OxidizePdf.NET.Benchmarks.Tests;

public class AdapterTests
{
    private static byte[] SamplePdf() =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "sample.pdf"));

    [Fact]
    public void OxidizeAdapter_ExtractsRealTextFromSamplePdf()
    {
        var adapter = new OxidizeAdapter();
        var result = adapter.Extract(SamplePdf());

        Assert.Equal("OxidizePdf.NET", adapter.Name);
        Assert.False(string.IsNullOrEmpty(adapter.Version));
        Assert.True(result.PageCount >= 1, "sample.pdf should have at least 1 page");
        // "SEVILLA" is a contiguous ASCII uppercase token in the real document,
        // extracted identically across libraries — verifies genuine extraction.
        Assert.Contains("SEVILLA", result.Text);
    }
}
```

- [ ] **Step 7: Run the test to verify it fails**

Run: `dotnet test dotnet/OxidizePdf.NET.Benchmarks.Tests --filter OxidizeAdapter_ExtractsRealTextFromSamplePdf`
Expected: FAIL — compile error, `OxidizeAdapter` does not exist.

- [ ] **Step 8: Implement the OxidizeAdapter**

Create `dotnet/OxidizePdf.NET.Benchmarks/Adapters/OxidizeAdapter.cs`:

```csharp
using OxidizePdf.NET;

namespace OxidizePdf.NET.Benchmarks.Adapters;

/// <summary>Drives OxidizePdf.NET's PdfExtractor.</summary>
public sealed class OxidizeAdapter : IPdfExtractorAdapter
{
    private readonly PdfExtractor _extractor = new();

    public string Name => "OxidizePdf.NET";
    public string License => "MIT";
    public string Version => PdfExtractor.Version;

    public ExtractResult Extract(byte[] pdfBytes)
    {
        // The library's API is async; the benchmark contract is synchronous and
        // single-threaded per call, so block here deliberately.
        int pageCount = _extractor.GetPageCountAsync(pdfBytes).GetAwaiter().GetResult();
        string text = _extractor.ExtractTextAsync(pdfBytes).GetAwaiter().GetResult();
        return new ExtractResult(pageCount, text);
    }
}
```

- [ ] **Step 9: Add both projects to the solution**

Run:

```bash
dotnet sln dotnet/OxidizePdf.sln add dotnet/OxidizePdf.NET.Benchmarks/OxidizePdf.NET.Benchmarks.csproj
dotnet sln dotnet/OxidizePdf.sln add dotnet/OxidizePdf.NET.Benchmarks.Tests/OxidizePdf.NET.Benchmarks.Tests.csproj
```

- [ ] **Step 10: Run the test to verify it passes**

Run: `dotnet test dotnet/OxidizePdf.NET.Benchmarks.Tests --filter OxidizeAdapter_ExtractsRealTextFromSamplePdf`
Expected: PASS (1 passed).

- [ ] **Step 11: Commit**

```bash
git add dotnet/OxidizePdf.NET.Benchmarks dotnet/OxidizePdf.NET.Benchmarks.Tests dotnet/OxidizePdf.sln
git commit -m "feat(benchmarks): scaffold harness project + OxidizeAdapter (#4)"
```

---

### Task 2: PdfPigAdapter

**Files:**
- Create: `dotnet/OxidizePdf.NET.Benchmarks/Adapters/PdfPigAdapter.cs`
- Modify: `dotnet/OxidizePdf.NET.Benchmarks.Tests/AdapterTests.cs`

**Interfaces:**
- Consumes: `IPdfExtractorAdapter`, `ExtractResult` (Task 1).
- Produces: `public sealed class PdfPigAdapter : IPdfExtractorAdapter` — `Name == "PdfPig"`, `License == "MIT"`.

- [ ] **Step 1: Write the failing test**

Add to `dotnet/OxidizePdf.NET.Benchmarks.Tests/AdapterTests.cs` (inside the class):

```csharp
    [Fact]
    public void PdfPigAdapter_ExtractsRealTextFromSamplePdf()
    {
        var adapter = new PdfPigAdapter();
        var result = adapter.Extract(SamplePdf());

        Assert.Equal("PdfPig", adapter.Name);
        Assert.False(string.IsNullOrEmpty(adapter.Version));
        Assert.True(result.PageCount >= 1);
        Assert.Contains("SEVILLA", result.Text);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test dotnet/OxidizePdf.NET.Benchmarks.Tests --filter PdfPigAdapter_ExtractsRealTextFromSamplePdf`
Expected: FAIL — `PdfPigAdapter` does not exist.

- [ ] **Step 3: Implement the PdfPigAdapter**

Create `dotnet/OxidizePdf.NET.Benchmarks/Adapters/PdfPigAdapter.cs`:

```csharp
using System.Reflection;
using System.Text;
// Alias: UglyToad.PdfPig.PdfDocument collides with OxidizePdf.NET.PdfDocument,
// which is in scope via the parent namespace.
using PigDocument = UglyToad.PdfPig.PdfDocument;

namespace OxidizePdf.NET.Benchmarks.Adapters;

/// <summary>Drives UglyToad.PdfPig.</summary>
public sealed class PdfPigAdapter : IPdfExtractorAdapter
{
    public string Name => "PdfPig";
    public string License => "MIT";

    public string Version =>
        typeof(PigDocument).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(PigDocument).Assembly.GetName().Version?.ToString()
        ?? "unknown";

    public ExtractResult Extract(byte[] pdfBytes)
    {
        using var doc = PigDocument.Open(pdfBytes);
        var sb = new StringBuilder();
        foreach (var page in doc.GetPages())
        {
            sb.Append(page.Text);
        }
        return new ExtractResult(doc.NumberOfPages, sb.ToString());
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test dotnet/OxidizePdf.NET.Benchmarks.Tests --filter PdfPigAdapter_ExtractsRealTextFromSamplePdf`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add dotnet/OxidizePdf.NET.Benchmarks/Adapters/PdfPigAdapter.cs dotnet/OxidizePdf.NET.Benchmarks.Tests/AdapterTests.cs
git commit -m "feat(benchmarks): add PdfPigAdapter (#4)"
```

---

### Task 3: IText7Adapter

**Files:**
- Create: `dotnet/OxidizePdf.NET.Benchmarks/Adapters/IText7Adapter.cs`
- Modify: `dotnet/OxidizePdf.NET.Benchmarks.Tests/AdapterTests.cs`

**Interfaces:**
- Consumes: `IPdfExtractorAdapter`, `ExtractResult` (Task 1).
- Produces: `public sealed class IText7Adapter : IPdfExtractorAdapter` — `Name == "iText7"`, `License == "AGPL"`.

- [ ] **Step 1: Write the failing test**

Add to `dotnet/OxidizePdf.NET.Benchmarks.Tests/AdapterTests.cs`:

```csharp
    [Fact]
    public void IText7Adapter_ExtractsRealTextFromSamplePdf()
    {
        var adapter = new IText7Adapter();
        var result = adapter.Extract(SamplePdf());

        Assert.Equal("iText7", adapter.Name);
        Assert.Equal("AGPL", adapter.License);
        Assert.False(string.IsNullOrEmpty(adapter.Version));
        Assert.True(result.PageCount >= 1);
        Assert.Contains("SEVILLA", result.Text);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test dotnet/OxidizePdf.NET.Benchmarks.Tests --filter IText7Adapter_ExtractsRealTextFromSamplePdf`
Expected: FAIL — `IText7Adapter` does not exist.

- [ ] **Step 3: Implement the IText7Adapter**

Create `dotnet/OxidizePdf.NET.Benchmarks/Adapters/IText7Adapter.cs`:

```csharp
using System.Reflection;
using System.Text;
using iText.Kernel.Pdf.Canvas.Parser;
// Aliases: iText's PdfReader / PdfDocument collide with OxidizePdf.NET types
// in scope via the parent namespace.
using ITextReader = iText.Kernel.Pdf.PdfReader;
using ITextDocument = iText.Kernel.Pdf.PdfDocument;

namespace OxidizePdf.NET.Benchmarks.Adapters;

/// <summary>Drives iText7 (itext7). AGPL-licensed; dev-only dependency.</summary>
public sealed class IText7Adapter : IPdfExtractorAdapter
{
    public string Name => "iText7";
    public string License => "AGPL";

    public string Version =>
        typeof(ITextDocument).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(ITextDocument).Assembly.GetName().Version?.ToString()
        ?? "unknown";

    public ExtractResult Extract(byte[] pdfBytes)
    {
        using var stream = new MemoryStream(pdfBytes);
        using var reader = new ITextReader(stream);
        using var pdf = new ITextDocument(reader);

        int pageCount = pdf.GetNumberOfPages();
        var sb = new StringBuilder();
        for (int i = 1; i <= pageCount; i++)
        {
            // pdf.GetPage(i) returns an iText PdfPage; passed directly so the
            // type name never needs to be written (avoids the PdfPage clash).
            sb.Append(PdfTextExtractor.GetTextFromPage(pdf.GetPage(i)));
        }
        return new ExtractResult(pageCount, sb.ToString());
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test dotnet/OxidizePdf.NET.Benchmarks.Tests --filter IText7Adapter_ExtractsRealTextFromSamplePdf`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add dotnet/OxidizePdf.NET.Benchmarks/Adapters/IText7Adapter.cs dotnet/OxidizePdf.NET.Benchmarks.Tests/AdapterTests.cs
git commit -m "feat(benchmarks): add IText7Adapter (#4)"
```

---

### Task 4: DocnetAdapter

**Files:**
- Create: `dotnet/OxidizePdf.NET.Benchmarks/Adapters/DocnetAdapter.cs`
- Modify: `dotnet/OxidizePdf.NET.Benchmarks.Tests/AdapterTests.cs`

**Interfaces:**
- Consumes: `IPdfExtractorAdapter`, `ExtractResult` (Task 1).
- Produces: `public sealed class DocnetAdapter : IPdfExtractorAdapter` — `Name == "Docnet.Core"`, `License == "MIT (PDFium BSD)"`.

- [ ] **Step 1: Write the failing test**

Add to `dotnet/OxidizePdf.NET.Benchmarks.Tests/AdapterTests.cs`:

```csharp
    [Fact]
    public void DocnetAdapter_ExtractsRealTextFromSamplePdf()
    {
        var adapter = new DocnetAdapter();
        var result = adapter.Extract(SamplePdf());

        Assert.Equal("Docnet.Core", adapter.Name);
        Assert.False(string.IsNullOrEmpty(adapter.Version));
        Assert.True(result.PageCount >= 1);
        Assert.Contains("SEVILLA", result.Text);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test dotnet/OxidizePdf.NET.Benchmarks.Tests --filter DocnetAdapter_ExtractsRealTextFromSamplePdf`
Expected: FAIL — `DocnetAdapter` does not exist.

- [ ] **Step 3: Implement the DocnetAdapter**

Create `dotnet/OxidizePdf.NET.Benchmarks/Adapters/DocnetAdapter.cs`:

```csharp
using System.Reflection;
using System.Text;
using Docnet.Core;
using Docnet.Core.Models;

namespace OxidizePdf.NET.Benchmarks.Adapters;

/// <summary>Drives Docnet.Core (a PDFium native wrapper).</summary>
public sealed class DocnetAdapter : IPdfExtractorAdapter
{
    public string Name => "Docnet.Core";
    public string License => "MIT (PDFium BSD)";

    public string Version =>
        typeof(DocLib).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(DocLib).Assembly.GetName().Version?.ToString()
        ?? "unknown";

    public ExtractResult Extract(byte[] pdfBytes)
    {
        // DocLib.Instance is a process-wide PDFium singleton — never disposed here.
        // Dimensions are required by the API but irrelevant to text extraction.
        using var docReader = DocLib.Instance.GetDocReader(pdfBytes, new PageDimensions(1080, 1920));
        int pageCount = docReader.GetPageCount();
        var sb = new StringBuilder();
        for (int i = 0; i < pageCount; i++)
        {
            using var pageReader = docReader.GetPageReader(i);
            sb.Append(pageReader.GetText());
        }
        return new ExtractResult(pageCount, sb.ToString());
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test dotnet/OxidizePdf.NET.Benchmarks.Tests --filter DocnetAdapter_ExtractsRealTextFromSamplePdf`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add dotnet/OxidizePdf.NET.Benchmarks/Adapters/DocnetAdapter.cs dotnet/OxidizePdf.NET.Benchmarks.Tests/AdapterTests.cs
git commit -m "feat(benchmarks): add DocnetAdapter (#4)"
```

> **Risk note (adapters):** the per-adapter assertion `Contains("SEVILLA")` was verified against the real `sample.pdf` using OxidizePdf.NET's extractor (8 pages, 12,310 chars; "SEVILLA" is a contiguous uppercase token). If a competitor extracts that token with interleaved spacing and the assertion fails, that is a genuine cross-library difference to record — do NOT weaken the assertion to "non-empty". Instead, pick another verified contiguous uppercase token from the document ("SOCIEDADES", "MINISTRO", "PRIETO") that the failing library does produce, and note the discrepancy in the task's commit message.

---

### Task 5: FileResult, ExtractStatus, and BenchmarkRunner

**Files:**
- Create: `dotnet/OxidizePdf.NET.Benchmarks/FileResult.cs`
- Create: `dotnet/OxidizePdf.NET.Benchmarks/BenchmarkRunner.cs`
- Create: `dotnet/OxidizePdf.NET.Benchmarks.Tests/BenchmarkRunnerTests.cs`

**Interfaces:**
- Consumes: `IPdfExtractorAdapter`, `ExtractResult` (Task 1).
- Produces:
  - `public enum ExtractStatus { Ok, Empty, Error, Timeout }`
  - `public sealed record FileResult(string Adapter, string File, int PageCount, long ElapsedMs, ExtractStatus Status, int TextLength, string? ErrorType);`
  - `public sealed class BenchmarkRunner` with `BenchmarkRunner(IReadOnlyList<IPdfExtractorAdapter> adapters, TimeSpan timeout)`, `FileResult RunOne(IPdfExtractorAdapter adapter, string file, byte[] pdfBytes)`, `List<FileResult> Run(IReadOnlyList<string> files)`.

- [ ] **Step 1: Create FileResult and ExtractStatus**

Create `dotnet/OxidizePdf.NET.Benchmarks/FileResult.cs`:

```csharp
namespace OxidizePdf.NET.Benchmarks;

/// <summary>Outcome of one extraction attempt.</summary>
public enum ExtractStatus
{
    /// <summary>Succeeded with non-empty text.</summary>
    Ok,
    /// <summary>Succeeded but produced zero characters (silent-failure mode).</summary>
    Empty,
    /// <summary>Threw an exception.</summary>
    Error,
    /// <summary>Exceeded the per-file timeout.</summary>
    Timeout,
}

/// <summary>One (adapter, pdf) measurement.</summary>
public sealed record FileResult(
    string Adapter,
    string File,
    int PageCount,
    long ElapsedMs,
    ExtractStatus Status,
    int TextLength,
    string? ErrorType);
```

- [ ] **Step 2: Write the failing tests**

Create `dotnet/OxidizePdf.NET.Benchmarks.Tests/BenchmarkRunnerTests.cs`:

```csharp
using OxidizePdf.NET.Benchmarks;

namespace OxidizePdf.NET.Benchmarks.Tests;

public class BenchmarkRunnerTests
{
    private sealed class FakeAdapter : IPdfExtractorAdapter
    {
        private readonly Func<byte[], ExtractResult> _fn;
        public FakeAdapter(string name, Func<byte[], ExtractResult> fn) { Name = name; _fn = fn; }
        public string Name { get; }
        public string License => "test";
        public string Version => "0.0.0";
        public ExtractResult Extract(byte[] pdfBytes) => _fn(pdfBytes);
    }

    private static readonly byte[] Dummy = new byte[] { 1, 2, 3 };

    [Fact]
    public void RunOne_Ok_WhenAdapterReturnsText()
    {
        var runner = new BenchmarkRunner(
            new[] { new FakeAdapter("A", _ => new ExtractResult(3, "hello")) },
            TimeSpan.FromSeconds(5));

        var r = runner.RunOne(runner.Adapters[0], "x.pdf", Dummy);

        Assert.Equal(ExtractStatus.Ok, r.Status);
        Assert.Equal(3, r.PageCount);
        Assert.Equal(5, r.TextLength);
        Assert.Null(r.ErrorType);
    }

    [Fact]
    public void RunOne_Empty_WhenAdapterReturnsZeroLengthText()
    {
        var runner = new BenchmarkRunner(
            new[] { new FakeAdapter("A", _ => new ExtractResult(2, "")) },
            TimeSpan.FromSeconds(5));

        var r = runner.RunOne(runner.Adapters[0], "x.pdf", Dummy);

        Assert.Equal(ExtractStatus.Empty, r.Status);
        Assert.Equal(0, r.TextLength);
    }

    [Fact]
    public void RunOne_Error_RecordsExceptionType_AndDoesNotThrow()
    {
        var runner = new BenchmarkRunner(
            new[] { new FakeAdapter("A", _ => throw new InvalidOperationException("boom")) },
            TimeSpan.FromSeconds(5));

        var r = runner.RunOne(runner.Adapters[0], "x.pdf", Dummy);

        Assert.Equal(ExtractStatus.Error, r.Status);
        Assert.Equal(nameof(InvalidOperationException), r.ErrorType);
    }

    [Fact]
    public void RunOne_Timeout_WhenAdapterExceedsBudget()
    {
        var runner = new BenchmarkRunner(
            new[] { new FakeAdapter("A", _ => { Thread.Sleep(2000); return new ExtractResult(1, "late"); }) },
            TimeSpan.FromMilliseconds(200));

        var r = runner.RunOne(runner.Adapters[0], "x.pdf", Dummy);

        Assert.Equal(ExtractStatus.Timeout, r.Status);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test dotnet/OxidizePdf.NET.Benchmarks.Tests --filter BenchmarkRunnerTests`
Expected: FAIL — `BenchmarkRunner` does not exist.

- [ ] **Step 4: Implement the BenchmarkRunner**

Create `dotnet/OxidizePdf.NET.Benchmarks/BenchmarkRunner.cs`:

```csharp
using System.Diagnostics;

namespace OxidizePdf.NET.Benchmarks;

/// <summary>
/// Runs every adapter over every file, isolating per-file timeouts and
/// exceptions so a single bad PDF never aborts the run.
/// </summary>
public sealed class BenchmarkRunner
{
    private readonly TimeSpan _timeout;

    public BenchmarkRunner(IReadOnlyList<IPdfExtractorAdapter> adapters, TimeSpan timeout)
    {
        Adapters = adapters;
        _timeout = timeout;
    }

    public IReadOnlyList<IPdfExtractorAdapter> Adapters { get; }

    /// <summary>Run one (adapter, file). Never throws; failures become statuses.</summary>
    public FileResult RunOne(IPdfExtractorAdapter adapter, string file, byte[] pdfBytes)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // A synchronous native call cannot be hard-aborted; run it on a
            // background task and stop waiting after the budget. The task may
            // linger, but the run continues — that is the spec's contract.
            var task = Task.Run(() => adapter.Extract(pdfBytes));
            if (!task.Wait(_timeout))
            {
                sw.Stop();
                return new FileResult(adapter.Name, file, 0, sw.ElapsedMilliseconds,
                    ExtractStatus.Timeout, 0, null);
            }

            sw.Stop();
            var result = task.Result;
            var status = result.Text.Length == 0 ? ExtractStatus.Empty : ExtractStatus.Ok;
            return new FileResult(adapter.Name, file, result.PageCount, sw.ElapsedMilliseconds,
                status, result.Text.Length, null);
        }
        catch (Exception ex)
        {
            sw.Stop();
            var inner = ex is AggregateException ae && ae.InnerException is not null
                ? ae.InnerException
                : ex;
            return new FileResult(adapter.Name, file, 0, sw.ElapsedMilliseconds,
                ExtractStatus.Error, 0, inner.GetType().Name);
        }
    }

    /// <summary>Run all adapters over all files, in order.</summary>
    public List<FileResult> Run(IReadOnlyList<string> files)
    {
        var results = new List<FileResult>(files.Count * Adapters.Count);
        foreach (var file in files)
        {
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(file);
            }
            catch (Exception ex)
            {
                // Unreadable file: record an Error for every adapter, keep going.
                foreach (var adapter in Adapters)
                {
                    results.Add(new FileResult(adapter.Name, file, 0, 0,
                        ExtractStatus.Error, 0, ex.GetType().Name));
                }
                continue;
            }

            foreach (var adapter in Adapters)
            {
                results.Add(RunOne(adapter, file, bytes));
            }
        }
        return results;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test dotnet/OxidizePdf.NET.Benchmarks.Tests --filter BenchmarkRunnerTests`
Expected: PASS (4 passed).

- [ ] **Step 6: Commit**

```bash
git add dotnet/OxidizePdf.NET.Benchmarks/FileResult.cs dotnet/OxidizePdf.NET.Benchmarks/BenchmarkRunner.cs dotnet/OxidizePdf.NET.Benchmarks.Tests/BenchmarkRunnerTests.cs
git commit -m "feat(benchmarks): BenchmarkRunner with timeout + error isolation (#4)"
```

---

### Task 6: ResultsAggregator (the honesty safeguard)

**Files:**
- Create: `dotnet/OxidizePdf.NET.Benchmarks/ResultsAggregator.cs`
- Create: `dotnet/OxidizePdf.NET.Benchmarks.Tests/ResultsAggregatorTests.cs`

**Interfaces:**
- Consumes: `FileResult`, `ExtractStatus` (Task 5).
- Produces:
  - `public sealed record SpeedMetric(string Adapter, double MedianMsPerPage, double PdfsPerSec, int SampleSize);`
  - `public sealed record RobustnessMetric(string Adapter, int Total, int Ok, int Empty, int Error, int Timeout);`
  - `public sealed record Aggregates(IReadOnlyList<string> CommonSuccessFiles, IReadOnlyList<SpeedMetric> Speed, IReadOnlyList<RobustnessMetric> Robustness);`
  - `public static class ResultsAggregator { public static Aggregates Aggregate(IReadOnlyList<FileResult> results, string referenceAdapter); }`

- [ ] **Step 1: Write the failing tests**

Create `dotnet/OxidizePdf.NET.Benchmarks.Tests/ResultsAggregatorTests.cs`:

```csharp
using OxidizePdf.NET.Benchmarks;

namespace OxidizePdf.NET.Benchmarks.Tests;

public class ResultsAggregatorTests
{
    private static FileResult Ok(string adapter, string file, int pages, long ms) =>
        new(adapter, file, pages, ms, ExtractStatus.Ok, 100, null);

    private static FileResult Bad(string adapter, string file, ExtractStatus status) =>
        new(adapter, file, 0, 0, status, 0, status == ExtractStatus.Error ? "X" : null);

    [Fact]
    public void CommonSuccess_IsIntersectionOfOkFilesAcrossAllAdapters()
    {
        // A and B both Ok on f1. A Ok on f2 but B Errored -> f2 excluded.
        var results = new List<FileResult>
        {
            Ok("A", "f1.pdf", 10, 100), Ok("B", "f1.pdf", 10, 200),
            Ok("A", "f2.pdf", 10, 100), Bad("B", "f2.pdf", ExtractStatus.Error),
        };

        var agg = ResultsAggregator.Aggregate(results, referenceAdapter: "B");

        Assert.Equal(new[] { "f1.pdf" }, agg.CommonSuccessFiles);
    }

    [Fact]
    public void Speed_IsComputedOnlyOverCommonSubset_UsingReferencePageCount()
    {
        // f1 common (ref B says 10 pages). f2 not common (B failed).
        // A on f1 took 100ms -> 10 ms/page. A's f2 time must NOT count.
        var results = new List<FileResult>
        {
            Ok("A", "f1.pdf", 999, 100), Ok("B", "f1.pdf", 10, 200),
            Ok("A", "f2.pdf", 999, 5),   Bad("B", "f2.pdf", ExtractStatus.Timeout),
        };

        var agg = ResultsAggregator.Aggregate(results, referenceAdapter: "B");
        var speedA = agg.Speed.Single(s => s.Adapter == "A");

        Assert.Equal(1, speedA.SampleSize);           // only f1
        Assert.Equal(10.0, speedA.MedianMsPerPage);   // 100ms / 10 ref pages
    }

    [Fact]
    public void MedianMsPerPage_IsTrueMedianOverCommonFiles()
    {
        // Three common files; ref C = 1 page each so ms/page == ms.
        var results = new List<FileResult>
        {
            Ok("A", "f1.pdf", 1, 10), Ok("A", "f2.pdf", 1, 50), Ok("A", "f3.pdf", 1, 90),
            Ok("C", "f1.pdf", 1, 1),  Ok("C", "f2.pdf", 1, 1),  Ok("C", "f3.pdf", 1, 1),
        };

        var agg = ResultsAggregator.Aggregate(results, referenceAdapter: "C");
        var speedA = agg.Speed.Single(s => s.Adapter == "A");

        Assert.Equal(50.0, speedA.MedianMsPerPage);   // median of {10,50,90}
    }

    [Fact]
    public void Robustness_IsOverFullCorpus_PerAdapter()
    {
        var results = new List<FileResult>
        {
            Ok("A", "f1.pdf", 1, 10), Ok("A", "f2.pdf", 1, 10),
            Bad("A", "f3.pdf", ExtractStatus.Empty), Bad("A", "f4.pdf", ExtractStatus.Error),
            Ok("B", "f1.pdf", 1, 10), Bad("B", "f2.pdf", ExtractStatus.Timeout),
            Ok("B", "f3.pdf", 1, 10), Ok("B", "f4.pdf", 1, 10),
        };

        var agg = ResultsAggregator.Aggregate(results, referenceAdapter: "B");
        var robA = agg.Robustness.Single(r => r.Adapter == "A");
        var robB = agg.Robustness.Single(r => r.Adapter == "B");

        Assert.Equal(4, robA.Total);
        Assert.Equal(2, robA.Ok);
        Assert.Equal(1, robA.Empty);
        Assert.Equal(1, robA.Error);
        Assert.Equal(0, robA.Timeout);

        Assert.Equal(4, robB.Total);
        Assert.Equal(3, robB.Ok);
        Assert.Equal(1, robB.Timeout);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test dotnet/OxidizePdf.NET.Benchmarks.Tests --filter ResultsAggregatorTests`
Expected: FAIL — `ResultsAggregator` does not exist.

- [ ] **Step 3: Implement the ResultsAggregator**

Create `dotnet/OxidizePdf.NET.Benchmarks/ResultsAggregator.cs`:

```csharp
namespace OxidizePdf.NET.Benchmarks;

/// <summary>Speed for one adapter, measured over the common-success subset only.</summary>
public sealed record SpeedMetric(string Adapter, double MedianMsPerPage, double PdfsPerSec, int SampleSize);

/// <summary>Robustness for one adapter, over the full corpus.</summary>
public sealed record RobustnessMetric(string Adapter, int Total, int Ok, int Empty, int Error, int Timeout);

/// <summary>The reported aggregates.</summary>
public sealed record Aggregates(
    IReadOnlyList<string> CommonSuccessFiles,
    IReadOnlyList<SpeedMetric> Speed,
    IReadOnlyList<RobustnessMetric> Robustness);

/// <summary>
/// Turns raw <see cref="FileResult"/>s into reported metrics. Speed is computed
/// ONLY over the set of files every adapter extracted with status Ok, using the
/// reference adapter's page count as the denominator. Robustness is over all files.
/// </summary>
public static class ResultsAggregator
{
    public static Aggregates Aggregate(IReadOnlyList<FileResult> results, string referenceAdapter)
    {
        var adapters = results.Select(r => r.Adapter).Distinct().ToList();
        var allFiles = results.Select(r => r.File).Distinct().ToList();

        // Common-success = files where EVERY adapter has status Ok.
        var okByFile = results
            .Where(r => r.Status == ExtractStatus.Ok)
            .GroupBy(r => r.File)
            .ToDictionary(g => g.Key, g => g.Select(r => r.Adapter).ToHashSet());

        var commonFiles = allFiles
            .Where(f => okByFile.TryGetValue(f, out var oks) && oks.Count == adapters.Count)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        // Reference page count per common file (neutral denominator).
        var refPages = results
            .Where(r => r.Adapter == referenceAdapter && r.Status == ExtractStatus.Ok)
            .GroupBy(r => r.File)
            .ToDictionary(g => g.Key, g => g.First().PageCount);

        var lookup = results.ToDictionary(r => (r.Adapter, r.File));

        var speed = new List<SpeedMetric>();
        foreach (var adapter in adapters)
        {
            var msPerPage = new List<double>();
            long totalMs = 0;
            foreach (var file in commonFiles)
            {
                var fr = lookup[(adapter, file)];
                int pages = refPages.TryGetValue(file, out var p) && p > 0 ? p : 1;
                msPerPage.Add((double)fr.ElapsedMs / pages);
                totalMs += fr.ElapsedMs;
            }

            double median = Median(msPerPage);
            double pdfsPerSec = totalMs > 0 ? commonFiles.Count / (totalMs / 1000.0) : 0.0;
            speed.Add(new SpeedMetric(adapter, median, pdfsPerSec, commonFiles.Count));
        }

        var robustness = new List<RobustnessMetric>();
        foreach (var adapter in adapters)
        {
            var rows = results.Where(r => r.Adapter == adapter).ToList();
            robustness.Add(new RobustnessMetric(
                adapter,
                Total: rows.Count,
                Ok: rows.Count(r => r.Status == ExtractStatus.Ok),
                Empty: rows.Count(r => r.Status == ExtractStatus.Empty),
                Error: rows.Count(r => r.Status == ExtractStatus.Error),
                Timeout: rows.Count(r => r.Status == ExtractStatus.Timeout)));
        }

        return new Aggregates(commonFiles, speed, robustness);
    }

    private static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0) return 0.0;
        var sorted = values.OrderBy(v => v).ToList();
        int mid = sorted.Count / 2;
        return sorted.Count % 2 == 1
            ? sorted[mid]
            : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test dotnet/OxidizePdf.NET.Benchmarks.Tests --filter ResultsAggregatorTests`
Expected: PASS (4 passed).

- [ ] **Step 5: Commit**

```bash
git add dotnet/OxidizePdf.NET.Benchmarks/ResultsAggregator.cs dotnet/OxidizePdf.NET.Benchmarks.Tests/ResultsAggregatorTests.cs
git commit -m "feat(benchmarks): ResultsAggregator with common-success honesty safeguard (#4)"
```

---

### Task 7: EnvironmentInfo and report writers

**Files:**
- Create: `dotnet/OxidizePdf.NET.Benchmarks/EnvironmentInfo.cs`
- Create: `dotnet/OxidizePdf.NET.Benchmarks/Reporting/JsonReportWriter.cs`
- Create: `dotnet/OxidizePdf.NET.Benchmarks/Reporting/MarkdownReportWriter.cs`
- Create: `dotnet/OxidizePdf.NET.Benchmarks.Tests/ReportWriterTests.cs`

**Interfaces:**
- Consumes: `FileResult` (Task 5); `Aggregates`, `SpeedMetric`, `RobustnessMetric` (Task 6); `IPdfExtractorAdapter` (Task 1).
- Produces:
  - `public sealed record AdapterInfo(string Name, string License, string Version);`
  - `public sealed record EnvironmentInfo(string Machine, string Os, string DotnetVersion, string CorpusPath, int FileCount, IReadOnlyList<AdapterInfo> Adapters);` with `static EnvironmentInfo Capture(string corpusPath, int fileCount, IReadOnlyList<IPdfExtractorAdapter> adapters)`.
  - `public static class JsonReportWriter { public static void Write(string path, EnvironmentInfo env, IReadOnlyList<FileResult> results, Aggregates aggregates); }`
  - `public static class MarkdownReportWriter { public static string Render(EnvironmentInfo env, Aggregates aggregates); public static void Write(string path, EnvironmentInfo env, Aggregates aggregates); }`

- [ ] **Step 1: Create EnvironmentInfo**

Create `dotnet/OxidizePdf.NET.Benchmarks/EnvironmentInfo.cs`:

```csharp
using System.Runtime.InteropServices;

namespace OxidizePdf.NET.Benchmarks;

/// <summary>One library's identity in the environment block.</summary>
public sealed record AdapterInfo(string Name, string License, string Version);

/// <summary>Reproducibility context written into results.json.</summary>
public sealed record EnvironmentInfo(
    string Machine,
    string Os,
    string DotnetVersion,
    string CorpusPath,
    int FileCount,
    IReadOnlyList<AdapterInfo> Adapters)
{
    public static EnvironmentInfo Capture(
        string corpusPath, int fileCount, IReadOnlyList<IPdfExtractorAdapter> adapters)
    {
        return new EnvironmentInfo(
            Machine: Environment.MachineName,
            Os: RuntimeInformation.OSDescription,
            DotnetVersion: RuntimeInformation.FrameworkDescription,
            CorpusPath: corpusPath,
            FileCount: fileCount,
            Adapters: adapters.Select(a => new AdapterInfo(a.Name, a.License, a.Version)).ToList());
    }
}
```

- [ ] **Step 2: Write the failing tests**

Create `dotnet/OxidizePdf.NET.Benchmarks.Tests/ReportWriterTests.cs`:

```csharp
using System.Text.Json;
using OxidizePdf.NET.Benchmarks;
using OxidizePdf.NET.Benchmarks.Reporting;

namespace OxidizePdf.NET.Benchmarks.Tests;

public class ReportWriterTests
{
    private static (EnvironmentInfo env, List<FileResult> results, Aggregates agg) Sample()
    {
        var env = new EnvironmentInfo(
            "host", "linux", "net8.0", "/corpus", 2,
            new[] { new AdapterInfo("OxidizePdf.NET", "MIT", "0.16.1") });
        var results = new List<FileResult>
        {
            new("OxidizePdf.NET", "f1.pdf", 1, 10, ExtractStatus.Ok, 100, null),
        };
        var agg = new Aggregates(
            new[] { "f1.pdf" },
            new[] { new SpeedMetric("OxidizePdf.NET", 10.0, 100.0, 1) },
            new[] { new RobustnessMetric("OxidizePdf.NET", 2, 1, 0, 1, 0) });
        return (env, results, agg);
    }

    [Fact]
    public void Json_RoundTripsEnvironmentResultsAndAggregates()
    {
        var (env, results, agg) = Sample();
        var path = Path.Combine(Path.GetTempPath(), $"bench-{Guid.NewGuid():N}.json");
        try
        {
            JsonReportWriter.Write(path, env, results, agg);
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;

            Assert.Equal("host", root.GetProperty("environment").GetProperty("machine").GetString());
            Assert.Equal(1, root.GetProperty("results").GetArrayLength());
            Assert.Equal("f1.pdf",
                root.GetProperty("aggregates").GetProperty("commonSuccessFiles")[0].GetString());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Markdown_ContainsSpeedRobustnessAndCapabilityTables()
    {
        var (env, _, agg) = Sample();
        string md = MarkdownReportWriter.Render(env, agg);

        Assert.Contains("## Speed", md);
        Assert.Contains("common-success subset", md);
        Assert.Contains("## Robustness", md);
        Assert.Contains("## Capability matrix", md);
        Assert.Contains("OxidizePdf.NET", md);
        // The speed table must state the subset size so the number is honest.
        Assert.Contains("1 of 2", md);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test dotnet/OxidizePdf.NET.Benchmarks.Tests --filter ReportWriterTests`
Expected: FAIL — `JsonReportWriter` / `MarkdownReportWriter` do not exist.

- [ ] **Step 4: Implement the JSON writer**

Create `dotnet/OxidizePdf.NET.Benchmarks/Reporting/JsonReportWriter.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OxidizePdf.NET.Benchmarks.Reporting;

/// <summary>Writes the raw, auditable record: env + every FileResult + aggregates.</summary>
public static class JsonReportWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static void Write(
        string path, EnvironmentInfo env, IReadOnlyList<FileResult> results, Aggregates aggregates)
    {
        var payload = new
        {
            environment = env,
            results,
            aggregates,
        };
        File.WriteAllText(path, JsonSerializer.Serialize(payload, Options));
    }
}
```

- [ ] **Step 5: Implement the Markdown writer**

Create `dotnet/OxidizePdf.NET.Benchmarks/Reporting/MarkdownReportWriter.cs`:

```csharp
using System.Globalization;
using System.Text;

namespace OxidizePdf.NET.Benchmarks.Reporting;

/// <summary>Renders the human-readable summary: speed + robustness + capability matrix.</summary>
public static class MarkdownReportWriter
{
    public static void Write(string path, EnvironmentInfo env, Aggregates aggregates) =>
        File.WriteAllText(path, Render(env, aggregates));

    public static string Render(EnvironmentInfo env, Aggregates aggregates)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# PDF Extraction Benchmark Results");
        sb.AppendLine();
        sb.AppendLine("## Environment");
        sb.AppendLine();
        sb.AppendLine($"- Machine: {env.Machine}");
        sb.AppendLine($"- OS: {env.Os}");
        sb.AppendLine($"- Runtime: {env.DotnetVersion}");
        sb.AppendLine($"- Corpus: `{env.CorpusPath}` ({env.FileCount} PDFs)");
        sb.AppendLine();
        foreach (var a in env.Adapters)
        {
            sb.AppendLine($"  - {a.Name} {a.Version} ({a.License})");
        }
        sb.AppendLine();

        int subset = aggregates.CommonSuccessFiles.Count;
        sb.AppendLine("## Speed");
        sb.AppendLine();
        sb.AppendLine($"Measured on the **common-success subset**: {subset} of {env.FileCount} "
            + "PDFs every library parsed with status Ok. ms/page uses the reference page count.");
        sb.AppendLine();
        sb.AppendLine("| Library | Median ms/page | PDFs/sec | Sample |");
        sb.AppendLine("|---|---:|---:|---:|");
        foreach (var s in aggregates.Speed)
        {
            sb.AppendLine($"| {s.Adapter} | {Fmt(s.MedianMsPerPage)} | {Fmt(s.PdfsPerSec)} | {s.SampleSize} |");
        }
        sb.AppendLine();

        sb.AppendLine("## Robustness");
        sb.AppendLine();
        sb.AppendLine($"Over the full corpus ({env.FileCount} PDFs), per library.");
        sb.AppendLine();
        sb.AppendLine("| Library | Total | % Ok | % Empty | % Error | % Timeout |");
        sb.AppendLine("|---|---:|---:|---:|---:|---:|");
        foreach (var r in aggregates.Robustness)
        {
            sb.AppendLine($"| {r.Adapter} | {r.Total} | {Pct(r.Ok, r.Total)} | {Pct(r.Empty, r.Total)} "
                + $"| {Pct(r.Error, r.Total)} | {Pct(r.Timeout, r.Total)} |");
        }
        sb.AppendLine();

        sb.AppendLine(CapabilityMatrix);
        return sb.ToString();
    }

    private static string Fmt(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Pct(int n, int total) =>
        total == 0 ? "-" : ((double)n / total * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";

    // Hand-authored qualitative comparison — NOT a computed score. Edit by hand
    // when capabilities change. Makes no quantitative quality claim.
    private const string CapabilityMatrix = """
        ## Capability matrix

        Qualitative feature comparison (hand-authored, not measured).

        | Capability | OxidizePdf.NET | PdfPig | iText7 | Docnet.Core |
        |---|:---:|:---:|:---:|:---:|
        | Plain text extraction | ✓ | ✓ | ✓ | ✓ |
        | Heading detection | ✓ | ✗ | ✗ | ✗ |
        | Table extraction | ✓ | ✗ | ✗ | ✗ |
        | Reading order / multi-column | ✓ | ✗ | ✗ | ✗ |
        | RAG chunking with page citations | ✓ | ✗ | ✗ | ✗ |
        """;
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test dotnet/OxidizePdf.NET.Benchmarks.Tests --filter ReportWriterTests`
Expected: PASS (2 passed).

- [ ] **Step 7: Commit**

```bash
git add dotnet/OxidizePdf.NET.Benchmarks/EnvironmentInfo.cs dotnet/OxidizePdf.NET.Benchmarks/Reporting dotnet/OxidizePdf.NET.Benchmarks.Tests/ReportWriterTests.cs
git commit -m "feat(benchmarks): environment block + JSON/Markdown report writers (#4)"
```

---

### Task 8: Program CLI wiring and manual run

**Files:**
- Modify: `dotnet/OxidizePdf.NET.Benchmarks/Program.cs` (replace the Task 1 stub)

**Interfaces:**
- Consumes: every type from Tasks 1, 5, 6, 7 (adapters, `BenchmarkRunner`, `ResultsAggregator`, `EnvironmentInfo`, both report writers).
- Produces: a runnable CLI. No new public types.

- [ ] **Step 1: Replace Program.cs with the CLI**

Replace the contents of `dotnet/OxidizePdf.NET.Benchmarks/Program.cs`:

```csharp
using OxidizePdf.NET.Benchmarks;
using OxidizePdf.NET.Benchmarks.Adapters;
using OxidizePdf.NET.Benchmarks.Reporting;

// --- Parse args: --corpus <dir> --timeout <seconds> --out <dir> ---
string? corpus = null;
int timeoutSeconds = 30;
string outDir = ".";

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--corpus" when i + 1 < args.Length:
            corpus = args[++i];
            break;
        case "--timeout" when i + 1 < args.Length:
            if (!int.TryParse(args[++i], out timeoutSeconds) || timeoutSeconds <= 0)
            {
                Console.Error.WriteLine("--timeout must be a positive integer (seconds).");
                return 2;
            }
            break;
        case "--out" when i + 1 < args.Length:
            outDir = args[++i];
            break;
        default:
            Console.Error.WriteLine($"Unknown or incomplete argument: {args[i]}");
            Console.Error.WriteLine("Usage: --corpus <dir> [--timeout <seconds>] [--out <dir>]");
            return 2;
    }
}

// --- Fail fast on a bad corpus before any work ---
if (string.IsNullOrWhiteSpace(corpus))
{
    Console.Error.WriteLine("--corpus <dir> is required (e.g. --corpus ../fixtures).");
    return 2;
}
if (!Directory.Exists(corpus))
{
    Console.Error.WriteLine($"Corpus directory not found: {corpus}");
    return 2;
}

var files = Directory.EnumerateFiles(corpus, "*.pdf", SearchOption.AllDirectories)
    .OrderBy(f => f, StringComparer.Ordinal)
    .ToList();

if (files.Count == 0)
{
    Console.Error.WriteLine($"No *.pdf files found under: {corpus}");
    return 2;
}

Directory.CreateDirectory(outDir);

// --- Adapters. PdfPig is the reference page-count provider (neutral third party). ---
var adapters = new IPdfExtractorAdapter[]
{
    new OxidizeAdapter(),
    new PdfPigAdapter(),
    new IText7Adapter(),
    new DocnetAdapter(),
};
const string referenceAdapter = "PdfPig";

Console.WriteLine($"Corpus: {corpus} ({files.Count} PDFs)");
Console.WriteLine($"Adapters: {string.Join(", ", adapters.Select(a => a.Name))}");
Console.WriteLine($"Per-file timeout: {timeoutSeconds}s");
Console.WriteLine("Running...");

var runner = new BenchmarkRunner(adapters, TimeSpan.FromSeconds(timeoutSeconds));

// Progress: report every 50 files so a long run is not silent.
var results = new List<FileResult>(files.Count * adapters.Length);
for (int i = 0; i < files.Count; i++)
{
    byte[] bytes;
    try { bytes = File.ReadAllBytes(files[i]); }
    catch (Exception ex)
    {
        foreach (var a in adapters)
            results.Add(new FileResult(a.Name, files[i], 0, 0, ExtractStatus.Error, 0, ex.GetType().Name));
        continue;
    }
    foreach (var a in adapters)
        results.Add(runner.RunOne(a, files[i], bytes));

    if ((i + 1) % 50 == 0 || i + 1 == files.Count)
        Console.WriteLine($"  {i + 1}/{files.Count}");
}

var aggregates = ResultsAggregator.Aggregate(results, referenceAdapter);
var env = EnvironmentInfo.Capture(Path.GetFullPath(corpus), files.Count, adapters);

string jsonPath = Path.Combine(outDir, "results.json");
string mdPath = Path.Combine(outDir, "results.md");
JsonReportWriter.Write(jsonPath, env, results, aggregates);
MarkdownReportWriter.Write(mdPath, env, aggregates);

Console.WriteLine($"Wrote {jsonPath}");
Console.WriteLine($"Wrote {mdPath}");
Console.WriteLine($"Common-success subset: {aggregates.CommonSuccessFiles.Count} of {files.Count}");
return 0;
```

- [ ] **Step 2: Build the whole solution (warnings = errors gate)**

Run: `dotnet build dotnet/OxidizePdf.NET.Benchmarks/OxidizePdf.NET.Benchmarks.csproj -c Release`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 3: Run the full test suite for the benchmark project**

Run: `dotnet test dotnet/OxidizePdf.NET.Benchmarks.Tests`
Expected: PASS — all adapter, runner, aggregator, and report tests green.

- [ ] **Step 4: Smoke-validate the CLI fails fast on a bad corpus (behavioral, not a smoke test)**

Run: `dotnet run --project dotnet/OxidizePdf.NET.Benchmarks -c Release -- --corpus /no/such/dir`
Expected: prints `Corpus directory not found: /no/such/dir` and exits non-zero.

- [ ] **Step 5: Run the harness over a small slice to verify end-to-end output**

Run:

```bash
mkdir -p /tmp/bench-slice
for f in $(ls ../fixtures/*.pdf | head -20); do cp "$f" /tmp/bench-slice/; done
dotnet run --project dotnet/OxidizePdf.NET.Benchmarks -c Release -- --corpus /tmp/bench-slice --timeout 30 --out /tmp/bench-out
```

Expected: writes `/tmp/bench-out/results.json` and `/tmp/bench-out/results.md`; prints the common-success subset size. Open `results.md` and confirm the speed table states "N of 20", the robustness table has a row per library, and the capability matrix is present.

- [ ] **Step 6: Commit**

```bash
git add dotnet/OxidizePdf.NET.Benchmarks/Program.cs
git commit -m "feat(benchmarks): CLI wiring + json/md output (#4)"
```

- [ ] **Step 7: Full run over the 802-PDF corpus (manual, the deliverable)**

Run:

```bash
dotnet run --project dotnet/OxidizePdf.NET.Benchmarks -c Release -- --corpus ../fixtures --timeout 30 --out docs/superpowers/specs/benchmark-results
```

Expected: completes without aborting (timeouts/errors recorded as statuses); produces `results.json` + `results.md`. These artifacts feed sub-project 2 (the comparative post). Review `results.md` for plausibility before using the numbers anywhere. Do NOT commit the corpus; committing the result artifacts is optional and a separate decision.

---

## Self-Review

**1. Spec coverage:**
- Competitors (PdfPig/iText7/Docnet, PDFsharp excluded) → Tasks 2–4; OxidizePdf → Task 1. ✓
- `IPdfExtractorAdapter` + `ExtractResult` exactly as specified → Task 1. ✓
- `BenchmarkRunner` with `FileResult{adapter,file,pageCount,elapsedMs,status,textLength}`, status ∈ {Ok,Error,Timeout,Empty}, per-file 30s timeout, exception isolation, Empty = succeeded-but-zero-length → Task 5. ✓ (added `ErrorType` to record the exception type the spec asks for in "Error status with the exception type recorded".)
- `ResultsAggregator` → Task 6. ✓
- Speed on common-success subset only; subset size reported; robustness over full corpus per adapter; PdfPig reference page count; environment block → Tasks 6 (compute) + 7 (env + report). ✓
- Capability matrix (hand-authored, ✓/✗, the five listed capabilities) → Task 7 constant. ✓
- `results.json` (env + all FileResults + aggregates) and `results.md` (speed + robustness + capability) → Task 7. ✓
- CLI `--corpus`/`--timeout`/`--out`, discovers `*.pdf`, fail-fast on empty/bad corpus → Task 8. ✓
- Adapter-unavailable handling: an adapter that throws at construction surfaces as a failure rather than silent omission — partially covered: construction happens in Program; if a ctor throws the run aborts. **Gap accepted as YAGNI:** all four adapters have trivial ctors (no I/O), so construction failure is not a real risk here; wrapping each `new` in try/catch would be dead defensive code. Documented here rather than implemented.
- Testing: per-adapter real-extraction (Task 1–4), aggregator intersection/subset/robustness (Task 6), timeout+error isolation (Task 5). ✓ No smoke tests.
- Out-of-scope items (no CI, no charts, no ground-truth scoring, no corpus/post publishing) → respected; Task 8 step 7 is a manual run, not CI. ✓

**2. Placeholder scan:** No TBD/TODO/"handle edge cases"/"similar to Task N". Every code step contains complete code. ✓

**3. Type consistency:** `ExtractResult(int PageCount, string Text)`, `FileResult(string,string,int,long,ExtractStatus,int,string?)`, `ExtractStatus{Ok,Empty,Error,Timeout}`, `SpeedMetric`/`RobustnessMetric`/`Aggregates`, `ResultsAggregator.Aggregate(results, referenceAdapter)`, `EnvironmentInfo.Capture(...)`, `JsonReportWriter.Write(...)`, `MarkdownReportWriter.Render/Write(...)`, adapter `Name` values ("OxidizePdf.NET", "PdfPig", "iText7", "Docnet.Core") used consistently in Program's `referenceAdapter = "PdfPig"`. ✓
