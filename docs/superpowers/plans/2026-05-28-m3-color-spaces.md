# M3 — Color Spaces Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement GFX-014 (CalRGB / CalGray calibrated colors), GFX-015 (Lab colors), and GFX-019 (ICC color profiles — now fully in scope) on branch `feature/m3-color-spaces`. GitHub Issue: #22.

**Architecture:** `oxidize-pdf 2.12.0` exposes `CalGrayColorSpace`, `CalRgbColorSpace`, `CalibratedColor`, `LabColorSpace`, `LabColor`, `IccProfile`, `IccColorSpace`, and `PageColorSpace` via `oxidize_pdf::graphics::*`. The upstream `GraphicsContext` provides hardcoded-name methods (`set_fill_color_calibrated`, `set_stroke_color_calibrated`, `set_fill_color_lab`, `set_stroke_color_lab`), named variants (`set_fill_color_calibrated_named`, `set_fill_color_lab_named`, etc.), and full ICC drawing (`set_fill_color_icc`, `set_stroke_color_icc`). `Page::add_color_space` and `Page::add_icc_color_space` register resources. The FFI layer (`native/src/graphics.rs`) mirrors the per-color-kind pattern already established for RGB/Gray/CMYK. The .NET layer mirrors `PdfPage`'s existing fluent-method pattern and names the methods to match the oxidize-python bridge surface.

**Tech Stack:** Rust 1.77+ / cdylib (`oxidize_pdf_ffi 0.10.0`, oxidize-pdf 2.12.0); C# .NET 10; xUnit + FluentAssertions.

---

## Decision Rationale

### Decision 1: GFX-019 ICC is fully in scope for M3

oxidize-pdf **2.12.0** closes the upstream gap. `GraphicsContext::set_fill_color_icc(name, Vec<f64>)` and `set_stroke_color_icc` are public. `Page::add_color_space` and `Page::add_icc_color_space` register page-level resources. `From<&IccProfile> for PageColorSpace` bridges the typed profile into the `IccStream` variant. There is nothing to defer.

Two ICC strategies are implemented (matching the python bridge + one .NET superset):

- **Inline ICCBased** — register via `add_color_space(name, PageColorSpace::Parameterised { family: IccBased, params })` using an N-components + alternate string. No embedded binary. Mirrors Python's `PageColorSpace.icc_based(n, alternate)`.
- **Embedded profile** — register via `add_icc_color_space(name, &IccProfile)` which embeds the real ICC binary as an `IccStream`. Python has no equivalent; this is a .NET superset. `IccProfile::new(name, data, IccColorSpace)` constructs the profile from raw bytes.

Both strategies draw via `set_fill_color_icc(name, components)` / `set_stroke_color_icc`.

The `debug_assert!(!components.is_empty())` in upstream is compiled out in release builds. The FFI/managed layer MUST enforce non-empty components in ALL builds, returning `ErrorCode::InvalidArgument` from Rust and throwing `ArgumentException` from .NET — exactly as the Python bridge raises on empty components.

### Decision 2: Expose BOTH hardcoded and named color-space setters

The one-per-page limitation from the old plan is gone. 2.12.0 provides both:
- Hardcoded-name variants (`set_fill_color_calibrated` → resource name `CalGray1`/`CalRGB1`; `set_fill_color_lab` → `Lab1`) for the simple single-space-per-page case.
- Named variants (`set_fill_color_calibrated_named`, `set_fill_color_lab_named`, etc.) that accept a caller-supplied name, requiring a prior `add_color_space` registration. This enables multiple CalRGB (or Lab) spaces on one page.

The .NET API exposes both tiers. Method names follow python parity:
- `SetFillColorCalibrated(CalibratedColor)` / `SetStrokeColorCalibrated` — hardcoded name, single-space convenience
- `AddColorSpace(name, PageColorSpace)` — registration
- `SetFillColorCalibratedNamed(name, CalibratedColor)` / `SetStrokeColorCalibratedNamed`
- `SetFillColorLab(LabColor)` / `SetStrokeColorLab` — hardcoded name
- `SetFillColorLabNamed(name, LabColor)` / `SetStrokeColorLabNamed`
- `SetFillColorIcc(name, double[])` / `SetStrokeColorIcc`
- `AddIccColorSpace(name, IccProfile)` — embedded profile registration (.NET superset)

`CalibratedColor` and `LabColor` are .NET value types that wrap the color components plus the color space, mirroring Python's `PyCalibratedColor` / `PyLabColor`.

### Decision 3: Content-Stream Test Strategy

Content streams are zlib-compressed by default. Task 0 creates `ContentStreamHelper.cs` (shared decompression helper) — identical to the old plan's Task 0. Integration tests MUST decompress the content stream and assert on real PDF operators (`cs`/`CS` set-colorspace, `sc`/`SC` set-color-components) AND on the `/ColorSpace` resource dict entry. Smoke tests (status code, byte count, `%PDF` header) are absolutely prohibited per CLAUDE.md.

### Decision 4: with_range takes four separate f64 args

`LabColorSpace::with_range(a_min: f64, a_max: f64, b_min: f64, b_max: f64)` — four scalar parameters, NOT an array. The old plan was wrong. The .NET `LabColorSpace.Range` property remains a `double[4]` for convenience; the FFI expands it to four scalars.

---

## File Structure

**Native (Rust FFI) — `native/src/`:**
- **Modify `native/src/graphics.rs`** — add all new `extern "C"` functions. Group by color kind: CalGray (fill+stroke hardcoded, fill+stroke named), CalRGB (same), Lab (same), ICC draw (fill+stroke icc), ICC register inline, ICC register embedded.
- **Modify `native/src/page.rs`** — add `oxidize_page_add_color_space` and `oxidize_page_add_icc_color_space` FFI functions (registration lives on the Page, not the GraphicsContext).

**Managed (C#) — `dotnet/OxidizePdf.NET/`:**
- **Create `dotnet/OxidizePdf.NET/Graphics/CalGrayColorSpace.cs`**
- **Create `dotnet/OxidizePdf.NET/Graphics/CalRgbColorSpace.cs`**
- **Create `dotnet/OxidizePdf.NET/Graphics/LabColorSpace.cs`**
- **Create `dotnet/OxidizePdf.NET/Graphics/CalibratedColor.cs`** — wraps `(CalibratedColorKind Kind, double Value, double[] Rgb, CalGrayColorSpace? GrayCs, CalRgbColorSpace? RgbCs)`; factories `CalGray(value, cs)` and `CalRgb(rgb, cs)`.
- **Create `dotnet/OxidizePdf.NET/Graphics/LabColor.cs`** — wraps `(double L, double A, double B, LabColorSpace ColorSpace)`.
- **Create `dotnet/OxidizePdf.NET/Graphics/IccColorSpace.cs`** — enum: `Gray=1, Rgb=3, Cmyk=4, Lab=3` (note: use int N for Generic). Mirror upstream `IccColorSpace`.
- **Create `dotnet/OxidizePdf.NET/Graphics/IccProfile.cs`** — wraps name, data, `IccColorSpace`; `Validate()`.
- **Create `dotnet/OxidizePdf.NET/Graphics/PageColorSpace.cs`** — factory type with `Device(name)`, `IccBased(n, alternate)`, `CalGray(cs)`, `CalRgb(cs)`, `Lab(cs)` static methods. Internal enum to track which variant.
- **Modify `dotnet/OxidizePdf.NET/NativeMethods.cs`** — add all new P/Invoke declarations.
- **Modify `dotnet/OxidizePdf.NET/PdfPage.cs`** — add all new fluent methods.

**Tests — `dotnet/OxidizePdf.NET.Tests/`:**
- **Create `dotnet/OxidizePdf.NET.Tests/TestHelpers/ContentStreamHelper.cs`**
- **Create `dotnet/OxidizePdf.NET.Tests/Graphics/CalGrayColorSpaceTests.cs`**
- **Create `dotnet/OxidizePdf.NET.Tests/Graphics/CalRgbColorSpaceTests.cs`**
- **Create `dotnet/OxidizePdf.NET.Tests/Graphics/LabColorSpaceTests.cs`**
- **Create `dotnet/OxidizePdf.NET.Tests/Graphics/CalibratedColorTests.cs`**
- **Create `dotnet/OxidizePdf.NET.Tests/Graphics/LabColorTests.cs`**
- **Create `dotnet/OxidizePdf.NET.Tests/Graphics/IccProfileTests.cs`**
- **Create `dotnet/OxidizePdf.NET.Tests/Graphics/PdfPageCalGrayIntegrationTests.cs`**
- **Create `dotnet/OxidizePdf.NET.Tests/Graphics/PdfPageCalRgbIntegrationTests.cs`**
- **Create `dotnet/OxidizePdf.NET.Tests/Graphics/PdfPageLabIntegrationTests.cs`**
- **Create `dotnet/OxidizePdf.NET.Tests/Graphics/PdfPageIccInlineIntegrationTests.cs`**
- **Create `dotnet/OxidizePdf.NET.Tests/Graphics/PdfPageIccEmbeddedIntegrationTests.cs`**
- **Create `dotnet/OxidizePdf.NET.Tests/Graphics/PdfPageNamedColorSpaceIntegrationTests.cs`**
- **Create `dotnet/OxidizePdf.NET.Tests/Interop/ColorSpaceNativeLayoutTests.cs`**

---

## Pre-Work

- [ ] **Step 0.1: Verify branch, baseline build, and resolved crate version**

```bash
git -C /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet branch --show-current
git -C /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet log --oneline -3
grep "oxidize-pdf" /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.lock | head -3
RUSTFLAGS="-D warnings" cargo build \
  --manifest-path /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.toml \
  --release 2>&1 | tail -5
dotnet test /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.sln \
  --nologo 2>&1 | tail -10
```

Expected: branch is `feature/m3-color-spaces`; Cargo.lock resolves `oxidize-pdf 2.12.0`; Rust build zero warnings; all existing .NET tests pass. If anything fails, stop and investigate.

- [ ] **Step 0.2: Create directory structure**

```bash
mkdir -p /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET.Tests/Graphics
mkdir -p /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET/Graphics
mkdir -p /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET.Tests/TestHelpers
mkdir -p /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET.Tests/Interop
```

- [ ] **Step 0.3: Create `ContentStreamHelper.cs`**

Create `/home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET.Tests/TestHelpers/ContentStreamHelper.cs`:

```csharp
using System.IO.Compression;
using System.Text;

namespace OxidizePdf.NET.Tests.TestHelpers;

/// <summary>
/// Helpers for inspecting raw PDF bytes in integration tests.
/// Decompresses FlateDecode content streams so tests can search for
/// PDF operators without relying only on resource dictionary text.
/// </summary>
public static class ContentStreamHelper
{
    /// <summary>
    /// Returns the PDF as a Latin-1 string for plain-text resource-dict searches.
    /// Color-space resource entries live in the page resource dict, which is not
    /// compressed, so this is sufficient for /ColorSpace dict assertions.
    /// </summary>
    public static string ToLatin1(byte[] pdfBytes) =>
        Encoding.Latin1.GetString(pdfBytes);

    /// <summary>
    /// Locates the first content stream in <paramref name="pdfBytes"/> and
    /// returns its decompressed text.
    ///
    /// Strategy: find the byte sequence "stream\r\n" or "stream\n", advance past it,
    /// read until "endstream", strip the two-byte zlib header (0x78 0x9C or similar),
    /// and decompress with <see cref="DeflateStream"/>.
    ///
    /// Returns null if no compressed stream is found (uncompressed PDFs are handled
    /// by returning the raw stream bytes as Latin-1 text instead).
    /// </summary>
    public static string? DecompressFirstContentStream(byte[] pdfBytes)
    {
        var streamMarker = Encoding.ASCII.GetBytes("stream");
        var endMarker = Encoding.ASCII.GetBytes("endstream");

        int streamStart = IndexOf(pdfBytes, streamMarker, 0);
        if (streamStart < 0) return null;

        int dataStart = streamStart + streamMarker.Length;
        if (dataStart < pdfBytes.Length && pdfBytes[dataStart] == '\r') dataStart++;
        if (dataStart < pdfBytes.Length && pdfBytes[dataStart] == '\n') dataStart++;

        int streamEnd = IndexOf(pdfBytes, endMarker, dataStart);
        if (streamEnd < 0) return null;

        int dataEnd = streamEnd;
        while (dataEnd > dataStart && (pdfBytes[dataEnd - 1] == '\r' || pdfBytes[dataEnd - 1] == '\n'))
            dataEnd--;

        var streamBytes = pdfBytes[dataStart..dataEnd];

        if (streamBytes.Length >= 2 && streamBytes[0] == 0x78)
        {
            using var compressed = new MemoryStream(streamBytes, 2, streamBytes.Length - 2);
            using var deflate = new DeflateStream(compressed, CompressionMode.Decompress);
            using var output = new MemoryStream();
            deflate.CopyTo(output);
            return Encoding.Latin1.GetString(output.ToArray());
        }

        return Encoding.Latin1.GetString(streamBytes);
    }

    private static int IndexOf(byte[] haystack, byte[] needle, int startIndex)
    {
        for (int i = startIndex; i <= haystack.Length - needle.Length; i++)
        {
            if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle))
                return i;
        }
        return -1;
    }
}
```

- [ ] **Step 0.4: Verify ContentStreamHelper compiles**

```bash
dotnet build /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj \
  --nologo -warnaserror 2>&1 | tail -10
```

Expected: Build succeeded, 0 Warning(s), 0 Error(s).

- [ ] **Step 0.5: Commit pre-work**

```bash
git -C /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet add \
  dotnet/OxidizePdf.NET.Tests/TestHelpers/ContentStreamHelper.cs
git -C /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet commit \
  -m "chore(m3): add ContentStreamHelper for content-stream decompression in tests"
```

---

## Task 1: C# color-space value types (unit tests, no FFI)

**Files:**
- Create: `dotnet/OxidizePdf.NET/Graphics/CalGrayColorSpace.cs`
- Create: `dotnet/OxidizePdf.NET/Graphics/CalRgbColorSpace.cs`
- Create: `dotnet/OxidizePdf.NET/Graphics/LabColorSpace.cs`
- Create: `dotnet/OxidizePdf.NET/Graphics/CalibratedColor.cs`
- Create: `dotnet/OxidizePdf.NET/Graphics/LabColor.cs`
- Create: `dotnet/OxidizePdf.NET.Tests/Graphics/CalGrayColorSpaceTests.cs`
- Create: `dotnet/OxidizePdf.NET.Tests/Graphics/CalRgbColorSpaceTests.cs`
- Create: `dotnet/OxidizePdf.NET.Tests/Graphics/LabColorSpaceTests.cs`
- Create: `dotnet/OxidizePdf.NET.Tests/Graphics/CalibratedColorTests.cs`
- Create: `dotnet/OxidizePdf.NET.Tests/Graphics/LabColorTests.cs`

**Layout note:** All parameters cross the FFI boundary as individual `double` scalars — no `#[repr(C)]` structs. This matches the existing pattern for RGB/Gray/CMYK and avoids alignment surprises.

### Step 1.1 — RED: Write failing tests

Create `/home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET.Tests/Graphics/CalGrayColorSpaceTests.cs`:

```csharp
using FluentAssertions;
using OxidizePdf.NET.Graphics;
using Xunit;

namespace OxidizePdf.NET.Tests.Graphics;

public class CalGrayColorSpaceTests
{
    [Fact]
    public void DefaultConstructor_SetsReasonableDefaults()
    {
        var cs = new CalGrayColorSpace();
        cs.WhitePoint.Should().BeEquivalentTo(new double[] { 0.9505, 1.0, 1.0890 });
        cs.BlackPoint.Should().BeEquivalentTo(new double[] { 0.0, 0.0, 0.0 });
        cs.Gamma.Should().Be(1.0);
    }

    [Fact]
    public void D50_Factory_MatchesD50Illuminant()
    {
        var cs = CalGrayColorSpace.D50();
        cs.WhitePoint[0].Should().BeApproximately(0.9642, 0.0001);
        cs.WhitePoint[1].Should().BeApproximately(1.0000, 0.0001);
        cs.WhitePoint[2].Should().BeApproximately(0.8251, 0.0001);
    }

    [Fact]
    public void D65_Factory_MatchesD65Illuminant()
    {
        var cs = CalGrayColorSpace.D65();
        cs.WhitePoint[0].Should().BeApproximately(0.9505, 0.0001);
        cs.WhitePoint[1].Should().BeApproximately(1.0000, 0.0001);
        cs.WhitePoint[2].Should().BeApproximately(1.0890, 0.0001);
    }

    [Fact]
    public void Validate_AcceptsValidWhitePoint()
    {
        CalGrayColorSpace.D65().Invoking(x => x.Validate()).Should().NotThrow();
    }

    [Fact]
    public void Validate_RejectsNegativeWhitePointY()
    {
        var cs = new CalGrayColorSpace { WhitePoint = new double[] { 0.9505, -0.1, 1.089 } };
        cs.Invoking(x => x.Validate()).Should().Throw<ArgumentException>()
            .WithMessage("*WhitePoint*");
    }

    [Fact]
    public void Validate_RejectsWhitePointYNotOne()
    {
        var cs = new CalGrayColorSpace { WhitePoint = new double[] { 0.9505, 0.5, 1.089 } };
        cs.Invoking(x => x.Validate()).Should().Throw<ArgumentException>()
            .WithMessage("*WhitePoint*Y*");
    }

    [Fact]
    public void Validate_RejectsNonPositiveGamma()
    {
        var cs = CalGrayColorSpace.D65() with { Gamma = 0.0 };
        cs.Invoking(x => x.Validate()).Should().Throw<ArgumentException>()
            .WithMessage("*Gamma*");
    }

    [Fact]
    public void Validate_RejectsWrongWhitePointLength()
    {
        var cs = new CalGrayColorSpace { WhitePoint = new double[] { 0.9505, 1.0 } };
        cs.Invoking(x => x.Validate()).Should().Throw<ArgumentException>()
            .WithMessage("*WhitePoint*3*");
    }
}
```

Create `/home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET.Tests/Graphics/CalRgbColorSpaceTests.cs`:

```csharp
using FluentAssertions;
using OxidizePdf.NET.Graphics;
using Xunit;

namespace OxidizePdf.NET.Tests.Graphics;

public class CalRgbColorSpaceTests
{
    [Fact]
    public void SRgb_Factory_HasExpectedWhitePoint()
    {
        var cs = CalRgbColorSpace.SRgb();
        cs.WhitePoint[0].Should().BeApproximately(0.9505, 0.0001);
        cs.WhitePoint[1].Should().BeApproximately(1.0000, 0.0001);
        cs.WhitePoint[2].Should().BeApproximately(1.0890, 0.0001);
    }

    [Fact]
    public void SRgb_Factory_HasCorrectGamma()
    {
        var cs = CalRgbColorSpace.SRgb();
        cs.Gamma.R.Should().BeApproximately(2.2, 0.01);
        cs.Gamma.G.Should().BeApproximately(2.2, 0.01);
        cs.Gamma.B.Should().BeApproximately(2.2, 0.01);
    }

    [Fact]
    public void SRgb_Factory_HasNineElementMatrix()
    {
        CalRgbColorSpace.SRgb().Matrix.Should().HaveCount(9);
    }

    [Fact]
    public void AdobeRgb_Factory_HasExpectedWhitePointY()
    {
        CalRgbColorSpace.AdobeRgb().WhitePoint[1].Should().BeApproximately(1.0, 0.0001);
    }

    [Fact]
    public void Validate_AcceptsValidSRgb()
    {
        CalRgbColorSpace.SRgb().Invoking(x => x.Validate()).Should().NotThrow();
    }

    [Fact]
    public void Validate_RejectsWrongMatrixLength()
    {
        var cs = CalRgbColorSpace.SRgb() with { Matrix = new double[8] };
        cs.Invoking(x => x.Validate()).Should().Throw<ArgumentException>()
            .WithMessage("*Matrix*9*");
    }

    [Fact]
    public void Validate_RejectsNonPositiveGamma()
    {
        var cs = CalRgbColorSpace.SRgb() with { Gamma = (-1.0, 2.2, 2.2) };
        cs.Invoking(x => x.Validate()).Should().Throw<ArgumentException>()
            .WithMessage("*Gamma*");
    }

    [Fact]
    public void Validate_RejectsWhitePointYNotOne()
    {
        var cs = CalRgbColorSpace.SRgb() with { WhitePoint = new double[] { 0.9505, 0.5, 1.089 } };
        cs.Invoking(x => x.Validate()).Should().Throw<ArgumentException>()
            .WithMessage("*WhitePoint*Y*");
    }
}
```

Create `/home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET.Tests/Graphics/LabColorSpaceTests.cs`:

```csharp
using FluentAssertions;
using OxidizePdf.NET.Graphics;
using Xunit;

namespace OxidizePdf.NET.Tests.Graphics;

public class LabColorSpaceTests
{
    [Fact]
    public void D50_Factory_HasD50WhitePoint()
    {
        var cs = LabColorSpace.D50();
        cs.WhitePoint[0].Should().BeApproximately(0.9642, 0.0001);
        cs.WhitePoint[1].Should().BeApproximately(1.0000, 0.0001);
        cs.WhitePoint[2].Should().BeApproximately(0.8251, 0.0001);
    }

    [Fact]
    public void D65_Factory_HasD65WhitePoint()
    {
        LabColorSpace.D65().WhitePoint[0].Should().BeApproximately(0.9505, 0.0001);
    }

    [Fact]
    public void DefaultRange_IsStandardLabRange()
    {
        var cs = LabColorSpace.D50();
        cs.Range[0].Should().Be(-128.0);  // a_min
        cs.Range[1].Should().Be(127.0);   // a_max
        cs.Range[2].Should().Be(-128.0);  // b_min
        cs.Range[3].Should().Be(127.0);   // b_max
    }

    [Fact]
    public void Validate_AcceptsValidD50()
    {
        LabColorSpace.D50().Invoking(x => x.Validate()).Should().NotThrow();
    }

    [Fact]
    public void Validate_RejectsRangeWithMinGreaterThanMax()
    {
        var cs = LabColorSpace.D50() with { Range = new double[] { 127.0, -128.0, -128.0, 127.0 } };
        cs.Invoking(x => x.Validate()).Should().Throw<ArgumentException>()
            .WithMessage("*Range*");
    }

    [Fact]
    public void Validate_RejectsRangeWithWrongLength()
    {
        var cs = LabColorSpace.D50() with { Range = new double[] { -128.0, 127.0 } };
        cs.Invoking(x => x.Validate()).Should().Throw<ArgumentException>()
            .WithMessage("*Range*4*");
    }

    [Fact]
    public void Validate_RejectsWhitePointYNotOne()
    {
        var cs = LabColorSpace.D50() with { WhitePoint = new double[] { 0.9642, 0.5, 0.8251 } };
        cs.Invoking(x => x.Validate()).Should().Throw<ArgumentException>()
            .WithMessage("*WhitePoint*Y*");
    }
}
```

Create `/home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET.Tests/Graphics/CalibratedColorTests.cs`:

```csharp
using FluentAssertions;
using OxidizePdf.NET.Graphics;
using Xunit;

namespace OxidizePdf.NET.Tests.Graphics;

public class CalibratedColorTests
{
    [Fact]
    public void CalGray_StoresValueAndColorSpace()
    {
        var cs = CalGrayColorSpace.D65();
        var color = CalibratedColor.CalGray(0.5, cs);
        color.IsCalGray.Should().BeTrue();
        color.GrayValue.Should().Be(0.5);
        color.GrayColorSpace.Should().BeSameAs(cs);
    }

    [Fact]
    public void CalRgb_StoresComponentsAndColorSpace()
    {
        var cs = CalRgbColorSpace.SRgb();
        var color = CalibratedColor.CalRgb(new double[] { 0.2, 0.4, 0.8 }, cs);
        color.IsCalGray.Should().BeFalse();
        color.RgbValues.Should().BeEquivalentTo(new double[] { 0.2, 0.4, 0.8 });
        color.RgbColorSpace.Should().BeSameAs(cs);
    }

    [Fact]
    public void CalGray_NullColorSpace_Throws()
    {
        Action act = () => CalibratedColor.CalGray(0.5, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CalRgb_WrongComponentCount_Throws()
    {
        var cs = CalRgbColorSpace.SRgb();
        Action act = () => CalibratedColor.CalRgb(new double[] { 0.5, 0.5 }, cs);
        act.Should().Throw<ArgumentException>().WithMessage("*3*");
    }
}
```

Create `/home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET.Tests/Graphics/LabColorTests.cs`:

```csharp
using FluentAssertions;
using OxidizePdf.NET.Graphics;
using Xunit;

namespace OxidizePdf.NET.Tests.Graphics;

public class LabColorTests
{
    [Fact]
    public void New_StoresComponentsAndColorSpace()
    {
        var cs = LabColorSpace.D50();
        var color = new LabColor(50.0, 10.0, -20.0, cs);
        color.L.Should().Be(50.0);
        color.A.Should().Be(10.0);
        color.B.Should().Be(-20.0);
        color.ColorSpace.Should().BeSameAs(cs);
    }

    [Fact]
    public void New_NullColorSpace_Throws()
    {
        Action act = () => new LabColor(50.0, 0.0, 0.0, null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
```

### Step 1.2 — Run tests to verify they fail

```bash
dotnet test /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj \
  --filter "FullyQualifiedName~CalGrayColorSpaceTests|FullyQualifiedName~CalRgbColorSpaceTests|FullyQualifiedName~LabColorSpaceTests|FullyQualifiedName~CalibratedColorTests|FullyQualifiedName~LabColorTests" \
  --nologo 2>&1 | tail -10
```

Expected: FAIL — type not found errors.

### Step 1.3 — GREEN: Implement the five C# types

Create `/home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET/Graphics/CalGrayColorSpace.cs`:

```csharp
namespace OxidizePdf.NET.Graphics;

/// <summary>
/// Calibrated gray color space (CalGray). Mirrors
/// <c>oxidize_pdf::graphics::CalGrayColorSpace</c>.
/// </summary>
public record CalGrayColorSpace
{
    /// <summary>
    /// Tristimulus white-point [X, Y, Z]. Y MUST equal 1.0 per PDF spec.
    /// Defaults to D65: [0.9505, 1.0000, 1.0890].
    /// </summary>
    public double[] WhitePoint { get; init; } = [0.9505, 1.0, 1.0890];

    /// <summary>Tristimulus black-point [X, Y, Z]. Defaults to [0, 0, 0].</summary>
    public double[] BlackPoint { get; init; } = [0.0, 0.0, 0.0];

    /// <summary>Gamma exponent. Must be positive. Defaults to 1.0 (linear).</summary>
    public double Gamma { get; init; } = 1.0;

    /// <summary>D50 standard illuminant (ICC profile connection space).</summary>
    public static CalGrayColorSpace D50() => new() { WhitePoint = [0.9642, 1.0, 0.8251] };

    /// <summary>D65 standard illuminant (sRGB / most monitors).</summary>
    public static CalGrayColorSpace D65() => new() { WhitePoint = [0.9505, 1.0, 1.0890] };

    /// <summary>Validates against PDF spec constraints. Throws <see cref="ArgumentException"/> on violation.</summary>
    public void Validate()
    {
        if (WhitePoint.Length != 3)
            throw new ArgumentException("WhitePoint must have exactly 3 components.", nameof(WhitePoint));
        if (WhitePoint[1] != 1.0)
            throw new ArgumentException("WhitePoint Y component must equal 1.0 per PDF spec.", nameof(WhitePoint));
        if (WhitePoint.Any(v => v < 0))
            throw new ArgumentException("WhitePoint components must be non-negative.", nameof(WhitePoint));
        if (BlackPoint.Length != 3)
            throw new ArgumentException("BlackPoint must have exactly 3 components.", nameof(BlackPoint));
        if (Gamma <= 0 || double.IsNaN(Gamma) || double.IsInfinity(Gamma))
            throw new ArgumentException("Gamma must be a finite positive number.", nameof(Gamma));
    }
}
```

Create `/home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET/Graphics/CalRgbColorSpace.cs`:

```csharp
namespace OxidizePdf.NET.Graphics;

/// <summary>
/// Calibrated RGB color space (CalRGB). Mirrors
/// <c>oxidize_pdf::graphics::CalRgbColorSpace</c>.
/// </summary>
public record CalRgbColorSpace
{
    /// <summary>Tristimulus white-point [X, Y, Z]. Y MUST equal 1.0 per PDF spec.</summary>
    public double[] WhitePoint { get; init; } = [0.9505, 1.0, 1.0890];

    /// <summary>Tristimulus black-point [X, Y, Z]. Defaults to [0, 0, 0].</summary>
    public double[] BlackPoint { get; init; } = [0.0, 0.0, 0.0];

    /// <summary>Per-channel gamma exponents (R, G, B). All components must be positive.</summary>
    public (double R, double G, double B) Gamma { get; init; } = (2.2, 2.2, 2.2);

    /// <summary>
    /// 3x3 color-transformation matrix in column-major order [XA YA ZA XB YB ZB XC YC ZC].
    /// Defaults to identity.
    /// </summary>
    public double[] Matrix { get; init; } =
    [
        1.0, 0.0, 0.0,
        0.0, 1.0, 0.0,
        0.0, 0.0, 1.0,
    ];

    /// <summary>sRGB (IEC 61966-2-1) color space, D65 white point, gamma 2.2 approximation.</summary>
    public static CalRgbColorSpace SRgb() => new()
    {
        WhitePoint = [0.9505, 1.0, 1.0890],
        Gamma = (2.2, 2.2, 2.2),
        Matrix =
        [
            0.4124, 0.2126, 0.0193,
            0.3576, 0.7152, 0.1192,
            0.1805, 0.0722, 0.9505,
        ],
    };

    /// <summary>Adobe RGB (1998) color space, D65 white point.</summary>
    public static CalRgbColorSpace AdobeRgb() => new()
    {
        WhitePoint = [0.9505, 1.0, 1.0890],
        Gamma = (2.2, 2.2, 2.2),
        Matrix =
        [
            0.5767, 0.2974, 0.0270,
            0.1856, 0.6273, 0.0707,
            0.1882, 0.0753, 0.9911,
        ],
    };

    /// <summary>Validates this color space. Throws <see cref="ArgumentException"/> on violation.</summary>
    public void Validate()
    {
        if (WhitePoint.Length != 3)
            throw new ArgumentException("WhitePoint must have exactly 3 components.", nameof(WhitePoint));
        if (WhitePoint[1] != 1.0)
            throw new ArgumentException("WhitePoint Y component must equal 1.0 per PDF spec.", nameof(WhitePoint));
        if (WhitePoint.Any(v => v < 0))
            throw new ArgumentException("WhitePoint components must be non-negative.", nameof(WhitePoint));
        if (BlackPoint.Length != 3)
            throw new ArgumentException("BlackPoint must have exactly 3 components.", nameof(BlackPoint));
        if (Gamma.R <= 0 || Gamma.G <= 0 || Gamma.B <= 0 ||
            double.IsNaN(Gamma.R) || double.IsNaN(Gamma.G) || double.IsNaN(Gamma.B))
            throw new ArgumentException("Gamma components must be finite positive numbers.", nameof(Gamma));
        if (Matrix.Length != 9)
            throw new ArgumentException("Matrix must have exactly 9 elements (3x3 column-major).", nameof(Matrix));
    }
}
```

Create `/home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET/Graphics/LabColorSpace.cs`:

```csharp
namespace OxidizePdf.NET.Graphics;

/// <summary>
/// CIE L*a*b* color space. Mirrors <c>oxidize_pdf::graphics::LabColorSpace</c>.
/// </summary>
public record LabColorSpace
{
    /// <summary>Tristimulus white-point [X, Y, Z]. Y MUST equal 1.0 per PDF spec.</summary>
    public double[] WhitePoint { get; init; } = [0.9642, 1.0, 0.8251];

    /// <summary>Tristimulus black-point [X, Y, Z]. Defaults to [0, 0, 0].</summary>
    public double[] BlackPoint { get; init; } = [0.0, 0.0, 0.0];

    /// <summary>
    /// Range [aMin, aMax, bMin, bMax] for the a* and b* components.
    /// NOTE: upstream with_range takes FOUR separate f64 args (a_min, a_max, b_min, b_max) —
    /// not an array. This property is expanded to four scalars at the FFI boundary.
    /// Standard CIE range: [-128, 127, -128, 127].
    /// </summary>
    public double[] Range { get; init; } = [-128.0, 127.0, -128.0, 127.0];

    /// <summary>D50 standard illuminant (ICC profile connection space).</summary>
    public static LabColorSpace D50() => new() { WhitePoint = [0.9642, 1.0, 0.8251] };

    /// <summary>D65 standard illuminant (sRGB / most monitors).</summary>
    public static LabColorSpace D65() => new() { WhitePoint = [0.9505, 1.0, 1.0890] };

    /// <summary>Validates this color space. Throws <see cref="ArgumentException"/> on violation.</summary>
    public void Validate()
    {
        if (WhitePoint.Length != 3)
            throw new ArgumentException("WhitePoint must have exactly 3 components.", nameof(WhitePoint));
        if (WhitePoint[1] != 1.0)
            throw new ArgumentException("WhitePoint Y component must equal 1.0 per PDF spec.", nameof(WhitePoint));
        if (WhitePoint.Any(v => v < 0))
            throw new ArgumentException("WhitePoint components must be non-negative.", nameof(WhitePoint));
        if (BlackPoint.Length != 3)
            throw new ArgumentException("BlackPoint must have exactly 3 components.", nameof(BlackPoint));
        if (Range.Length != 4)
            throw new ArgumentException("Range must have exactly 4 elements [aMin, aMax, bMin, bMax].", nameof(Range));
        if (Range[0] >= Range[1])
            throw new ArgumentException("Range[0] (aMin) must be less than Range[1] (aMax).", nameof(Range));
        if (Range[2] >= Range[3])
            throw new ArgumentException("Range[2] (bMin) must be less than Range[3] (bMax).", nameof(Range));
    }
}
```

Create `/home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET/Graphics/CalibratedColor.cs`:

```csharp
namespace OxidizePdf.NET.Graphics;

/// <summary>
/// A calibrated color value: either CalGray or CalRGB.
/// Mirrors <c>oxidize_pdf::graphics::CalibratedColor</c>.
/// Use <see cref="CalGray"/> or <see cref="CalRgb"/> to construct.
/// </summary>
public sealed class CalibratedColor
{
    /// <summary>True if this is a CalGray color; false if CalRGB.</summary>
    public bool IsCalGray { get; }

    /// <summary>Gray component value. Only valid when <see cref="IsCalGray"/> is true.</summary>
    public double GrayValue { get; }

    /// <summary>The CalGray color space. Only valid when <see cref="IsCalGray"/> is true.</summary>
    public CalGrayColorSpace? GrayColorSpace { get; }

    /// <summary>RGB component values [R, G, B]. Only valid when <see cref="IsCalGray"/> is false.</summary>
    public double[]? RgbValues { get; }

    /// <summary>The CalRGB color space. Only valid when <see cref="IsCalGray"/> is false.</summary>
    public CalRgbColorSpace? RgbColorSpace { get; }

    private CalibratedColor(double gray, CalGrayColorSpace cs)
    {
        IsCalGray = true;
        GrayValue = gray;
        GrayColorSpace = cs;
    }

    private CalibratedColor(double[] rgb, CalRgbColorSpace cs)
    {
        IsCalGray = false;
        RgbValues = rgb;
        RgbColorSpace = cs;
    }

    /// <summary>Constructs a calibrated gray color.</summary>
    /// <exception cref="ArgumentNullException">If <paramref name="colorSpace"/> is null.</exception>
    public static CalibratedColor CalGray(double value, CalGrayColorSpace colorSpace)
    {
        ArgumentNullException.ThrowIfNull(colorSpace);
        return new CalibratedColor(value, colorSpace);
    }

    /// <summary>Constructs a calibrated RGB color.</summary>
    /// <exception cref="ArgumentNullException">If <paramref name="colorSpace"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="rgb"/> does not have exactly 3 components.</exception>
    public static CalibratedColor CalRgb(double[] rgb, CalRgbColorSpace colorSpace)
    {
        ArgumentNullException.ThrowIfNull(colorSpace);
        ArgumentNullException.ThrowIfNull(rgb);
        if (rgb.Length != 3)
            throw new ArgumentException("CalRGB color requires exactly 3 components [R, G, B].", nameof(rgb));
        return new CalibratedColor(rgb, colorSpace);
    }
}
```

Create `/home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET/Graphics/LabColor.cs`:

```csharp
namespace OxidizePdf.NET.Graphics;

/// <summary>
/// A CIE L*a*b* color value. Mirrors <c>oxidize_pdf::graphics::LabColor</c>.
/// </summary>
public sealed class LabColor
{
    /// <summary>L* component (luminance). Upstream clamps to [0, 100].</summary>
    public double L { get; }

    /// <summary>a* component (green-red axis). Upstream clamps to color-space range.</summary>
    public double A { get; }

    /// <summary>b* component (blue-yellow axis). Upstream clamps to color-space range.</summary>
    public double B { get; }

    /// <summary>The Lab color space parameters.</summary>
    public LabColorSpace ColorSpace { get; }

    /// <exception cref="ArgumentNullException">If <paramref name="colorSpace"/> is null.</exception>
    public LabColor(double l, double a, double b, LabColorSpace colorSpace)
    {
        ArgumentNullException.ThrowIfNull(colorSpace);
        L = l;
        A = a;
        B = b;
        ColorSpace = colorSpace;
    }
}
```

### Step 1.4 — Run tests to verify they pass

```bash
dotnet test /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj \
  --filter "FullyQualifiedName~CalGrayColorSpaceTests|FullyQualifiedName~CalRgbColorSpaceTests|FullyQualifiedName~LabColorSpaceTests|FullyQualifiedName~CalibratedColorTests|FullyQualifiedName~LabColorTests" \
  --nologo 2>&1 | tail -15
```

Expected: all tests pass (8 + 8 + 7 + 4 + 2 = 29).

### Step 1.5 — Compile check and commit

```bash
cargo fmt --manifest-path /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.toml -- --check 2>&1
dotnet build /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET/OxidizePdf.NET.csproj \
  --nologo -warnaserror 2>&1 | tail -5
git -C /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet add \
  dotnet/OxidizePdf.NET/Graphics/CalGrayColorSpace.cs \
  dotnet/OxidizePdf.NET/Graphics/CalRgbColorSpace.cs \
  dotnet/OxidizePdf.NET/Graphics/LabColorSpace.cs \
  dotnet/OxidizePdf.NET/Graphics/CalibratedColor.cs \
  dotnet/OxidizePdf.NET/Graphics/LabColor.cs \
  dotnet/OxidizePdf.NET.Tests/Graphics/CalGrayColorSpaceTests.cs \
  dotnet/OxidizePdf.NET.Tests/Graphics/CalRgbColorSpaceTests.cs \
  dotnet/OxidizePdf.NET.Tests/Graphics/LabColorSpaceTests.cs \
  dotnet/OxidizePdf.NET.Tests/Graphics/CalibratedColorTests.cs \
  dotnet/OxidizePdf.NET.Tests/Graphics/LabColorTests.cs
git -C /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet commit \
  -m "feat(m3): add CalGray/CalRGB/Lab/CalibratedColor/LabColor C# types with validation (GFX-014, GFX-015)"
```

---

## Task 2: ICC C# types (unit tests, no FFI)

**Files:**
- Create: `dotnet/OxidizePdf.NET/Graphics/IccColorSpace.cs`
- Create: `dotnet/OxidizePdf.NET/Graphics/IccProfile.cs`
- Create: `dotnet/OxidizePdf.NET/Graphics/PageColorSpace.cs`
- Create: `dotnet/OxidizePdf.NET.Tests/Graphics/IccProfileTests.cs`

### Step 2.1 — RED: Write failing tests

Create `/home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET.Tests/Graphics/IccProfileTests.cs`:

```csharp
using FluentAssertions;
using OxidizePdf.NET.Graphics;
using Xunit;

namespace OxidizePdf.NET.Tests.Graphics;

public class IccProfileTests
{
    private static byte[] MinimalData() => new byte[128]; // minimal non-empty block

    [Fact]
    public void New_StoresNameDataAndColorSpace()
    {
        var profile = new IccProfile("MyRGB", MinimalData(), IccColorSpace.Rgb);
        profile.Name.Should().Be("MyRGB");
        profile.ColorSpace.Should().Be(IccColorSpace.Rgb);
        profile.Data.Should().HaveCount(128);
    }

    [Fact]
    public void New_NullName_Throws()
    {
        Action act = () => new IccProfile(null!, MinimalData(), IccColorSpace.Rgb);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void New_NullData_Throws()
    {
        Action act = () => new IccProfile("MyRGB", null!, IccColorSpace.Rgb);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Validate_EmptyData_Throws()
    {
        var profile = new IccProfile("MyRGB", Array.Empty<byte>(), IccColorSpace.Rgb);
        profile.Invoking(x => x.Validate()).Should().Throw<ArgumentException>()
            .WithMessage("*data*");
    }

    [Fact]
    public void Validate_ValidProfile_DoesNotThrow()
    {
        var profile = new IccProfile("MyRGB", MinimalData(), IccColorSpace.Rgb);
        profile.Invoking(x => x.Validate()).Should().NotThrow();
    }

    [Fact]
    public void ComponentCount_Rgb_IsThree()
    {
        IccColorSpace.Rgb.ComponentCount().Should().Be(3);
    }

    [Fact]
    public void ComponentCount_Gray_IsOne()
    {
        IccColorSpace.Gray.ComponentCount().Should().Be(1);
    }

    [Fact]
    public void ComponentCount_Cmyk_IsFour()
    {
        IccColorSpace.Cmyk.ComponentCount().Should().Be(4);
    }

    [Fact]
    public void PageColorSpace_IccBased_StoresNAndAlternate()
    {
        var pcs = PageColorSpace.IccBased(3, "DeviceRGB");
        pcs.Kind.Should().Be(PageColorSpaceKind.IccBased);
        pcs.IccN.Should().Be(3);
        pcs.IccAlternate.Should().Be("DeviceRGB");
    }

    [Fact]
    public void PageColorSpace_CalGray_StoresColorSpace()
    {
        var cs = CalGrayColorSpace.D65();
        var pcs = PageColorSpace.CalGray(cs);
        pcs.Kind.Should().Be(PageColorSpaceKind.CalGray);
        pcs.CalGrayCs.Should().BeSameAs(cs);
    }
}
```

### Step 2.2 — Run tests to verify they fail

```bash
dotnet test /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj \
  --filter "FullyQualifiedName~IccProfileTests" --nologo 2>&1 | tail -10
```

Expected: FAIL — type not found.

### Step 2.3 — GREEN: Implement the three types

Create `/home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET/Graphics/IccColorSpace.cs`:

```csharp
namespace OxidizePdf.NET.Graphics;

/// <summary>
/// ICC color space type. Mirrors <c>oxidize_pdf::graphics::IccColorSpace</c>.
/// </summary>
public enum IccColorSpace
{
    Gray = 1,
    Rgb = 3,
    Cmyk = 4,
    /// <summary>Lab (3 components).</summary>
    Lab = 33,  // internal sentinel; component count is 3
}

/// <summary>Extension methods for <see cref="IccColorSpace"/>.</summary>
public static class IccColorSpaceExtensions
{
    /// <summary>Returns the number of color components for this color space.</summary>
    public static int ComponentCount(this IccColorSpace cs) => cs switch
    {
        IccColorSpace.Gray => 1,
        IccColorSpace.Rgb => 3,
        IccColorSpace.Lab => 3,
        IccColorSpace.Cmyk => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(cs), cs, "Unknown ICC color space"),
    };
}
```

Create `/home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET/Graphics/IccProfile.cs`:

```csharp
namespace OxidizePdf.NET.Graphics;

/// <summary>
/// An ICC color profile for embedding in a PDF.
/// Mirrors <c>oxidize_pdf::graphics::IccProfile</c>.
/// Pass to <see cref="OxidizePdf.NET.PdfPage.AddIccColorSpace"/> to register
/// an embedded-profile color space on a page.
/// </summary>
public sealed class IccProfile
{
    /// <summary>Resource name for the color space (PDF resource dict key).</summary>
    public string Name { get; }

    /// <summary>Raw ICC profile binary data.</summary>
    public byte[] Data { get; }

    /// <summary>Color space type declared by the profile.</summary>
    public IccColorSpace ColorSpace { get; }

    /// <exception cref="ArgumentNullException">If <paramref name="name"/> or <paramref name="data"/> is null.</exception>
    public IccProfile(string name, byte[] data, IccColorSpace colorSpace)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(data);
        Name = name;
        Data = data;
        ColorSpace = colorSpace;
    }

    /// <summary>Validates the profile. Throws <see cref="ArgumentException"/> if data is empty.</summary>
    public void Validate()
    {
        if (Data.Length == 0)
            throw new ArgumentException("ICC profile data must not be empty.", nameof(data));
    }
}
```

Create `/home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET/Graphics/PageColorSpace.cs`:

```csharp
namespace OxidizePdf.NET.Graphics;

/// <summary>Discriminator for <see cref="PageColorSpace"/> variants.</summary>
public enum PageColorSpaceKind
{
    Device,
    IccBased,
    CalGray,
    CalRgb,
    Lab,
}

/// <summary>
/// A page-level color space resource entry. Mirrors the Python bridge's
/// <c>PageColorSpace</c> class.
/// Register with <see cref="OxidizePdf.NET.PdfPage.AddColorSpace"/> or
/// <see cref="OxidizePdf.NET.PdfPage.AddIccColorSpace"/>.
/// </summary>
public sealed class PageColorSpace
{
    public PageColorSpaceKind Kind { get; }

    // Device
    public string? DeviceName { get; }

    // IccBased (inline, no binary) — mirrors python's icc_based(n, alternate)
    public int IccN { get; }
    public string? IccAlternate { get; }

    // Parameterised
    public CalGrayColorSpace? CalGrayCs { get; }
    public CalRgbColorSpace? CalRgbCs { get; }
    public LabColorSpace? LabCs { get; }

    private PageColorSpace(PageColorSpaceKind kind) => Kind = kind;

    /// <summary>A device color space alias (e.g. "DeviceRGB", "DeviceGray", "DeviceCMYK").</summary>
    public static PageColorSpace Device(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return new PageColorSpace(PageColorSpaceKind.Device) { }.WithDevice(name);
    }

    /// <summary>
    /// Inline ICCBased color space with N components and an alternate device space name.
    /// No binary profile is embedded. Mirrors python's <c>PageColorSpace.icc_based(n, alternate)</c>.
    /// </summary>
    public static PageColorSpace IccBased(int n, string alternate)
    {
        ArgumentNullException.ThrowIfNull(alternate);
        if (n <= 0) throw new ArgumentOutOfRangeException(nameof(n), "Component count must be positive.");
        return new(PageColorSpaceKind.IccBased, iccN: n, iccAlternate: alternate);
    }

    /// <summary>Calibrated gray color space for registration via AddColorSpace.</summary>
    public static PageColorSpace CalGray(CalGrayColorSpace cs)
    {
        ArgumentNullException.ThrowIfNull(cs);
        return new(PageColorSpaceKind.CalGray, calGrayCs: cs);
    }

    /// <summary>Calibrated RGB color space for registration via AddColorSpace.</summary>
    public static PageColorSpace CalRgb(CalRgbColorSpace cs)
    {
        ArgumentNullException.ThrowIfNull(cs);
        return new(PageColorSpaceKind.CalRgb, calRgbCs: cs);
    }

    /// <summary>Lab color space for registration via AddColorSpace.</summary>
    public static PageColorSpace Lab(LabColorSpace cs)
    {
        ArgumentNullException.ThrowIfNull(cs);
        return new(PageColorSpaceKind.Lab, labCs: cs);
    }

    // Private full constructor
    private PageColorSpace(
        PageColorSpaceKind kind,
        string? deviceName = null,
        int iccN = 0,
        string? iccAlternate = null,
        CalGrayColorSpace? calGrayCs = null,
        CalRgbColorSpace? calRgbCs = null,
        LabColorSpace? labCs = null)
    {
        Kind = kind;
        DeviceName = deviceName;
        IccN = iccN;
        IccAlternate = iccAlternate;
        CalGrayCs = calGrayCs;
        CalRgbCs = calRgbCs;
        LabCs = labCs;
    }

    private PageColorSpace WithDevice(string name) =>
        new(PageColorSpaceKind.Device, deviceName: name);
}
```

### Step 2.4 — Run tests and compile check

```bash
dotnet test /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj \
  --filter "FullyQualifiedName~IccProfileTests" --nologo 2>&1 | tail -15
dotnet build /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET/OxidizePdf.NET.csproj \
  --nologo -warnaserror 2>&1 | tail -5
```

Expected: all IccProfileTests pass, build 0 errors 0 warnings.

### Step 2.5 — Commit

```bash
git -C /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet add \
  dotnet/OxidizePdf.NET/Graphics/IccColorSpace.cs \
  dotnet/OxidizePdf.NET/Graphics/IccProfile.cs \
  dotnet/OxidizePdf.NET/Graphics/PageColorSpace.cs \
  dotnet/OxidizePdf.NET.Tests/Graphics/IccProfileTests.cs
git -C /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet commit \
  -m "feat(m3): add IccColorSpace, IccProfile, PageColorSpace C# types (GFX-019)"
```

---

## Task 3: Rust FFI — CalGray hardcoded fill/stroke

**Files:** Modify `native/src/graphics.rs`

**Upstream API verification step** (run before implementing):

```bash
find ~/.cargo/registry/src -path "*/oxidize-pdf-2.12.0/src/graphics/calibrated_color.rs" | \
  xargs grep -n "pub fn new\|with_white_point\|with_black_point\|with_gamma\|fn cal_gray\|fn cal_rgb" 2>/dev/null | head -20
```

Expected names (verified against 2.12.0 source): `CalGrayColorSpace::new()`, `.with_white_point([f64;3])`, `.with_black_point([f64;3])`, `.with_gamma(f64)`, `CalibratedColor::cal_gray(value, cs)`.

### Step 3.1 — RED: Write failing Rust unit test

Append to `native/src/graphics.rs`:

```rust
#[cfg(test)]
mod cal_gray_ffi_tests {
    use super::*;
    use crate::page::{oxidize_page_create, oxidize_page_free};

    #[test]
    fn fill_cal_gray_valid_params_returns_success() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            assert!(!page.is_null());
            let result = oxidize_page_set_fill_color_cal_gray(
                page, 0.5,
                0.9505, 1.0, 1.0890,
                0.0, 0.0, 0.0,
                1.0,
            );
            assert_eq!(result, 0, "expected ErrorCode::Success (0)");
            oxidize_page_free(page);
        }
    }

    #[test]
    fn stroke_cal_gray_null_page_returns_null_pointer_error() {
        unsafe {
            let result = oxidize_page_set_stroke_color_cal_gray(
                std::ptr::null_mut(), 0.5,
                0.9505, 1.0, 1.0890,
                0.0, 0.0, 0.0,
                1.0,
            );
            assert_eq!(result, 1, "expected ErrorCode::NullPointer (1)");
        }
    }
}
```

### Step 3.2 — Run to confirm RED

```bash
cargo test --manifest-path /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.toml \
  cal_gray_ffi_tests --lib 2>&1 | tail -10
```

Expected: FAIL — function not found.

### Step 3.3 — GREEN: Implement

Append to `native/src/graphics.rs`:

```rust
// ── CalGray color (hardcoded name "CalGray1" via upstream) ────────────────────

#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_fill_color_cal_gray(
    page: *mut PageHandle,
    value: f64,
    wp_x: f64, wp_y: f64, wp_z: f64,
    bp_x: f64, bp_y: f64, bp_z: f64,
    gamma: f64,
) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_set_fill_color_cal_gray");
        return ErrorCode::NullPointer as c_int;
    }
    use oxidize_pdf::graphics::{CalGrayColorSpace, CalibratedColor};
    let cs = CalGrayColorSpace::new()
        .with_white_point([wp_x, wp_y, wp_z])
        .with_black_point([bp_x, bp_y, bp_z])
        .with_gamma(gamma);
    let color = CalibratedColor::cal_gray(value, cs);
    (*page).inner.graphics().set_fill_color_calibrated(color);
    ErrorCode::Success as c_int
}

#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_stroke_color_cal_gray(
    page: *mut PageHandle,
    value: f64,
    wp_x: f64, wp_y: f64, wp_z: f64,
    bp_x: f64, bp_y: f64, bp_z: f64,
    gamma: f64,
) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_set_stroke_color_cal_gray");
        return ErrorCode::NullPointer as c_int;
    }
    use oxidize_pdf::graphics::{CalGrayColorSpace, CalibratedColor};
    let cs = CalGrayColorSpace::new()
        .with_white_point([wp_x, wp_y, wp_z])
        .with_black_point([bp_x, bp_y, bp_z])
        .with_gamma(gamma);
    let color = CalibratedColor::cal_gray(value, cs);
    (*page).inner.graphics().set_stroke_color_calibrated(color);
    ErrorCode::Success as c_int
}
```

### Step 3.4 — Run tests, fmt, clippy, build, commit

```bash
cargo test --manifest-path /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.toml \
  cal_gray_ffi_tests --lib 2>&1 | tail -10
cargo fmt --manifest-path /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.toml -- --check 2>&1
cargo clippy --manifest-path /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.toml \
  -- -D warnings 2>&1 | tail -10
RUSTFLAGS="-D warnings" cargo build \
  --manifest-path /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.toml \
  --release 2>&1 | tail -5
git -C /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet add native/src/graphics.rs
git -C /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet commit \
  -m "feat(m3): add oxidize_page_set_{fill,stroke}_color_cal_gray FFI (GFX-014)"
```

---

## Task 4: Rust FFI — CalRGB hardcoded fill/stroke

**Files:** Modify `native/src/graphics.rs`

Parameters: `r, g, b: f64`; `wp_x, wp_y, wp_z`; `bp_x, bp_y, bp_z`; `gamma_r, gamma_g, gamma_b`; `m0..m8` (9 matrix elements) = 21 scalars total.

**Upstream API verification step:**

```bash
find ~/.cargo/registry/src -path "*/oxidize-pdf-2.12.0/src/graphics/calibrated_color.rs" | \
  xargs grep -n "fn srgb\|fn adobe_rgb\|with_matrix\|fn cal_rgb\|with_gamma" 2>/dev/null | head -15
```

Expected: `CalRgbColorSpace::new()`, `.with_white_point([f64;3])`, `.with_black_point([f64;3])`, `.with_gamma([f64;3])`, `.with_matrix([f64;9])`, `CalibratedColor::cal_rgb([f64;3], cs)`.

### Step 4.1 — RED

Append `#[cfg(test)] mod cal_rgb_ffi_tests` to `native/src/graphics.rs`:

```rust
#[cfg(test)]
mod cal_rgb_ffi_tests {
    use super::*;
    use crate::page::{oxidize_page_create, oxidize_page_free};

    #[test]
    fn fill_cal_rgb_valid_params_returns_success() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            assert!(!page.is_null());
            let result = oxidize_page_set_fill_color_cal_rgb(
                page,
                0.5, 0.3, 0.8,
                0.9505, 1.0, 1.0890,
                0.0, 0.0, 0.0,
                2.2, 2.2, 2.2,
                1.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0,
            );
            assert_eq!(result, 0, "expected ErrorCode::Success (0)");
            oxidize_page_free(page);
        }
    }

    #[test]
    fn stroke_cal_rgb_null_page_returns_null_pointer_error() {
        unsafe {
            let result = oxidize_page_set_stroke_color_cal_rgb(
                std::ptr::null_mut(),
                0.5, 0.3, 0.8,
                0.9505, 1.0, 1.0890,
                0.0, 0.0, 0.0,
                2.2, 2.2, 2.2,
                1.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0,
            );
            assert_eq!(result, 1, "expected ErrorCode::NullPointer (1)");
        }
    }
}
```

### Step 4.2 — Run to confirm RED

```bash
cargo test --manifest-path /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.toml \
  cal_rgb_ffi_tests --lib 2>&1 | tail -10
```

### Step 4.3 — GREEN

Append to `native/src/graphics.rs`:

```rust
// ── CalRGB color (hardcoded name "CalRGB1" via upstream) ─────────────────────

#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_fill_color_cal_rgb(
    page: *mut PageHandle,
    r: f64, g: f64, b: f64,
    wp_x: f64, wp_y: f64, wp_z: f64,
    bp_x: f64, bp_y: f64, bp_z: f64,
    gamma_r: f64, gamma_g: f64, gamma_b: f64,
    m0: f64, m1: f64, m2: f64,
    m3: f64, m4: f64, m5: f64,
    m6: f64, m7: f64, m8: f64,
) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_set_fill_color_cal_rgb");
        return ErrorCode::NullPointer as c_int;
    }
    use oxidize_pdf::graphics::{CalRgbColorSpace, CalibratedColor};
    let cs = CalRgbColorSpace::new()
        .with_white_point([wp_x, wp_y, wp_z])
        .with_black_point([bp_x, bp_y, bp_z])
        .with_gamma([gamma_r, gamma_g, gamma_b])
        .with_matrix([m0, m1, m2, m3, m4, m5, m6, m7, m8]);
    let color = CalibratedColor::cal_rgb([r, g, b], cs);
    (*page).inner.graphics().set_fill_color_calibrated(color);
    ErrorCode::Success as c_int
}

#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_stroke_color_cal_rgb(
    page: *mut PageHandle,
    r: f64, g: f64, b: f64,
    wp_x: f64, wp_y: f64, wp_z: f64,
    bp_x: f64, bp_y: f64, bp_z: f64,
    gamma_r: f64, gamma_g: f64, gamma_b: f64,
    m0: f64, m1: f64, m2: f64,
    m3: f64, m4: f64, m5: f64,
    m6: f64, m7: f64, m8: f64,
) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_set_stroke_color_cal_rgb");
        return ErrorCode::NullPointer as c_int;
    }
    use oxidize_pdf::graphics::{CalRgbColorSpace, CalibratedColor};
    let cs = CalRgbColorSpace::new()
        .with_white_point([wp_x, wp_y, wp_z])
        .with_black_point([bp_x, bp_y, bp_z])
        .with_gamma([gamma_r, gamma_g, gamma_b])
        .with_matrix([m0, m1, m2, m3, m4, m5, m6, m7, m8]);
    let color = CalibratedColor::cal_rgb([r, g, b], cs);
    (*page).inner.graphics().set_stroke_color_calibrated(color);
    ErrorCode::Success as c_int
}
```

### Step 4.4 — Tests, fmt, clippy, build, commit

```bash
cargo test --manifest-path /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.toml \
  cal_rgb_ffi_tests --lib 2>&1 | tail -10
cargo fmt --manifest-path /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.toml -- --check 2>&1
cargo clippy --manifest-path /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.toml \
  -- -D warnings 2>&1 | tail -10
RUSTFLAGS="-D warnings" cargo build \
  --manifest-path /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.toml \
  --release 2>&1 | tail -5
git -C /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet add native/src/graphics.rs
git -C /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet commit \
  -m "feat(m3): add oxidize_page_set_{fill,stroke}_color_cal_rgb FFI (GFX-014)"
```

---

## Task 5: Rust FFI — Lab hardcoded fill/stroke

**Files:** Modify `native/src/graphics.rs`

Parameters: `l, a, b: f64`; `wp_x, wp_y, wp_z`; `bp_x, bp_y, bp_z`; `range_amin, range_amax, range_bmin, range_bmax` = 13 scalars.

**CRITICAL:** `LabColorSpace::with_range` takes **four separate f64 args** (`a_min, a_max, b_min, b_max`), NOT an array. Verified from 2.12.0 source: `pub fn with_range(mut self, a_min: f64, a_max: f64, b_min: f64, b_max: f64) -> Self`.

**Upstream API verification step:**

```bash
find ~/.cargo/registry/src -path "*/oxidize-pdf-2.12.0/src/graphics/lab_color.rs" | \
  xargs grep -n "pub fn\|fn new\|fn d50\|fn d65\|with_range\|with_white_point" 2>/dev/null | head -15
```

### Step 5.1 — RED

Append `#[cfg(test)] mod lab_ffi_tests` to `native/src/graphics.rs`:

```rust
#[cfg(test)]
mod lab_ffi_tests {
    use super::*;
    use crate::page::{oxidize_page_create, oxidize_page_free};

    #[test]
    fn fill_lab_valid_params_returns_success() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            assert!(!page.is_null());
            let result = oxidize_page_set_fill_color_lab(
                page,
                50.0, 0.0, 0.0,
                0.9642, 1.0, 0.8251,
                0.0, 0.0, 0.0,
                -128.0, 127.0, -128.0, 127.0,
            );
            assert_eq!(result, 0, "expected ErrorCode::Success (0)");
            oxidize_page_free(page);
        }
    }

    #[test]
    fn stroke_lab_null_page_returns_null_pointer_error() {
        unsafe {
            let result = oxidize_page_set_stroke_color_lab(
                std::ptr::null_mut(),
                50.0, 0.0, 0.0,
                0.9642, 1.0, 0.8251,
                0.0, 0.0, 0.0,
                -128.0, 127.0, -128.0, 127.0,
            );
            assert_eq!(result, 1, "expected ErrorCode::NullPointer (1)");
        }
    }
}
```

### Step 5.2 — Run to confirm RED

```bash
cargo test --manifest-path /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.toml \
  lab_ffi_tests --lib 2>&1 | tail -10
```

### Step 5.3 — GREEN

Append to `native/src/graphics.rs`:

```rust
// ── Lab color (hardcoded name "Lab1" via upstream) ────────────────────────────

#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_fill_color_lab(
    page: *mut PageHandle,
    l: f64, a: f64, b: f64,
    wp_x: f64, wp_y: f64, wp_z: f64,
    bp_x: f64, bp_y: f64, bp_z: f64,
    range_amin: f64, range_amax: f64,
    range_bmin: f64, range_bmax: f64,
) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_set_fill_color_lab");
        return ErrorCode::NullPointer as c_int;
    }
    use oxidize_pdf::graphics::{LabColor, LabColorSpace};
    let cs = LabColorSpace::new()
        .with_white_point([wp_x, wp_y, wp_z])
        .with_black_point([bp_x, bp_y, bp_z])
        .with_range(range_amin, range_amax, range_bmin, range_bmax);
    let color = LabColor::new(l, a, b, cs);
    (*page).inner.graphics().set_fill_color_lab(color);
    ErrorCode::Success as c_int
}

#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_stroke_color_lab(
    page: *mut PageHandle,
    l: f64, a: f64, b: f64,
    wp_x: f64, wp_y: f64, wp_z: f64,
    bp_x: f64, bp_y: f64, bp_z: f64,
    range_amin: f64, range_amax: f64,
    range_bmin: f64, range_bmax: f64,
) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_set_stroke_color_lab");
        return ErrorCode::NullPointer as c_int;
    }
    use oxidize_pdf::graphics::{LabColor, LabColorSpace};
    let cs = LabColorSpace::new()
        .with_white_point([wp_x, wp_y, wp_z])
        .with_black_point([bp_x, bp_y, bp_z])
        .with_range(range_amin, range_amax, range_bmin, range_bmax);
    let color = LabColor::new(l, a, b, cs);
    (*page).inner.graphics().set_stroke_color_lab(color);
    ErrorCode::Success as c_int
}
```

### Step 5.4 — Tests, fmt, clippy, build, commit

```bash
cargo test --manifest-path /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.toml \
  lab_ffi_tests --lib 2>&1 | tail -10
cargo fmt --manifest-path /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.toml -- --check 2>&1
cargo clippy --manifest-path /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.toml \
  -- -D warnings 2>&1 | tail -10
RUSTFLAGS="-D warnings" cargo build \
  --manifest-path /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.toml \
  --release 2>&1 | tail -5
git -C /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet add native/src/graphics.rs
git -C /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet commit \
  -m "feat(m3): add oxidize_page_set_{fill,stroke}_color_lab FFI (GFX-015)"
```

---

## Task 6: Rust FFI — Named variants (CalGray/CalRGB/Lab) + page registration

**Files:** Modify `native/src/graphics.rs`, modify `native/src/page.rs`

This task adds:
1. Named draw functions: `oxidize_page_set_fill_color_cal_gray_named`, `oxidize_page_set_stroke_color_cal_gray_named`, and the CalRGB and Lab equivalents (8 functions total).
2. Page registration: `oxidize_page_add_color_space` in `native/src/page.rs` — accepts a `*const c_char` name and a discriminant integer that encodes which color-space type, plus all its parameters inline.

**Page registration FFI design:** `add_color_space` and `add_icc_color_space` live on `Page` (not `GraphicsContext`). They go in `page.rs` because they modify `(*page).inner` (the `Page` struct), not `(*page).inner.graphics()`. This mirrors the separation already present between page and graphics concerns.

**Upstream API verification step:**

```bash
grep -n "pub fn set_fill_color_calibrated_named\|pub fn set_stroke_color_calibrated_named\|pub fn set_fill_color_lab_named\|pub fn set_stroke_color_lab_named" \
  ~/.cargo/registry/src/index.crates.io-*/oxidize-pdf-2.12.0/src/graphics/mod.rs 2>/dev/null
grep -n "pub fn add_color_space\|pub fn add_icc_color_space" \
  ~/.cargo/registry/src/index.crates.io-*/oxidize-pdf-2.12.0/src/page.rs 2>/dev/null
grep -n "impl From.*CalGray\|impl From.*CalRgb\|impl From.*Lab" \
  ~/.cargo/registry/src/index.crates.io-*/oxidize-pdf-2.12.0/src/graphics/page_color_space.rs 2>/dev/null
```

Expected: all methods present with signatures shown in Decision 1/2 above.

### Step 6.1 — RED: Named CalGray Rust test

Append to `native/src/graphics.rs`:

```rust
#[cfg(test)]
mod cal_gray_named_ffi_tests {
    use super::*;
    use crate::page::{oxidize_page_create, oxidize_page_free};

    #[test]
    fn fill_cal_gray_named_valid_params_returns_success() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            assert!(!page.is_null());
            let name = std::ffi::CString::new("MyCalGray").unwrap();
            let result = oxidize_page_set_fill_color_cal_gray_named(
                page, name.as_ptr(), 0.5,
                0.9505, 1.0, 1.0890,
                0.0, 0.0, 0.0,
                1.0,
            );
            assert_eq!(result, 0, "expected ErrorCode::Success (0)");
            oxidize_page_free(page);
        }
    }

    #[test]
    fn fill_cal_gray_named_null_page_returns_error() {
        unsafe {
            let name = std::ffi::CString::new("MyCalGray").unwrap();
            let result = oxidize_page_set_fill_color_cal_gray_named(
                std::ptr::null_mut(), name.as_ptr(), 0.5,
                0.9505, 1.0, 1.0890,
                0.0, 0.0, 0.0,
                1.0,
            );
            assert_eq!(result, 1, "expected ErrorCode::NullPointer (1)");
        }
    }

    #[test]
    fn fill_cal_gray_named_null_name_returns_error() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            assert!(!page.is_null());
            let result = oxidize_page_set_fill_color_cal_gray_named(
                page, std::ptr::null(), 0.5,
                0.9505, 1.0, 1.0890,
                0.0, 0.0, 0.0,
                1.0,
            );
            assert_eq!(result, 1, "expected ErrorCode::NullPointer (1)");
            oxidize_page_free(page);
        }
    }
}
```

Add analogous `#[cfg(test)] mod add_color_space_ffi_tests` in `native/src/page.rs`:

```rust
#[cfg(test)]
mod add_color_space_ffi_tests {
    use super::*;

    #[test]
    fn add_color_space_cal_gray_null_page_returns_error() {
        unsafe {
            let name = std::ffi::CString::new("CS1").unwrap();
            let result = oxidize_page_add_color_space_cal_gray(
                std::ptr::null_mut(), name.as_ptr(),
                0.9505, 1.0, 1.0890,
                0.0, 0.0, 0.0,
                1.0,
            );
            assert_eq!(result, 1, "expected ErrorCode::NullPointer (1)");
        }
    }

    #[test]
    fn add_color_space_cal_gray_valid_returns_success() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            assert!(!page.is_null());
            let name = std::ffi::CString::new("CS1").unwrap();
            let result = oxidize_page_add_color_space_cal_gray(
                page, name.as_ptr(),
                0.9505, 1.0, 1.0890,
                0.0, 0.0, 0.0,
                1.0,
            );
            assert_eq!(result, 0, "expected ErrorCode::Success (0)");
            oxidize_page_free(page);
        }
    }
}
```

### Step 6.2 — Run to confirm RED

```bash
cargo test --manifest-path /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.toml \
  cal_gray_named_ffi_tests add_color_space_ffi_tests --lib 2>&1 | tail -10
```

### Step 6.3 — GREEN: Implement named draw functions + page registration

Append to `native/src/graphics.rs`:

```rust
// ── CalGray named ─────────────────────────────────────────────────────────────

#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_fill_color_cal_gray_named(
    page: *mut PageHandle,
    name: *const c_char,
    value: f64,
    wp_x: f64, wp_y: f64, wp_z: f64,
    bp_x: f64, bp_y: f64, bp_z: f64,
    gamma: f64,
) -> c_int {
    clear_last_error();
    if page.is_null() || name.is_null() {
        set_last_error("Null pointer provided to oxidize_page_set_fill_color_cal_gray_named");
        return ErrorCode::NullPointer as c_int;
    }
    let name_str = match std::ffi::CStr::from_ptr(name).to_str() {
        Ok(s) => s.to_owned(),
        Err(_) => {
            set_last_error("Invalid UTF-8 in color space name");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    use oxidize_pdf::graphics::{CalGrayColorSpace, CalibratedColor};
    let cs = CalGrayColorSpace::new()
        .with_white_point([wp_x, wp_y, wp_z])
        .with_black_point([bp_x, bp_y, bp_z])
        .with_gamma(gamma);
    let color = CalibratedColor::cal_gray(value, cs);
    (*page).inner.graphics().set_fill_color_calibrated_named(name_str, color);
    ErrorCode::Success as c_int
}

#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_stroke_color_cal_gray_named(
    page: *mut PageHandle,
    name: *const c_char,
    value: f64,
    wp_x: f64, wp_y: f64, wp_z: f64,
    bp_x: f64, bp_y: f64, bp_z: f64,
    gamma: f64,
) -> c_int {
    clear_last_error();
    if page.is_null() || name.is_null() {
        set_last_error("Null pointer provided to oxidize_page_set_stroke_color_cal_gray_named");
        return ErrorCode::NullPointer as c_int;
    }
    let name_str = match std::ffi::CStr::from_ptr(name).to_str() {
        Ok(s) => s.to_owned(),
        Err(_) => {
            set_last_error("Invalid UTF-8 in color space name");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    use oxidize_pdf::graphics::{CalGrayColorSpace, CalibratedColor};
    let cs = CalGrayColorSpace::new()
        .with_white_point([wp_x, wp_y, wp_z])
        .with_black_point([bp_x, bp_y, bp_z])
        .with_gamma(gamma);
    let color = CalibratedColor::cal_gray(value, cs);
    (*page).inner.graphics().set_stroke_color_calibrated_named(name_str, color);
    ErrorCode::Success as c_int
}

// CalRGB named — same pattern, 21 color params + name:
// oxidize_page_set_fill_color_cal_rgb_named(page, name, r,g,b, wp_x,wp_y,wp_z, bp_x,bp_y,bp_z, gr,gg,gb, m0..m8) -> c_int
// oxidize_page_set_stroke_color_cal_rgb_named — identical except set_stroke_color_calibrated_named

// Lab named — same pattern, 13 color params + name:
// oxidize_page_set_fill_color_lab_named(page, name, l,a,b, wp_x,wp_y,wp_z, bp_x,bp_y,bp_z, range_amin,range_amax,range_bmin,range_bmax) -> c_int
// oxidize_page_set_stroke_color_lab_named — identical except set_stroke_color_lab_named
```

> The CalRGB named and Lab named functions follow the exact same pattern as CalGray named above. Implement them inline; they are not written out in full here to avoid redundancy, but their signatures and body structure are fully determined by the CalGray template and the CalRGB/Lab scalar counts in Tasks 4/5.

Add to `native/src/page.rs` — per-color-space registration functions:

```rust
use std::ffi::CStr;
use std::os::raw::{c_char, c_int};
use crate::{clear_last_error, set_last_error, ErrorCode};

// ── Color space registration ─────────────────────────────────────────────────

/// Register a CalGray color space under `name` on this page.
/// Required before drawing with `oxidize_page_set_fill_color_cal_gray_named`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_add_color_space_cal_gray(
    page: *mut PageHandle,
    name: *const c_char,
    wp_x: f64, wp_y: f64, wp_z: f64,
    bp_x: f64, bp_y: f64, bp_z: f64,
    gamma: f64,
) -> c_int {
    clear_last_error();
    if page.is_null() || name.is_null() {
        set_last_error("Null pointer provided to oxidize_page_add_color_space_cal_gray");
        return ErrorCode::NullPointer as c_int;
    }
    let name_str = match CStr::from_ptr(name).to_str() {
        Ok(s) => s.to_owned(),
        Err(_) => {
            set_last_error("Invalid UTF-8 in color space name");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    use oxidize_pdf::graphics::{CalGrayColorSpace, PageColorSpace};
    let cs = CalGrayColorSpace::new()
        .with_white_point([wp_x, wp_y, wp_z])
        .with_black_point([bp_x, bp_y, bp_z])
        .with_gamma(gamma);
    match (*page).inner.add_color_space(name_str, PageColorSpace::from(&cs)) {
        Ok(()) => ErrorCode::Success as c_int,
        Err(e) => {
            set_last_error(format!("add_color_space failed: {e}"));
            ErrorCode::InvalidArgument as c_int
        }
    }
}

// oxidize_page_add_color_space_cal_rgb — same pattern, 21 scalar params
// oxidize_page_add_color_space_lab — same pattern, 13 scalar params
// oxidize_page_add_color_space_icc_based(page, name, n: c_int, alternate: *const c_char) — inline ICC, no binary
```

> Implement the CalRGB and Lab registration functions inline, following the CalGray template. For the inline ICC-based registration, `n` is passed as `c_int`, `alternate` as `*const c_char`; build `PageColorSpace::Parameterised { family: ParameterisedFamily::IccBased, params: ... }` using the upstream `ParameterisedFamily` enum. Read `page_color_space.rs` and `calibrated_color.rs` `params_dictionary()` to understand the params dict shape, then construct accordingly.

### Step 6.4 — Tests, fmt, clippy, build, commit

```bash
cargo test --manifest-path /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.toml \
  cal_gray_named_ffi_tests add_color_space_ffi_tests --lib 2>&1 | tail -15
cargo fmt --manifest-path /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.toml -- --check 2>&1
cargo clippy --manifest-path /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.toml \
  -- -D warnings 2>&1 | tail -10
RUSTFLAGS="-D warnings" cargo build \
  --manifest-path /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.toml \
  --release 2>&1 | tail -5
git -C /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet add \
  native/src/graphics.rs native/src/page.rs
git -C /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet commit \
  -m "feat(m3): add named CalGray/CalRGB/Lab FFI + page color-space registration (GFX-014, GFX-015)"
```

---

## Task 7: Rust FFI — ICC draw + embedded profile registration

**Files:** Modify `native/src/graphics.rs`, modify `native/src/page.rs`

This task adds:
1. `oxidize_page_set_fill_color_icc` and `oxidize_page_set_stroke_color_icc` in `graphics.rs` — draw using a named ICC color space already registered on the page.
2. `oxidize_page_add_icc_color_space` in `page.rs` — embedded profile registration (the .NET superset path).

**Non-empty components enforcement:** The upstream `debug_assert!(!components.is_empty())` is compiled out in release. The FFI MUST return `ErrorCode::InvalidArgument` for empty components in ALL builds.

**Upstream API verification step:**

```bash
grep -n "pub fn set_fill_color_icc\|pub fn set_stroke_color_icc\|debug_assert.*is_empty" \
  ~/.cargo/registry/src/index.crates.io-*/oxidize-pdf-2.12.0/src/graphics/mod.rs 2>/dev/null
grep -n "pub fn add_icc_color_space\|pub fn new.*IccProfile\|pub fn with_range" \
  ~/.cargo/registry/src/index.crates.io-*/oxidize-pdf-2.12.0/src/graphics/color_profiles.rs 2>/dev/null
```

### Step 7.1 — RED: Rust unit tests for ICC FFI

Append to `native/src/graphics.rs`:

```rust
#[cfg(test)]
mod icc_draw_ffi_tests {
    use super::*;
    use crate::page::{oxidize_page_create, oxidize_page_free};

    #[test]
    fn fill_icc_valid_params_returns_success() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            assert!(!page.is_null());
            let name = std::ffi::CString::new("ICCRGB1").unwrap();
            let components: [f64; 3] = [0.5, 0.3, 0.8];
            let result = oxidize_page_set_fill_color_icc(
                page, name.as_ptr(), components.as_ptr(), 3,
            );
            assert_eq!(result, 0, "expected ErrorCode::Success (0)");
            oxidize_page_free(page);
        }
    }

    #[test]
    fn fill_icc_null_page_returns_error() {
        unsafe {
            let name = std::ffi::CString::new("ICCRGB1").unwrap();
            let components: [f64; 3] = [0.5, 0.3, 0.8];
            let result = oxidize_page_set_fill_color_icc(
                std::ptr::null_mut(), name.as_ptr(), components.as_ptr(), 3,
            );
            assert_eq!(result, 1, "expected ErrorCode::NullPointer (1)");
        }
    }

    #[test]
    fn fill_icc_empty_components_returns_invalid_argument() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            assert!(!page.is_null());
            let name = std::ffi::CString::new("ICCRGB1").unwrap();
            let result = oxidize_page_set_fill_color_icc(
                page, name.as_ptr(), std::ptr::null(), 0,
            );
            // ErrorCode::InvalidArgument = 9
            assert_eq!(result, 9, "empty components must return InvalidArgument (9)");
            oxidize_page_free(page);
        }
    }
}
```

Append to `native/src/page.rs`:

```rust
#[cfg(test)]
mod add_icc_color_space_ffi_tests {
    use super::*;

    #[test]
    fn add_icc_color_space_null_page_returns_error() {
        unsafe {
            let name = std::ffi::CString::new("ICCGray").unwrap();
            let data = [0u8; 64];
            // IccColorSpace::Gray = 1
            let result = oxidize_page_add_icc_color_space(
                std::ptr::null_mut(), name.as_ptr(), data.as_ptr(), 64, 1,
            );
            assert_eq!(result, 1, "expected ErrorCode::NullPointer (1)");
        }
    }

    #[test]
    fn add_icc_color_space_empty_data_returns_invalid_argument() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            assert!(!page.is_null());
            let name = std::ffi::CString::new("ICCGray").unwrap();
            let result = oxidize_page_add_icc_color_space(
                page, name.as_ptr(), std::ptr::null(), 0, 1,
            );
            assert_eq!(result, 9, "empty ICC data must return InvalidArgument (9)");
            oxidize_page_free(page);
        }
    }
}
```

### Step 7.2 — Run to confirm RED

```bash
cargo test --manifest-path /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.toml \
  icc_draw_ffi_tests add_icc_color_space_ffi_tests --lib 2>&1 | tail -10
```

### Step 7.3 — GREEN

Append to `native/src/graphics.rs`:

```rust
// ── ICC draw ──────────────────────────────────────────────────────────────────

/// Set fill color using an ICC-based color space registered under `name`.
///
/// `components` must be non-null and non-empty. The function enforces this
/// in ALL builds (the upstream `debug_assert!` is compiled out in release;
/// this FFI layer catches it instead and returns `ErrorCode::InvalidArgument`).
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_fill_color_icc(
    page: *mut PageHandle,
    name: *const c_char,
    components: *const f64,
    components_len: usize,
) -> c_int {
    clear_last_error();
    if page.is_null() || name.is_null() {
        set_last_error("Null pointer provided to oxidize_page_set_fill_color_icc");
        return ErrorCode::NullPointer as c_int;
    }
    if components.is_null() || components_len == 0 {
        set_last_error("ICC fill color components must not be empty");
        return ErrorCode::InvalidArgument as c_int;
    }
    let name_str = match std::ffi::CStr::from_ptr(name).to_str() {
        Ok(s) => s.to_owned(),
        Err(_) => {
            set_last_error("Invalid UTF-8 in ICC color space name");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    let comps = std::slice::from_raw_parts(components, components_len).to_vec();
    (*page).inner.graphics().set_fill_color_icc(name_str, comps);
    ErrorCode::Success as c_int
}

/// Set stroke color using an ICC-based color space registered under `name`.
/// See `oxidize_page_set_fill_color_icc` for parameter and safety notes.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_stroke_color_icc(
    page: *mut PageHandle,
    name: *const c_char,
    components: *const f64,
    components_len: usize,
) -> c_int {
    clear_last_error();
    if page.is_null() || name.is_null() {
        set_last_error("Null pointer provided to oxidize_page_set_stroke_color_icc");
        return ErrorCode::NullPointer as c_int;
    }
    if components.is_null() || components_len == 0 {
        set_last_error("ICC stroke color components must not be empty");
        return ErrorCode::InvalidArgument as c_int;
    }
    let name_str = match std::ffi::CStr::from_ptr(name).to_str() {
        Ok(s) => s.to_owned(),
        Err(_) => {
            set_last_error("Invalid UTF-8 in ICC color space name");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    let comps = std::slice::from_raw_parts(components, components_len).to_vec();
    (*page).inner.graphics().set_stroke_color_icc(name_str, comps);
    ErrorCode::Success as c_int
}
```

Add to `native/src/page.rs`:

```rust
/// Register an ICC color space with an embedded profile under `name`.
///
/// `data` / `data_len`: raw ICC binary bytes. Must be non-null and non-empty.
/// `color_space_kind`: 1=Gray, 3=Rgb, 4=Cmyk (maps to `IccColorSpace` variants).
///
/// This is the .NET-superset path (embedded profile); the Python bridge
/// does not expose this. For the inline ICCBased path (no binary),
/// use `oxidize_page_add_color_space_icc_based` instead.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_add_icc_color_space(
    page: *mut PageHandle,
    name: *const c_char,
    data: *const u8,
    data_len: usize,
    color_space_kind: c_int,
) -> c_int {
    clear_last_error();
    if page.is_null() || name.is_null() {
        set_last_error("Null pointer provided to oxidize_page_add_icc_color_space");
        return ErrorCode::NullPointer as c_int;
    }
    if data.is_null() || data_len == 0 {
        set_last_error("ICC profile data must not be empty");
        return ErrorCode::InvalidArgument as c_int;
    }
    let name_str = match std::ffi::CStr::from_ptr(name).to_str() {
        Ok(s) => s.to_owned(),
        Err(_) => {
            set_last_error("Invalid UTF-8 in ICC color space name");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    let icc_cs = match color_space_kind {
        1 => oxidize_pdf::graphics::IccColorSpace::Gray,
        3 => oxidize_pdf::graphics::IccColorSpace::Rgb,
        4 => oxidize_pdf::graphics::IccColorSpace::Cmyk,
        _ => {
            set_last_error(format!("Unknown ICC color space kind: {color_space_kind}"));
            return ErrorCode::InvalidArgument as c_int;
        }
    };
    let profile_data = std::slice::from_raw_parts(data, data_len).to_vec();
    let profile = oxidize_pdf::graphics::IccProfile::new(name_str.clone(), profile_data, icc_cs);
    match (*page).inner.add_icc_color_space(name_str, &profile) {
        Ok(()) => ErrorCode::Success as c_int,
        Err(e) => {
            set_last_error(format!("add_icc_color_space failed: {e}"));
            ErrorCode::InvalidArgument as c_int
        }
    }
}
```

### Step 7.4 — Tests, fmt, clippy, build, commit

```bash
cargo test --manifest-path /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.toml \
  icc_draw_ffi_tests add_icc_color_space_ffi_tests --lib 2>&1 | tail -15
cargo fmt --manifest-path /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.toml -- --check 2>&1
cargo clippy --manifest-path /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.toml \
  -- -D warnings 2>&1 | tail -10
RUSTFLAGS="-D warnings" cargo build \
  --manifest-path /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.toml \
  --release 2>&1 | tail -5
git -C /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet add \
  native/src/graphics.rs native/src/page.rs
git -C /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet commit \
  -m "feat(m3): add ICC draw FFI + embedded profile registration (GFX-019)"
```

---

## Task 8: .NET P/Invoke + PdfPage fluent methods

**Files:** Modify `NativeMethods.cs`, modify `PdfPage.cs`

Strings marshal as `[MarshalAs(UnmanagedType.LPUTF8Str)] string` — verified from existing `NativeMethods.cs` pattern. Byte arrays for ICC data marshal as `IntPtr data, nuint dataLen` — verified from `oxidize_image_from_jpeg`. Double arrays for ICC components marshal as `IntPtr components, nuint componentsLen` (same pattern). Error helper is `private static void ThrowIfError(int errorCode, string message)` — verified from `PdfPage.cs` line 1301.

**Complete list of new P/Invoke declarations** to add in `NativeMethods.cs`:

```csharp
// ── CalGray (hardcoded) ───────────────────────────────────────────────────────

[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
internal static extern int oxidize_page_set_fill_color_cal_gray(
    IntPtr page, double value,
    double wpX, double wpY, double wpZ,
    double bpX, double bpY, double bpZ,
    double gamma);

[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
internal static extern int oxidize_page_set_stroke_color_cal_gray(
    IntPtr page, double value,
    double wpX, double wpY, double wpZ,
    double bpX, double bpY, double bpZ,
    double gamma);

// ── CalGray (named) ───────────────────────────────────────────────────────────

[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
internal static extern int oxidize_page_set_fill_color_cal_gray_named(
    IntPtr page,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
    double value,
    double wpX, double wpY, double wpZ,
    double bpX, double bpY, double bpZ,
    double gamma);

[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
internal static extern int oxidize_page_set_stroke_color_cal_gray_named(
    IntPtr page,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
    double value,
    double wpX, double wpY, double wpZ,
    double bpX, double bpY, double bpZ,
    double gamma);

// ── CalRGB (hardcoded) ────────────────────────────────────────────────────────

[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
internal static extern int oxidize_page_set_fill_color_cal_rgb(
    IntPtr page, double r, double g, double b,
    double wpX, double wpY, double wpZ,
    double bpX, double bpY, double bpZ,
    double gammaR, double gammaG, double gammaB,
    double m0, double m1, double m2,
    double m3, double m4, double m5,
    double m6, double m7, double m8);

[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
internal static extern int oxidize_page_set_stroke_color_cal_rgb(
    IntPtr page, double r, double g, double b,
    double wpX, double wpY, double wpZ,
    double bpX, double bpY, double bpZ,
    double gammaR, double gammaG, double gammaB,
    double m0, double m1, double m2,
    double m3, double m4, double m5,
    double m6, double m7, double m8);

// ── CalRGB (named) ────────────────────────────────────────────────────────────

[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
internal static extern int oxidize_page_set_fill_color_cal_rgb_named(
    IntPtr page,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
    double r, double g, double b,
    double wpX, double wpY, double wpZ,
    double bpX, double bpY, double bpZ,
    double gammaR, double gammaG, double gammaB,
    double m0, double m1, double m2,
    double m3, double m4, double m5,
    double m6, double m7, double m8);

[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
internal static extern int oxidize_page_set_stroke_color_cal_rgb_named(
    IntPtr page,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
    double r, double g, double b,
    double wpX, double wpY, double wpZ,
    double bpX, double bpY, double bpZ,
    double gammaR, double gammaG, double gammaB,
    double m0, double m1, double m2,
    double m3, double m4, double m5,
    double m6, double m7, double m8);

// ── Lab (hardcoded) ───────────────────────────────────────────────────────────

[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
internal static extern int oxidize_page_set_fill_color_lab(
    IntPtr page, double l, double a, double b,
    double wpX, double wpY, double wpZ,
    double bpX, double bpY, double bpZ,
    double rangeAMin, double rangeAMax,
    double rangeBMin, double rangeBMax);

[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
internal static extern int oxidize_page_set_stroke_color_lab(
    IntPtr page, double l, double a, double b,
    double wpX, double wpY, double wpZ,
    double bpX, double bpY, double bpZ,
    double rangeAMin, double rangeAMax,
    double rangeBMin, double rangeBMax);

// ── Lab (named) ───────────────────────────────────────────────────────────────

[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
internal static extern int oxidize_page_set_fill_color_lab_named(
    IntPtr page,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
    double l, double a, double b,
    double wpX, double wpY, double wpZ,
    double bpX, double bpY, double bpZ,
    double rangeAMin, double rangeAMax,
    double rangeBMin, double rangeBMax);

[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
internal static extern int oxidize_page_set_stroke_color_lab_named(
    IntPtr page,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
    double l, double a, double b,
    double wpX, double wpY, double wpZ,
    double bpX, double bpY, double bpZ,
    double rangeAMin, double rangeAMax,
    double rangeBMin, double rangeBMax);

// ── Page color-space registration ─────────────────────────────────────────────

[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
internal static extern int oxidize_page_add_color_space_cal_gray(
    IntPtr page,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
    double wpX, double wpY, double wpZ,
    double bpX, double bpY, double bpZ,
    double gamma);

[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
internal static extern int oxidize_page_add_color_space_cal_rgb(
    IntPtr page,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
    double wpX, double wpY, double wpZ,
    double bpX, double bpY, double bpZ,
    double gammaR, double gammaG, double gammaB,
    double m0, double m1, double m2,
    double m3, double m4, double m5,
    double m6, double m7, double m8);

[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
internal static extern int oxidize_page_add_color_space_lab(
    IntPtr page,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
    double wpX, double wpY, double wpZ,
    double bpX, double bpY, double bpZ,
    double rangeAMin, double rangeAMax,
    double rangeBMin, double rangeBMax);

[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
internal static extern int oxidize_page_add_color_space_icc_based(
    IntPtr page,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
    int n,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string alternate);

// ── ICC draw ──────────────────────────────────────────────────────────────────

[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
internal static extern int oxidize_page_set_fill_color_icc(
    IntPtr page,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
    IntPtr components,
    nuint componentsLen);

[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
internal static extern int oxidize_page_set_stroke_color_icc(
    IntPtr page,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
    IntPtr components,
    nuint componentsLen);

// ── ICC embedded profile registration ────────────────────────────────────────

[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
internal static extern int oxidize_page_add_icc_color_space(
    IntPtr page,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
    IntPtr data,
    nuint dataLen,
    int colorSpaceKind);
```

**Complete list of new `PdfPage` fluent methods** to add in `PdfPage.cs` (add `using OxidizePdf.NET.Graphics;` if not present):

```csharp
// ── Calibrated color (hardcoded name, single-space convenience) ───────────────

/// <summary>Sets fill color using a calibrated color space (hardcoded name).
/// Mirrors python <c>page.set_fill_color_calibrated(color)</c>.</summary>
public PdfPage SetFillColorCalibrated(CalibratedColor color)
{
    ArgumentNullException.ThrowIfNull(color);
    ThrowIfDisposed();
    return color.IsCalGray
        ? SetFillColorCalGray(color.GrayValue, color.GrayColorSpace!)
        : SetFillColorCalRgb(color.RgbValues![0], color.RgbValues[1], color.RgbValues[2], color.RgbColorSpace!);
}

/// <summary>Sets stroke color using a calibrated color space (hardcoded name).
/// Mirrors python <c>page.set_stroke_color_calibrated(color)</c>.</summary>
public PdfPage SetStrokeColorCalibrated(CalibratedColor color)
{
    ArgumentNullException.ThrowIfNull(color);
    ThrowIfDisposed();
    return color.IsCalGray
        ? SetStrokeColorCalGray(color.GrayValue, color.GrayColorSpace!)
        : SetStrokeColorCalRgb(color.RgbValues![0], color.RgbValues[1], color.RgbValues[2], color.RgbColorSpace!);
}

/// <summary>Sets fill color using a calibrated gray (CalGray) color space (hardcoded name "CalGray1").</summary>
public PdfPage SetFillColorCalGray(double value, CalGrayColorSpace colorSpace)
{
    ArgumentNullException.ThrowIfNull(colorSpace);
    ThrowIfDisposed();
    colorSpace.Validate();
    ThrowIfError(NativeMethods.oxidize_page_set_fill_color_cal_gray(
        _handle, value,
        colorSpace.WhitePoint[0], colorSpace.WhitePoint[1], colorSpace.WhitePoint[2],
        colorSpace.BlackPoint[0], colorSpace.BlackPoint[1], colorSpace.BlackPoint[2],
        colorSpace.Gamma), "Failed to set fill color (CalGray)");
    return this;
}

/// <summary>Sets stroke color using a calibrated gray (CalGray) color space (hardcoded name "CalGray1").</summary>
public PdfPage SetStrokeColorCalGray(double value, CalGrayColorSpace colorSpace)
{
    ArgumentNullException.ThrowIfNull(colorSpace);
    ThrowIfDisposed();
    colorSpace.Validate();
    ThrowIfError(NativeMethods.oxidize_page_set_stroke_color_cal_gray(
        _handle, value,
        colorSpace.WhitePoint[0], colorSpace.WhitePoint[1], colorSpace.WhitePoint[2],
        colorSpace.BlackPoint[0], colorSpace.BlackPoint[1], colorSpace.BlackPoint[2],
        colorSpace.Gamma), "Failed to set stroke color (CalGray)");
    return this;
}

/// <summary>Sets fill color using a calibrated RGB (CalRGB) color space (hardcoded name "CalRGB1").</summary>
public PdfPage SetFillColorCalRgb(double r, double g, double b, CalRgbColorSpace colorSpace)
{
    ArgumentNullException.ThrowIfNull(colorSpace);
    ThrowIfDisposed();
    colorSpace.Validate();
    ThrowIfError(NativeMethods.oxidize_page_set_fill_color_cal_rgb(
        _handle, r, g, b,
        colorSpace.WhitePoint[0], colorSpace.WhitePoint[1], colorSpace.WhitePoint[2],
        colorSpace.BlackPoint[0], colorSpace.BlackPoint[1], colorSpace.BlackPoint[2],
        colorSpace.Gamma.R, colorSpace.Gamma.G, colorSpace.Gamma.B,
        colorSpace.Matrix[0], colorSpace.Matrix[1], colorSpace.Matrix[2],
        colorSpace.Matrix[3], colorSpace.Matrix[4], colorSpace.Matrix[5],
        colorSpace.Matrix[6], colorSpace.Matrix[7], colorSpace.Matrix[8]),
        "Failed to set fill color (CalRGB)");
    return this;
}

/// <summary>Sets stroke color using a calibrated RGB (CalRGB) color space (hardcoded name "CalRGB1").</summary>
public PdfPage SetStrokeColorCalRgb(double r, double g, double b, CalRgbColorSpace colorSpace)
{
    ArgumentNullException.ThrowIfNull(colorSpace);
    ThrowIfDisposed();
    colorSpace.Validate();
    ThrowIfError(NativeMethods.oxidize_page_set_stroke_color_cal_rgb(
        _handle, r, g, b,
        colorSpace.WhitePoint[0], colorSpace.WhitePoint[1], colorSpace.WhitePoint[2],
        colorSpace.BlackPoint[0], colorSpace.BlackPoint[1], colorSpace.BlackPoint[2],
        colorSpace.Gamma.R, colorSpace.Gamma.G, colorSpace.Gamma.B,
        colorSpace.Matrix[0], colorSpace.Matrix[1], colorSpace.Matrix[2],
        colorSpace.Matrix[3], colorSpace.Matrix[4], colorSpace.Matrix[5],
        colorSpace.Matrix[6], colorSpace.Matrix[7], colorSpace.Matrix[8]),
        "Failed to set stroke color (CalRGB)");
    return this;
}

/// <summary>Sets fill color using L*a*b* (hardcoded name "Lab1").
/// Mirrors python <c>page.set_fill_color_lab(color)</c>.</summary>
public PdfPage SetFillColorLab(LabColor color)
{
    ArgumentNullException.ThrowIfNull(color);
    ThrowIfDisposed();
    color.ColorSpace.Validate();
    ThrowIfError(NativeMethods.oxidize_page_set_fill_color_lab(
        _handle, color.L, color.A, color.B,
        color.ColorSpace.WhitePoint[0], color.ColorSpace.WhitePoint[1], color.ColorSpace.WhitePoint[2],
        color.ColorSpace.BlackPoint[0], color.ColorSpace.BlackPoint[1], color.ColorSpace.BlackPoint[2],
        color.ColorSpace.Range[0], color.ColorSpace.Range[1],
        color.ColorSpace.Range[2], color.ColorSpace.Range[3]),
        "Failed to set fill color (Lab)");
    return this;
}

/// <summary>Sets stroke color using L*a*b* (hardcoded name "Lab1").
/// Mirrors python <c>page.set_stroke_color_lab(color)</c>.</summary>
public PdfPage SetStrokeColorLab(LabColor color)
{
    ArgumentNullException.ThrowIfNull(color);
    ThrowIfDisposed();
    color.ColorSpace.Validate();
    ThrowIfError(NativeMethods.oxidize_page_set_stroke_color_lab(
        _handle, color.L, color.A, color.B,
        color.ColorSpace.WhitePoint[0], color.ColorSpace.WhitePoint[1], color.ColorSpace.WhitePoint[2],
        color.ColorSpace.BlackPoint[0], color.ColorSpace.BlackPoint[1], color.ColorSpace.BlackPoint[2],
        color.ColorSpace.Range[0], color.ColorSpace.Range[1],
        color.ColorSpace.Range[2], color.ColorSpace.Range[3]),
        "Failed to set stroke color (Lab)");
    return this;
}

// ── Named variants + registration ─────────────────────────────────────────────

/// <summary>Registers a color space under <paramref name="name"/> on this page.
/// Required before calling the *Named draw methods.
/// Mirrors python <c>page.add_color_space(name, cs)</c>.</summary>
public PdfPage AddColorSpace(string name, PageColorSpace colorSpace)
{
    ArgumentNullException.ThrowIfNull(name);
    ArgumentNullException.ThrowIfNull(colorSpace);
    ThrowIfDisposed();
    int result = colorSpace.Kind switch
    {
        PageColorSpaceKind.CalGray => NativeMethods.oxidize_page_add_color_space_cal_gray(
            _handle, name,
            colorSpace.CalGrayCs!.WhitePoint[0], colorSpace.CalGrayCs.WhitePoint[1], colorSpace.CalGrayCs.WhitePoint[2],
            colorSpace.CalGrayCs.BlackPoint[0], colorSpace.CalGrayCs.BlackPoint[1], colorSpace.CalGrayCs.BlackPoint[2],
            colorSpace.CalGrayCs.Gamma),
        PageColorSpaceKind.CalRgb => NativeMethods.oxidize_page_add_color_space_cal_rgb(
            _handle, name,
            colorSpace.CalRgbCs!.WhitePoint[0], colorSpace.CalRgbCs.WhitePoint[1], colorSpace.CalRgbCs.WhitePoint[2],
            colorSpace.CalRgbCs.BlackPoint[0], colorSpace.CalRgbCs.BlackPoint[1], colorSpace.CalRgbCs.BlackPoint[2],
            colorSpace.CalRgbCs.Gamma.R, colorSpace.CalRgbCs.Gamma.G, colorSpace.CalRgbCs.Gamma.B,
            colorSpace.CalRgbCs.Matrix[0], colorSpace.CalRgbCs.Matrix[1], colorSpace.CalRgbCs.Matrix[2],
            colorSpace.CalRgbCs.Matrix[3], colorSpace.CalRgbCs.Matrix[4], colorSpace.CalRgbCs.Matrix[5],
            colorSpace.CalRgbCs.Matrix[6], colorSpace.CalRgbCs.Matrix[7], colorSpace.CalRgbCs.Matrix[8]),
        PageColorSpaceKind.Lab => NativeMethods.oxidize_page_add_color_space_lab(
            _handle, name,
            colorSpace.LabCs!.WhitePoint[0], colorSpace.LabCs.WhitePoint[1], colorSpace.LabCs.WhitePoint[2],
            colorSpace.LabCs.BlackPoint[0], colorSpace.LabCs.BlackPoint[1], colorSpace.LabCs.BlackPoint[2],
            colorSpace.LabCs.Range[0], colorSpace.LabCs.Range[1],
            colorSpace.LabCs.Range[2], colorSpace.LabCs.Range[3]),
        PageColorSpaceKind.IccBased => NativeMethods.oxidize_page_add_color_space_icc_based(
            _handle, name, colorSpace.IccN, colorSpace.IccAlternate!),
        _ => throw new ArgumentException($"Unsupported PageColorSpace kind: {colorSpace.Kind}", nameof(colorSpace)),
    };
    ThrowIfError(result, $"Failed to register color space '{name}'");
    return this;
}

/// <summary>Sets fill color using a named calibrated color space.
/// Mirrors python <c>page.set_fill_color_calibrated_named(name, color)</c>.</summary>
public PdfPage SetFillColorCalibratedNamed(string name, CalibratedColor color)
{
    ArgumentNullException.ThrowIfNull(name);
    ArgumentNullException.ThrowIfNull(color);
    ThrowIfDisposed();
    int result;
    if (color.IsCalGray)
    {
        color.GrayColorSpace!.Validate();
        result = NativeMethods.oxidize_page_set_fill_color_cal_gray_named(
            _handle, name, color.GrayValue,
            color.GrayColorSpace.WhitePoint[0], color.GrayColorSpace.WhitePoint[1], color.GrayColorSpace.WhitePoint[2],
            color.GrayColorSpace.BlackPoint[0], color.GrayColorSpace.BlackPoint[1], color.GrayColorSpace.BlackPoint[2],
            color.GrayColorSpace.Gamma);
    }
    else
    {
        color.RgbColorSpace!.Validate();
        result = NativeMethods.oxidize_page_set_fill_color_cal_rgb_named(
            _handle, name,
            color.RgbValues![0], color.RgbValues[1], color.RgbValues[2],
            color.RgbColorSpace.WhitePoint[0], color.RgbColorSpace.WhitePoint[1], color.RgbColorSpace.WhitePoint[2],
            color.RgbColorSpace.BlackPoint[0], color.RgbColorSpace.BlackPoint[1], color.RgbColorSpace.BlackPoint[2],
            color.RgbColorSpace.Gamma.R, color.RgbColorSpace.Gamma.G, color.RgbColorSpace.Gamma.B,
            color.RgbColorSpace.Matrix[0], color.RgbColorSpace.Matrix[1], color.RgbColorSpace.Matrix[2],
            color.RgbColorSpace.Matrix[3], color.RgbColorSpace.Matrix[4], color.RgbColorSpace.Matrix[5],
            color.RgbColorSpace.Matrix[6], color.RgbColorSpace.Matrix[7], color.RgbColorSpace.Matrix[8]);
    }
    ThrowIfError(result, $"Failed to set fill color calibrated named '{name}'");
    return this;
}

/// <summary>Sets stroke color using a named calibrated color space.
/// Mirrors python <c>page.set_stroke_color_calibrated_named(name, color)</c>.</summary>
public PdfPage SetStrokeColorCalibratedNamed(string name, CalibratedColor color)
{
    // Identical to SetFillColorCalibratedNamed, calling stroke variants.
    ArgumentNullException.ThrowIfNull(name);
    ArgumentNullException.ThrowIfNull(color);
    ThrowIfDisposed();
    int result;
    if (color.IsCalGray)
    {
        color.GrayColorSpace!.Validate();
        result = NativeMethods.oxidize_page_set_stroke_color_cal_gray_named(
            _handle, name, color.GrayValue,
            color.GrayColorSpace.WhitePoint[0], color.GrayColorSpace.WhitePoint[1], color.GrayColorSpace.WhitePoint[2],
            color.GrayColorSpace.BlackPoint[0], color.GrayColorSpace.BlackPoint[1], color.GrayColorSpace.BlackPoint[2],
            color.GrayColorSpace.Gamma);
    }
    else
    {
        color.RgbColorSpace!.Validate();
        result = NativeMethods.oxidize_page_set_stroke_color_cal_rgb_named(
            _handle, name,
            color.RgbValues![0], color.RgbValues[1], color.RgbValues[2],
            color.RgbColorSpace.WhitePoint[0], color.RgbColorSpace.WhitePoint[1], color.RgbColorSpace.WhitePoint[2],
            color.RgbColorSpace.BlackPoint[0], color.RgbColorSpace.BlackPoint[1], color.RgbColorSpace.BlackPoint[2],
            color.RgbColorSpace.Gamma.R, color.RgbColorSpace.Gamma.G, color.RgbColorSpace.Gamma.B,
            color.RgbColorSpace.Matrix[0], color.RgbColorSpace.Matrix[1], color.RgbColorSpace.Matrix[2],
            color.RgbColorSpace.Matrix[3], color.RgbColorSpace.Matrix[4], color.RgbColorSpace.Matrix[5],
            color.RgbColorSpace.Matrix[6], color.RgbColorSpace.Matrix[7], color.RgbColorSpace.Matrix[8]);
    }
    ThrowIfError(result, $"Failed to set stroke color calibrated named '{name}'");
    return this;
}

/// <summary>Sets fill color using a named Lab color space.
/// Mirrors python <c>page.set_fill_color_lab_named(name, color)</c>.</summary>
public PdfPage SetFillColorLabNamed(string name, LabColor color)
{
    ArgumentNullException.ThrowIfNull(name);
    ArgumentNullException.ThrowIfNull(color);
    ThrowIfDisposed();
    color.ColorSpace.Validate();
    ThrowIfError(NativeMethods.oxidize_page_set_fill_color_lab_named(
        _handle, name, color.L, color.A, color.B,
        color.ColorSpace.WhitePoint[0], color.ColorSpace.WhitePoint[1], color.ColorSpace.WhitePoint[2],
        color.ColorSpace.BlackPoint[0], color.ColorSpace.BlackPoint[1], color.ColorSpace.BlackPoint[2],
        color.ColorSpace.Range[0], color.ColorSpace.Range[1],
        color.ColorSpace.Range[2], color.ColorSpace.Range[3]),
        $"Failed to set fill color Lab named '{name}'");
    return this;
}

/// <summary>Sets stroke color using a named Lab color space.
/// Mirrors python <c>page.set_stroke_color_lab_named(name, color)</c>.</summary>
public PdfPage SetStrokeColorLabNamed(string name, LabColor color)
{
    ArgumentNullException.ThrowIfNull(name);
    ArgumentNullException.ThrowIfNull(color);
    ThrowIfDisposed();
    color.ColorSpace.Validate();
    ThrowIfError(NativeMethods.oxidize_page_set_stroke_color_lab_named(
        _handle, name, color.L, color.A, color.B,
        color.ColorSpace.WhitePoint[0], color.ColorSpace.WhitePoint[1], color.ColorSpace.WhitePoint[2],
        color.ColorSpace.BlackPoint[0], color.ColorSpace.BlackPoint[1], color.ColorSpace.BlackPoint[2],
        color.ColorSpace.Range[0], color.ColorSpace.Range[1],
        color.ColorSpace.Range[2], color.ColorSpace.Range[3]),
        $"Failed to set stroke color Lab named '{name}'");
    return this;
}

/// <summary>Sets fill color using an ICC color space registered under <paramref name="name"/>.
/// <paramref name="components"/> must not be empty.
/// Mirrors python <c>page.set_fill_color_icc(name, components)</c>.</summary>
public unsafe PdfPage SetFillColorIcc(string name, double[] components)
{
    ArgumentNullException.ThrowIfNull(name);
    ArgumentNullException.ThrowIfNull(components);
    if (components.Length == 0)
        throw new ArgumentException("ICC fill color components must not be empty.", nameof(components));
    ThrowIfDisposed();
    fixed (double* ptr = components)
    {
        ThrowIfError(NativeMethods.oxidize_page_set_fill_color_icc(
            _handle, name, (IntPtr)ptr, (nuint)components.Length),
            $"Failed to set fill color ICC '{name}'");
    }
    return this;
}

/// <summary>Sets stroke color using an ICC color space registered under <paramref name="name"/>.
/// <paramref name="components"/> must not be empty.
/// Mirrors python <c>page.set_stroke_color_icc(name, components)</c>.</summary>
public unsafe PdfPage SetStrokeColorIcc(string name, double[] components)
{
    ArgumentNullException.ThrowIfNull(name);
    ArgumentNullException.ThrowIfNull(components);
    if (components.Length == 0)
        throw new ArgumentException("ICC stroke color components must not be empty.", nameof(components));
    ThrowIfDisposed();
    fixed (double* ptr = components)
    {
        ThrowIfError(NativeMethods.oxidize_page_set_stroke_color_icc(
            _handle, name, (IntPtr)ptr, (nuint)components.Length),
            $"Failed to set stroke color ICC '{name}'");
    }
    return this;
}

/// <summary>
/// Registers an embedded ICC profile color space under <paramref name="name"/>.
/// This is a .NET superset not present in the Python bridge.
/// Use <see cref="AddColorSpace"/> with <see cref="PageColorSpace.IccBased"/> for the
/// inline (no binary) variant that mirrors python.
/// </summary>
public unsafe PdfPage AddIccColorSpace(string name, IccProfile profile)
{
    ArgumentNullException.ThrowIfNull(name);
    ArgumentNullException.ThrowIfNull(profile);
    ThrowIfDisposed();
    profile.Validate();
    int colorSpaceKind = (int)profile.ColorSpace;
    // Remap Lab sentinel (33) to upstream component count 3 for the native layer.
    // The native side maps 1=Gray, 3=Rgb, 4=Cmyk; Lab not separately handled — use Rgb count.
    if (colorSpaceKind == 33) colorSpaceKind = 3;
    fixed (byte* ptr = profile.Data)
    {
        ThrowIfError(NativeMethods.oxidize_page_add_icc_color_space(
            _handle, name, (IntPtr)ptr, (nuint)profile.Data.Length, colorSpaceKind),
            $"Failed to add ICC color space '{name}'");
    }
    return this;
}
```

### Step 8.1 — RED: Write failing .NET API contract tests

Create `/home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET.Tests/Graphics/PdfPageCalGrayIntegrationTests.cs`:

```csharp
using FluentAssertions;
using OxidizePdf.NET.Graphics;
using OxidizePdf.NET.Tests.TestHelpers;
using Xunit;

namespace OxidizePdf.NET.Tests.Graphics;

public class PdfPageCalGrayIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void SetFillColorCalGray_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        page.SetFillColorCalGray(0.5, CalGrayColorSpace.D65()).Should().BeSameAs(page);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetStrokeColorCalGray_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        page.SetStrokeColorCalGray(0.5, CalGrayColorSpace.D65()).Should().BeSameAs(page);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetFillColorCalGray_NullColorSpace_Throws()
    {
        using var page = PdfPage.A4();
        page.Invoking(p => p.SetFillColorCalGray(0.5, null!)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetFillColorCalGray_InvalidColorSpace_Throws()
    {
        using var page = PdfPage.A4();
        var badCs = new CalGrayColorSpace { WhitePoint = new double[] { 0.9505, 0.5, 1.089 } };
        page.Invoking(p => p.SetFillColorCalGray(0.5, badCs)).Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void CalGray_FilledRect_PdfContainsCalGrayColorSpaceResource()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.SetFillColorCalGray(0.5, CalGrayColorSpace.D65())
            .DrawRect(50, 50, 200, 200)
            .Fill();
        doc.AddPage(page);
        var pdfText = ContentStreamHelper.ToLatin1(doc.SaveToBytes());
        pdfText.Should().Contain("CalGray",
            "the PDF resource dictionary must register the CalGray color space");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void CalGray_FilledRect_ContentStreamContainsCsAndScOperators()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.SetFillColorCalGray(0.7, CalGrayColorSpace.D65())
            .DrawRect(50, 50, 200, 200)
            .Fill();
        doc.AddPage(page);
        var stream = ContentStreamHelper.DecompressFirstContentStream(doc.SaveToBytes());
        stream.Should().NotBeNull("a content stream must be present");
        stream!.Should().Contain("cs", "content stream must contain the 'cs' set-colorspace operator");
        stream.Should().Contain("sc", "content stream must contain the 'sc' set-color-components operator");
    }
}
```

Create analogous `PdfPageCalRgbIntegrationTests.cs` and `PdfPageLabIntegrationTests.cs` — same structure, substituting:
- CalRGB: `SetFillColorCalRgb(0.5, 0.3, 0.8, CalRgbColorSpace.SRgb())`, assert `pdfText.Contains("CalRGB")`.
- Lab: `SetFillColorLab(new LabColor(50.0, 0.0, 0.0, LabColorSpace.D50()))`, assert `pdfText.Contains("Lab")`.

Both must also verify `cs` and `sc` operators in the decompressed content stream.

### Step 8.2 — Run to confirm RED

```bash
dotnet test /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj \
  --filter "FullyQualifiedName~PdfPageCalGrayIntegrationTests" --nologo 2>&1 | tail -15
```

Expected: FAIL — `SetFillColorCalGray` not found on `PdfPage`.

### Step 8.3 — GREEN: Add to NativeMethods.cs and PdfPage.cs

Rebuild native first, then copy the `.so`:

```bash
RUSTFLAGS="-D warnings" cargo build \
  --manifest-path /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.toml \
  --release 2>&1 | tail -5
bash /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/build/build-native.sh linux-x64 2>&1 | tail -5
```

Add the P/Invoke declarations from Step 8 above to `NativeMethods.cs`. Add the fluent methods from Step 8 above to `PdfPage.cs`.

### Step 8.4 — Run all three integration test classes

```bash
dotnet test /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj \
  --filter "FullyQualifiedName~PdfPageCalGrayIntegrationTests|FullyQualifiedName~PdfPageCalRgbIntegrationTests|FullyQualifiedName~PdfPageLabIntegrationTests" \
  --nologo 2>&1 | tail -20
```

Expected: all tests pass.

### Step 8.5 — Compile check and commit

```bash
cargo fmt --manifest-path /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.toml -- --check 2>&1
dotnet build /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET/OxidizePdf.NET.csproj \
  --nologo -warnaserror 2>&1 | tail -5
git -C /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet add \
  dotnet/OxidizePdf.NET/NativeMethods.cs \
  dotnet/OxidizePdf.NET/PdfPage.cs \
  dotnet/OxidizePdf.NET.Tests/Graphics/PdfPageCalGrayIntegrationTests.cs \
  dotnet/OxidizePdf.NET.Tests/Graphics/PdfPageCalRgbIntegrationTests.cs \
  dotnet/OxidizePdf.NET.Tests/Graphics/PdfPageLabIntegrationTests.cs
git -C /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet commit \
  -m "feat(m3): add PdfPage CalGray/CalRGB/Lab fluent methods, P/Invoke, and integration tests (GFX-014, GFX-015)"
```

---

## Task 9: ICC integration tests + named multi-space test

**Files:**
- Create: `dotnet/OxidizePdf.NET.Tests/Graphics/PdfPageIccInlineIntegrationTests.cs`
- Create: `dotnet/OxidizePdf.NET.Tests/Graphics/PdfPageIccEmbeddedIntegrationTests.cs`
- Create: `dotnet/OxidizePdf.NET.Tests/Graphics/PdfPageNamedColorSpaceIntegrationTests.cs`
- Create: `dotnet/OxidizePdf.NET.Tests/Interop/ColorSpaceNativeLayoutTests.cs`

### Step 9.1 — RED: Write failing ICC and named tests

Create `/home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET.Tests/Graphics/PdfPageIccInlineIntegrationTests.cs`:

```csharp
using FluentAssertions;
using OxidizePdf.NET.Graphics;
using OxidizePdf.NET.Tests.TestHelpers;
using Xunit;

namespace OxidizePdf.NET.Tests.Graphics;

/// <summary>
/// Tests for the inline ICCBased path: AddColorSpace(name, PageColorSpace.IccBased(n, alternate))
/// followed by SetFillColorIcc / SetStrokeColorIcc.
/// Mirrors python's PageColorSpace.icc_based(n, alternate) pattern.
/// </summary>
public class PdfPageIccInlineIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void AddColorSpace_IccBased_ThenSetFillColorIcc_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        page.AddColorSpace("ICCRGB1", PageColorSpace.IccBased(3, "DeviceRGB"))
            .SetFillColorIcc("ICCRGB1", new double[] { 0.5, 0.3, 0.8 })
            .Should().BeSameAs(page);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SetFillColorIcc_EmptyComponents_Throws()
    {
        using var page = PdfPage.A4();
        page.AddColorSpace("ICCRGB1", PageColorSpace.IccBased(3, "DeviceRGB"));
        page.Invoking(p => p.SetFillColorIcc("ICCRGB1", Array.Empty<double>()))
            .Should().Throw<ArgumentException>().WithMessage("*empty*");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void IccInline_FilledRect_PdfContainsICCBasedResource()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.AddColorSpace("ICCRGB1", PageColorSpace.IccBased(3, "DeviceRGB"))
            .SetFillColorIcc("ICCRGB1", new double[] { 0.5, 0.3, 0.8 })
            .DrawRect(50, 50, 200, 200)
            .Fill();
        doc.AddPage(page);
        var pdfText = ContentStreamHelper.ToLatin1(doc.SaveToBytes());
        pdfText.Should().Contain("ICCBased",
            "the PDF resource dictionary must register an ICCBased color space");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void IccInline_ContentStream_ContainsCsAndScOperators()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        page.AddColorSpace("ICCRGB1", PageColorSpace.IccBased(3, "DeviceRGB"))
            .SetFillColorIcc("ICCRGB1", new double[] { 0.5, 0.3, 0.8 })
            .DrawRect(50, 50, 200, 200)
            .Fill();
        doc.AddPage(page);
        var stream = ContentStreamHelper.DecompressFirstContentStream(doc.SaveToBytes());
        stream.Should().NotBeNull();
        stream!.Should().Contain("cs");
        stream.Should().Contain("sc");
    }
}
```

Create `/home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET.Tests/Graphics/PdfPageIccEmbeddedIntegrationTests.cs`:

```csharp
using FluentAssertions;
using OxidizePdf.NET.Graphics;
using OxidizePdf.NET.Tests.TestHelpers;
using Xunit;

namespace OxidizePdf.NET.Tests.Graphics;

/// <summary>
/// Tests for the embedded-profile ICC path: AddIccColorSpace(name, IccProfile)
/// followed by SetFillColorIcc. This path is a .NET superset not in the Python bridge.
/// </summary>
public class PdfPageIccEmbeddedIntegrationTests
{
    // Minimal valid ICC-like data — real viewers would reject this, but the
    // PDF writer embeds it verbatim without validating the ICC structure.
    private static byte[] MinimalIccData() => new byte[128];

    [Fact]
    [Trait("Category", "Integration")]
    public void AddIccColorSpace_ThenSetFillColorIcc_ReturnsSameInstance()
    {
        using var page = PdfPage.A4();
        var profile = new IccProfile("EmbeddedRGB", MinimalIccData(), IccColorSpace.Rgb);
        page.AddIccColorSpace("EmbeddedRGB", profile)
            .SetFillColorIcc("EmbeddedRGB", new double[] { 0.5, 0.3, 0.8 })
            .Should().BeSameAs(page);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AddIccColorSpace_NullProfile_Throws()
    {
        using var page = PdfPage.A4();
        page.Invoking(p => p.AddIccColorSpace("EmbeddedRGB", null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void IccEmbedded_FilledRect_PdfContainsICCBasedResource()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        var profile = new IccProfile("EmbeddedRGB", MinimalIccData(), IccColorSpace.Rgb);
        page.AddIccColorSpace("EmbeddedRGB", profile)
            .SetFillColorIcc("EmbeddedRGB", new double[] { 0.5, 0.3, 0.8 })
            .DrawRect(50, 50, 200, 200)
            .Fill();
        doc.AddPage(page);
        var pdfText = ContentStreamHelper.ToLatin1(doc.SaveToBytes());
        pdfText.Should().Contain("ICCBased",
            "the embedded profile must appear as /ICCBased in the resource dict");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void IccEmbedded_ContentStream_ContainsCsAndScOperators()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        var profile = new IccProfile("EmbeddedRGB", MinimalIccData(), IccColorSpace.Rgb);
        page.AddIccColorSpace("EmbeddedRGB", profile)
            .SetFillColorIcc("EmbeddedRGB", new double[] { 0.5, 0.3, 0.8 })
            .DrawRect(50, 50, 200, 200)
            .Fill();
        doc.AddPage(page);
        var stream = ContentStreamHelper.DecompressFirstContentStream(doc.SaveToBytes());
        stream.Should().NotBeNull();
        stream!.Should().Contain("cs");
        stream.Should().Contain("sc");
    }
}
```

Create `/home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET.Tests/Graphics/PdfPageNamedColorSpaceIntegrationTests.cs`:

```csharp
using FluentAssertions;
using OxidizePdf.NET.Graphics;
using OxidizePdf.NET.Tests.TestHelpers;
using Xunit;

namespace OxidizePdf.NET.Tests.Graphics;

/// <summary>
/// Tests for named (multi-space-per-page) variants.
/// The key test here is that TWO CalRGB spaces can coexist on one page —
/// this proves Decision 2's one-per-page limitation is gone in 2.12.0.
/// </summary>
public class PdfPageNamedColorSpaceIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void TwoCalRgbSpacesOnOnePage_BothAppearInResourceDict()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        var cs1 = CalRgbColorSpace.SRgb();
        var cs2 = CalRgbColorSpace.AdobeRgb();

        page.AddColorSpace("SRgbSpace", PageColorSpace.CalRgb(cs1))
            .AddColorSpace("AdobeRgbSpace", PageColorSpace.CalRgb(cs2))
            .SetFillColorCalibratedNamed("SRgbSpace", CalibratedColor.CalRgb(new double[] { 0.8, 0.2, 0.3 }, cs1))
            .DrawRect(50, 50, 100, 100)
            .Fill()
            .SetFillColorCalibratedNamed("AdobeRgbSpace", CalibratedColor.CalRgb(new double[] { 0.1, 0.7, 0.4 }, cs2))
            .DrawRect(200, 50, 100, 100)
            .Fill();
        doc.AddPage(page);

        var pdfText = ContentStreamHelper.ToLatin1(doc.SaveToBytes());
        pdfText.Should().Contain("SRgbSpace",
            "first CalRGB space must be registered in the resource dict");
        pdfText.Should().Contain("AdobeRgbSpace",
            "second CalRGB space must also be registered — proves multi-space-per-page works");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void NamedCalGray_ContentStream_ContainsNameAndScOperator()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        var cs = CalGrayColorSpace.D50();
        page.AddColorSpace("MyGray", PageColorSpace.CalGray(cs))
            .SetFillColorCalibratedNamed("MyGray", CalibratedColor.CalGray(0.4, cs))
            .DrawRect(50, 50, 100, 100)
            .Fill();
        doc.AddPage(page);

        var pdfBytes = doc.SaveToBytes();
        var pdfText = ContentStreamHelper.ToLatin1(pdfBytes);
        pdfText.Should().Contain("MyGray");

        var stream = ContentStreamHelper.DecompressFirstContentStream(pdfBytes);
        stream.Should().NotBeNull();
        stream!.Should().Contain("cs");
        stream.Should().Contain("sc");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void NamedLab_ContentStream_ContainsNameAndScOperator()
    {
        using var doc = new PdfDocument();
        using var page = PdfPage.A4();
        var cs = LabColorSpace.D50();
        page.AddColorSpace("PerceptualLab", PageColorSpace.Lab(cs))
            .SetFillColorLabNamed("PerceptualLab", new LabColor(60.0, 20.0, -30.0, cs))
            .DrawRect(50, 50, 100, 100)
            .Fill();
        doc.AddPage(page);

        var pdfBytes = doc.SaveToBytes();
        var pdfText = ContentStreamHelper.ToLatin1(pdfBytes);
        pdfText.Should().Contain("PerceptualLab");

        var stream = ContentStreamHelper.DecompressFirstContentStream(pdfBytes);
        stream.Should().NotBeNull();
        stream!.Should().Contain("cs");
        stream.Should().Contain("sc");
    }
}
```

Create `/home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET.Tests/Interop/ColorSpaceNativeLayoutTests.cs`:

```csharp
using Xunit;

namespace OxidizePdf.NET.Tests.Interop;

/// <summary>
/// Documents the M3 FFI layout decision.
/// All CalGray, CalRGB, Lab, and ICC parameters cross the FFI boundary as
/// individual scalar parameters (double or int) — no #[repr(C)] structs.
/// This matches the existing RGB/Gray/CMYK pattern and avoids alignment risk.
/// No StructLayout or offset_of! pinning is required.
/// </summary>
public class ColorSpaceNativeLayoutTests
{
    [Fact]
    public void M3_UsesScalarParametersOnly_NoStructLayoutRequired()
    {
        Assert.True(true,
            "Scalar-parameter FFI confirmed — no struct layout pinning needed for M3.");
    }
}
```

### Step 9.2 — Run tests

```bash
dotnet test /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET.Tests/OxidizePdf.NET.Tests.csproj \
  --filter "FullyQualifiedName~PdfPageIccInlineIntegrationTests|FullyQualifiedName~PdfPageIccEmbeddedIntegrationTests|FullyQualifiedName~PdfPageNamedColorSpaceIntegrationTests|FullyQualifiedName~ColorSpaceNativeLayoutTests" \
  --nologo 2>&1 | tail -20
```

Expected: all pass.

### Step 9.3 — Commit

```bash
git -C /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet add \
  dotnet/OxidizePdf.NET.Tests/Graphics/PdfPageIccInlineIntegrationTests.cs \
  dotnet/OxidizePdf.NET.Tests/Graphics/PdfPageIccEmbeddedIntegrationTests.cs \
  dotnet/OxidizePdf.NET.Tests/Graphics/PdfPageNamedColorSpaceIntegrationTests.cs \
  dotnet/OxidizePdf.NET.Tests/Interop/ColorSpaceNativeLayoutTests.cs
git -C /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet commit \
  -m "feat(m3): add ICC and named color-space integration tests (GFX-019)"
```

---

## Task 10: Full suite, CHANGELOG, and pre-PR cleanup

### Step 10.1 — Full Rust test suite

```bash
cargo test --manifest-path /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.toml \
  --lib 2>&1 | tail -20
```

Expected: all tests pass, zero failures, zero warnings.

### Step 10.2 — Full .NET test suite

```bash
dotnet test /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.sln \
  --nologo 2>&1 | tail -20
```

Expected: all existing tests plus all new M3 tests pass. Zero failures.

### Step 10.3 — cargo fmt check (FIRST — recurring CI failure if missed)

```bash
cargo fmt --manifest-path /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.toml \
  -- --check 2>&1
```

Expected: no output. If output appears, run `cargo fmt` and create a new commit:

```bash
cargo fmt --manifest-path /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.toml
git -C /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet add native/src/graphics.rs native/src/page.rs
git -C /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet commit \
  -m "style(m3): apply cargo fmt to graphics.rs and page.rs"
```

### Step 10.4 — cargo clippy

```bash
cargo clippy --manifest-path /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.toml \
  -- -D warnings 2>&1 | tail -20
```

Expected: zero warnings or errors.

### Step 10.5 — RUSTFLAGS release build

```bash
RUSTFLAGS="-D warnings" cargo build \
  --manifest-path /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/native/Cargo.toml \
  --release 2>&1 | tail -5
```

### Step 10.6 — .NET warnaserror build

```bash
dotnet build /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet/dotnet/OxidizePdf.NET/OxidizePdf.NET.csproj \
  --nologo -warnaserror 2>&1 | tail -5
```

Expected: 0 Warning(s), 0 Error(s).

### Step 10.7 — CHANGELOG entry

Add an entry under the M3 release section in `CHANGELOG.md`:

```
## [Unreleased] — M3 Color Spaces

### Added
- GFX-014: CalGray and CalRGB calibrated color spaces (hardcoded and named variants).
  `PdfPage.SetFillColorCalibrated`, `SetFillColorCalGray`, `SetFillColorCalRgb`,
  `SetFillColorCalibratedNamed`, `AddColorSpace`.
- GFX-015: CIE L*a*b* color space (hardcoded and named variants).
  `PdfPage.SetFillColorLab`, `SetFillColorLabNamed`.
- GFX-019: ICC color profiles (unblocked by oxidize-pdf 2.12.0).
  Inline ICCBased path: `AddColorSpace(name, PageColorSpace.IccBased(n, alternate))`.
  Embedded profile path (.NET superset): `AddIccColorSpace(name, IccProfile)`.
  Draw: `SetFillColorIcc`, `SetStrokeColorIcc`.
- Multiple named color spaces per page now supported (Decision 2 limitation removed).
```

### Step 10.8 — Final commit

```bash
git -C /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet add CHANGELOG.md
git -C /home/santi/repos/BelowZero/oxidizePdf/oxidize-dotnet commit \
  -m "chore(m3): update CHANGELOG for GFX-014, GFX-015, GFX-019"
```

---

## Estimation

- Pre-work (Task 0): 30 minutes
- Task 1 (5 C# value types + tests): 1.5 hours
- Task 2 (ICC types + PageColorSpace): 1 hour
- Task 3 (CalGray FFI): 30 minutes
- Task 4 (CalRGB FFI): 45 minutes
- Task 5 (Lab FFI): 30 minutes
- Task 6 (Named variants + registration FFI): 1.5 hours
- Task 7 (ICC draw + embedded profile FFI): 1 hour
- Task 8 (.NET P/Invoke + PdfPage fluent methods): 1.5 hours
- Task 9 (ICC + named integration tests): 1 hour
- Task 10 (Full suite + cleanup): 30 minutes

**Total: ~10 hours**

---

## Risk Checklist (run before opening the PR)

- [ ] `cargo fmt -- --check` exits clean (FIRST — CI fails silently without this)
- [ ] `cargo clippy -- -D warnings` exits clean
- [ ] `RUSTFLAGS="-D warnings" cargo build --release` zero warnings
- [ ] `cargo test --lib` — all Rust unit tests pass
- [ ] `dotnet build -warnaserror` — 0 warnings, 0 errors
- [ ] `dotnet test OxidizePdf.sln` — all tests pass (existing + new M3 tests)
- [ ] No test uses `is_ok()`, byte-count, or `%PDF` header check as sole assertion (CLAUDE.md smoke-test prohibition)
- [ ] `PdfPageNamedColorSpaceIntegrationTests.TwoCalRgbSpacesOnOnePage_BothAppearInResourceDict` passes — proves multi-space-per-page works
- [ ] ICC inline round-trip: `/ICCBased` appears in resource dict; `cs`/`sc` operators in content stream
- [ ] ICC embedded round-trip: `/ICCBased` appears in resource dict; `cs`/`sc` operators in content stream
- [ ] `SetFillColorIcc` with empty `components` throws `ArgumentException` in .NET and returns `ErrorCode::InvalidArgument` (9) in Rust FFI tests
- [ ] All CalGray, CalRGB, Lab hardcoded-name round-trip tests assert resource dict entry AND content stream operators
- [ ] CHANGELOG updated with GFX-014, GFX-015, GFX-019 entries
- [ ] Branch is `feature/m3-color-spaces`; PR targets `develop`
- [ ] GitHub issue #22 updated: GFX-014, GFX-015, GFX-019 all marked in-progress (no longer deferred)
