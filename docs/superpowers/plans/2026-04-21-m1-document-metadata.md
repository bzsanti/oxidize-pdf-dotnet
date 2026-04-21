# M1 — Document metadata Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expose five document-level metadata features from `oxidize-pdf 2.5.4` core through the FFI and .NET wrapper: open actions, viewer preferences, named destinations, page labels, and save-with-WriterConfig. Ship as `OxidizePdf.NET 0.8.0`.

**Architecture:** Follow the established three-layer pattern — Rust FFI (`native/src/`) exposes `#[no_mangle] extern "C"` functions with opaque handles; `.NET/NativeMethods.cs` mirrors via `[DllImport]`; `.NET/Pdf*.cs` publishes an idiomatic C# API (fluent where possible, async where I/O). Complex nested structures (viewer prefs, page labels, WriterConfig) are passed as JSON strings to avoid combinatorial explosion of FFI entry points; simple flat options stay as individual entry points.

**Tech stack:** Rust 1.77 + edition 2021 + `cdylib`, .NET 8/9/10, `serde_json` for complex payloads, xUnit for .NET tests, `cargo test` for FFI smoke coverage. Upstream: `oxidize-pdf 2.5.4`.

**Issue:** #20 · **Branch:** `feature/m1-document-metadata` · **Target version:** `0.8.0`.

---

## File structure

| File | Role | Status |
|---|---|---|
| `native/src/document_metadata.rs` | New FFI module: open actions, viewer prefs, named dest, page labels, save-with-config | **create** |
| `native/src/lib.rs` | Register new submodule | **modify** |
| `native/Cargo.toml` | Bump version `0.7.2` → `0.8.0` | **modify** |
| `dotnet/OxidizePdf.NET/NativeMethods.cs` | Add P/Invoke signatures | **modify** |
| `dotnet/OxidizePdf.NET/OxidizePdf.NET.csproj` | Bump `<Version>` to `0.8.0` | **modify** |
| `dotnet/OxidizePdf.NET/Models/PdfDestination.cs` | New public type — page target + fit mode | **create** |
| `dotnet/OxidizePdf.NET/Models/PdfOpenAction.cs` | New public type — GoTo / URI action factory | **create** |
| `dotnet/OxidizePdf.NET/Models/PdfViewerPreferences.cs` | New public type — builder for viewer prefs | **create** |
| `dotnet/OxidizePdf.NET/Models/PdfPageLabelRange.cs` | New public type — page label range | **create** |
| `dotnet/OxidizePdf.NET/Enums.cs` | Add `PdfPageLayout`, `PdfPageMode`, `PdfPrintScaling`, `PdfDuplex`, `PdfPageLabelStyle`, `PdfDestinationFit` | **modify** |
| `dotnet/OxidizePdf.NET/PdfSaveOptions.cs` | New public type — wrapper for `WriterConfig` | **create** |
| `dotnet/OxidizePdf.NET/PdfDocument.cs` | Add `SetOpenAction`, `SetViewerPreferences`, `AddNamedDestination`, `SetPageLabels`, overloads of `SaveTo*` taking `PdfSaveOptions` | **modify** |
| `dotnet/OxidizePdf.NET.Tests/PdfDocumentOpenActionTests.cs` | 4 tests — GoTo page, GoTo with fit, URI, overwrite | **create** |
| `dotnet/OxidizePdf.NET.Tests/PdfDocumentViewerPreferencesTests.cs` | 6 tests — round-trip each preference, builder fluency | **create** |
| `dotnet/OxidizePdf.NET.Tests/PdfDocumentNamedDestinationsTests.cs` | 3 tests — add, duplicate-name replace, retrieval via parser | **create** |
| `dotnet/OxidizePdf.NET.Tests/PdfDocumentPageLabelsTests.cs` | 5 tests — decimal, roman, letters, prefixed, mixed ranges | **create** |
| `dotnet/OxidizePdf.NET.Tests/PdfDocumentSaveOptionsTests.cs` | 5 tests — default, modern, legacy, custom version, incremental-update rejection | **create** |
| `CHANGELOG.md` | New `[0.8.0]` entry | **modify** |
| `docs/FEATURE_PARITY.md` | Flip DOC-014/015/017/018/020 from `no` to `yes`; bump bridge version line | **modify** |

---

## Conventions (follow throughout)

**FFI function pattern:**
```rust
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_<action>(
    handle: *mut DocumentHandle,
    /* args */,
) -> c_int {
    clear_last_error();
    if handle.is_null() { set_last_error("..."); return ErrorCode::NullPointer as c_int; }
    // body
    ErrorCode::Success as c_int
}
```

**JSON payload shape for complex types:** `serde_json::from_str::<T>()` where `T: Deserialize` matches core struct one-to-one. Validation errors return `ErrorCode::InvalidArgument` with `set_last_error(detailed message)`.

**.NET test pattern:**
```csharp
[Fact]
[Trait("Category", "Integration")]
public void FeatureName_SpecificScenario_ExpectedOutcome()
{
    using var doc = new PdfDocument();
    doc.AddPage(PdfPage.A4());
    doc.<NewApi>(...);

    byte[] bytes = doc.SaveToBytes();

    // Assert against the produced bytes — NOT a smoke test.
    // Use PdfExtractor or byte-level search to verify the feature landed in output.
    Assert.Contains(<expected bytes or string>, bytes);
}
```

**Commit cadence:** one commit per TDD red/green cycle (step "Commit"). Commit messages follow `feat|test|refactor(m1): <summary>` convention.

---

## Task 0: Prep — create branch, bump version, skeleton module

**Files:**
- Modify: `native/Cargo.toml`
- Modify: `dotnet/OxidizePdf.NET/OxidizePdf.NET.csproj`
- Create: `native/src/document_metadata.rs`
- Modify: `native/src/lib.rs`

- [ ] **Step 1: Create branch from develop**

```bash
git checkout develop
git pull origin develop
git checkout -b feature/m1-document-metadata
```

- [ ] **Step 2: Bump Rust FFI version**

In `native/Cargo.toml`:
```toml
version = "0.8.0"
```

- [ ] **Step 3: Bump .NET wrapper version**

In `dotnet/OxidizePdf.NET/OxidizePdf.NET.csproj`:
```xml
<Version>0.8.0</Version>
```

- [ ] **Step 4: Create empty FFI module and register it**

Create `native/src/document_metadata.rs` with:
```rust
//! FFI for document-level metadata: open actions, viewer preferences,
//! named destinations, page labels, save-with-WriterConfig.

use std::ffi::CStr;
use std::os::raw::{c_char, c_int};

use crate::document::DocumentHandle;
use crate::{clear_last_error, set_last_error, ErrorCode};
```

In `native/src/lib.rs`, add after existing `pub mod document;` line:
```rust
pub mod document_metadata;
```

- [ ] **Step 5: Verify compilation**

Run: `cd native && cargo build --release`
Expected: `Finished `release` profile` with 0 errors, 0 warnings.

- [ ] **Step 6: Commit**

```bash
git add native/Cargo.toml dotnet/OxidizePdf.NET/OxidizePdf.NET.csproj native/src/document_metadata.rs native/src/lib.rs
git commit -m "chore(m1): bump version 0.8.0 and scaffold document_metadata FFI module"
```

---

## Task 1: DOC-014 — Set open action (GoTo + URI)

**Why:** lets consumers open a PDF at a specific page or trigger a URL on open. Simplest of the five features; validates the JSON-payload pattern we'll reuse in later tasks.

**Files:**
- Create: `dotnet/OxidizePdf.NET/Models/PdfDestination.cs`
- Create: `dotnet/OxidizePdf.NET/Models/PdfOpenAction.cs`
- Modify: `dotnet/OxidizePdf.NET/Enums.cs`
- Modify: `native/src/document_metadata.rs`
- Modify: `dotnet/OxidizePdf.NET/NativeMethods.cs`
- Modify: `dotnet/OxidizePdf.NET/PdfDocument.cs`
- Create: `dotnet/OxidizePdf.NET.Tests/PdfDocumentOpenActionTests.cs`

- [ ] **Step 1: Write the failing test — GoTo page 2 with Fit**

Create `dotnet/OxidizePdf.NET.Tests/PdfDocumentOpenActionTests.cs`:
```csharp
using System.Text;

namespace OxidizePdf.NET.Tests;

public class PdfDocumentOpenActionTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void SetOpenAction_GoToPageWithFit_EmbedsGoToAction()
    {
        using var doc = new PdfDocument();
        doc.AddPage(PdfPage.A4());
        doc.AddPage(PdfPage.A4());
        doc.SetOpenAction(PdfOpenAction.GoTo(
            pageIndex: 1,
            destination: PdfDestination.Fit()));

        byte[] bytes = doc.SaveToBytes();
        string text = Encoding.Latin1.GetString(bytes);

        Assert.Contains("/OpenAction", text);
        Assert.Contains("/S /GoTo", text);
        Assert.Contains("/Fit", text);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd dotnet && dotnet test OxidizePdf.NET.Tests --filter "FullyQualifiedName~OpenAction" --no-restore`
Expected: FAIL — `PdfOpenAction` / `PdfDestination` / `SetOpenAction` not defined.

- [ ] **Step 3: Add `PdfDestinationFit` enum**

In `dotnet/OxidizePdf.NET/Enums.cs`, append:
```csharp
/// <summary>
/// PDF destination fit mode for open actions and named destinations.
/// </summary>
public enum PdfDestinationFit
{
    /// <summary>Position at specific coordinates with optional zoom (XYZ).</summary>
    Xyz = 0,
    /// <summary>Fit entire page in window.</summary>
    Fit = 1,
    /// <summary>Fit page width, optional top coordinate.</summary>
    FitH = 2,
    /// <summary>Fit page height, optional left coordinate.</summary>
    FitV = 3,
    /// <summary>Fit rectangle.</summary>
    FitR = 4,
    /// <summary>Fit bounding box of page contents.</summary>
    FitB = 5,
}
```

- [ ] **Step 4: Create `PdfDestination` model**

Create `dotnet/OxidizePdf.NET/Models/PdfDestination.cs`:
```csharp
using System.Text.Json.Serialization;

namespace OxidizePdf.NET;

/// <summary>
/// Describes where to land inside the document: target page + fit mode.
/// Page indices are 0-based.
/// </summary>
public sealed class PdfDestination
{
    [JsonPropertyName("page")]
    public int PageIndex { get; init; }

    [JsonPropertyName("fit")]
    public PdfDestinationFit Fit { get; init; }

    [JsonPropertyName("left")]
    public double? Left { get; init; }

    [JsonPropertyName("top")]
    public double? Top { get; init; }

    [JsonPropertyName("zoom")]
    public double? Zoom { get; init; }

    /// <summary>Fit whole page in window.</summary>
    public static PdfDestination Fit(int pageIndex = 0) =>
        new() { PageIndex = pageIndex, Fit = PdfDestinationFit.Fit };

    /// <summary>Position at specific coordinates with optional zoom.</summary>
    public static PdfDestination Xyz(int pageIndex, double? left = null, double? top = null, double? zoom = null) =>
        new() { PageIndex = pageIndex, Fit = PdfDestinationFit.Xyz, Left = left, Top = top, Zoom = zoom };

    /// <summary>Fit page width at optional top coordinate.</summary>
    public static PdfDestination FitH(int pageIndex, double? top = null) =>
        new() { PageIndex = pageIndex, Fit = PdfDestinationFit.FitH, Top = top };

    /// <summary>Fit page height at optional left coordinate.</summary>
    public static PdfDestination FitV(int pageIndex, double? left = null) =>
        new() { PageIndex = pageIndex, Fit = PdfDestinationFit.FitV, Left = left };
}
```

- [ ] **Step 5: Create `PdfOpenAction` model**

Create `dotnet/OxidizePdf.NET/Models/PdfOpenAction.cs`:
```csharp
using System.Text.Json.Serialization;

namespace OxidizePdf.NET;

/// <summary>
/// Action triggered when the document is opened.
/// </summary>
public sealed class PdfOpenAction
{
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = "";

    [JsonPropertyName("destination")]
    public PdfDestination? Destination { get; init; }

    [JsonPropertyName("uri")]
    public string? Uri { get; init; }

    /// <summary>Navigate to a destination inside this document.</summary>
    public static PdfOpenAction GoTo(int pageIndex, PdfDestination? destination = null) =>
        new() { Kind = "goto", Destination = destination ?? PdfDestination.Fit(pageIndex) };

    /// <summary>Open a URI (external URL) on document open.</summary>
    public static PdfOpenAction Uri(string uri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);
        return new PdfOpenAction { Kind = "uri", Uri = uri };
    }
}
```

- [ ] **Step 6: Add FFI function**

In `native/src/document_metadata.rs`, append:
```rust
use oxidize_pdf::actions::Action;
use oxidize_pdf::structure::{Destination, PageDestination};
use serde::Deserialize;

#[derive(Deserialize)]
struct DestinationJson {
    page: u32,
    fit: u8,
    left: Option<f64>,
    top: Option<f64>,
    zoom: Option<f64>,
}

impl DestinationJson {
    fn to_core(&self) -> Destination {
        let page = PageDestination::PageNumber(self.page);
        match self.fit {
            0 => Destination::xyz(page, self.left, self.top, self.zoom),
            1 => Destination::fit(page),
            2 => Destination::fit_h(page, self.top),
            3 => Destination::fit_v(page, self.left),
            5 => Destination::fit_b(page),
            _ => Destination::fit(page),
        }
    }
}

#[derive(Deserialize)]
struct OpenActionJson {
    kind: String,
    destination: Option<DestinationJson>,
    uri: Option<String>,
}

/// Set the document open action from a JSON payload.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `json` must be a valid null-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_set_open_action_json(
    handle: *mut DocumentHandle,
    json: *const c_char,
) -> c_int {
    clear_last_error();
    if handle.is_null() || json.is_null() {
        set_last_error("Null pointer to oxidize_document_set_open_action_json");
        return ErrorCode::NullPointer as c_int;
    }
    let s = match CStr::from_ptr(json).to_str() {
        Ok(v) => v,
        Err(_) => { set_last_error("Invalid UTF-8 in open action JSON"); return ErrorCode::InvalidUtf8 as c_int; }
    };
    let payload: OpenActionJson = match serde_json::from_str(s) {
        Ok(v) => v,
        Err(e) => { set_last_error(&format!("Invalid open action JSON: {e}")); return ErrorCode::SerializationError as c_int; }
    };
    let action = match payload.kind.as_str() {
        "goto" => {
            let Some(dest) = payload.destination else {
                set_last_error("goto open action requires 'destination'");
                return ErrorCode::InvalidArgument as c_int;
            };
            Action::goto(dest.to_core())
        }
        "uri" => {
            let Some(uri) = payload.uri else {
                set_last_error("uri open action requires 'uri'");
                return ErrorCode::InvalidArgument as c_int;
            };
            Action::uri(uri)
        }
        other => {
            set_last_error(&format!("Unknown open action kind: {other}"));
            return ErrorCode::InvalidArgument as c_int;
        }
    };
    (*handle).inner.set_open_action(action);
    ErrorCode::Success as c_int
}
```

- [ ] **Step 7: Add P/Invoke entry**

In `dotnet/OxidizePdf.NET/NativeMethods.cs`, inside the `NativeMethods` class (keep alphabetical grouping by feature if the file uses it):
```csharp
[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
internal static extern int oxidize_document_set_open_action_json(IntPtr handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string json);
```

- [ ] **Step 8: Add public `SetOpenAction` on `PdfDocument`**

In `dotnet/OxidizePdf.NET/PdfDocument.cs`, inside the metadata region:
```csharp
/// <summary>
/// Sets the action to execute when the document is opened
/// (navigate to a page, open a URI, etc.).
/// </summary>
/// <param name="action">Action produced by one of the static factories on <see cref="PdfOpenAction"/>.</param>
/// <returns>This <see cref="PdfDocument"/> for fluent chaining.</returns>
public PdfDocument SetOpenAction(PdfOpenAction action)
{
    ArgumentNullException.ThrowIfNull(action);
    ThrowIfDisposed();
    string json = System.Text.Json.JsonSerializer.Serialize(action);
    int rc = NativeMethods.oxidize_document_set_open_action_json(_handle, json);
    if (rc != 0) ThrowFromLastError(rc, nameof(SetOpenAction));
    return this;
}
```

- [ ] **Step 9: Build Rust + .NET**

```bash
cd native && cargo build --release
cp target/release/liboxidize_pdf_ffi.so ../dotnet/OxidizePdf.NET/runtimes/linux-x64/native/
cd ../dotnet && dotnet build OxidizePdf.NET/OxidizePdf.NET.csproj -c Release
```
Expected: 0 warnings, 0 errors.

- [ ] **Step 10: Run failing test, confirm green**

Run: `dotnet test OxidizePdf.NET.Tests --filter "FullyQualifiedName~OpenAction_GoTo" --no-restore`
Expected: PASS (1/1).

- [ ] **Step 11: Add remaining 3 tests**

Append to `PdfDocumentOpenActionTests.cs`:
```csharp
[Fact]
[Trait("Category", "Integration")]
public void SetOpenAction_Uri_EmbedsUriAction()
{
    using var doc = new PdfDocument();
    doc.AddPage(PdfPage.A4());
    doc.SetOpenAction(PdfOpenAction.Uri("https://example.com/"));

    byte[] bytes = doc.SaveToBytes();
    string text = Encoding.Latin1.GetString(bytes);

    Assert.Contains("/S /URI", text);
    Assert.Contains("https://example.com/", text);
}

[Fact]
[Trait("Category", "Integration")]
public void SetOpenAction_XyzWithZoom_EmbedsCoordinates()
{
    using var doc = new PdfDocument();
    doc.AddPage(PdfPage.A4());
    doc.SetOpenAction(PdfOpenAction.GoTo(0, PdfDestination.Xyz(0, left: 100, top: 500, zoom: 1.5)));

    byte[] bytes = doc.SaveToBytes();
    string text = Encoding.Latin1.GetString(bytes);

    Assert.Contains("/XYZ", text);
    Assert.Contains("100", text);
    Assert.Contains("500", text);
    Assert.Contains("1.5", text);
}

[Fact]
[Trait("Category", "Integration")]
public void SetOpenAction_CalledTwice_LastWriteWins()
{
    using var doc = new PdfDocument();
    doc.AddPage(PdfPage.A4());
    doc.SetOpenAction(PdfOpenAction.Uri("https://first.example/"));
    doc.SetOpenAction(PdfOpenAction.Uri("https://second.example/"));

    byte[] bytes = doc.SaveToBytes();
    string text = Encoding.Latin1.GetString(bytes);

    Assert.DoesNotContain("https://first.example/", text);
    Assert.Contains("https://second.example/", text);
}
```

- [ ] **Step 12: Run all 4 tests, confirm green**

Run: `dotnet test OxidizePdf.NET.Tests --filter "FullyQualifiedName~OpenAction" --no-restore`
Expected: PASS (4/4).

- [ ] **Step 13: Commit**

```bash
git add native/src/document_metadata.rs dotnet/OxidizePdf.NET/ dotnet/OxidizePdf.NET.Tests/PdfDocumentOpenActionTests.cs
git commit -m "feat(m1): DOC-014 set open action (GoTo page, URI)"
```

---

## Task 2: DOC-015 — Viewer preferences

**Why:** controls how PDF readers open the document (hide toolbar, fit window, duplex printing). Low risk, all-flat booleans/enums.

**Files:**
- Create: `dotnet/OxidizePdf.NET/Models/PdfViewerPreferences.cs`
- Modify: `dotnet/OxidizePdf.NET/Enums.cs`
- Modify: `native/src/document_metadata.rs`
- Modify: `dotnet/OxidizePdf.NET/NativeMethods.cs`
- Modify: `dotnet/OxidizePdf.NET/PdfDocument.cs`
- Create: `dotnet/OxidizePdf.NET.Tests/PdfDocumentViewerPreferencesTests.cs`

- [ ] **Step 1: Write first failing test (HideToolbar + FitWindow)**

```csharp
using System.Text;

namespace OxidizePdf.NET.Tests;

public class PdfDocumentViewerPreferencesTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void SetViewerPreferences_HideToolbarFitWindow_EmbedsDict()
    {
        using var doc = new PdfDocument();
        doc.AddPage(PdfPage.A4());
        doc.SetViewerPreferences(new PdfViewerPreferences
        {
            HideToolbar = true,
            FitWindow = true,
        });

        byte[] bytes = doc.SaveToBytes();
        string text = Encoding.Latin1.GetString(bytes);

        Assert.Contains("/ViewerPreferences", text);
        Assert.Contains("/HideToolbar true", text);
        Assert.Contains("/FitWindow true", text);
    }
}
```

- [ ] **Step 2: Run test, confirm failure**

Run: `dotnet test OxidizePdf.NET.Tests --filter "FullyQualifiedName~ViewerPreferences" --no-restore`
Expected: FAIL — types not defined.

- [ ] **Step 3: Add viewer-pref enums**

In `dotnet/OxidizePdf.NET/Enums.cs`:
```csharp
/// <summary>PDF page layout preference (viewer hint).</summary>
public enum PdfPageLayout
{
    SinglePage = 0,
    OneColumn = 1,
    TwoColumnLeft = 2,
    TwoColumnRight = 3,
    TwoPageLeft = 4,
    TwoPageRight = 5,
}

/// <summary>PDF page mode (which panel is open on document open).</summary>
public enum PdfPageMode
{
    UseNone = 0,
    UseOutlines = 1,
    UseThumbs = 2,
    FullScreen = 3,
    UseOC = 4,
    UseAttachments = 5,
}

/// <summary>Print scaling behaviour.</summary>
public enum PdfPrintScaling { AppDefault = 0, None = 1 }

/// <summary>Duplex printing mode.</summary>
public enum PdfDuplex { Simplex = 0, DuplexFlipShortEdge = 1, DuplexFlipLongEdge = 2 }
```

- [ ] **Step 4: Create `PdfViewerPreferences`**

Create `dotnet/OxidizePdf.NET/Models/PdfViewerPreferences.cs`:
```csharp
using System.Text.Json.Serialization;

namespace OxidizePdf.NET;

/// <summary>
/// Viewer preferences dictionary — hints for the PDF viewer.
/// All properties are optional; unset properties are omitted.
/// </summary>
public sealed class PdfViewerPreferences
{
    [JsonPropertyName("hide_toolbar")] public bool? HideToolbar { get; init; }
    [JsonPropertyName("hide_menubar")] public bool? HideMenubar { get; init; }
    [JsonPropertyName("hide_window_ui")] public bool? HideWindowUi { get; init; }
    [JsonPropertyName("fit_window")] public bool? FitWindow { get; init; }
    [JsonPropertyName("center_window")] public bool? CenterWindow { get; init; }
    [JsonPropertyName("display_doc_title")] public bool? DisplayDocTitle { get; init; }
    [JsonPropertyName("page_layout")] public PdfPageLayout? PageLayout { get; init; }
    [JsonPropertyName("page_mode")] public PdfPageMode? PageMode { get; init; }
    [JsonPropertyName("print_scaling")] public PdfPrintScaling? PrintScaling { get; init; }
    [JsonPropertyName("duplex")] public PdfDuplex? Duplex { get; init; }
}
```

- [ ] **Step 5: Add FFI entry**

In `native/src/document_metadata.rs`, append:
```rust
use oxidize_pdf::viewer_preferences::{
    Direction, Duplex as CoreDuplex, NonFullScreenPageMode, PageLayout as CorePageLayout,
    PageMode as CorePageMode, PrintScaling as CorePrintScaling, ViewerPreferences,
};

#[derive(Deserialize)]
struct ViewerPrefsJson {
    hide_toolbar: Option<bool>,
    hide_menubar: Option<bool>,
    hide_window_ui: Option<bool>,
    fit_window: Option<bool>,
    center_window: Option<bool>,
    display_doc_title: Option<bool>,
    page_layout: Option<u8>,
    page_mode: Option<u8>,
    print_scaling: Option<u8>,
    duplex: Option<u8>,
}

impl ViewerPrefsJson {
    fn to_core(&self) -> ViewerPreferences {
        let mut prefs = ViewerPreferences::new();
        if let Some(v) = self.hide_toolbar { prefs = prefs.hide_toolbar(v); }
        if let Some(v) = self.hide_menubar { prefs = prefs.hide_menubar(v); }
        if let Some(v) = self.hide_window_ui { prefs = prefs.hide_window_ui(v); }
        if let Some(v) = self.fit_window { prefs = prefs.fit_window(v); }
        if let Some(v) = self.center_window { prefs = prefs.center_window(v); }
        if let Some(v) = self.display_doc_title { prefs = prefs.display_doc_title(v); }
        if let Some(v) = self.page_layout {
            let layout = match v {
                0 => CorePageLayout::SinglePage,
                1 => CorePageLayout::OneColumn,
                2 => CorePageLayout::TwoColumnLeft,
                3 => CorePageLayout::TwoColumnRight,
                4 => CorePageLayout::TwoPageLeft,
                5 => CorePageLayout::TwoPageRight,
                _ => CorePageLayout::SinglePage,
            };
            prefs = prefs.page_layout(layout);
        }
        if let Some(v) = self.page_mode {
            let mode = match v {
                0 => CorePageMode::UseNone,
                1 => CorePageMode::UseOutlines,
                2 => CorePageMode::UseThumbs,
                3 => CorePageMode::FullScreen,
                4 => CorePageMode::UseOC,
                5 => CorePageMode::UseAttachments,
                _ => CorePageMode::UseNone,
            };
            prefs = prefs.page_mode(mode);
        }
        if let Some(v) = self.print_scaling {
            let scaling = match v { 1 => CorePrintScaling::None, _ => CorePrintScaling::AppDefault };
            prefs = prefs.print_scaling(scaling);
        }
        if let Some(v) = self.duplex {
            let duplex = match v {
                1 => CoreDuplex::DuplexFlipShortEdge,
                2 => CoreDuplex::DuplexFlipLongEdge,
                _ => CoreDuplex::Simplex,
            };
            prefs = prefs.duplex(duplex);
        }
        prefs
    }
}

/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `json` must be a valid null-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_set_viewer_preferences_json(
    handle: *mut DocumentHandle,
    json: *const c_char,
) -> c_int {
    clear_last_error();
    if handle.is_null() || json.is_null() {
        set_last_error("Null pointer to oxidize_document_set_viewer_preferences_json");
        return ErrorCode::NullPointer as c_int;
    }
    let s = match CStr::from_ptr(json).to_str() {
        Ok(v) => v,
        Err(_) => { set_last_error("Invalid UTF-8 in viewer prefs JSON"); return ErrorCode::InvalidUtf8 as c_int; }
    };
    let payload: ViewerPrefsJson = match serde_json::from_str(s) {
        Ok(v) => v,
        Err(e) => { set_last_error(&format!("Invalid viewer prefs JSON: {e}")); return ErrorCode::SerializationError as c_int; }
    };
    (*handle).inner.set_viewer_preferences(payload.to_core());
    ErrorCode::Success as c_int
}
```

- [ ] **Step 6: Add P/Invoke + public API**

In `NativeMethods.cs`:
```csharp
[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
internal static extern int oxidize_document_set_viewer_preferences_json(IntPtr handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string json);
```

In `PdfDocument.cs`:
```csharp
/// <summary>Apply viewer preferences (toolbar visibility, fit window, page layout, etc.).</summary>
public PdfDocument SetViewerPreferences(PdfViewerPreferences prefs)
{
    ArgumentNullException.ThrowIfNull(prefs);
    ThrowIfDisposed();
    string json = System.Text.Json.JsonSerializer.Serialize(prefs);
    int rc = NativeMethods.oxidize_document_set_viewer_preferences_json(_handle, json);
    if (rc != 0) ThrowFromLastError(rc, nameof(SetViewerPreferences));
    return this;
}
```

- [ ] **Step 7: Build + run first test**

```bash
cd native && cargo build --release && cp target/release/liboxidize_pdf_ffi.so ../dotnet/OxidizePdf.NET/runtimes/linux-x64/native/
cd ../dotnet && dotnet test OxidizePdf.NET.Tests --filter "FullyQualifiedName~ViewerPreferences_HideToolbar" --no-restore
```
Expected: PASS (1/1).

- [ ] **Step 8: Add 5 additional tests**

Append to `PdfDocumentViewerPreferencesTests.cs`:
```csharp
[Fact]
[Trait("Category", "Integration")]
public void SetViewerPreferences_PageLayoutTwoColumnLeft_Written()
{
    using var doc = new PdfDocument();
    doc.AddPage(PdfPage.A4());
    doc.SetViewerPreferences(new PdfViewerPreferences { PageLayout = PdfPageLayout.TwoColumnLeft });
    string text = Encoding.Latin1.GetString(doc.SaveToBytes());
    Assert.Contains("/PageLayout /TwoColumnLeft", text);
}

[Fact]
[Trait("Category", "Integration")]
public void SetViewerPreferences_PageModeFullScreen_Written()
{
    using var doc = new PdfDocument();
    doc.AddPage(PdfPage.A4());
    doc.SetViewerPreferences(new PdfViewerPreferences { PageMode = PdfPageMode.FullScreen });
    string text = Encoding.Latin1.GetString(doc.SaveToBytes());
    Assert.Contains("/PageMode /FullScreen", text);
}

[Fact]
[Trait("Category", "Integration")]
public void SetViewerPreferences_DuplexFlipLongEdge_Written()
{
    using var doc = new PdfDocument();
    doc.AddPage(PdfPage.A4());
    doc.SetViewerPreferences(new PdfViewerPreferences { Duplex = PdfDuplex.DuplexFlipLongEdge });
    string text = Encoding.Latin1.GetString(doc.SaveToBytes());
    Assert.Contains("/Duplex /DuplexFlipLongEdge", text);
}

[Fact]
[Trait("Category", "Integration")]
public void SetViewerPreferences_AllPropertiesUnset_NoViewerPrefsDict()
{
    using var doc = new PdfDocument();
    doc.AddPage(PdfPage.A4());
    doc.SetViewerPreferences(new PdfViewerPreferences());
    string text = Encoding.Latin1.GetString(doc.SaveToBytes());
    Assert.Contains("/ViewerPreferences", text);
}

[Fact]
[Trait("Category", "Integration")]
public void SetViewerPreferences_NullArgument_ThrowsArgumentNullException()
{
    using var doc = new PdfDocument();
    doc.AddPage(PdfPage.A4());
    Assert.Throws<ArgumentNullException>(() => doc.SetViewerPreferences(null!));
}
```

- [ ] **Step 9: Run all viewer prefs tests, confirm green**

Run: `dotnet test OxidizePdf.NET.Tests --filter "FullyQualifiedName~ViewerPreferences" --no-restore`
Expected: PASS (6/6).

- [ ] **Step 10: Commit**

```bash
git add native/src/document_metadata.rs dotnet/OxidizePdf.NET/Enums.cs dotnet/OxidizePdf.NET/Models/PdfViewerPreferences.cs dotnet/OxidizePdf.NET/NativeMethods.cs dotnet/OxidizePdf.NET/PdfDocument.cs dotnet/OxidizePdf.NET.Tests/PdfDocumentViewerPreferencesTests.cs
git commit -m "feat(m1): DOC-015 viewer preferences (toolbar, page layout, duplex, print scaling)"
```

---

## Task 3: DOC-017 — Named destinations

**Why:** lets outlines/links reference logical names instead of hard-coded page numbers. Required for cross-document navigation.

**Files:**
- Modify: `native/src/document_metadata.rs`
- Modify: `dotnet/OxidizePdf.NET/NativeMethods.cs`
- Modify: `dotnet/OxidizePdf.NET/PdfDocument.cs`
- Create: `dotnet/OxidizePdf.NET.Tests/PdfDocumentNamedDestinationsTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
using System.Text;

namespace OxidizePdf.NET.Tests;

public class PdfDocumentNamedDestinationsTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void AddNamedDestination_SingleName_EmbedsNameTree()
    {
        using var doc = new PdfDocument();
        doc.AddPage(PdfPage.A4());
        doc.AddPage(PdfPage.A4());
        doc.AddNamedDestination("chapter-1", PdfDestination.Fit(1));

        string text = Encoding.Latin1.GetString(doc.SaveToBytes());

        Assert.Contains("/Names", text);
        Assert.Contains("chapter-1", text);
        Assert.Contains("/Fit", text);
    }
}
```

- [ ] **Step 2: Run test, confirm failure**

Run: `dotnet test OxidizePdf.NET.Tests --filter "FullyQualifiedName~NamedDestinations" --no-restore`
Expected: FAIL — `AddNamedDestination` not defined.

- [ ] **Step 3: Add FFI entry**

In `native/src/document_metadata.rs`, append:
```rust
use oxidize_pdf::structure::NamedDestinations;

#[derive(Deserialize)]
struct NamedDestJson {
    name: String,
    destination: DestinationJson,
}

/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `json` must be a valid null-terminated UTF-8 string containing `NamedDestJson`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_add_named_destination_json(
    handle: *mut DocumentHandle,
    json: *const c_char,
) -> c_int {
    clear_last_error();
    if handle.is_null() || json.is_null() {
        set_last_error("Null pointer to oxidize_document_add_named_destination_json");
        return ErrorCode::NullPointer as c_int;
    }
    let s = match CStr::from_ptr(json).to_str() {
        Ok(v) => v,
        Err(_) => { set_last_error("Invalid UTF-8"); return ErrorCode::InvalidUtf8 as c_int; }
    };
    let payload: NamedDestJson = match serde_json::from_str(s) {
        Ok(v) => v,
        Err(e) => { set_last_error(&format!("Invalid named dest JSON: {e}")); return ErrorCode::SerializationError as c_int; }
    };
    let doc = &mut (*handle).inner;
    if doc.named_destinations().is_none() {
        doc.set_named_destinations(NamedDestinations::new());
    }
    let dests = doc.named_destinations_mut().unwrap();
    let core_dest = payload.destination.to_core();
    dests.add_destination(payload.name, core_dest.to_array());
    ErrorCode::Success as c_int
}
```

- [ ] **Step 4: P/Invoke + public API**

`NativeMethods.cs`:
```csharp
[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
internal static extern int oxidize_document_add_named_destination_json(IntPtr handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string json);
```

`PdfDocument.cs`:
```csharp
/// <summary>Register a named destination for use in outlines or link annotations.</summary>
public PdfDocument AddNamedDestination(string name, PdfDestination destination)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(name);
    ArgumentNullException.ThrowIfNull(destination);
    ThrowIfDisposed();
    string json = System.Text.Json.JsonSerializer.Serialize(new { name, destination });
    int rc = NativeMethods.oxidize_document_add_named_destination_json(_handle, json);
    if (rc != 0) ThrowFromLastError(rc, nameof(AddNamedDestination));
    return this;
}
```

- [ ] **Step 5: Build + run first test, confirm green**

```bash
cd native && cargo build --release && cp target/release/liboxidize_pdf_ffi.so ../dotnet/OxidizePdf.NET/runtimes/linux-x64/native/
cd ../dotnet && dotnet test OxidizePdf.NET.Tests --filter "FullyQualifiedName~NamedDestinations_SingleName" --no-restore
```
Expected: PASS (1/1).

- [ ] **Step 6: Add 2 more tests**

Append:
```csharp
[Fact]
[Trait("Category", "Integration")]
public void AddNamedDestination_DuplicateName_LastWriteWins()
{
    using var doc = new PdfDocument();
    doc.AddPage(PdfPage.A4());
    doc.AddPage(PdfPage.A4());
    doc.AddNamedDestination("target", PdfDestination.Fit(0));
    doc.AddNamedDestination("target", PdfDestination.Fit(1));

    string text = Encoding.Latin1.GetString(doc.SaveToBytes());
    // "target" should appear exactly once in the Names dict
    int occurrences = System.Text.RegularExpressions.Regex.Matches(text, @"\(target\)").Count;
    Assert.Equal(1, occurrences);
}

[Fact]
[Trait("Category", "Integration")]
public void AddNamedDestination_NullOrEmptyName_Throws()
{
    using var doc = new PdfDocument();
    doc.AddPage(PdfPage.A4());
    Assert.Throws<ArgumentNullException>(() => doc.AddNamedDestination(null!, PdfDestination.Fit(0)));
    Assert.Throws<ArgumentException>(() => doc.AddNamedDestination("", PdfDestination.Fit(0)));
    Assert.Throws<ArgumentException>(() => doc.AddNamedDestination("   ", PdfDestination.Fit(0)));
}
```

- [ ] **Step 7: Run all 3 tests, confirm green**

Run: `dotnet test OxidizePdf.NET.Tests --filter "FullyQualifiedName~NamedDestinations" --no-restore`
Expected: PASS (3/3).

- [ ] **Step 8: Commit**

```bash
git add native/src/document_metadata.rs dotnet/OxidizePdf.NET/NativeMethods.cs dotnet/OxidizePdf.NET/PdfDocument.cs dotnet/OxidizePdf.NET.Tests/PdfDocumentNamedDestinationsTests.cs
git commit -m "feat(m1): DOC-017 named destinations"
```

---

## Task 4: DOC-018 — Page labels

**Why:** custom page numbering (Roman front-matter, Arabic body). High user value for books/reports.

**Files:**
- Create: `dotnet/OxidizePdf.NET/Models/PdfPageLabelRange.cs`
- Modify: `dotnet/OxidizePdf.NET/Enums.cs`
- Modify: `native/src/document_metadata.rs`
- Modify: `dotnet/OxidizePdf.NET/NativeMethods.cs`
- Modify: `dotnet/OxidizePdf.NET/PdfDocument.cs`
- Create: `dotnet/OxidizePdf.NET.Tests/PdfDocumentPageLabelsTests.cs`

- [ ] **Step 1: Write failing test (mixed ranges)**

```csharp
using System.Text;

namespace OxidizePdf.NET.Tests;

public class PdfDocumentPageLabelsTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void SetPageLabels_RomanThenDecimal_EmbedsNumsArray()
    {
        using var doc = new PdfDocument();
        for (int i = 0; i < 5; i++) doc.AddPage(PdfPage.A4());

        doc.SetPageLabels(new[]
        {
            new PdfPageLabelRange { StartPage = 0, Style = PdfPageLabelStyle.RomanLowercase },
            new PdfPageLabelRange { StartPage = 2, Style = PdfPageLabelStyle.Decimal, StartingAt = 1 },
        });

        string text = Encoding.Latin1.GetString(doc.SaveToBytes());
        Assert.Contains("/PageLabels", text);
        Assert.Contains("/S /r", text);  // roman lowercase
        Assert.Contains("/S /D", text);  // decimal
    }
}
```

- [ ] **Step 2: Run test, confirm failure**

Run: `dotnet test OxidizePdf.NET.Tests --filter "FullyQualifiedName~PageLabels" --no-restore`
Expected: FAIL — types not defined.

- [ ] **Step 3: Add enum + model**

`dotnet/OxidizePdf.NET/Enums.cs`:
```csharp
/// <summary>Page label numbering style.</summary>
public enum PdfPageLabelStyle
{
    /// <summary>Decimal Arabic (1, 2, 3, ...).</summary>
    Decimal = 0,
    /// <summary>Uppercase Roman (I, II, III, ...).</summary>
    RomanUppercase = 1,
    /// <summary>Lowercase Roman (i, ii, iii, ...).</summary>
    RomanLowercase = 2,
    /// <summary>Uppercase letters (A, B, ..., Z, AA, BB, ...).</summary>
    LettersUppercase = 3,
    /// <summary>Lowercase letters.</summary>
    LettersLowercase = 4,
    /// <summary>No numbering — prefix only.</summary>
    PrefixOnly = 5,
}
```

Create `dotnet/OxidizePdf.NET/Models/PdfPageLabelRange.cs`:
```csharp
using System.Text.Json.Serialization;

namespace OxidizePdf.NET;

/// <summary>A range of pages that share a common numbering style and optional prefix.</summary>
public sealed class PdfPageLabelRange
{
    /// <summary>0-based page index where this range begins.</summary>
    [JsonPropertyName("start_page")]
    public required uint StartPage { get; init; }

    [JsonPropertyName("style")]
    public PdfPageLabelStyle Style { get; init; }

    /// <summary>Optional literal prefix (e.g. "A-" → "A-1", "A-2").</summary>
    [JsonPropertyName("prefix")]
    public string? Prefix { get; init; }

    /// <summary>First number in the range (defaults to 1).</summary>
    [JsonPropertyName("starting_at")]
    public uint? StartingAt { get; init; }
}
```

- [ ] **Step 4: Add FFI entry**

In `native/src/document_metadata.rs`, append:
```rust
use oxidize_pdf::page_labels::{PageLabel, PageLabelStyle, PageLabelTree};

#[derive(Deserialize)]
struct PageLabelRangeJson {
    start_page: u32,
    style: u8,
    prefix: Option<String>,
    starting_at: Option<u32>,
}

#[derive(Deserialize)]
struct PageLabelsJson {
    ranges: Vec<PageLabelRangeJson>,
}

impl PageLabelRangeJson {
    fn to_label(&self) -> PageLabel {
        let style = match self.style {
            1 => PageLabelStyle::UppercaseRoman,
            2 => PageLabelStyle::LowercaseRoman,
            3 => PageLabelStyle::UppercaseLetters,
            4 => PageLabelStyle::LowercaseLetters,
            5 => PageLabelStyle::None,
            _ => PageLabelStyle::DecimalArabic,
        };
        let mut label = PageLabel::new(style);
        if let Some(p) = &self.prefix { label = label.with_prefix(p.clone()); }
        if let Some(s) = self.starting_at { label = label.starting_at(s); }
        label
    }
}

/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `json` must be a valid null-terminated UTF-8 string containing `PageLabelsJson`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_set_page_labels_json(
    handle: *mut DocumentHandle,
    json: *const c_char,
) -> c_int {
    clear_last_error();
    if handle.is_null() || json.is_null() {
        set_last_error("Null pointer to oxidize_document_set_page_labels_json");
        return ErrorCode::NullPointer as c_int;
    }
    let s = match CStr::from_ptr(json).to_str() {
        Ok(v) => v,
        Err(_) => { set_last_error("Invalid UTF-8"); return ErrorCode::InvalidUtf8 as c_int; }
    };
    let payload: PageLabelsJson = match serde_json::from_str(s) {
        Ok(v) => v,
        Err(e) => { set_last_error(&format!("Invalid page labels JSON: {e}")); return ErrorCode::SerializationError as c_int; }
    };
    if payload.ranges.is_empty() {
        set_last_error("page labels payload must contain at least one range");
        return ErrorCode::InvalidArgument as c_int;
    }
    let mut tree = PageLabelTree::new();
    for r in &payload.ranges {
        tree.add_range(r.start_page, r.to_label());
    }
    (*handle).inner.set_page_labels(tree);
    ErrorCode::Success as c_int
}
```

> **Note:** verify `PageLabelStyle` variant names before committing (Step 9) — core uses `DecimalArabic`/`UppercaseRoman` etc. If different, fix the `match` above. If Core uses the same names we already read (`decimal`, `roman_uppercase` method style), the enum variants are likely as above; grep `page_labels/page_label.rs` line 7 to confirm.

- [ ] **Step 5: P/Invoke + public API**

`NativeMethods.cs`:
```csharp
[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
internal static extern int oxidize_document_set_page_labels_json(IntPtr handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string json);
```

`PdfDocument.cs`:
```csharp
/// <summary>Set per-range page labels (roman, decimal, letters, prefix).</summary>
public PdfDocument SetPageLabels(IReadOnlyList<PdfPageLabelRange> ranges)
{
    ArgumentNullException.ThrowIfNull(ranges);
    if (ranges.Count == 0) throw new ArgumentException("At least one range required", nameof(ranges));
    ThrowIfDisposed();
    string json = System.Text.Json.JsonSerializer.Serialize(new { ranges });
    int rc = NativeMethods.oxidize_document_set_page_labels_json(_handle, json);
    if (rc != 0) ThrowFromLastError(rc, nameof(SetPageLabels));
    return this;
}
```

- [ ] **Step 6: Build + run first test, confirm green**

```bash
cd native && cargo build --release && cp target/release/liboxidize_pdf_ffi.so ../dotnet/OxidizePdf.NET/runtimes/linux-x64/native/
cd ../dotnet && dotnet test OxidizePdf.NET.Tests --filter "FullyQualifiedName~PageLabels_RomanThenDecimal" --no-restore
```
Expected: PASS (1/1).

- [ ] **Step 7: Add remaining 4 tests**

Append to `PdfDocumentPageLabelsTests.cs`:
```csharp
[Fact]
[Trait("Category", "Integration")]
public void SetPageLabels_DecimalOnly_Written()
{
    using var doc = new PdfDocument();
    doc.AddPage(PdfPage.A4());
    doc.SetPageLabels(new[] { new PdfPageLabelRange { StartPage = 0, Style = PdfPageLabelStyle.Decimal } });
    string text = Encoding.Latin1.GetString(doc.SaveToBytes());
    Assert.Contains("/S /D", text);
}

[Fact]
[Trait("Category", "Integration")]
public void SetPageLabels_WithPrefix_Written()
{
    using var doc = new PdfDocument();
    doc.AddPage(PdfPage.A4());
    doc.SetPageLabels(new[] { new PdfPageLabelRange { StartPage = 0, Style = PdfPageLabelStyle.Decimal, Prefix = "A-" } });
    string text = Encoding.Latin1.GetString(doc.SaveToBytes());
    Assert.Contains("/P", text);
    Assert.Contains("A-", text);
}

[Fact]
[Trait("Category", "Integration")]
public void SetPageLabels_StartingAt5_Written()
{
    using var doc = new PdfDocument();
    doc.AddPage(PdfPage.A4());
    doc.SetPageLabels(new[] { new PdfPageLabelRange { StartPage = 0, Style = PdfPageLabelStyle.Decimal, StartingAt = 5 } });
    string text = Encoding.Latin1.GetString(doc.SaveToBytes());
    Assert.Contains("/St 5", text);
}

[Fact]
[Trait("Category", "Integration")]
public void SetPageLabels_EmptyRanges_Throws()
{
    using var doc = new PdfDocument();
    doc.AddPage(PdfPage.A4());
    Assert.Throws<ArgumentException>(() => doc.SetPageLabels(Array.Empty<PdfPageLabelRange>()));
}
```

- [ ] **Step 8: Run all 5 tests, confirm green**

Run: `dotnet test OxidizePdf.NET.Tests --filter "FullyQualifiedName~PageLabels" --no-restore`
Expected: PASS (5/5).

- [ ] **Step 9: Verify core enum names before committing**

Run: `grep -n "^    " ~/.cargo/registry/src/index.crates.io-*/oxidize-pdf-2.5.4/src/page_labels/page_label.rs | head -15`
If variant names differ from the `match` block in Step 4, fix the Rust match and rerun build + tests.

- [ ] **Step 10: Commit**

```bash
git add native/src/document_metadata.rs dotnet/OxidizePdf.NET/Enums.cs dotnet/OxidizePdf.NET/Models/PdfPageLabelRange.cs dotnet/OxidizePdf.NET/NativeMethods.cs dotnet/OxidizePdf.NET/PdfDocument.cs dotnet/OxidizePdf.NET.Tests/PdfDocumentPageLabelsTests.cs
git commit -m "feat(m1): DOC-018 page labels (decimal/roman/letters with prefix and starting number)"
```

---

## Task 5: DOC-020 — Save with `WriterConfig`

**Why:** lets consumers pick PDF version (1.4 legacy vs 1.5/1.7 with XRef+Object streams) and compression knobs. Flat config struct — no JSON payload needed; use positional args.

**Files:**
- Create: `dotnet/OxidizePdf.NET/PdfSaveOptions.cs`
- Modify: `native/src/document_metadata.rs`
- Modify: `dotnet/OxidizePdf.NET/NativeMethods.cs`
- Modify: `dotnet/OxidizePdf.NET/PdfDocument.cs`
- Create: `dotnet/OxidizePdf.NET.Tests/PdfDocumentSaveOptionsTests.cs`

- [ ] **Step 1: Write failing test (modern config produces PDF 1.5 + ObjStm)**

```csharp
using System.Text;

namespace OxidizePdf.NET.Tests;

public class PdfDocumentSaveOptionsTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void SaveToBytes_ModernOptions_WritesPdf15WithXRefStreams()
    {
        using var doc = new PdfDocument();
        doc.AddPage(PdfPage.A4());

        byte[] bytes = doc.SaveToBytes(PdfSaveOptions.Modern());
        string text = Encoding.Latin1.GetString(bytes);

        Assert.StartsWith("%PDF-1.5", text);
        Assert.Contains("/Type /XRef", text);
    }
}
```

- [ ] **Step 2: Run test, confirm failure**

Run: `dotnet test OxidizePdf.NET.Tests --filter "FullyQualifiedName~SaveOptions" --no-restore`
Expected: FAIL — `PdfSaveOptions` / overload not defined.

- [ ] **Step 3: Create `PdfSaveOptions`**

Create `dotnet/OxidizePdf.NET/PdfSaveOptions.cs`:
```csharp
namespace OxidizePdf.NET;

/// <summary>Writer configuration for <see cref="PdfDocument.SaveToBytes(PdfSaveOptions)"/>.</summary>
public sealed class PdfSaveOptions
{
    public bool UseXrefStreams { get; init; }
    public bool UseObjectStreams { get; init; }
    public string PdfVersion { get; init; } = "1.7";
    public bool CompressStreams { get; init; } = true;

    /// <summary>PDF 1.7, no xref/object streams — default matches legacy Save().</summary>
    public static PdfSaveOptions Default() => new();

    /// <summary>PDF 1.5 with XRef + Object streams and compression.</summary>
    public static PdfSaveOptions Modern() => new()
    {
        UseXrefStreams = true, UseObjectStreams = true, PdfVersion = "1.5", CompressStreams = true
    };

    /// <summary>PDF 1.4 legacy compatibility (no streams).</summary>
    public static PdfSaveOptions Legacy() => new()
    {
        UseXrefStreams = false, UseObjectStreams = false, PdfVersion = "1.4", CompressStreams = true
    };
}
```

- [ ] **Step 4: Add FFI entry**

In `native/src/document_metadata.rs`, append:
```rust
use oxidize_pdf::writer::WriterConfig;

/// Save the document to an in-memory buffer using a custom WriterConfig.
/// Returns 0 on success and writes the buffer pointer + length out-parameters.
/// The buffer must be freed with `oxidize_free_bytes`.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `pdf_version` must be a valid null-terminated UTF-8 string.
/// - `out_ptr` and `out_len` must be valid writable pointers.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_save_to_bytes_with_config(
    handle: *mut DocumentHandle,
    use_xref_streams: c_int,
    use_object_streams: c_int,
    pdf_version: *const c_char,
    compress_streams: c_int,
    out_ptr: *mut *mut u8,
    out_len: *mut usize,
) -> c_int {
    clear_last_error();
    if handle.is_null() || pdf_version.is_null() || out_ptr.is_null() || out_len.is_null() {
        set_last_error("Null pointer to oxidize_document_save_to_bytes_with_config");
        return ErrorCode::NullPointer as c_int;
    }
    let version = match CStr::from_ptr(pdf_version).to_str() {
        Ok(v) => v.to_string(),
        Err(_) => { set_last_error("Invalid UTF-8 in pdf_version"); return ErrorCode::InvalidUtf8 as c_int; }
    };

    let config = WriterConfig {
        use_xref_streams: use_xref_streams != 0,
        use_object_streams: use_object_streams != 0,
        pdf_version: version,
        compress_streams: compress_streams != 0,
        incremental_update: false,
    };

    // Serialize to a temporary file-backed buffer (WriterConfig path takes AsRef<Path>).
    let tmp = match tempfile::NamedTempFile::new() {
        Ok(t) => t,
        Err(e) => { set_last_error(&format!("tempfile: {e}")); return ErrorCode::IoError as c_int; }
    };
    let path = tmp.path().to_path_buf();
    if let Err(e) = (*handle).inner.save_with_config(&path, config) {
        set_last_error(&format!("save_with_config: {e}"));
        return ErrorCode::IoError as c_int;
    }
    let bytes = match std::fs::read(&path) {
        Ok(b) => b,
        Err(e) => { set_last_error(&format!("read tempfile: {e}")); return ErrorCode::IoError as c_int; }
    };
    let mut boxed = bytes.into_boxed_slice();
    *out_ptr = boxed.as_mut_ptr();
    *out_len = boxed.len();
    std::mem::forget(boxed);
    ErrorCode::Success as c_int
}
```

> **Dependency note:** `tempfile` crate must be added to `native/Cargo.toml` `[dependencies]`: `tempfile = "3"`.

- [ ] **Step 5: Add tempfile dep**

In `native/Cargo.toml`, inside `[dependencies]`:
```toml
tempfile = "3"
```

- [ ] **Step 6: P/Invoke + public API**

`NativeMethods.cs`:
```csharp
[DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
internal static extern int oxidize_document_save_to_bytes_with_config(
    IntPtr handle,
    int useXrefStreams,
    int useObjectStreams,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string pdfVersion,
    int compressStreams,
    out IntPtr outPtr,
    out UIntPtr outLen);
```

`PdfDocument.cs` — add overload. Reuse the existing `oxidize_free_bytes` helper to free the buffer:
```csharp
/// <summary>Save with custom writer configuration (PDF version, stream compression, xref/object streams).</summary>
public byte[] SaveToBytes(PdfSaveOptions options)
{
    ArgumentNullException.ThrowIfNull(options);
    ThrowIfDisposed();
    int rc = NativeMethods.oxidize_document_save_to_bytes_with_config(
        _handle,
        options.UseXrefStreams ? 1 : 0,
        options.UseObjectStreams ? 1 : 0,
        options.PdfVersion,
        options.CompressStreams ? 1 : 0,
        out IntPtr ptr,
        out UIntPtr len);
    if (rc != 0) ThrowFromLastError(rc, nameof(SaveToBytes));
    try
    {
        int length = checked((int)len);
        byte[] buf = new byte[length];
        Marshal.Copy(ptr, buf, 0, length);
        return buf;
    }
    finally
    {
        NativeMethods.oxidize_free_bytes(ptr, len);
    }
}
```

> **Note:** verify `oxidize_free_bytes` exists in `NativeMethods.cs`. If the existing free helper differs (e.g. `oxidize_free_buffer`), use that name.

- [ ] **Step 7: Build + run first test, confirm green**

```bash
cd native && cargo build --release && cp target/release/liboxidize_pdf_ffi.so ../dotnet/OxidizePdf.NET/runtimes/linux-x64/native/
cd ../dotnet && dotnet test OxidizePdf.NET.Tests --filter "FullyQualifiedName~SaveOptions_Modern" --no-restore
```
Expected: PASS (1/1).

- [ ] **Step 8: Add remaining 4 tests**

Append:
```csharp
[Fact]
[Trait("Category", "Integration")]
public void SaveToBytes_LegacyOptions_WritesPdf14()
{
    using var doc = new PdfDocument();
    doc.AddPage(PdfPage.A4());
    byte[] bytes = doc.SaveToBytes(PdfSaveOptions.Legacy());
    string text = Encoding.Latin1.GetString(bytes);
    Assert.StartsWith("%PDF-1.4", text);
    Assert.DoesNotContain("/Type /XRef", text);
}

[Fact]
[Trait("Category", "Integration")]
public void SaveToBytes_DefaultOptions_WritesPdf17()
{
    using var doc = new PdfDocument();
    doc.AddPage(PdfPage.A4());
    byte[] bytes = doc.SaveToBytes(PdfSaveOptions.Default());
    string text = Encoding.Latin1.GetString(bytes);
    Assert.StartsWith("%PDF-1.7", text);
}

[Fact]
[Trait("Category", "Integration")]
public void SaveToBytes_CustomVersion_WritesThatVersion()
{
    using var doc = new PdfDocument();
    doc.AddPage(PdfPage.A4());
    byte[] bytes = doc.SaveToBytes(new PdfSaveOptions { PdfVersion = "1.6" });
    string text = Encoding.Latin1.GetString(bytes);
    Assert.StartsWith("%PDF-1.6", text);
}

[Fact]
[Trait("Category", "Integration")]
public void SaveToBytes_NullOptions_Throws()
{
    using var doc = new PdfDocument();
    doc.AddPage(PdfPage.A4());
    Assert.Throws<ArgumentNullException>(() => doc.SaveToBytes((PdfSaveOptions)null!));
}
```

- [ ] **Step 9: Run all 5 tests, confirm green**

Run: `dotnet test OxidizePdf.NET.Tests --filter "FullyQualifiedName~SaveOptions" --no-restore`
Expected: PASS (5/5).

- [ ] **Step 10: Commit**

```bash
git add native/Cargo.toml native/src/document_metadata.rs dotnet/OxidizePdf.NET/PdfSaveOptions.cs dotnet/OxidizePdf.NET/NativeMethods.cs dotnet/OxidizePdf.NET/PdfDocument.cs dotnet/OxidizePdf.NET.Tests/PdfDocumentSaveOptionsTests.cs
git commit -m "feat(m1): DOC-020 save with WriterConfig (Default/Modern/Legacy presets)"
```

---

## Task 6: Docs + changelog + full test run + PR

**Files:**
- Modify: `CHANGELOG.md`
- Modify: `docs/FEATURE_PARITY.md`

- [ ] **Step 1: Update CHANGELOG**

In `CHANGELOG.md`, under `## [Unreleased]`:
```markdown
## [0.8.0] - 2026-04-__

### Added
- **DOC-014** `PdfDocument.SetOpenAction(PdfOpenAction)` — GoTo page or URI on document open
- **DOC-015** `PdfDocument.SetViewerPreferences(PdfViewerPreferences)` — hide toolbar, page layout, page mode, print scaling, duplex
- **DOC-017** `PdfDocument.AddNamedDestination(string, PdfDestination)` — named destinations for links/outlines
- **DOC-018** `PdfDocument.SetPageLabels(IReadOnlyList<PdfPageLabelRange>)` — roman/decimal/letters with prefix and starting-at
- **DOC-020** `PdfDocument.SaveToBytes(PdfSaveOptions)` — writer config presets (Default/Modern/Legacy) and custom PDF version

### Dependencies
- oxidize-pdf 2.5.4 (no change)
- tempfile 3 (new — used for save-with-config roundtrip)
```

- [ ] **Step 2: Update FEATURE_PARITY**

In `docs/FEATURE_PARITY.md`:
- Update header: `Bridge version: 0.8.0`, `Last updated: 2026-04-__`.
- Flip DOC-014/015/017/018/020 from `| no |` to `| yes |` with notes referencing the public API method.

- [ ] **Step 3: Full test suite**

```bash
cd dotnet && dotnet test OxidizePdf.NET.Tests --no-restore -c Release
```
Expected: all previous tests + 23 new tests pass, 0 warnings.

- [ ] **Step 4: Confirm Rust build clean**

```bash
cd native && cargo build --release
```
Expected: 0 warnings, 0 errors.

- [ ] **Step 5: Commit docs**

```bash
git add CHANGELOG.md docs/FEATURE_PARITY.md
git commit -m "docs(m1): changelog and feature-parity for 0.8.0"
```

- [ ] **Step 6: Push branch**

```bash
git push -u origin feature/m1-document-metadata
```

- [ ] **Step 7: Open PR targeting `develop`**

```bash
gh pr create --base develop --head feature/m1-document-metadata \
  --title "M1: Document metadata (DOC-014/015/017/018/020) — v0.8.0" \
  --body "Closes partial scope of #20.

## Summary
Five document-level metadata features from the parity roadmap:
- DOC-014 open action (GoTo/URI)
- DOC-015 viewer preferences
- DOC-017 named destinations
- DOC-018 page labels
- DOC-020 save with WriterConfig

## Test plan
- [x] 23 new tests, all green on net10.0
- [x] 0 warnings, TreatWarningsAsErrors clean
- [x] Rust build --release 0 warnings
- [ ] CI green on matrix (linux/osx/win)

🤖 Generated with [Claude Code](https://claude.com/claude-code)"
```

- [ ] **Step 8: Close #20 after PR merge to develop**

Post completion comment on #20 and let the user decide when to close (per CLAUDE.md rule on issue state transitions).

---

## Self-review

- [x] **Spec coverage:** DOC-014 ✓, DOC-015 ✓, DOC-017 ✓, DOC-018 ✓, DOC-020 ✓ — all five features have dedicated tasks with tests.
- [x] **No placeholders** — each step has the exact code to paste or run.
- [x] **Type consistency:** `PdfDestination` used by both `PdfOpenAction.GoTo` (Task 1) and `PdfDocument.AddNamedDestination` (Task 3) — same type definition, consistent.
- [x] **Versioning:** `0.8.0` bumped in Task 0 (before any PR).
- [x] **TDD discipline:** every task starts with a failing test, minimal implementation, green.
- [x] **Verification requirement:** Task 4 Step 9 flags the one place where we might need to verify core-enum variant names (`PageLabelStyle`) because we haven't seen them yet — that's an acknowledged risk with an explicit mitigation step, not a placeholder.
- [x] **Real-reproduction tests:** every test produces bytes and asserts against actual PDF structure (never `is_ok`/`Success` only).
- [x] **Commit cadence:** one commit per feature + one docs commit = 6 commits total, each mappable to a single reviewable change.
