# TDD Plan: Full PARSE Gap Resolution

**Stack:** Rust (cdylib FFI) + C# / .NET 10, xUnit, serde_json
**Core dependency:** oxidize-pdf 2.3.2
**Branch:** `feature/dotnet-gaps-phase1-graphics`
**Created:** 2026-03-17

---

## Status Overview

| Phase | Gap(s) | Description | Status |
|-------|--------|-------------|--------|
| 1 | PARSE-002 + PARSE-005 | Lenient parsing + efficient page count | **DONE** |
| 2 | PARSE-011 | Document metadata from existing PDFs | **DONE** |
| 3 | PARSE-014 | ExtractionOptions (granular control) | **DONE** |
| 4 | PARSE-013 | Structured export (markdown, json, contextual) | **DONE** |
| 5 | PARSE-010 + PARSE-012 | RAG pipeline (partition, chunk) | **DONE** |
| 6 | PARSE-016 | Read annotations | **DONE** |
| 7 | PARSE-017 | Page resources + content streams | **DONE** |
| 8 | PARSE-015 | Page content analysis (scanned vs text) | **DONE** |
| 9 | Wiring | Final verification | **DONE** |

---

## Phase 1: Lenient Parsing + Direct Page Count (DONE)

### Cycle 1.1: Lenient parsing (PARSE-002)

- **RED**: Test PDF with corrupted xref fails with `PdfExtractionException`
- **GREEN**:
  - Import `ParseOptions` in `native/src/parser.rs`
  - Extract helper `open_lenient(bytes) -> Result<PdfReader<Cursor<&[u8]>>, String>`
  - Replace all 9 `PdfReader::new(cursor)` with `open_lenient(bytes)` in FFI functions:
    - `oxidize_extract_text`
    - `oxidize_extract_chunks`
    - `oxidize_get_page_count`
    - `oxidize_extract_text_from_page`
    - `oxidize_extract_chunks_from_page`
    - `oxidize_is_encrypted`
    - `oxidize_unlock_pdf`
    - `oxidize_get_pdf_version`
    - `oxidize_get_page_dimensions`
- **REFACTOR**: `open_lenient` consolidates all PdfReader creation — zero direct `PdfReader::new()` calls remain

### Cycle 1.2: Efficient page count (PARSE-005)

- **RED**: `oxidize_get_page_count` uses `extract_text().len()` as proxy (slow)
- **GREEN**: Replace with `document.page_count()` which reads page tree directly
- **REFACTOR**: None needed

### Results
- 137 real-world PDFs tested: 126 OK (92%), 10 encrypted (expected), 1 empty
- Non-encrypted success rate: 126/126 = 100%
- Commit: `326789f`

---

## Phase 2: Document Metadata (PARSE-011)

### Cycle 2.1: PdfMetadata model

- **RED**: Write test `test_extract_metadata_returns_model` in `PdfExtractorMetadataTests.cs`
  - Assert: `extractor.ExtractMetadataAsync(pdfBytes)` compiles
  - Assert: return type is `Task<PdfMetadata>`
  - Assert: `PdfMetadata` has properties: `Title`, `Author`, `Subject`, `Keywords`, `Creator`, `Producer`, `CreationDate`, `ModificationDate`, `Version`
- **GREEN**:
  - Create `dotnet/OxidizePdf.NET/Models/PdfMetadata.cs`
  - Deserializes JSON from FFI with snake_case naming

### Cycle 2.2: FFI function `oxidize_get_metadata`

- **RED**: `DllImport` doesn't exist, test throws `EntryPointNotFoundException`
- **GREEN**:
  - Rust: Add `MetadataResult` serialization struct + `oxidize_get_metadata(pdf_bytes, pdf_len, out_json) -> c_int` that calls `document.metadata()`
  - `NativeMethods.cs`: Add `[DllImport]` for `oxidize_get_metadata`
  - `PdfExtractor.cs`: Add `ExtractMetadataAsync(byte[], CancellationToken)` + private `ExtractMetadata(byte[])`

### Cycle 2.3: Metadata tests

- **RED**: Add tests:
  - `test_metadata_from_pdf_without_info_dict_returns_defaults` — optional fields are null
  - `test_metadata_version_is_populated` — every PDF has a version
  - `test_metadata_null_fields_when_no_info_dict` — no crash on missing `/Info`
- **GREEN**: Implementation from Cycle 2.2 covers these
- **Verify**: `nice cargo build && nice dotnet test 2>/dev/null`

---

## Phase 3: ExtractionOptions (PARSE-014)

### Cycle 3.1: ExtractionOptions model + FFI

- **RED**: Write test `test_extract_text_with_layout_preservation` in `PdfExtractorExtractionOptionsTests.cs`
  - Assert: `extractor.ExtractTextAsync(pdfBytes, extractionOptions)` compiles (overload doesn't exist)
- **GREEN**:
  - Create `dotnet/OxidizePdf.NET/Models/ExtractionOptions.cs`:
    ```csharp
    public class ExtractionOptions
    {
        public bool PreserveLayout { get; set; } = false;
        public bool SortByPosition { get; set; } = true;
        public bool MergeHyphenated { get; set; } = false;
    }
    ```
  - Rust: Add `#[repr(C)] ExtractionOptionsFFI` struct + `oxidize_extract_text_with_options(pdf_bytes, pdf_len, options, out_text) -> c_int`
  - `NativeMethods.cs`: Add `ExtractionOptionsNative` struct + `[DllImport]`
  - `PdfExtractor.cs`: Add overload `ExtractTextAsync(byte[], ExtractionOptions, CancellationToken)`

### Cycle 3.2: Tests

- **RED**: Add tests:
  - `test_extract_text_with_options_returns_text` — default options, text not empty
  - `test_extract_text_with_preserve_layout_differs_from_default`
  - `test_extract_text_with_null_options_uses_defaults`
- **Verify**: `nice cargo build && nice dotnet test 2>/dev/null`

---

## Phase 4: Structured Export (PARSE-013)

### Cycle 4.1: FFI functions for structured export

- **RED**: Write test `test_to_markdown_returns_non_empty_string` in `PdfExtractorStructuredExportTests.cs`
  - Assert: `extractor.ToMarkdownAsync(pdfBytes)` doesn't exist, compilation fails
- **GREEN**:
  - Rust: Add 3 FFI functions:
    - `oxidize_to_markdown(pdf_bytes, pdf_len, out_text) -> c_int` → calls `document.to_markdown()`
    - `oxidize_to_contextual(pdf_bytes, pdf_len, out_text) -> c_int` → calls `document.to_contextual()`
    - `oxidize_to_json(pdf_bytes, pdf_len, out_text) -> c_int` → calls `document.to_json()`
  - `NativeMethods.cs`: Add 3 `[DllImport]`
  - `PdfExtractor.cs`: Add `ToMarkdownAsync`, `ToContextualAsync`, `ToJsonAsync`

### Cycle 4.2: Tests

- **RED**: Add tests:
  - `test_to_markdown_contains_content` — result not empty
  - `test_to_contextual_returns_string`
  - `test_to_json_is_valid_json` — `JsonDocument.Parse(result)` doesn't throw
  - `test_all_export_formats_work_on_empty_content_pdf`
- **Verify**: `nice cargo build && nice dotnet test 2>/dev/null`

---

## Phase 5: RAG Pipeline (PARSE-010 improvement + PARSE-012)

### Cycle 5.1: Pipeline models

- **RED**: Test models exist with correct properties
  - `PdfElement`: `ElementType`, `Text`, `PageNumber`, `BoundingBox`
  - `RagChunk`: `Id`, `Content`, `Tokens`, `PageNumbers`, `ChunkIndex`
- **GREEN**:
  - Create `dotnet/OxidizePdf.NET/Models/PdfElement.cs`
  - Create `dotnet/OxidizePdf.NET/Models/RagChunk.cs`

### Cycle 5.2: FFI function `oxidize_partition`

- **RED**: `extractor.PartitionAsync(pdfBytes)` doesn't exist
- **GREEN**:
  - Rust: Add `PdfElementResult` serialization struct + `oxidize_partition(pdf_bytes, pdf_len, out_json) -> c_int`
    - Calls `document.partition()` → maps Element variants to JSON
    - Helper `fn element_type_name(e: &Element) -> String`
  - `NativeMethods.cs`: Add `[DllImport]`
  - `PdfExtractor.cs`: Add `PartitionAsync(byte[], CancellationToken)` returning `List<PdfElement>`

### Cycle 5.3: FFI function `oxidize_chunk_rag`

- **RED**: `extractor.ChunkRagAsync(pdfBytes, targetTokens: 512)` doesn't exist
- **GREEN**:
  - Rust: Add `RagChunkResult` struct + `oxidize_chunk_rag(pdf_bytes, pdf_len, target_tokens, out_json) -> c_int`
    - Calls `document.chunk(target_tokens)`
  - `NativeMethods.cs`: Add `[DllImport]`
  - `PdfExtractor.cs`: Add `ChunkRagAsync(byte[], int targetTokens, CancellationToken)`

### Cycle 5.4: Pipeline tests

- **RED**: Add tests in `PdfExtractorPipelineTests.cs`:
  - `test_partition_single_page_returns_elements` — `Count >= 1`
  - `test_partition_element_types_are_valid` — types in known set
  - `test_partition_page_numbers_are_1based` — `PageNumber >= 1`
  - `test_chunk_rag_returns_at_least_one_chunk`
  - `test_chunk_rag_chunks_have_content`
  - `test_chunk_rag_page_numbers_not_empty`
- **Verify**: `nice cargo build && nice dotnet test 2>/dev/null`

---

## Phase 6: Annotations (PARSE-016)

### Cycle 6.1: PdfAnnotation model

- **RED**: Test `PdfAnnotation` exists with properties: `Subtype`, `Contents`, `Title`, `PageNumber`, `Rect`
- **GREEN**: Create `dotnet/OxidizePdf.NET/Models/PdfAnnotation.cs`

### Cycle 6.2: FFI function `oxidize_get_annotations`

- **RED**: `extractor.GetAnnotationsAsync(pdfBytes)` doesn't exist
- **GREEN**:
  - Rust: Add `AnnotationResult` struct + `oxidize_get_annotations(pdf_bytes, pdf_len, out_json) -> c_int`
    - Calls `document.get_all_annotations()` → extracts Subtype, Contents, Rect from PdfDictionary
  - `NativeMethods.cs`: Add `[DllImport]`
  - `PdfExtractor.cs`: Add `GetAnnotationsAsync(byte[], CancellationToken)`

### Cycle 6.3: Annotation tests

- **RED**: Add tests in `PdfExtractorAnnotationsTests.cs`:
  - `test_get_annotations_on_pdf_without_annotations_returns_empty_list`
  - `test_get_annotations_does_not_throw`
  - `test_annotation_page_numbers_are_1based`
- **Verify**: `nice cargo build && nice dotnet test 2>/dev/null`

---

## Phase 7: Page Resources + Content Streams (PARSE-017)

### Cycle 7.1: PageResources model

- **RED**: Test `PageResources` exists with: `FontNames`, `HasImages`, `ResourceKeys`
- **GREEN**: Create `dotnet/OxidizePdf.NET/Models/PageResources.cs`

### Cycle 7.2: FFI functions

- **RED**: `extractor.GetPageResourcesAsync()` and `extractor.GetPageContentStreamAsync()` don't exist
- **GREEN**:
  - Rust: `oxidize_get_page_resources(pdf_bytes, pdf_len, page_number, out_json) -> c_int`
    - Calls `document.get_page_resources(&page)` → extracts font names, image presence, resource keys
  - Rust: `oxidize_get_page_content_stream(pdf_bytes, pdf_len, page_number, out_json) -> c_int`
    - Calls `document.get_page_content_streams(&page)` → base64-encodes raw streams
  - `NativeMethods.cs`: Add 2 `[DllImport]`
  - `PdfExtractor.cs`: Add both async methods

### Cycle 7.3: Tests

- **RED**: Add tests in `PdfExtractorPageResourcesTests.cs`:
  - `test_get_page_resources_page1` — has at least one font
  - `test_get_page_resources_no_images` — generated PDF has no images
  - `test_get_page_content_stream_returns_bytes` — non-empty list
  - `test_get_page_resources_invalid_page_throws`
- **Verify**: `nice cargo build && nice dotnet test 2>/dev/null`

---

## Phase 8: Page Content Analysis (PARSE-015)

### Cycle 8.1: Investigate PageContentAnalyzer API

- **Prerequisite**: Check if `PageContentAnalyzer` is generic over `R: Read + Seek`
  - If NOT generic (only `PdfDocument<File>`): implement heuristic in FFI
  - If generic: use directly

### Cycle 8.2: ContentAnalysis model + FFI

- **RED**: `extractor.AnalyzePageContentAsync()` doesn't exist
- **GREEN**:
  - Create `dotnet/OxidizePdf.NET/Models/ContentAnalysis.cs`:
    ```csharp
    public class ContentAnalysis
    {
        public string PageType { get; set; } = "Unknown"; // "Text", "Scanned", "Mixed"
        public int CharacterCount { get; set; }
        public bool HasContentStream { get; set; }
        public bool IsScanned => PageType == "Scanned";
        public bool IsText => PageType == "Text";
    }
    ```
  - Rust: `oxidize_analyze_page_content(pdf_bytes, pdf_len, page_number, out_json) -> c_int`
    - Heuristic: extract text char count + check content streams
  - `NativeMethods.cs` + `PdfExtractor.cs`: Wire up

### Cycle 8.3: Tests

- **RED**: Add tests in `PdfExtractorContentAnalysisTests.cs`:
  - `test_analyze_text_page_returns_text_type`
  - `test_analyze_page_has_content_stream`
  - `test_analyze_invalid_page_throws`
  - `test_is_scanned_false_for_text_pdf`
- **Verify**: `nice cargo build && nice dotnet test 2>/dev/null`

---

## Phase 9 (FINAL): Wiring & Integration Verification

### Wiring Checklist

- [ ] Every new FFI function has a call site in `PdfExtractor.cs` (not just tests)
- [ ] Every new `[DllImport]` in `NativeMethods.cs` matches a `#[no_mangle]` in Rust
- [ ] Zero `PdfReader::new(cursor)` calls outside `open_lenient` in production code
- [ ] `open_lenient` has ≥9 call sites in production
- [ ] No stale references to replaced functions
- [ ] All new models have at least one test
- [ ] `cargo build` — zero errors, zero warnings
- [ ] `dotnet build --warnaserror` — zero errors
- [ ] Full test suite GREEN

### Verification Commands

```bash
# No direct PdfReader::new() in production
grep -n "PdfReader::new(" native/src/parser.rs | grep -v "open_lenient\|#\[cfg(test)\]"

# All FFI functions have .NET call sites
grep -n "oxidize_get_metadata\|oxidize_to_markdown\|oxidize_to_contextual\|oxidize_to_json\|oxidize_partition\|oxidize_chunk_rag\|oxidize_get_annotations\|oxidize_get_page_resources\|oxidize_get_page_content_stream\|oxidize_analyze_page_content\|oxidize_extract_text_with_options" \
  dotnet/OxidizePdf.NET/PdfExtractor.cs

# Symbol check
nm -gU native/target/debug/liboxidize_pdf_ffi.dylib | grep "oxidize_"

# Full build + test
nice cargo build --manifest-path native/Cargo.toml && \
nice dotnet build dotnet/OxidizePdf.sln && \
nice dotnet test dotnet/OxidizePdf.sln 2>/dev/null
```

---

## Important Notes

1. **After every `cargo build`**, copy the dylib:
   ```bash
   cp native/target/debug/liboxidize_pdf_ffi.dylib dotnet/OxidizePdf.NET/runtimes/osx-x64/native/
   ```
2. **Always run tests with**: `nice -n 10 dotnet test ... 2>/dev/null` (lenient parser floods stderr)
3. **Production PDFs for validation**: `~/failed-pdf/` (137 files, 126 non-encrypted)
4. **Verify oxidize-pdf API** before implementing each phase — check actual signatures in `~/.cargo/registry/src/*/oxidize-pdf-2.3.2/src/`
