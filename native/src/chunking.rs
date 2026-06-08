//! FFI bridge for `oxidize_pdf::ai` document chunking, language detection, and
//! the token-efficient chunk exporter (new in oxidize-pdf 2.13.0).
//!
//! The core `DocumentChunk`/`ChunkMetadata`/`ChunkPosition`/`DetectedLanguage`
//! types do NOT derive serde, so this module defines local DTO mirrors that
//! cross the FFI boundary as JSON and convert to/from the core types via
//! [`DocumentChunkDto::from_core`] / [`DocumentChunkDto::into_core`].
//!
//! Entry points:
//! - [`oxidize_chunk_pdf`] — PDF bytes → `DocumentChunkDto[]` JSON, optionally
//!   running per-chunk language detection.
//! - [`oxidize_document_language`] — `DocumentChunkDto[]` JSON → dominant
//!   [`DetectedLanguageDto`] JSON (or the literal `null`).
//! - [`oxidize_export_chunks_token_efficient`] — `DocumentChunkDto[]` JSON →
//!   token-efficient TOON-style payload string.
//! - [`oxidize_parse_chunks_token_efficient`] — token-efficient payload string
//!   → `DocumentChunkDto[]` JSON (inverse of the exporter).

use oxidize_pdf::ai::{
    ChunkMetadata, ChunkPosition, DetectedLanguage, DocumentChunk, DocumentChunker,
    TokenEfficientExporter,
};
use oxidize_pdf::parser::PdfDocument;
use serde::{Deserialize, Serialize};
use std::ffi::{CStr, CString};
use std::os::raw::{c_char, c_int};
use std::ptr;
use std::slice;

use crate::parser::open_lenient;
use crate::{clear_last_error, set_last_error, ErrorCode};

// ── DTOs ────────────────────────────────────────────────────────────────────

/// Serde mirror of `oxidize_pdf::ai::DetectedLanguage`.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct DetectedLanguageDto {
    pub code: String,
    pub confidence: f32,
    pub reliable: bool,
}

impl DetectedLanguageDto {
    fn from_core(l: &DetectedLanguage) -> Self {
        Self {
            code: l.code.clone(),
            confidence: l.confidence,
            reliable: l.reliable,
        }
    }

    fn into_core(self) -> DetectedLanguage {
        DetectedLanguage {
            code: self.code,
            confidence: self.confidence,
            reliable: self.reliable,
        }
    }
}

/// Serde mirror of `oxidize_pdf::ai::ChunkPosition`.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ChunkPositionDto {
    pub start_char: usize,
    pub end_char: usize,
    pub first_page: usize,
    pub last_page: usize,
}

/// Serde mirror of `oxidize_pdf::ai::ChunkMetadata`.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ChunkMetadataDto {
    pub position: ChunkPositionDto,
    pub confidence: f32,
    pub sentence_boundary_respected: bool,
    pub language: Option<DetectedLanguageDto>,
}

/// Serde mirror of `oxidize_pdf::ai::DocumentChunk`.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct DocumentChunkDto {
    pub id: String,
    pub content: String,
    pub tokens: usize,
    pub page_numbers: Vec<usize>,
    pub chunk_index: usize,
    pub metadata: ChunkMetadataDto,
}

impl DocumentChunkDto {
    fn from_core(c: &DocumentChunk) -> Self {
        Self {
            id: c.id.clone(),
            content: c.content.clone(),
            tokens: c.tokens,
            page_numbers: c.page_numbers.clone(),
            chunk_index: c.chunk_index,
            metadata: ChunkMetadataDto {
                position: ChunkPositionDto {
                    start_char: c.metadata.position.start_char,
                    end_char: c.metadata.position.end_char,
                    first_page: c.metadata.position.first_page,
                    last_page: c.metadata.position.last_page,
                },
                confidence: c.metadata.confidence,
                sentence_boundary_respected: c.metadata.sentence_boundary_respected,
                language: c
                    .metadata
                    .language
                    .as_ref()
                    .map(DetectedLanguageDto::from_core),
            },
        }
    }

    fn into_core(self) -> DocumentChunk {
        DocumentChunk {
            id: self.id,
            content: self.content,
            tokens: self.tokens,
            page_numbers: self.page_numbers,
            chunk_index: self.chunk_index,
            metadata: ChunkMetadata {
                position: ChunkPosition {
                    start_char: self.metadata.position.start_char,
                    end_char: self.metadata.position.end_char,
                    first_page: self.metadata.position.first_page,
                    last_page: self.metadata.position.last_page,
                },
                confidence: self.metadata.confidence,
                sentence_boundary_respected: self.metadata.sentence_boundary_respected,
                language: self.metadata.language.map(DetectedLanguageDto::into_core),
            },
        }
    }
}

// ── Internal helpers ──────────────────────────────────────────────────────────

/// Emit a heap-allocated C string into `*out`, mapping a NUL-byte failure to
/// `InvalidUtf8`. Caller must guarantee `out` is non-null.
unsafe fn emit_cstring(s: String, out: *mut *mut c_char) -> c_int {
    match CString::new(s) {
        Ok(cs) => {
            *out = cs.into_raw();
            ErrorCode::Success as c_int
        }
        Err(e) => {
            set_last_error(format!("output contains interior NUL byte: {e}"));
            ErrorCode::InvalidUtf8 as c_int
        }
    }
}

/// Deserialize a `DocumentChunkDto[]` JSON C string into core chunks.
unsafe fn read_chunks(json_ptr: *const c_char) -> Result<Vec<DocumentChunk>, c_int> {
    let s = match CStr::from_ptr(json_ptr).to_str() {
        Ok(v) => v,
        Err(e) => {
            set_last_error(format!("invalid UTF-8 in chunks JSON: {e}"));
            return Err(ErrorCode::InvalidUtf8 as c_int);
        }
    };
    match serde_json::from_str::<Vec<DocumentChunkDto>>(s) {
        Ok(dtos) => Ok(dtos.into_iter().map(DocumentChunkDto::into_core).collect()),
        Err(e) => {
            set_last_error(format!("invalid DocumentChunk[] JSON: {e}"));
            Err(ErrorCode::InvalidArgument as c_int)
        }
    }
}

// ── FFI entry points ──────────────────────────────────────────────────────────

/// Chunk a PDF into `DocumentChunk` records suitable for RAG/LLM pipelines,
/// using the core `DocumentChunker` (token-window chunking with overlap).
///
/// When `detect_language` is non-zero, per-chunk language detection runs (the
/// `language-detection` core feature is compiled in) and each chunk's
/// `metadata.language` is populated; otherwise it stays `null`.
///
/// # Arguments
/// * `pdf_bytes` / `pdf_len` — the PDF data.
/// * `chunk_size` — target chunk size in tokens (whitespace-word estimate).
/// * `overlap` — token overlap between consecutive chunks.
/// * `detect_language` — `0` disables, non-zero enables language detection.
/// * `out_json` — receives a heap-allocated UTF-8 JSON array of
///   `DocumentChunkDto`. Free with `oxidize_free_string`.
///
/// # Returns
/// `Success`; or `NullPointer` (`pdf_bytes`/`out_json` null), `PdfParseError`
/// (`pdf_len == 0`, parse or chunk failure), `SerializationError` /
/// `InvalidUtf8` (response build failure). `*out_json` is null on any error.
///
/// # Safety
/// - `pdf_bytes` must point to `pdf_len` readable bytes.
/// - `out_json` must be a writeable `*mut *mut c_char`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_chunk_pdf(
    pdf_bytes: *const u8,
    pdf_len: usize,
    chunk_size: usize,
    overlap: usize,
    detect_language: u8,
    out_json: *mut *mut c_char,
) -> c_int {
    clear_last_error();

    if pdf_bytes.is_null() || out_json.is_null() {
        set_last_error("Null pointer provided to oxidize_chunk_pdf");
        return ErrorCode::NullPointer as c_int;
    }

    *out_json = ptr::null_mut();

    if pdf_len == 0 {
        set_last_error("PDF data is empty (0 bytes)");
        return ErrorCode::PdfParseError as c_int;
    }

    let bytes = slice::from_raw_parts(pdf_bytes, pdf_len);
    let reader = match open_lenient(bytes) {
        Ok(r) => r,
        Err(e) => {
            set_last_error(e);
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let document = PdfDocument::new(reader);
    let text_pages = match document.extract_text() {
        Ok(pages) => pages,
        Err(e) => {
            set_last_error(format!("Failed to extract text from PDF: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let page_texts: Vec<(usize, String)> = text_pages
        .iter()
        .enumerate()
        .map(|(i, page)| (i + 1, page.text.clone()))
        .collect();

    let chunker =
        DocumentChunker::new(chunk_size, overlap).with_language_detection(detect_language != 0);
    let chunks = match chunker.chunk_text_with_pages(&page_texts) {
        Ok(c) => c,
        Err(e) => {
            set_last_error(format!("Failed to chunk PDF text: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let dtos: Vec<DocumentChunkDto> = chunks.iter().map(DocumentChunkDto::from_core).collect();
    let json = match serde_json::to_string(&dtos) {
        Ok(j) => j,
        Err(e) => {
            set_last_error(format!("Failed to serialize chunks: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };

    emit_cstring(json, out_json)
}

/// Compute the dominant language across a set of chunks that already carry a
/// detected language (the aggregate of `DocumentChunker::document_language`).
///
/// # Arguments
/// * `chunks_json` — a `DocumentChunkDto[]` JSON C string (e.g. the output of
///   [`oxidize_chunk_pdf`] run with detection enabled).
/// * `out_json` — receives a heap-allocated UTF-8 JSON `DetectedLanguageDto`,
///   or the literal `null` when no chunk carries a language. Free with
///   `oxidize_free_string`.
///
/// # Returns
/// `Success`; or `NullPointer`, `InvalidUtf8`, `InvalidArgument` (bad chunk
/// JSON), `SerializationError`. `*out_json` is null on any error.
///
/// # Safety
/// - `chunks_json` must be a NUL-terminated UTF-8 C string.
/// - `out_json` must be a writeable `*mut *mut c_char`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_language(
    chunks_json: *const c_char,
    out_json: *mut *mut c_char,
) -> c_int {
    clear_last_error();

    if chunks_json.is_null() || out_json.is_null() {
        set_last_error("Null pointer provided to oxidize_document_language");
        return ErrorCode::NullPointer as c_int;
    }

    *out_json = ptr::null_mut();

    let chunks = match read_chunks(chunks_json) {
        Ok(c) => c,
        Err(code) => return code,
    };

    let json = match DocumentChunker::document_language(&chunks) {
        Some(lang) => match serde_json::to_string(&DetectedLanguageDto::from_core(&lang)) {
            Ok(j) => j,
            Err(e) => {
                set_last_error(format!("Failed to serialize detected language: {e}"));
                return ErrorCode::SerializationError as c_int;
            }
        },
        None => "null".to_string(),
    };

    emit_cstring(json, out_json)
}

/// Serialize a set of chunks into the token-efficient TOON-style payload
/// (`TokenEfficientExporter::export_chunks`).
///
/// # Arguments
/// * `chunks_json` — a `DocumentChunkDto[]` JSON C string.
/// * `out_str` — receives the heap-allocated UTF-8 payload string. Free with
///   `oxidize_free_string`.
///
/// # Returns
/// `Success`; or `NullPointer`, `InvalidUtf8`, `InvalidArgument` (bad chunk
/// JSON), `SerializationError` (exporter failure). `*out_str` is null on error.
///
/// # Safety
/// - `chunks_json` must be a NUL-terminated UTF-8 C string.
/// - `out_str` must be a writeable `*mut *mut c_char`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_export_chunks_token_efficient(
    chunks_json: *const c_char,
    out_str: *mut *mut c_char,
) -> c_int {
    clear_last_error();

    if chunks_json.is_null() || out_str.is_null() {
        set_last_error("Null pointer provided to oxidize_export_chunks_token_efficient");
        return ErrorCode::NullPointer as c_int;
    }

    *out_str = ptr::null_mut();

    let chunks = match read_chunks(chunks_json) {
        Ok(c) => c,
        Err(code) => return code,
    };

    let payload = match TokenEfficientExporter::new().export_chunks(&chunks) {
        Ok(s) => s,
        Err(e) => {
            set_last_error(format!("Failed to export chunks: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };

    emit_cstring(payload, out_str)
}

/// Parse a token-efficient payload back into chunks
/// (`TokenEfficientExporter::parse_chunks`), the inverse of
/// [`oxidize_export_chunks_token_efficient`].
///
/// # Arguments
/// * `input` — the token-efficient payload string.
/// * `out_json` — receives a heap-allocated UTF-8 JSON array of
///   `DocumentChunkDto`. Free with `oxidize_free_string`.
///
/// # Returns
/// `Success`; or `NullPointer`, `InvalidUtf8`, `InvalidArgument` (malformed
/// payload: wrong magic/header/column count), `SerializationError`. `*out_json`
/// is null on any error.
///
/// # Safety
/// - `input` must be a NUL-terminated UTF-8 C string.
/// - `out_json` must be a writeable `*mut *mut c_char`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_parse_chunks_token_efficient(
    input: *const c_char,
    out_json: *mut *mut c_char,
) -> c_int {
    clear_last_error();

    if input.is_null() || out_json.is_null() {
        set_last_error("Null pointer provided to oxidize_parse_chunks_token_efficient");
        return ErrorCode::NullPointer as c_int;
    }

    *out_json = ptr::null_mut();

    let s = match CStr::from_ptr(input).to_str() {
        Ok(v) => v,
        Err(e) => {
            set_last_error(format!("invalid UTF-8 in token-efficient payload: {e}"));
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    let chunks = match TokenEfficientExporter::parse_chunks(s) {
        Ok(c) => c,
        Err(e) => {
            set_last_error(format!("Failed to parse token-efficient payload: {e}"));
            return ErrorCode::InvalidArgument as c_int;
        }
    };

    let dtos: Vec<DocumentChunkDto> = chunks.iter().map(DocumentChunkDto::from_core).collect();
    let json = match serde_json::to_string(&dtos) {
        Ok(j) => j,
        Err(e) => {
            set_last_error(format!("Failed to serialize parsed chunks: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };

    emit_cstring(json, out_json)
}

#[cfg(test)]
mod tests {
    use super::*;

    /// PDF fixture carrying a substantial English paragraph so the language
    /// detector has enough signal to classify reliably.
    fn english_pdf() -> Vec<u8> {
        use oxidize_pdf::{Document, Font, Page};
        let mut doc = Document::new();
        let mut page = Page::a4();
        page.text()
            .set_font(Font::Helvetica, 12.0)
            .at(50.0, 750.0)
            .write(
                "The quick brown fox jumps over the lazy dog near the river bank. \
                 This paragraph is written in clear English prose so that the \
                 language detector has enough textual signal to classify the \
                 dominant language of the document with high confidence.",
            )
            .unwrap();
        doc.add_page(page);
        doc.to_bytes().unwrap()
    }

    unsafe fn read_and_free(ptr: *mut c_char) -> String {
        let s = CStr::from_ptr(ptr).to_string_lossy().into_owned();
        crate::oxidize_free_string(ptr);
        s
    }

    #[test]
    fn chunk_pdf_returns_chunk_array_with_content() {
        let pdf = english_pdf();
        let mut out: *mut c_char = ptr::null_mut();
        let code = unsafe { oxidize_chunk_pdf(pdf.as_ptr(), pdf.len(), 512, 50, 0, &mut out) };
        assert_eq!(code, ErrorCode::Success as c_int);
        assert!(!out.is_null());
        let json = unsafe { read_and_free(out) };
        let chunks: Vec<DocumentChunkDto> = serde_json::from_str(&json).unwrap();
        assert!(!chunks.is_empty(), "expected at least one chunk");
        assert!(
            chunks.iter().any(|c| c.content.contains("quick brown fox")),
            "chunk content should preserve the source text"
        );
        // Detection off → no per-chunk language.
        assert!(chunks.iter().all(|c| c.metadata.language.is_none()));
    }

    #[test]
    fn chunk_pdf_with_detection_populates_language() {
        let pdf = english_pdf();
        let mut out: *mut c_char = ptr::null_mut();
        let code = unsafe { oxidize_chunk_pdf(pdf.as_ptr(), pdf.len(), 512, 50, 1, &mut out) };
        assert_eq!(code, ErrorCode::Success as c_int);
        let json = unsafe { read_and_free(out) };
        let chunks: Vec<DocumentChunkDto> = serde_json::from_str(&json).unwrap();
        let lang = chunks
            .iter()
            .find_map(|c| c.metadata.language.as_ref())
            .expect("at least one chunk should carry a detected language");
        assert_eq!(
            lang.code, "eng",
            "English prose must detect as ISO-639-3 eng"
        );
    }

    #[test]
    fn chunk_pdf_null_pdf_returns_null_pointer() {
        let mut out: *mut c_char = ptr::null_mut();
        let code = unsafe { oxidize_chunk_pdf(ptr::null(), 0, 512, 50, 0, &mut out) };
        assert_eq!(code, ErrorCode::NullPointer as c_int);
        assert!(out.is_null());
    }

    #[test]
    fn document_language_aggregates_dominant_language() {
        let pdf = english_pdf();
        let mut chunks_out: *mut c_char = ptr::null_mut();
        let code =
            unsafe { oxidize_chunk_pdf(pdf.as_ptr(), pdf.len(), 512, 50, 1, &mut chunks_out) };
        assert_eq!(code, ErrorCode::Success as c_int);
        let chunks_json = unsafe { read_and_free(chunks_out) };
        let chunks_cstr = CString::new(chunks_json).unwrap();

        let mut out: *mut c_char = ptr::null_mut();
        let code = unsafe { oxidize_document_language(chunks_cstr.as_ptr(), &mut out) };
        assert_eq!(code, ErrorCode::Success as c_int);
        let json = unsafe { read_and_free(out) };
        let lang: DetectedLanguageDto = serde_json::from_str(&json).unwrap();
        assert_eq!(lang.code, "eng");
    }

    #[test]
    fn document_language_returns_null_when_no_language() {
        // Chunks without detection carry no language → aggregate is JSON null.
        let chunks = r#"[{"id":"chunk_0","content":"hello","tokens":1,"page_numbers":[1],"chunk_index":0,"metadata":{"position":{"start_char":0,"end_char":5,"first_page":1,"last_page":1},"confidence":1.0,"sentence_boundary_respected":true,"language":null}}]"#;
        let cstr = CString::new(chunks).unwrap();
        let mut out: *mut c_char = ptr::null_mut();
        let code = unsafe { oxidize_document_language(cstr.as_ptr(), &mut out) };
        assert_eq!(code, ErrorCode::Success as c_int);
        let json = unsafe { read_and_free(out) };
        assert_eq!(json, "null");
    }

    /// Build a `DocumentChunkDto` with the given identity/content/pages and
    /// otherwise plausible metadata. Used to exercise the exporter round-trip in
    /// isolation from the chunker.
    fn sample_chunk(index: usize, content: &str, pages: Vec<usize>) -> DocumentChunkDto {
        let first = *pages.first().unwrap_or(&1);
        let last = *pages.last().unwrap_or(&1);
        DocumentChunkDto {
            id: format!("chunk_{index}"),
            content: content.to_string(),
            tokens: content.split_whitespace().count(),
            page_numbers: pages,
            chunk_index: index,
            metadata: ChunkMetadataDto {
                position: ChunkPositionDto {
                    start_char: index * 100,
                    end_char: index * 100 + content.len(),
                    first_page: first,
                    last_page: last,
                },
                confidence: 1.0,
                sentence_boundary_respected: true,
                language: None,
            },
        }
    }

    #[test]
    fn token_efficient_export_then_parse_round_trips() {
        // Build chunks directly so the round-trip isolates the exporter/parser
        // (the chunker itself is covered by the chunk_pdf tests).
        let original = vec![
            sample_chunk(0, "First chunk content spanning page one.", vec![1]),
            sample_chunk(1, "Second chunk that crosses a page boundary.", vec![1, 2]),
            sample_chunk(2, "Third and final chunk on the last page.", vec![2]),
        ];
        let chunks_json = serde_json::to_string(&original).unwrap();
        let chunks_cstr = CString::new(chunks_json).unwrap();

        // Export
        let mut exp_out: *mut c_char = ptr::null_mut();
        let code =
            unsafe { oxidize_export_chunks_token_efficient(chunks_cstr.as_ptr(), &mut exp_out) };
        assert_eq!(code, ErrorCode::Success as c_int);
        let toon = unsafe { read_and_free(exp_out) };
        assert!(
            toon.starts_with("#oxct/1"),
            "must emit the format magic line"
        );

        // Parse back
        let toon_cstr = CString::new(toon).unwrap();
        let mut parse_out: *mut c_char = ptr::null_mut();
        let code =
            unsafe { oxidize_parse_chunks_token_efficient(toon_cstr.as_ptr(), &mut parse_out) };
        assert_eq!(code, ErrorCode::Success as c_int);
        let parsed_json = unsafe { read_and_free(parse_out) };
        let restored: Vec<DocumentChunkDto> = serde_json::from_str(&parsed_json).unwrap();

        assert_eq!(restored.len(), original.len());
        for (a, b) in original.iter().zip(restored.iter()) {
            assert_eq!(a.id, b.id);
            assert_eq!(a.content, b.content);
            assert_eq!(a.tokens, b.tokens);
            assert_eq!(a.chunk_index, b.chunk_index);
            assert_eq!(a.page_numbers, b.page_numbers);
        }
    }

    #[test]
    fn export_chunks_invalid_json_returns_invalid_argument() {
        let bad = CString::new("not json").unwrap();
        let mut out: *mut c_char = ptr::null_mut();
        let code = unsafe { oxidize_export_chunks_token_efficient(bad.as_ptr(), &mut out) };
        assert_eq!(code, ErrorCode::InvalidArgument as c_int);
        assert!(out.is_null());
    }

    #[test]
    fn parse_chunks_wrong_magic_returns_invalid_argument() {
        let bad = CString::new("#wrong/9\nheader\nrow").unwrap();
        let mut out: *mut c_char = ptr::null_mut();
        let code = unsafe { oxidize_parse_chunks_token_efficient(bad.as_ptr(), &mut out) };
        assert_eq!(code, ErrorCode::InvalidArgument as c_int);
        assert!(out.is_null());
    }
}
