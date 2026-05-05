use base64::Engine as _;
use oxidize_pdf::parser::{ParseOptions, PdfDocument, PdfReader};
use oxidize_pdf::signatures;
use serde::{Deserialize, Serialize};
use std::ffi::{CStr, CString};
use std::io::Cursor;
use std::os::raw::{c_char, c_int};
use std::ptr;
use std::slice;

use crate::{clear_last_error, find_char_boundary, set_last_error, ErrorCode};

// ── PDF reader helper ─────────────────────────────────────────────────────────

/// Open a PDF from raw bytes using lenient parsing mode.
///
/// Lenient mode enables xref recovery, tolerant syntax parsing, stream error
/// recovery, and other compatibility mechanisms required for real-world PDFs.
/// This is the only way to create a PdfReader in this module — all FFI
/// functions must use this helper instead of `PdfReader::new()`.
fn open_lenient(bytes: &[u8]) -> Result<PdfReader<Cursor<&[u8]>>, String> {
    let cursor = Cursor::new(bytes);
    PdfReader::new_with_options(cursor, ParseOptions::lenient())
        .map_err(|e| format!("Failed to parse PDF: {e}"))
}

// ── Chunk types ───────────────────────────────────────────────────────────────

/// Document chunk for RAG/LLM pipelines.
#[derive(Debug, Serialize, Deserialize)]
pub struct DocumentChunk {
    pub index: usize,
    pub page_number: usize,
    pub text: String,
    pub confidence: f64,
    pub x: f64,
    pub y: f64,
    pub width: f64,
    pub height: f64,
}

/// Chunk options supplied from the caller.
#[repr(C)]
#[derive(Copy, Clone)]
pub struct ChunkOptions {
    pub max_chunk_size: usize,
    pub overlap: usize,
    pub preserve_sentence_boundaries: bool,
    pub include_metadata: bool,
}

// ── Internal helpers ──────────────────────────────────────────────────────────

fn default_chunk_options() -> ChunkOptions {
    ChunkOptions {
        max_chunk_size: 512,
        overlap: 50,
        preserve_sentence_boundaries: true,
        include_metadata: true,
    }
}

fn chunk_page_content(
    page_content: &str,
    page_number: usize,
    opts: ChunkOptions,
    chunks: &mut Vec<DocumentChunk>,
    chunk_index: &mut usize,
) {
    let mut byte_start = 0;

    while byte_start < page_content.len() {
        let start = find_char_boundary(page_content, byte_start);
        if start >= page_content.len() {
            break;
        }

        let raw_end = (start + opts.max_chunk_size).min(page_content.len());
        let end = find_char_boundary(page_content, raw_end);

        let chunk_end = if opts.preserve_sentence_boundaries && end < page_content.len() {
            let raw_search_start =
                start + (opts.max_chunk_size * 4 / 5).min(end.saturating_sub(start));
            let search_start = find_char_boundary(page_content, raw_search_start);

            if search_start < end {
                page_content[search_start..end]
                    .rfind(&['.', '!', '?'][..])
                    .map(|i| {
                        let pos = search_start + i + 1;
                        find_char_boundary(page_content, pos)
                    })
                    .unwrap_or(end)
            } else {
                end
            }
        } else {
            end
        };

        let chunk_text = page_content[start..chunk_end].trim().to_string();

        if !chunk_text.is_empty() {
            chunks.push(DocumentChunk {
                index: *chunk_index,
                page_number,
                text: chunk_text,
                confidence: 1.0,
                x: 0.0,
                y: 0.0,
                width: 0.0,
                height: 0.0,
            });
            *chunk_index += 1;
        }

        let next_start = chunk_end.saturating_sub(opts.overlap);
        if next_start <= byte_start || chunk_end >= page_content.len() {
            break;
        }
        byte_start = next_start;
    }
}

fn chunks_to_cstring(chunks: &[DocumentChunk]) -> Result<CString, c_int> {
    let json = match serde_json::to_string(chunks) {
        Ok(j) => j,
        Err(e) => {
            set_last_error(format!("Failed to serialize chunks to JSON: {e}"));
            return Err(ErrorCode::SerializationError as c_int);
        }
    };
    match CString::new(json) {
        Ok(cs) => Ok(cs),
        Err(e) => {
            set_last_error(format!("JSON contains invalid UTF-8: {e}"));
            Err(ErrorCode::InvalidUtf8 as c_int)
        }
    }
}

// ── Extraction options (FFI-compatible) ─────────────────────────────────────

/// FFI-compatible extraction options struct.
/// Mirrors C# ExtractionOptions and maps to oxidize_pdf::text::ExtractionOptions.
#[repr(C)]
#[derive(Copy, Clone)]
pub struct ExtractionOptionsFFI {
    pub preserve_layout: bool,
    pub space_threshold: f64,
    pub newline_threshold: f64,
    pub sort_by_position: bool,
    pub detect_columns: bool,
    pub column_threshold: f64,
    pub merge_hyphenated: bool,
}

impl ExtractionOptionsFFI {
    fn to_core(self) -> oxidize_pdf::text::ExtractionOptions {
        oxidize_pdf::text::ExtractionOptions {
            preserve_layout: self.preserve_layout,
            space_threshold: self.space_threshold,
            newline_threshold: self.newline_threshold,
            sort_by_position: self.sort_by_position,
            detect_columns: self.detect_columns,
            column_threshold: self.column_threshold,
            merge_hyphenated: self.merge_hyphenated,
            track_space_decisions: false,
        }
    }
}

// ── Pipeline result types ────────────────────────────────────────────────────

/// Serialization-friendly element struct for FFI output (partition).
#[derive(Debug, Serialize)]
struct PdfElementResult {
    element_type: String,
    text: String,
    page_number: u32,
    x: f64,
    y: f64,
    width: f64,
    height: f64,
    confidence: f64,
}

/// Serialization-friendly RAG chunk struct for FFI output.
#[derive(Debug, Serialize)]
struct RagChunkResult {
    chunk_index: usize,
    text: String,
    full_text: String,
    page_numbers: Vec<u32>,
    element_types: Vec<String>,
    heading_context: Option<String>,
    token_estimate: usize,
    is_oversized: bool,
}

/// Serialization-friendly semantic chunk struct for FFI output. Mirrors
/// `oxidize_pdf::pipeline::SemanticChunk`'s public accessor surface
/// (`text()`, `token_estimate()`, `page_numbers()`, `is_oversized()`).
/// Page numbers are emitted 1-based (FFI contract).
#[derive(Debug, Serialize)]
struct SemanticChunkResult {
    chunk_index: usize,
    text: String,
    page_numbers: Vec<u32>,
    token_estimate: usize,
    is_oversized: bool,
}

/// Serialization-friendly text-chunk struct for the standalone
/// `DocumentChunker` FFI (`oxidize_chunk_text`). Mirrors
/// `oxidize_pdf::ai::DocumentChunk`'s public scalar fields, intentionally
/// dropping the nested `metadata` (position + sentence-boundary flags) —
/// the .NET `TextChunk` POCO doesn't expose them and adding them would
/// change the wire format unnecessarily.
#[derive(Debug, Serialize)]
struct TextChunkResult {
    id: String,
    content: String,
    tokens: usize,
    page_numbers: Vec<usize>,
    chunk_index: usize,
}

// ── Signature result types ────────────────────────────────────────────────────

#[derive(Debug, Serialize)]
struct SignatureFieldResult {
    field_name: Option<String>,
    filter: String,
    sub_filter: Option<String>,
    reason: Option<String>,
    location: Option<String>,
    contact_info: Option<String>,
    signing_time: Option<String>,
    signer_name: Option<String>,
    contents_size: usize,
    is_pades: bool,
    is_pkcs7_detached: bool,
}

#[derive(Debug, Serialize)]
struct CertificateInfoResult {
    subject: String,
    issuer: String,
    valid_from: String,
    valid_to: String,
    is_time_valid: bool,
    is_trusted: bool,
    is_signature_capable: bool,
    warnings: Vec<String>,
}

#[derive(Debug, Serialize)]
struct SignatureVerificationFFIResult {
    field_name: Option<String>,
    signer_name: Option<String>,
    signing_time: Option<String>,
    hash_valid: bool,
    signature_valid: bool,
    is_valid: bool,
    has_modifications_after_signing: bool,
    errors: Vec<String>,
    warnings: Vec<String>,
    digest_algorithm: Option<String>,
    signature_algorithm: Option<String>,
    certificate: Option<CertificateInfoResult>,
}

// ── Form field result types ───────────────────────────────────────────────────

#[derive(Debug, Serialize)]
struct FormFieldOptionResult {
    export_value: String,
    display_text: String,
}

#[derive(Debug, Serialize)]
struct FormFieldResult {
    field_name: String,
    field_type: String,
    page_number: u32,
    value: Option<String>,
    default_value: Option<String>,
    is_read_only: bool,
    is_required: bool,
    is_multiline: bool,
    max_length: Option<i64>,
    options: Vec<FormFieldOptionResult>,
    rect: Option<[f64; 4]>,
}

// ── Content analysis result ───────────────────────────────────────────────────

/// Serialization-friendly content analysis struct for FFI output.
#[derive(Debug, Serialize)]
struct ContentAnalysisResult {
    page_type: String,
    character_count: usize,
    has_content_stream: bool,
    image_count: usize,
}

// ── Page resources result ─────────────────────────────────────────────────────

/// Serialization-friendly page resources struct for FFI output.
#[derive(Debug, Serialize)]
struct PageResourcesResult {
    font_names: Vec<String>,
    has_xobjects: bool,
    resource_keys: Vec<String>,
}

/// Serialization-friendly content stream result for FFI output.
#[derive(Debug, Serialize)]
struct ContentStreamResult {
    streams: Vec<String>, // base64-encoded raw bytes
}

// ── Annotation result ────────────────────────────────────────────────────────

/// Serialization-friendly annotation struct for FFI output.
#[derive(Debug, Serialize)]
struct AnnotationResult {
    subtype: String,
    contents: Option<String>,
    title: Option<String>,
    page_number: u32,
    rect: Option<[f64; 4]>,
}

// ── Metadata result ─────────────────────────────────────────────────────────

/// Serialization-friendly metadata struct for FFI output.
#[derive(Debug, Serialize)]
struct MetadataResult {
    title: Option<String>,
    author: Option<String>,
    subject: Option<String>,
    keywords: Option<String>,
    creator: Option<String>,
    producer: Option<String>,
    creation_date: Option<String>,
    modification_date: Option<String>,
    version: String,
    page_count: Option<u32>,
}

// ── Public FFI functions ──────────────────────────────────────────────────────

/// Extract plain text from PDF bytes.
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `out_text` will be allocated by this function and must be freed with `oxidize_free_string`.
/// - Returns `ErrorCode::Success` on success.
#[no_mangle]
pub unsafe extern "C" fn oxidize_extract_text(
    pdf_bytes: *const u8,
    pdf_len: usize,
    out_text: *mut *mut c_char,
) -> c_int {
    clear_last_error();

    if pdf_bytes.is_null() || out_text.is_null() {
        set_last_error("Null pointer provided to oxidize_extract_text");
        return ErrorCode::NullPointer as c_int;
    }

    *out_text = ptr::null_mut();

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

    let text = text_pages
        .iter()
        .map(|p| p.text.as_str())
        .collect::<Vec<_>>()
        .join("\n\n");

    let c_string = match CString::new(text) {
        Ok(s) => s,
        Err(e) => {
            set_last_error(format!("Text contains invalid UTF-8: {e}"));
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    *out_text = c_string.into_raw();
    ErrorCode::Success as c_int
}

/// Extract text chunks optimized for RAG/LLM pipelines.
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `options` can be null (defaults will be used).
/// - `out_json` will contain a JSON array of `DocumentChunk`; must be freed with
///   `oxidize_free_string`.
/// - Returns `ErrorCode::Success` on success.
#[no_mangle]
pub unsafe extern "C" fn oxidize_extract_chunks(
    pdf_bytes: *const u8,
    pdf_len: usize,
    options: *const ChunkOptions,
    out_json: *mut *mut c_char,
) -> c_int {
    clear_last_error();

    if pdf_bytes.is_null() || out_json.is_null() {
        set_last_error("Null pointer provided to oxidize_extract_chunks");
        return ErrorCode::NullPointer as c_int;
    }

    *out_json = ptr::null_mut();

    if pdf_len == 0 {
        set_last_error("PDF data is empty (0 bytes)");
        return ErrorCode::PdfParseError as c_int;
    }

    let chunk_opts = if options.is_null() {
        default_chunk_options()
    } else {
        *options
    };

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

    let mut chunks = Vec::new();
    let mut chunk_index = 0;

    for (page_num, page_text) in text_pages.iter().enumerate() {
        chunk_page_content(
            &page_text.text,
            page_num + 1,
            chunk_opts,
            &mut chunks,
            &mut chunk_index,
        );
    }

    match chunks_to_cstring(&chunks) {
        Ok(cs) => {
            *out_json = cs.into_raw();
            ErrorCode::Success as c_int
        }
        Err(code) => code,
    }
}

/// Get the number of pages in a PDF.
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `out_count` must be a valid pointer to a `usize`.
/// - Returns `ErrorCode::Success` on success.
#[no_mangle]
pub unsafe extern "C" fn oxidize_get_page_count(
    pdf_bytes: *const u8,
    pdf_len: usize,
    out_count: *mut usize,
) -> c_int {
    clear_last_error();

    if pdf_bytes.is_null() || out_count.is_null() {
        set_last_error("Null pointer provided to oxidize_get_page_count");
        return ErrorCode::NullPointer as c_int;
    }

    *out_count = 0;

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
    let count = match document.page_count() {
        Ok(c) => c,
        Err(e) => {
            set_last_error(format!("Failed to get page count: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    *out_count = count as usize;
    ErrorCode::Success as c_int
}

/// Extract plain text from a specific page of a PDF (1-based page number).
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `page_number` is 1-based (first page = 1).
/// - `out_text` will be allocated by this function and must be freed with `oxidize_free_string`.
/// - Returns `ErrorCode::Success` on success.
#[no_mangle]
pub unsafe extern "C" fn oxidize_extract_text_from_page(
    pdf_bytes: *const u8,
    pdf_len: usize,
    page_number: usize,
    out_text: *mut *mut c_char,
) -> c_int {
    clear_last_error();

    if pdf_bytes.is_null() || out_text.is_null() {
        set_last_error("Null pointer provided to oxidize_extract_text_from_page");
        return ErrorCode::NullPointer as c_int;
    }

    *out_text = ptr::null_mut();

    if pdf_len == 0 {
        set_last_error("PDF data is empty (0 bytes)");
        return ErrorCode::PdfParseError as c_int;
    }

    if page_number == 0 {
        set_last_error("Page number must be >= 1 (1-based indexing)");
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

    let page_index = page_number - 1;
    if page_index >= text_pages.len() {
        set_last_error(format!(
            "Page number {page_number} is out of range (PDF has {} pages)",
            text_pages.len()
        ));
        return ErrorCode::PdfParseError as c_int;
    }

    let text = &text_pages[page_index].text;

    let c_string = match CString::new(text.as_str()) {
        Ok(s) => s,
        Err(e) => {
            set_last_error(format!("Text contains invalid UTF-8: {e}"));
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    *out_text = c_string.into_raw();
    ErrorCode::Success as c_int
}

/// Extract text chunks from a specific page of a PDF (1-based page number).
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `page_number` is 1-based (first page = 1).
/// - `options` can be null (defaults will be used).
/// - `out_json` will contain a JSON array of `DocumentChunk`; must be freed with
///   `oxidize_free_string`.
/// - Returns `ErrorCode::Success` on success.
#[no_mangle]
pub unsafe extern "C" fn oxidize_extract_chunks_from_page(
    pdf_bytes: *const u8,
    pdf_len: usize,
    page_number: usize,
    options: *const ChunkOptions,
    out_json: *mut *mut c_char,
) -> c_int {
    clear_last_error();

    if pdf_bytes.is_null() || out_json.is_null() {
        set_last_error("Null pointer provided to oxidize_extract_chunks_from_page");
        return ErrorCode::NullPointer as c_int;
    }

    *out_json = ptr::null_mut();

    if pdf_len == 0 {
        set_last_error("PDF data is empty (0 bytes)");
        return ErrorCode::PdfParseError as c_int;
    }

    if page_number == 0 {
        set_last_error("Page number must be >= 1 (1-based indexing)");
        return ErrorCode::PdfParseError as c_int;
    }

    let chunk_opts = if options.is_null() {
        default_chunk_options()
    } else {
        *options
    };

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

    let page_index = page_number - 1;
    if page_index >= text_pages.len() {
        set_last_error(format!(
            "Page number {page_number} is out of range (PDF has {} pages)",
            text_pages.len()
        ));
        return ErrorCode::PdfParseError as c_int;
    }

    let page_text = &text_pages[page_index];
    let mut chunks = Vec::new();
    let mut chunk_index = 0;

    chunk_page_content(
        &page_text.text,
        page_number,
        chunk_opts,
        &mut chunks,
        &mut chunk_index,
    );

    match chunks_to_cstring(&chunks) {
        Ok(cs) => {
            *out_json = cs.into_raw();
            ErrorCode::Success as c_int
        }
        Err(code) => code,
    }
}

// ── Additional parser FFI functions ─────────────────────────────────────────

/// Check if a PDF is encrypted.
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `out_encrypted` must be a valid pointer to a `bool`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_is_encrypted(
    pdf_bytes: *const u8,
    pdf_len: usize,
    out_encrypted: *mut bool,
) -> c_int {
    clear_last_error();
    if pdf_bytes.is_null() || out_encrypted.is_null() {
        set_last_error("Null pointer provided to oxidize_is_encrypted");
        return ErrorCode::NullPointer as c_int;
    }
    *out_encrypted = false;
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
    *out_encrypted = reader.is_encrypted();
    ErrorCode::Success as c_int
}

/// Try to unlock an encrypted PDF with a password.
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `password` must be a valid null-terminated UTF-8 string.
/// - `out_unlocked` must be a valid pointer to a `bool`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_unlock_pdf(
    pdf_bytes: *const u8,
    pdf_len: usize,
    password: *const c_char,
    out_unlocked: *mut bool,
) -> c_int {
    clear_last_error();
    if pdf_bytes.is_null() || password.is_null() || out_unlocked.is_null() {
        set_last_error("Null pointer provided to oxidize_unlock_pdf");
        return ErrorCode::NullPointer as c_int;
    }
    *out_unlocked = false;
    if pdf_len == 0 {
        set_last_error("PDF data is empty (0 bytes)");
        return ErrorCode::PdfParseError as c_int;
    }
    let pw = match CStr::from_ptr(password).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in password");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    let bytes = slice::from_raw_parts(pdf_bytes, pdf_len);
    let mut reader = match open_lenient(bytes) {
        Ok(r) => r,
        Err(e) => {
            set_last_error(e);
            return ErrorCode::PdfParseError as c_int;
        }
    };
    if !reader.is_encrypted() {
        *out_unlocked = true;
        return ErrorCode::Success as c_int;
    }
    match reader.unlock_with_password(pw) {
        Ok(success) => {
            *out_unlocked = success;
            ErrorCode::Success as c_int
        }
        Err(e) => {
            set_last_error(format!("Failed to unlock PDF: {e}"));
            ErrorCode::EncryptionError as c_int
        }
    }
}

/// Get the PDF version string (e.g. "1.4", "1.7").
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `out_version` must be a valid pointer; on success it will point to a
///   heap-allocated C string that must be freed with `oxidize_free_string`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_get_pdf_version(
    pdf_bytes: *const u8,
    pdf_len: usize,
    out_version: *mut *mut c_char,
) -> c_int {
    clear_last_error();
    if pdf_bytes.is_null() || out_version.is_null() {
        set_last_error("Null pointer provided to oxidize_get_pdf_version");
        return ErrorCode::NullPointer as c_int;
    }
    *out_version = ptr::null_mut();
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
    let version = reader.version().to_string();
    let c_string = match CString::new(version) {
        Ok(s) => s,
        Err(e) => {
            set_last_error(format!("Version string contains invalid data: {e}"));
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    *out_version = c_string.into_raw();
    ErrorCode::Success as c_int
}

/// Get the dimensions of a specific page from a parsed PDF (1-based page number).
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `page_number` is 1-based (first page = 1).
/// - `out_width` and `out_height` must be valid pointers to `f64`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_get_page_dimensions(
    pdf_bytes: *const u8,
    pdf_len: usize,
    page_number: usize,
    out_width: *mut f64,
    out_height: *mut f64,
) -> c_int {
    clear_last_error();
    if pdf_bytes.is_null() || out_width.is_null() || out_height.is_null() {
        set_last_error("Null pointer provided to oxidize_get_page_dimensions");
        return ErrorCode::NullPointer as c_int;
    }
    *out_width = 0.0;
    *out_height = 0.0;
    if pdf_len == 0 {
        set_last_error("PDF data is empty (0 bytes)");
        return ErrorCode::PdfParseError as c_int;
    }
    if page_number == 0 {
        set_last_error("Page number must be >= 1 (1-based indexing)");
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
    let page_index = (page_number - 1) as u32;
    let page = match document.get_page(page_index) {
        Ok(p) => p,
        Err(e) => {
            set_last_error(format!("Failed to get page {page_number}: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
    };
    *out_width = page.width();
    *out_height = page.height();
    ErrorCode::Success as c_int
}

/// Extract document metadata (Info dictionary + version + page count) from a PDF.
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `out_json` will be allocated by this function and must be freed with `oxidize_free_string`.
/// - Returns `ErrorCode::Success` on success.
#[no_mangle]
pub unsafe extern "C" fn oxidize_get_metadata(
    pdf_bytes: *const u8,
    pdf_len: usize,
    out_json: *mut *mut c_char,
) -> c_int {
    clear_last_error();

    if pdf_bytes.is_null() || out_json.is_null() {
        set_last_error("Null pointer provided to oxidize_get_metadata");
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
    let metadata = match document.metadata() {
        Ok(m) => m,
        Err(e) => {
            set_last_error(format!("Failed to extract metadata: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let result = MetadataResult {
        title: metadata.title,
        author: metadata.author,
        subject: metadata.subject,
        keywords: metadata.keywords,
        creator: metadata.creator,
        producer: metadata.producer,
        creation_date: metadata.creation_date,
        modification_date: metadata.modification_date,
        version: metadata.version,
        page_count: metadata.page_count,
    };

    let json = match serde_json::to_string(&result) {
        Ok(j) => j,
        Err(e) => {
            set_last_error(format!("Failed to serialize metadata to JSON: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };

    let c_string = match CString::new(json) {
        Ok(cs) => cs,
        Err(e) => {
            set_last_error(format!("JSON contains null bytes: {e}"));
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    *out_json = c_string.into_raw();
    ErrorCode::Success as c_int
}

/// Extract plain text from PDF bytes using custom extraction options.
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `options` must be a valid pointer to an `ExtractionOptionsFFI` struct.
/// - `out_text` will be allocated by this function and must be freed with `oxidize_free_string`.
/// - Returns `ErrorCode::Success` on success.
#[no_mangle]
pub unsafe extern "C" fn oxidize_extract_text_with_options(
    pdf_bytes: *const u8,
    pdf_len: usize,
    options: *const ExtractionOptionsFFI,
    out_text: *mut *mut c_char,
) -> c_int {
    clear_last_error();

    if pdf_bytes.is_null() || out_text.is_null() {
        set_last_error("Null pointer provided to oxidize_extract_text_with_options");
        return ErrorCode::NullPointer as c_int;
    }

    *out_text = ptr::null_mut();

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

    let core_options = if options.is_null() {
        oxidize_pdf::text::ExtractionOptions::default()
    } else {
        (*options).to_core()
    };
    let document = PdfDocument::new(reader);
    let text_pages = match document.extract_text_with_options(core_options) {
        Ok(pages) => pages,
        Err(e) => {
            set_last_error(format!("Failed to extract text with options: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let text = text_pages
        .iter()
        .map(|p| p.text.as_str())
        .collect::<Vec<_>>()
        .join("\n\n");

    let c_string = match CString::new(text) {
        Ok(s) => s,
        Err(e) => {
            set_last_error(format!("Text contains invalid UTF-8: {e}"));
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    *out_text = c_string.into_raw();
    ErrorCode::Success as c_int
}

// ── Structured export helpers ────────────────────────────────────────────────

type ExportFn = fn(&PdfDocument<Cursor<&[u8]>>) -> Result<String, oxidize_pdf::error::PdfError>;

/// Common implementation for structured export functions.
/// Opens the PDF, calls `export_fn` on the PdfDocument, and returns the result as a C string.
unsafe fn structured_export_impl(
    pdf_bytes: *const u8,
    pdf_len: usize,
    out_text: *mut *mut c_char,
    fn_name: &str,
    export_fn: ExportFn,
) -> c_int {
    clear_last_error();

    if pdf_bytes.is_null() || out_text.is_null() {
        set_last_error(format!("Null pointer provided to {fn_name}"));
        return ErrorCode::NullPointer as c_int;
    }

    *out_text = ptr::null_mut();

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
    let output = match export_fn(&document) {
        Ok(s) => s,
        Err(e) => {
            set_last_error(format!("Failed in {fn_name}: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let c_string = match CString::new(output) {
        Ok(s) => s,
        Err(e) => {
            set_last_error(format!("Output contains null bytes: {e}"));
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    *out_text = c_string.into_raw();
    ErrorCode::Success as c_int
}

/// Export PDF content as Markdown using explicit `MarkdownOptions`.
///
/// `MarkdownExporter::new(opts).export(&text)` in oxidize-pdf 2.6.0 is a
/// Phase-1 stub that ignores `include_page_numbers` and emits at most a
/// trivial `# Document\n\n` heading — strictly poorer than the legacy
/// [`oxidize_to_markdown`]. To honour `MarkdownOptions` semantically per
/// RAG-012, this function dispatches to the four well-tested static
/// exporters whose output matrix already matches the flag combinations:
///
/// | `include_metadata` | `include_page_numbers` | Backend |
/// |--------------------|------------------------|---------|
/// | `true`             | `true`                 | `export_with_metadata_and_pages` |
/// | `true`             | `false`               | `export_with_metadata` |
/// | `false`            | `true`                 | `export_with_pages` |
/// | `false`            | `false`               | `export_text` |
///
/// `include_metadata=true` emits a YAML frontmatter (`title:`, `pages:`,
/// optional `created:` / `author:`). `include_page_numbers=true` emits
/// per-page `**Page N**` markers separated by `\n\n---\n\n`.
///
/// # Arguments
/// * `pdf_bytes` — pointer to `pdf_len` bytes of PDF data.
/// * `pdf_len` — length in bytes.
/// * `options_json` — required NUL-terminated UTF-8 C string matching
///   [`crate::pipeline_config::MarkdownOptionsDto`].
/// * `out_text` — receives a heap-allocated UTF-8 markdown string. Free
///   with `oxidize_free_string`.
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `options_json` must be a NUL-terminated UTF-8 C string.
/// - `out_text` must be a writeable `*mut *mut c_char`. On any non-Success
///   return, `*out_text` is set to null.
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

    let opts_str = match CStr::from_ptr(options_json).to_str() {
        Ok(s) => s,
        Err(e) => {
            set_last_error(format!("invalid UTF-8 in options_json: {e}"));
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    let opts: oxidize_pdf::ai::MarkdownOptions =
        match serde_json::from_str::<crate::pipeline_config::MarkdownOptionsDto>(opts_str) {
            Ok(d) => d.into(),
            Err(e) => {
                set_last_error(format!("invalid MarkdownOptions JSON: {e}"));
                return ErrorCode::InvalidArgument as c_int;
            }
        };

    let bytes = slice::from_raw_parts(pdf_bytes, pdf_len);
    let reader = match open_lenient(bytes) {
        Ok(r) => r,
        Err(e) => {
            set_last_error(e);
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let document = PdfDocument::new(reader);

    // Extract per-page text once — used regardless of flag combination.
    let extracted = match document.extract_text() {
        Ok(t) => t,
        Err(e) => {
            set_last_error(format!("Failed to extract text: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
    };
    let pages: Vec<(usize, String)> = extracted
        .iter()
        .enumerate()
        .map(|(i, p)| (i + 1, p.text.clone()))
        .collect();
    let flat_text = pages
        .iter()
        .map(|(_, t)| t.clone())
        .collect::<Vec<_>>()
        .join("\n\n");

    // Dispatch by the flag matrix. Each branch uses the concrete static
    // exporter that matches its semantics — we never call the Phase-1
    // stub `MarkdownExporter::new(opts).export()`.
    let md_result = match (opts.include_metadata, opts.include_page_numbers) {
        (true, _) => {
            // Need DocumentMetadata for any metadata-enabled path.
            let parsed = match document.metadata() {
                Ok(m) => m,
                Err(e) => {
                    set_last_error(format!("Failed to read PDF metadata: {e}"));
                    return ErrorCode::PdfParseError as c_int;
                }
            };
            let ai_metadata = oxidize_pdf::ai::DocumentMetadata {
                title: parsed
                    .title
                    .unwrap_or_else(|| "Untitled Document".to_string()),
                page_count: pages.len(),
                created_at: parsed.creation_date.clone(),
                author: parsed.author,
            };
            if opts.include_page_numbers {
                oxidize_pdf::ai::MarkdownExporter::export_with_metadata_and_pages(
                    &pages,
                    &ai_metadata,
                )
            } else {
                oxidize_pdf::ai::MarkdownExporter::export_with_metadata(&flat_text, &ai_metadata)
            }
        }
        (false, true) => oxidize_pdf::ai::MarkdownExporter::export_with_pages(&pages),
        (false, false) => oxidize_pdf::ai::MarkdownExporter::export_text(&flat_text),
    };

    let md = match md_result {
        Ok(s) => s,
        Err(e) => {
            set_last_error(format!("Markdown export failed: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let c_string = match CString::new(md) {
        Ok(cs) => cs,
        Err(e) => {
            set_last_error(format!("Markdown contains null bytes: {e}"));
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    *out_text = c_string.into_raw();
    ErrorCode::Success as c_int
}

/// Export PDF content as Markdown.
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `out_text` will be allocated and must be freed with `oxidize_free_string`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_to_markdown(
    pdf_bytes: *const u8,
    pdf_len: usize,
    out_text: *mut *mut c_char,
) -> c_int {
    structured_export_impl(pdf_bytes, pdf_len, out_text, "oxidize_to_markdown", |doc| {
        doc.to_markdown()
    })
}

/// Export PDF content in contextual format (optimized for LLM context windows).
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `out_text` will be allocated and must be freed with `oxidize_free_string`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_to_contextual(
    pdf_bytes: *const u8,
    pdf_len: usize,
    out_text: *mut *mut c_char,
) -> c_int {
    structured_export_impl(
        pdf_bytes,
        pdf_len,
        out_text,
        "oxidize_to_contextual",
        |doc| doc.to_contextual(),
    )
}

/// Export PDF content as structured JSON.
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `out_text` will be allocated and must be freed with `oxidize_free_string`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_to_json(
    pdf_bytes: *const u8,
    pdf_len: usize,
    out_text: *mut *mut c_char,
) -> c_int {
    structured_export_impl(pdf_bytes, pdf_len, out_text, "oxidize_to_json", |doc| {
        doc.to_json()
    })
}

// ── Pipeline FFI functions ───────────────────────────────────────────────────

/// Partition a PDF into typed semantic elements (title, paragraph, table, etc.).
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `out_json` will be allocated and must be freed with `oxidize_free_string`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_partition(
    pdf_bytes: *const u8,
    pdf_len: usize,
    out_json: *mut *mut c_char,
) -> c_int {
    clear_last_error();

    if pdf_bytes.is_null() || out_json.is_null() {
        set_last_error("Null pointer provided to oxidize_partition");
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
    let elements = match document.partition() {
        Ok(elems) => elems,
        Err(e) => {
            set_last_error(format!("Failed to partition PDF: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let results: Vec<PdfElementResult> = elements
        .iter()
        .map(|el| {
            let bbox = el.bbox();
            PdfElementResult {
                element_type: el.type_name().to_string(),
                text: el.display_text(),
                page_number: el.page() + 1, // Convert 0-based to 1-based
                x: bbox.x,
                y: bbox.y,
                width: bbox.width,
                height: bbox.height,
                confidence: el.metadata().confidence,
            }
        })
        .collect();

    let json = match serde_json::to_string(&results) {
        Ok(j) => j,
        Err(e) => {
            set_last_error(format!("Failed to serialize elements: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };

    let c_string = match CString::new(json) {
        Ok(cs) => cs,
        Err(e) => {
            set_last_error(format!("JSON contains null bytes: {e}"));
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    *out_json = c_string.into_raw();
    ErrorCode::Success as c_int
}

/// Partition a PDF using a pre-configured extraction profile.
///
/// The profile selects sensible defaults for `ExtractionOptions` (column
/// detection, space threshold) and `PartitionConfig` (header/footer zones,
/// title font ratio, table confidence) tuned for a class of documents
/// (`Standard`, `Academic`, `Form`, `Government`, `Dense`, `Presentation`,
/// `Rag`).
///
/// # Arguments
/// * `pdf_bytes` — pointer to `pdf_len` bytes of PDF data.
/// * `pdf_len` — length in bytes.
/// * `profile` — `u8` discriminant matching the C# `ExtractionProfile` enum
///   (0 = Standard, 1 = Academic, 2 = Form, 3 = Government, 4 = Dense,
///   5 = Presentation, 6 = Rag). Mapping verified by
///   [`crate::pipeline_config::profile_from_u8`].
/// * `out_json` — receives a heap-allocated UTF-8 JSON array of element
///   results. Caller must free with `oxidize_free_string`.
///
/// # Returns
/// `ErrorCode::Success` on success, otherwise an error code; details available
/// via `oxidize_get_last_error`.
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes (or null only
///   when `pdf_len == 0` is also paired with a non-null sentinel — see
///   tests). `pdf_len == 0` always returns `PdfParseError`.
/// - `out_json` must be a writeable `*mut *mut c_char`. On any non-Success
///   return, `*out_json` is set to null.
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
        Err(e) => {
            set_last_error(e);
            return ErrorCode::InvalidArgument as c_int;
        }
    };

    let bytes = slice::from_raw_parts(pdf_bytes, pdf_len);
    let reader = match open_lenient(bytes) {
        Ok(r) => r,
        Err(e) => {
            set_last_error(e);
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let document = PdfDocument::new(reader);
    let elements = match document.partition_with_profile(prof) {
        Ok(elems) => elems,
        Err(e) => {
            set_last_error(format!("Failed to partition PDF: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let results: Vec<PdfElementResult> = elements
        .iter()
        .map(|el| {
            let bbox = el.bbox();
            PdfElementResult {
                element_type: el.type_name().to_string(),
                text: el.display_text(),
                page_number: el.page() + 1, // Convert 0-based to 1-based
                x: bbox.x,
                y: bbox.y,
                width: bbox.width,
                height: bbox.height,
                confidence: el.metadata().confidence,
            }
        })
        .collect();

    let json = match serde_json::to_string(&results) {
        Ok(j) => j,
        Err(e) => {
            set_last_error(format!("Failed to serialize elements: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };

    let c_string = match CString::new(json) {
        Ok(cs) => cs,
        Err(e) => {
            set_last_error(format!("JSON contains null bytes: {e}"));
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    *out_json = c_string.into_raw();
    ErrorCode::Success as c_int
}

/// Partition a PDF using an explicit `PartitionConfig` supplied as JSON.
///
/// Use this when callers need fine-grained control over the partitioner —
/// custom `title_min_font_ratio`, header/footer zones, table confidence
/// threshold, or a non-default `ReadingOrderStrategy` (`Simple`, `None`,
/// or `XYCut { min_gap }`). For the common case of "give me sane defaults
/// for academic papers / forms / dense text", call
/// [`oxidize_partition_with_profile`] instead.
///
/// # Arguments
/// * `pdf_bytes` — pointer to `pdf_len` bytes of PDF data.
/// * `pdf_len` — length in bytes.
/// * `config_json` — NUL-terminated UTF-8 C string containing a JSON object
///   matching [`crate::pipeline_config::PartitionConfigDto`]. Required fields
///   (all): `detect_tables`, `detect_headers_footers`, `title_min_font_ratio`,
///   `header_zone`, `footer_zone`, `reading_order`, `min_table_confidence`.
/// * `out_json` — receives a heap-allocated UTF-8 JSON array of element
///   results. Caller must free with `oxidize_free_string`.
///
/// # Returns
/// `ErrorCode::Success` on success. Error codes:
/// - `NullPointer`: any of the three pointer parameters is null.
/// - `InvalidUtf8`: `config_json` is not valid UTF-8.
/// - `InvalidArgument`: `config_json` does not deserialize as `PartitionConfigDto`.
/// - `PdfParseError`: `pdf_len == 0`, the lenient parser rejects the bytes,
///   or `partition_with` itself fails.
/// - `SerializationError` / `InvalidUtf8`: serde or `CString::new` failure
///   while building the response.
///
/// On any non-Success return, `*out_json` is set to null.
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `config_json` must be a valid NUL-terminated UTF-8 C string.
/// - `out_json` must be a writeable `*mut *mut c_char`.
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

    let cfg_str = match CStr::from_ptr(config_json).to_str() {
        Ok(s) => s,
        Err(e) => {
            set_last_error(format!("invalid UTF-8 in config_json: {e}"));
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    let dto: crate::pipeline_config::PartitionConfigDto = match serde_json::from_str(cfg_str) {
        Ok(d) => d,
        Err(e) => {
            set_last_error(format!("invalid PartitionConfig JSON: {e}"));
            return ErrorCode::InvalidArgument as c_int;
        }
    };
    let cfg: oxidize_pdf::pipeline::PartitionConfig = dto.into();

    let bytes = slice::from_raw_parts(pdf_bytes, pdf_len);
    let reader = match open_lenient(bytes) {
        Ok(r) => r,
        Err(e) => {
            set_last_error(e);
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let document = PdfDocument::new(reader);
    let elements = match document.partition_with(cfg) {
        Ok(elems) => elems,
        Err(e) => {
            set_last_error(format!("Failed to partition PDF: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let results: Vec<PdfElementResult> = elements
        .iter()
        .map(|el| {
            let bbox = el.bbox();
            PdfElementResult {
                element_type: el.type_name().to_string(),
                text: el.display_text(),
                page_number: el.page() + 1,
                x: bbox.x,
                y: bbox.y,
                width: bbox.width,
                height: bbox.height,
                confidence: el.metadata().confidence,
            }
        })
        .collect();

    let json = match serde_json::to_string(&results) {
        Ok(j) => j,
        Err(e) => {
            set_last_error(format!("Failed to serialize elements: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };

    let c_string = match CString::new(json) {
        Ok(cs) => cs,
        Err(e) => {
            set_last_error(format!("JSON contains null bytes: {e}"));
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    *out_json = c_string.into_raw();
    ErrorCode::Success as c_int
}

/// Extract RAG chunks using a pre-configured extraction profile.
///
/// Combines [`oxidize_partition_with_profile`] with the default
/// `HybridChunker` settings (max_tokens 512, overlap_tokens 50,
/// `MergePolicy::AnyInlineContent`). Use [`oxidize_rag_chunks_with_config`]
/// when you need to tune the chunk size or merge policy.
///
/// # Arguments
/// * `pdf_bytes` — pointer to `pdf_len` bytes of PDF data.
/// * `pdf_len` — length in bytes.
/// * `profile` — `u8` discriminant; same mapping as
///   [`oxidize_partition_with_profile`].
/// * `out_json` — receives a heap-allocated UTF-8 JSON array of
///   `RagChunkResult` records (`chunk_index`, `text`, `full_text`,
///   `page_numbers` (1-based), `element_types`, `heading_context`,
///   `token_estimate`, `is_oversized`). Free with `oxidize_free_string`.
///
/// # Returns
/// `ErrorCode::Success` on success. Error codes match
/// [`oxidize_partition_with_profile`].
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `out_json` must be a writeable `*mut *mut c_char`.
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
        Err(e) => {
            set_last_error(e);
            return ErrorCode::InvalidArgument as c_int;
        }
    };

    let bytes = slice::from_raw_parts(pdf_bytes, pdf_len);
    let reader = match open_lenient(bytes) {
        Ok(r) => r,
        Err(e) => {
            set_last_error(e);
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let document = PdfDocument::new(reader);
    let chunks = match document.rag_chunks_with_profile(prof) {
        Ok(c) => c,
        Err(e) => {
            set_last_error(format!("Failed to extract RAG chunks: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let results: Vec<RagChunkResult> = chunks
        .iter()
        .enumerate()
        .map(|(i, chunk)| RagChunkResult {
            chunk_index: i,
            text: chunk.text.clone(),
            full_text: chunk.full_text.clone(),
            page_numbers: chunk.page_numbers.iter().map(|p| p + 1).collect(), // 0-based to 1-based
            element_types: chunk.element_types.clone(),
            heading_context: chunk.heading_context.clone(),
            token_estimate: chunk.token_estimate,
            is_oversized: chunk.is_oversized,
        })
        .collect();

    let json = match serde_json::to_string(&results) {
        Ok(j) => j,
        Err(e) => {
            set_last_error(format!("Failed to serialize RAG chunks: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };

    let c_string = match CString::new(json) {
        Ok(cs) => cs,
        Err(e) => {
            set_last_error(format!("JSON contains null bytes: {e}"));
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    *out_json = c_string.into_raw();
    ErrorCode::Success as c_int
}

/// Extract semantic chunks (element-boundary-aware) from a PDF.
///
/// The `SemanticChunker` differs from the `HybridChunker` (used by
/// [`oxidize_rag_chunks`] and friends) in that it respects element
/// boundaries — titles, tables, and code blocks are kept whole when
/// `respect_element_boundaries` is true. Use this entry point when the
/// downstream consumer cares about preserving structural unity over
/// merging adjacent prose.
///
/// # Arguments
/// * `pdf_bytes` — pointer to `pdf_len` bytes of PDF data.
/// * `pdf_len` — length in bytes.
/// * `partition_config_json` — optional `PartitionConfigDto` JSON, or
///   `NULL` for defaults.
/// * `semantic_config_json` — **required** `SemanticChunkConfigDto` JSON
///   (no sensible default that all callers would agree on; failing fast
///   is the correct behaviour).
/// * `out_json` — receives a heap-allocated UTF-8 JSON array of
///   `SemanticChunkResult` records (`chunk_index`, `text`, `page_numbers`
///   (1-based), `token_estimate`, `is_oversized`). Free with
///   `oxidize_free_string`.
///
/// # Returns
/// `ErrorCode::Success` on success. Error codes:
/// - `NullPointer`: `pdf_bytes`, `semantic_config_json`, or `out_json` is null.
/// - `InvalidUtf8`: a non-null config pointer is not valid UTF-8.
/// - `InvalidArgument`: a non-null config does not deserialize as the
///   matching DTO.
/// - `PdfParseError`: `pdf_len == 0`, parser fails, or partition fails.
/// - `SerializationError` / `InvalidUtf8`: response build failure.
///
/// On any non-Success return, `*out_json` is set to null.
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `partition_config_json`, if non-null, must be a NUL-terminated UTF-8
///   C string. `semantic_config_json` must always be a NUL-terminated
///   UTF-8 C string.
/// - `out_json` must be a writeable `*mut *mut c_char`.
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

    let partition_cfg: oxidize_pdf::pipeline::PartitionConfig = if partition_config_json.is_null() {
        oxidize_pdf::pipeline::PartitionConfig::default()
    } else {
        let s = match CStr::from_ptr(partition_config_json).to_str() {
            Ok(v) => v,
            Err(e) => {
                set_last_error(format!("invalid UTF-8 in partition_config_json: {e}"));
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };
        match serde_json::from_str::<crate::pipeline_config::PartitionConfigDto>(s) {
            Ok(d) => d.into(),
            Err(e) => {
                set_last_error(format!("invalid PartitionConfig JSON: {e}"));
                return ErrorCode::InvalidArgument as c_int;
            }
        }
    };

    let sem_str = match CStr::from_ptr(semantic_config_json).to_str() {
        Ok(v) => v,
        Err(e) => {
            set_last_error(format!("invalid UTF-8 in semantic_config_json: {e}"));
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    let sem_cfg: oxidize_pdf::pipeline::SemanticChunkConfig =
        match serde_json::from_str::<crate::pipeline_config::SemanticChunkConfigDto>(sem_str) {
            Ok(d) => d.into(),
            Err(e) => {
                set_last_error(format!("invalid SemanticChunkConfig JSON: {e}"));
                return ErrorCode::InvalidArgument as c_int;
            }
        };

    let bytes = slice::from_raw_parts(pdf_bytes, pdf_len);
    let reader = match open_lenient(bytes) {
        Ok(r) => r,
        Err(e) => {
            set_last_error(e);
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let document = PdfDocument::new(reader);
    let elements = match document.partition_with(partition_cfg) {
        Ok(e) => e,
        Err(e) => {
            set_last_error(format!("Failed to partition PDF: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let chunker = oxidize_pdf::pipeline::SemanticChunker::new(sem_cfg);
    let sem_chunks = chunker.chunk(&elements);

    let results: Vec<SemanticChunkResult> = sem_chunks
        .iter()
        .enumerate()
        .map(|(i, sc)| SemanticChunkResult {
            chunk_index: i,
            text: sc.text(),
            page_numbers: sc.page_numbers().into_iter().map(|p| p + 1).collect(),
            token_estimate: sc.token_estimate(),
            is_oversized: sc.is_oversized(),
        })
        .collect();

    let json = match serde_json::to_string(&results) {
        Ok(j) => j,
        Err(e) => {
            set_last_error(format!("Failed to serialize semantic chunks: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };

    let c_string = match CString::new(json) {
        Ok(cs) => cs,
        Err(e) => {
            set_last_error(format!("JSON contains null bytes: {e}"));
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    *out_json = c_string.into_raw();
    ErrorCode::Success as c_int
}

/// Extract RAG chunks using explicit partition and/or hybrid configs.
///
/// Both config pointers are independently optional: passing `NULL` for either
/// uses the upstream default (`PartitionConfig::default()` /
/// `HybridChunkConfig::default()`). This lets callers tune just the chunk
/// size while keeping default partitioning, or vice versa.
///
/// # Arguments
/// * `pdf_bytes` — pointer to `pdf_len` bytes of PDF data.
/// * `pdf_len` — length in bytes.
/// * `partition_config_json` — optional `PartitionConfigDto` UTF-8 JSON string,
///   or `NULL` for defaults.
/// * `hybrid_config_json` — optional `HybridChunkConfigDto` UTF-8 JSON string,
///   or `NULL` for defaults.
/// * `out_json` — receives a heap-allocated UTF-8 JSON array of
///   `RagChunkResult` records (1-based page numbers). Free with
///   `oxidize_free_string`.
///
/// # Returns
/// `ErrorCode::Success` on success. Error codes:
/// - `NullPointer`: `pdf_bytes` or `out_json` is null.
/// - `InvalidUtf8`: a non-null config pointer is not valid UTF-8.
/// - `InvalidArgument`: a non-null config does not deserialize as the
///   matching DTO (malformed JSON or missing fields).
/// - `PdfParseError`: `pdf_len == 0`, parser fails, or partition fails.
/// - `SerializationError` / `InvalidUtf8`: response build failure.
///
/// On any non-Success return, `*out_json` is set to null.
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `partition_config_json` and `hybrid_config_json`, if non-null, must
///   each be NUL-terminated UTF-8 C strings.
/// - `out_json` must be a writeable `*mut *mut c_char`.
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

    let partition_cfg: oxidize_pdf::pipeline::PartitionConfig = if partition_config_json.is_null() {
        oxidize_pdf::pipeline::PartitionConfig::default()
    } else {
        let s = match CStr::from_ptr(partition_config_json).to_str() {
            Ok(v) => v,
            Err(e) => {
                set_last_error(format!("invalid UTF-8 in partition_config_json: {e}"));
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };
        match serde_json::from_str::<crate::pipeline_config::PartitionConfigDto>(s) {
            Ok(d) => d.into(),
            Err(e) => {
                set_last_error(format!("invalid PartitionConfig JSON: {e}"));
                return ErrorCode::InvalidArgument as c_int;
            }
        }
    };

    let hybrid_cfg: oxidize_pdf::pipeline::HybridChunkConfig = if hybrid_config_json.is_null() {
        oxidize_pdf::pipeline::HybridChunkConfig::default()
    } else {
        let s = match CStr::from_ptr(hybrid_config_json).to_str() {
            Ok(v) => v,
            Err(e) => {
                set_last_error(format!("invalid UTF-8 in hybrid_config_json: {e}"));
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };
        match serde_json::from_str::<crate::pipeline_config::HybridChunkConfigDto>(s) {
            Ok(d) => d.into(),
            Err(e) => {
                set_last_error(format!("invalid HybridChunkConfig JSON: {e}"));
                return ErrorCode::InvalidArgument as c_int;
            }
        }
    };

    let bytes = slice::from_raw_parts(pdf_bytes, pdf_len);
    let reader = match open_lenient(bytes) {
        Ok(r) => r,
        Err(e) => {
            set_last_error(e);
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let document = PdfDocument::new(reader);
    let elements = match document.partition_with(partition_cfg) {
        Ok(e) => e,
        Err(e) => {
            set_last_error(format!("Failed to partition PDF: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let chunker = oxidize_pdf::pipeline::HybridChunker::new(hybrid_cfg);
    let hybrid_chunks = chunker.chunk(&elements);
    let chunks: Vec<oxidize_pdf::pipeline::RagChunk> = hybrid_chunks
        .iter()
        .enumerate()
        .map(|(i, hc)| oxidize_pdf::pipeline::RagChunk::from_hybrid_chunk(i, hc))
        .collect();

    let results: Vec<RagChunkResult> = chunks
        .iter()
        .enumerate()
        .map(|(i, chunk)| RagChunkResult {
            chunk_index: i,
            text: chunk.text.clone(),
            full_text: chunk.full_text.clone(),
            page_numbers: chunk.page_numbers.iter().map(|p| p + 1).collect(),
            element_types: chunk.element_types.clone(),
            heading_context: chunk.heading_context.clone(),
            token_estimate: chunk.token_estimate,
            is_oversized: chunk.is_oversized,
        })
        .collect();

    let json = match serde_json::to_string(&results) {
        Ok(j) => j,
        Err(e) => {
            set_last_error(format!("Failed to serialize RAG chunks: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };

    let c_string = match CString::new(json) {
        Ok(cs) => cs,
        Err(e) => {
            set_last_error(format!("JSON contains null bytes: {e}"));
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    *out_json = c_string.into_raw();
    ErrorCode::Success as c_int
}

/// Extract structure-aware RAG chunks from a PDF.
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `out_json` will be allocated and must be freed with `oxidize_free_string`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_rag_chunks(
    pdf_bytes: *const u8,
    pdf_len: usize,
    out_json: *mut *mut c_char,
) -> c_int {
    clear_last_error();

    if pdf_bytes.is_null() || out_json.is_null() {
        set_last_error("Null pointer provided to oxidize_rag_chunks");
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
    let chunks = match document.rag_chunks() {
        Ok(c) => c,
        Err(e) => {
            set_last_error(format!("Failed to extract RAG chunks: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let results: Vec<RagChunkResult> = chunks
        .iter()
        .enumerate()
        .map(|(i, chunk)| RagChunkResult {
            chunk_index: i,
            text: chunk.text.clone(),
            full_text: chunk.full_text.clone(),
            page_numbers: chunk.page_numbers.iter().map(|p| p + 1).collect(), // 0-based to 1-based
            element_types: chunk.element_types.clone(),
            heading_context: chunk.heading_context.clone(),
            token_estimate: chunk.token_estimate,
            is_oversized: chunk.is_oversized,
        })
        .collect();

    let json = match serde_json::to_string(&results) {
        Ok(j) => j,
        Err(e) => {
            set_last_error(format!("Failed to serialize RAG chunks: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };

    let c_string = match CString::new(json) {
        Ok(cs) => cs,
        Err(e) => {
            set_last_error(format!("JSON contains null bytes: {e}"));
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    *out_json = c_string.into_raw();
    ErrorCode::Success as c_int
}

/// Estimate the number of tokens in a text string using the upstream
/// `DocumentChunker::estimate_tokens` heuristic — currently
/// `(words * 1.33) as usize` where `words` is `text.split_whitespace().count()`.
///
/// # Arguments
/// * `text` — NUL-terminated UTF-8 C string.
/// * `out_count` — receives the estimated token count.
///
/// # Returns
/// `ErrorCode::Success` on success. `NullPointer` if either argument is
/// null. `InvalidUtf8` if `text` is not valid UTF-8.
///
/// # Safety
/// `text` must be a valid NUL-terminated UTF-8 C string. `out_count` must
/// point to a writeable `usize`.
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

    let s = match CStr::from_ptr(text).to_str() {
        Ok(v) => v,
        Err(e) => {
            set_last_error(format!("invalid UTF-8 in text: {e}"));
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    *out_count = oxidize_pdf::ai::DocumentChunker::estimate_tokens(s);
    ErrorCode::Success as c_int
}

/// Chunk arbitrary text using a fixed-size + overlap strategy. No PDF
/// parsing involved — this is the standalone path for callers who already
/// have raw text in hand.
///
/// `chunk_size` and `overlap` are interpreted in **whitespace-separated
/// tokens** (matching the upstream chunker's tokenisation). The chunker
/// also tries to respect sentence boundaries inside the last 10 tokens of
/// each chunk.
///
/// # Arguments
/// * `text` — NUL-terminated UTF-8 C string to chunk.
/// * `chunk_size` — target tokens per chunk; must be positive.
/// * `overlap` — overlap between consecutive chunks; must be strictly
///   less than `chunk_size`.
/// * `out_json` — receives a heap-allocated UTF-8 JSON array of
///   `TextChunkResult` records (`id`, `content`, `tokens`,
///   `page_numbers`, `chunk_index`). `page_numbers` is empty for
///   text-only input — there's no page concept here.
///
/// # Returns
/// `ErrorCode::Success` on success. Error codes:
/// - `NullPointer`: `text` or `out_json` is null.
/// - `InvalidUtf8`: `text` is not valid UTF-8.
/// - `InvalidArgument`: `chunk_size == 0` or `overlap >= chunk_size`.
/// - `PdfParseError`: the upstream chunker failed (rare for valid input).
/// - `SerializationError` / `InvalidUtf8`: response build failure.
///
/// On any non-Success return, `*out_json` is set to null.
///
/// # Safety
/// - `text` must be a valid NUL-terminated UTF-8 C string.
/// - `out_json` must be a writeable `*mut *mut c_char`.
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
        set_last_error("chunk_size must be > 0");
        return ErrorCode::InvalidArgument as c_int;
    }

    if overlap >= chunk_size {
        set_last_error(format!(
            "overlap ({overlap}) must be strictly less than chunk_size ({chunk_size})"
        ));
        return ErrorCode::InvalidArgument as c_int;
    }

    let s = match CStr::from_ptr(text).to_str() {
        Ok(v) => v,
        Err(e) => {
            set_last_error(format!("invalid UTF-8 in text: {e}"));
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    let chunker = oxidize_pdf::ai::DocumentChunker::new(chunk_size, overlap);
    let chunks = match chunker.chunk_text(s) {
        Ok(c) => c,
        Err(e) => {
            set_last_error(format!("chunk_text failed: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let results: Vec<TextChunkResult> = chunks
        .into_iter()
        .map(|c| TextChunkResult {
            id: c.id,
            content: c.content,
            tokens: c.tokens,
            page_numbers: c.page_numbers,
            chunk_index: c.chunk_index,
        })
        .collect();

    let json = match serde_json::to_string(&results) {
        Ok(j) => j,
        Err(e) => {
            set_last_error(format!("Failed to serialize chunks: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };

    let c_string = match CString::new(json) {
        Ok(cs) => cs,
        Err(e) => {
            set_last_error(format!("JSON contains null bytes: {e}"));
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    *out_json = c_string.into_raw();
    ErrorCode::Success as c_int
}

// ── Annotations FFI ──────────────────────────────────────────────────────────

/// Extract all annotations from a PDF document as JSON.
///
/// Returns a JSON array of annotation objects with subtype, contents, title,
/// page_number (1-based), and rect fields.
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `out_json` will be allocated and must be freed with `oxidize_free_string`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_get_annotations(
    pdf_bytes: *const u8,
    pdf_len: usize,
    out_json: *mut *mut c_char,
) -> c_int {
    clear_last_error();

    if pdf_bytes.is_null() || out_json.is_null() {
        set_last_error("Null pointer provided to oxidize_get_annotations");
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
    let all_annotations = match document.get_all_annotations() {
        Ok(a) => a,
        Err(e) => {
            set_last_error(format!("Failed to get annotations: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let mut annotations: Vec<AnnotationResult> = Vec::new();

    for (page_index, dicts) in &all_annotations {
        for dict in dicts {
            let subtype = dict
                .get("Subtype")
                .and_then(|o| o.as_name())
                .map(|n| n.as_str().to_string())
                .unwrap_or_default();

            let contents = dict
                .get("Contents")
                .and_then(|o| o.as_string())
                .and_then(|s| s.as_str().ok())
                .map(|s| s.to_string());

            let title = dict
                .get("T")
                .and_then(|o| o.as_string())
                .and_then(|s| s.as_str().ok())
                .map(|s| s.to_string());

            let rect = dict.get("Rect").and_then(|o| o.as_array()).and_then(|arr| {
                if arr.0.len() == 4 {
                    let values: Vec<f64> = arr.0.iter().filter_map(|v| v.as_real()).collect();
                    if values.len() == 4 {
                        Some([values[0], values[1], values[2], values[3]])
                    } else {
                        None
                    }
                } else {
                    None
                }
            });

            annotations.push(AnnotationResult {
                subtype,
                contents,
                title,
                page_number: page_index.saturating_add(1), // 0-based to 1-based
                rect,
            });
        }
    }

    let json = match serde_json::to_string(&annotations) {
        Ok(j) => j,
        Err(e) => {
            set_last_error(format!("Failed to serialize annotations: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };

    let c_string = match CString::new(json) {
        Ok(cs) => cs,
        Err(e) => {
            set_last_error(format!("Annotations JSON contains null bytes: {e}"));
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    *out_json = c_string.into_raw();
    ErrorCode::Success as c_int
}

// ── Page Resources FFI ───────────────────────────────────────────────────────

/// Get the resources for a specific page (fonts, images, resource keys).
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `page_number` is 1-based (first page = 1).
/// - `out_json` will be allocated and must be freed with `oxidize_free_string`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_get_page_resources(
    pdf_bytes: *const u8,
    pdf_len: usize,
    page_number: usize,
    out_json: *mut *mut c_char,
) -> c_int {
    clear_last_error();

    if pdf_bytes.is_null() || out_json.is_null() {
        set_last_error("Null pointer provided to oxidize_get_page_resources");
        return ErrorCode::NullPointer as c_int;
    }

    *out_json = ptr::null_mut();

    if pdf_len == 0 {
        set_last_error("PDF data is empty (0 bytes)");
        return ErrorCode::PdfParseError as c_int;
    }

    if page_number == 0 {
        set_last_error("Page number must be >= 1 (1-based indexing)");
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
    let page_index = (page_number - 1) as u32;
    let page = match document.get_page(page_index) {
        Ok(p) => p,
        Err(e) => {
            set_last_error(format!("Failed to get page {page_number}: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let mut font_names = Vec::new();
    let mut has_xobjects = false;
    let mut resource_keys = Vec::new();

    if let Some(resources) = page.get_resources() {
        // Collect top-level resource keys
        for key in resources.0.keys() {
            resource_keys.push(key.as_str().to_string());
        }
        resource_keys.sort();

        // Extract font names
        if let Some(fonts) = resources.get("Font").and_then(|f| f.as_dict()) {
            for key in fonts.0.keys() {
                font_names.push(key.as_str().to_string());
            }
            font_names.sort();
        }

        // Check for images in XObjects
        if let Some(xobjects) = resources.get("XObject").and_then(|x| x.as_dict()) {
            has_xobjects = !xobjects.0.is_empty();
        }
    }

    let result = PageResourcesResult {
        font_names,
        has_xobjects,
        resource_keys,
    };

    let json = match serde_json::to_string(&result) {
        Ok(j) => j,
        Err(e) => {
            set_last_error(format!("Failed to serialize page resources: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };

    let c_string = match CString::new(json) {
        Ok(cs) => cs,
        Err(e) => {
            set_last_error(format!("Page resources JSON contains null bytes: {e}"));
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    *out_json = c_string.into_raw();
    ErrorCode::Success as c_int
}

/// Get the raw content streams for a specific page as base64-encoded JSON.
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `page_number` is 1-based (first page = 1).
/// - `out_json` will be allocated and must be freed with `oxidize_free_string`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_get_page_content_stream(
    pdf_bytes: *const u8,
    pdf_len: usize,
    page_number: usize,
    out_json: *mut *mut c_char,
) -> c_int {
    clear_last_error();

    if pdf_bytes.is_null() || out_json.is_null() {
        set_last_error("Null pointer provided to oxidize_get_page_content_stream");
        return ErrorCode::NullPointer as c_int;
    }

    *out_json = ptr::null_mut();

    if pdf_len == 0 {
        set_last_error("PDF data is empty (0 bytes)");
        return ErrorCode::PdfParseError as c_int;
    }

    if page_number == 0 {
        set_last_error("Page number must be >= 1 (1-based indexing)");
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
    let page_index = (page_number - 1) as u32;
    let page = match document.get_page(page_index) {
        Ok(p) => p,
        Err(e) => {
            set_last_error(format!("Failed to get page {page_number}: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let raw_streams = match page.content_streams_with_document(&document) {
        Ok(s) => s,
        Err(e) => {
            set_last_error(format!(
                "Failed to get content streams for page {page_number}: {e}"
            ));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let b64_engine = base64::engine::general_purpose::STANDARD;
    let streams: Vec<String> = raw_streams.iter().map(|s| b64_engine.encode(s)).collect();

    let result = ContentStreamResult { streams };

    let json = match serde_json::to_string(&result) {
        Ok(j) => j,
        Err(e) => {
            set_last_error(format!("Failed to serialize content streams: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };

    let c_string = match CString::new(json) {
        Ok(cs) => cs,
        Err(e) => {
            set_last_error(format!("Content stream JSON contains null bytes: {e}"));
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    *out_json = c_string.into_raw();
    ErrorCode::Success as c_int
}

// ── Page Content Analysis FFI ────────────────────────────────────────────────

/// Analyze a page's content to determine if it's text-based, scanned, or mixed.
///
/// Uses a heuristic: extracts text character count and counts image XObjects.
/// - "Text": character_count > 0 and image_count == 0
/// - "Scanned": character_count == 0 and image_count > 0
/// - "Mixed": both character_count > 0 and image_count > 0
/// - "Text": character_count == 0 and image_count == 0 (empty page defaults to Text)
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `page_number` is 1-based (first page = 1).
/// - `out_json` will be allocated and must be freed with `oxidize_free_string`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_analyze_page_content(
    pdf_bytes: *const u8,
    pdf_len: usize,
    page_number: usize,
    out_json: *mut *mut c_char,
) -> c_int {
    clear_last_error();

    if pdf_bytes.is_null() || out_json.is_null() {
        set_last_error("Null pointer provided to oxidize_analyze_page_content");
        return ErrorCode::NullPointer as c_int;
    }

    *out_json = ptr::null_mut();

    if pdf_len == 0 {
        set_last_error("PDF data is empty (0 bytes)");
        return ErrorCode::PdfParseError as c_int;
    }

    if page_number == 0 {
        set_last_error("Page number must be >= 1 (1-based indexing)");
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
    let page_index = (page_number - 1) as u32;
    let page = match document.get_page(page_index) {
        Ok(p) => p,
        Err(e) => {
            set_last_error(format!("Failed to get page {page_number}: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    // Count characters from extracted text for this specific page only
    let character_count = match document.extract_text_from_page(page_index) {
        Ok(extracted) => extracted.text.chars().count(),
        Err(_) => 0,
    };

    // Check for content streams
    let has_content_stream = page.get_contents().is_some();

    // Count image XObjects
    let image_count = if let Some(resources) = page.get_resources() {
        if let Some(xobjects) = resources.get("XObject").and_then(|x| x.as_dict()) {
            xobjects.0.len()
        } else {
            0
        }
    } else {
        0
    };

    // Determine page type using heuristic
    let page_type = if character_count > 0 && image_count > 0 {
        "Mixed"
    } else if character_count == 0 && image_count > 0 {
        "Scanned"
    } else if character_count > 0 {
        "Text"
    } else {
        "Unknown" // truly empty page — no text, no images
    };

    let result = ContentAnalysisResult {
        page_type: page_type.to_string(),
        character_count,
        has_content_stream,
        image_count,
    };

    let json = match serde_json::to_string(&result) {
        Ok(j) => j,
        Err(e) => {
            set_last_error(format!("Failed to serialize content analysis: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };

    let c_string = match CString::new(json) {
        Ok(cs) => cs,
        Err(e) => {
            set_last_error(format!("Content analysis JSON contains null bytes: {e}"));
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    *out_json = c_string.into_raw();
    ErrorCode::Success as c_int
}

// ── Digital Signatures FFI ───────────────────────────────────────────────────

/// Helper: detect signature fields from raw PDF bytes.
fn detect_sigs(bytes: &[u8]) -> Result<Vec<signatures::SignatureField>, c_int> {
    let mut reader = open_lenient(bytes).map_err(|e| {
        set_last_error(e);
        ErrorCode::PdfParseError as c_int
    })?;

    signatures::detect_signature_fields(&mut reader).map_err(|e| {
        set_last_error(format!("Failed to detect signatures: {e}"));
        ErrorCode::PdfParseError as c_int
    })
}

/// Helper: build SignatureFieldResult from a SignatureField, optionally parsing CMS.
fn build_signature_result(sig: &signatures::SignatureField) -> SignatureFieldResult {
    let signer_name = signatures::parse_pkcs7_signature(&sig.contents)
        .ok()
        .and_then(|p| p.signer_common_name().ok());

    SignatureFieldResult {
        field_name: sig.name.clone(),
        filter: sig.filter.clone(),
        sub_filter: sig.sub_filter.clone(),
        reason: sig.reason.clone(),
        location: sig.location.clone(),
        contact_info: sig.contact_info.clone(),
        signing_time: sig.signing_time.clone(),
        signer_name,
        contents_size: sig.contents_size(),
        is_pades: sig.is_pades(),
        is_pkcs7_detached: sig.is_pkcs7_detached(),
    }
}

/// Check if a PDF contains any digital signature fields.
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `out_has_signatures` must be a valid pointer to a `bool`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_has_signatures(
    pdf_bytes: *const u8,
    pdf_len: usize,
    out_has_signatures: *mut bool,
) -> c_int {
    clear_last_error();

    if pdf_bytes.is_null() || out_has_signatures.is_null() {
        set_last_error("Null pointer provided to oxidize_has_signatures");
        return ErrorCode::NullPointer as c_int;
    }

    *out_has_signatures = false;

    if pdf_len == 0 {
        set_last_error("PDF data is empty (0 bytes)");
        return ErrorCode::PdfParseError as c_int;
    }

    let bytes = slice::from_raw_parts(pdf_bytes, pdf_len);
    let sigs = match detect_sigs(bytes) {
        Ok(s) => s,
        Err(code) => return code,
    };

    *out_has_signatures = !sigs.is_empty();
    ErrorCode::Success as c_int
}

/// Extract all digital signature fields from a PDF as JSON.
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `out_json` will be allocated and must be freed with `oxidize_free_string`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_get_signatures(
    pdf_bytes: *const u8,
    pdf_len: usize,
    out_json: *mut *mut c_char,
) -> c_int {
    clear_last_error();

    if pdf_bytes.is_null() || out_json.is_null() {
        set_last_error("Null pointer provided to oxidize_get_signatures");
        return ErrorCode::NullPointer as c_int;
    }

    *out_json = ptr::null_mut();

    if pdf_len == 0 {
        set_last_error("PDF data is empty (0 bytes)");
        return ErrorCode::PdfParseError as c_int;
    }

    let bytes = slice::from_raw_parts(pdf_bytes, pdf_len);
    let sigs = match detect_sigs(bytes) {
        Ok(s) => s,
        Err(code) => return code,
    };

    let results: Vec<SignatureFieldResult> = sigs.iter().map(build_signature_result).collect();

    let json = match serde_json::to_string(&results) {
        Ok(j) => j,
        Err(e) => {
            set_last_error(format!("Failed to serialize signatures: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };

    let c_string = match CString::new(json) {
        Ok(cs) => cs,
        Err(e) => {
            set_last_error(format!("Signatures JSON contains null bytes: {e}"));
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    *out_json = c_string.into_raw();
    ErrorCode::Success as c_int
}

/// Verify all digital signatures in a PDF and return detailed results as JSON.
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `out_json` will be allocated and must be freed with `oxidize_free_string`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_verify_signatures(
    pdf_bytes: *const u8,
    pdf_len: usize,
    out_json: *mut *mut c_char,
) -> c_int {
    clear_last_error();

    if pdf_bytes.is_null() || out_json.is_null() {
        set_last_error("Null pointer provided to oxidize_verify_signatures");
        return ErrorCode::NullPointer as c_int;
    }

    *out_json = ptr::null_mut();

    if pdf_len == 0 {
        set_last_error("PDF data is empty (0 bytes)");
        return ErrorCode::PdfParseError as c_int;
    }

    let bytes = slice::from_raw_parts(pdf_bytes, pdf_len);
    let sigs = match detect_sigs(bytes) {
        Ok(s) => s,
        Err(code) => return code,
    };

    let mut results: Vec<SignatureVerificationFFIResult> = Vec::new();

    for sig in &sigs {
        let has_modifications = signatures::has_incremental_update(bytes, &sig.byte_range);

        // Parse CMS and verify
        let parsed = signatures::parse_pkcs7_signature(&sig.contents);

        let (
            signer_name,
            hash_valid,
            signature_valid,
            digest_algorithm,
            signature_algorithm,
            certificate,
            errors,
            mut warnings,
        ) = match &parsed {
            Ok(p) => {
                let signer = p.signer_common_name().ok();
                let verify_result = signatures::verify_signature(bytes, p, &sig.byte_range);
                let (hv, sv, da, sa, mut errs) = match verify_result {
                    Ok(vr) => (
                        vr.hash_valid,
                        vr.signature_valid,
                        Some(vr.digest_algorithm.name().to_string()),
                        Some(vr.signature_algorithm.name().to_string()),
                        vec![],
                    ),
                    Err(e) => (
                        false,
                        false,
                        None,
                        None,
                        vec![format!("Verify failed: {e}")],
                    ),
                };

                let cert_result = signatures::validate_certificate(
                    &p.signer_certificate_der,
                    &signatures::TrustStore::default(),
                );
                let (cert_info, cert_warnings) = match cert_result {
                    Ok(cr) => {
                        let w = cr.warnings.clone();
                        (
                            Some(CertificateInfoResult {
                                subject: cr.subject,
                                issuer: cr.issuer,
                                valid_from: cr.valid_from,
                                valid_to: cr.valid_to,
                                is_time_valid: cr.is_time_valid,
                                is_trusted: cr.is_trusted,
                                is_signature_capable: cr.is_signature_capable,
                                warnings: cr.warnings,
                            }),
                            w,
                        )
                    }
                    Err(e) => {
                        errs.push(format!("Certificate validation failed: {e}"));
                        (None, vec![])
                    }
                };

                (signer, hv, sv, da, sa, cert_info, errs, cert_warnings)
            }
            Err(e) => (
                None,
                false,
                false,
                None,
                None,
                None,
                vec![format!("CMS parsing failed: {e}")],
                vec![],
            ),
        };

        if has_modifications {
            warnings.push("Document was modified after signing".to_string());
        }

        let is_valid = hash_valid
            && signature_valid
            && errors.is_empty()
            && !has_modifications
            && certificate
                .as_ref()
                .map(|c| c.is_time_valid && c.is_trusted && c.is_signature_capable)
                .unwrap_or(false);

        results.push(SignatureVerificationFFIResult {
            field_name: sig.name.clone(),
            signer_name,
            signing_time: sig.signing_time.clone(),
            hash_valid,
            signature_valid,
            is_valid,
            has_modifications_after_signing: has_modifications,
            errors,
            warnings,
            digest_algorithm,
            signature_algorithm,
            certificate,
        });
    }

    let json = match serde_json::to_string(&results) {
        Ok(j) => j,
        Err(e) => {
            set_last_error(format!("Failed to serialize verification results: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };

    let c_string = match CString::new(json) {
        Ok(cs) => cs,
        Err(e) => {
            set_last_error(format!("Verification JSON contains null bytes: {e}"));
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    *out_json = c_string.into_raw();
    ErrorCode::Success as c_int
}

// ── Form Fields FFI ──────────────────────────────────────────────────────────

use oxidize_pdf::parser::objects::PdfDictionary;

/// Extract a string value from a PdfObject (tries Name then String).
fn pdf_obj_to_string(obj: &oxidize_pdf::parser::objects::PdfObject) -> Option<String> {
    if let Some(name) = obj.as_name() {
        return Some(name.as_str().to_string());
    }
    if let Some(s) = obj.as_string() {
        return s.as_str().ok().map(|v| v.to_string());
    }
    None
}

/// Classify a Widget annotation dictionary as a form field.
/// Returns None if it's not a Widget or has no FT entry.
fn classify_form_field(dict: &PdfDictionary, page_number: u32) -> Option<FormFieldResult> {
    // Must be Widget subtype
    let subtype = dict.get("Subtype").and_then(|o| o.as_name())?;
    if subtype.as_str() != "Widget" {
        return None;
    }

    // Must have FT (field type).
    // NOTE: AcroForm fields can inherit FT from a parent field dictionary.
    // Child widget annotations that omit FT are silently dropped here.
    // Full AcroForm tree traversal (following /Parent chains) would be
    // required to resolve inherited FT values. PDFs produced by Adobe
    // Acrobat often use this structure, so some fields may be missed.
    // See: PDF spec ISO 32000 section 12.7.3.1 "Field Dictionaries".
    let ft = dict.get("FT").and_then(|o| o.as_name())?;
    let ft_str = ft.as_str();

    // Field flags
    let ff = dict
        .get("Ff")
        .and_then(|o| o.as_integer())
        .map(|v| v.max(0) as u32)
        .unwrap_or(0);

    let is_read_only = ff & 1 != 0;
    let is_required = (ff >> 1) & 1 != 0;
    let is_multiline = (ff >> 12) & 1 != 0;
    let is_pushbutton = (ff >> 16) & 1 != 0;
    let is_radio = (ff >> 15) & 1 != 0;
    let is_combo = (ff >> 17) & 1 != 0;

    let field_type = match ft_str {
        "Tx" => "text",
        "Btn" => {
            if is_pushbutton {
                "pushbutton"
            } else if is_radio {
                "radio"
            } else {
                "checkbox"
            }
        }
        "Ch" => {
            if is_combo {
                "dropdown"
            } else {
                "listbox"
            }
        }
        "Sig" => "signature",
        _ => "unknown",
    };

    let field_name = dict
        .get("T")
        .and_then(|o| o.as_string())
        .and_then(|s| s.as_str().ok())
        .map(|s| s.to_string())
        .unwrap_or_default();

    let value = dict.get("V").and_then(pdf_obj_to_string);
    let default_value = dict.get("DV").and_then(pdf_obj_to_string);

    let max_length = dict.get("MaxLen").and_then(|o| o.as_integer());

    // Parse Opt array for choice fields
    let options = dict
        .get("Opt")
        .and_then(|o| o.as_array())
        .map(|arr| {
            arr.0
                .iter()
                .map(|item| {
                    // Option can be a simple string or [export_value, display_text]
                    if let Some(sub_arr) = item.as_array() {
                        let export = sub_arr
                            .0
                            .first()
                            .and_then(pdf_obj_to_string)
                            .unwrap_or_default();
                        let display = sub_arr
                            .0
                            .get(1)
                            .and_then(pdf_obj_to_string)
                            .unwrap_or_else(|| export.clone());
                        FormFieldOptionResult {
                            export_value: export,
                            display_text: display,
                        }
                    } else {
                        let text = pdf_obj_to_string(item).unwrap_or_default();
                        FormFieldOptionResult {
                            export_value: text.clone(),
                            display_text: text,
                        }
                    }
                })
                .collect()
        })
        .unwrap_or_default();

    let rect = dict.get("Rect").and_then(|o| o.as_array()).and_then(|arr| {
        if arr.0.len() == 4 {
            let vals: Vec<f64> = arr.0.iter().filter_map(|v| v.as_real()).collect();
            if vals.len() == 4 {
                Some([vals[0], vals[1], vals[2], vals[3]])
            } else {
                None
            }
        } else {
            None
        }
    });

    Some(FormFieldResult {
        field_name,
        field_type: field_type.to_string(),
        page_number,
        value,
        default_value,
        is_read_only,
        is_required,
        is_multiline,
        max_length,
        options,
        rect,
    })
}

/// Check if a PDF contains any form fields (Widget annotations with FT entry).
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `out_has` must be a valid pointer to a `bool`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_has_form_fields(
    pdf_bytes: *const u8,
    pdf_len: usize,
    out_has: *mut bool,
) -> c_int {
    clear_last_error();

    if pdf_bytes.is_null() || out_has.is_null() {
        set_last_error("Null pointer provided to oxidize_has_form_fields");
        return ErrorCode::NullPointer as c_int;
    }

    *out_has = false;

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
    let all_annots = match document.get_all_annotations() {
        Ok(a) => a,
        Err(e) => {
            set_last_error(format!("Failed to get annotations: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    for (page_index, dicts) in &all_annots {
        for dict in dicts {
            if classify_form_field(dict, page_index.saturating_add(1)).is_some() {
                *out_has = true;
                return ErrorCode::Success as c_int;
            }
        }
    }

    ErrorCode::Success as c_int
}

/// Extract all form fields from a PDF as JSON.
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `out_json` will be allocated and must be freed with `oxidize_free_string`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_get_form_fields(
    pdf_bytes: *const u8,
    pdf_len: usize,
    out_json: *mut *mut c_char,
) -> c_int {
    clear_last_error();

    if pdf_bytes.is_null() || out_json.is_null() {
        set_last_error("Null pointer provided to oxidize_get_form_fields");
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
    let all_annots = match document.get_all_annotations() {
        Ok(a) => a,
        Err(e) => {
            set_last_error(format!("Failed to get annotations: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let mut fields: Vec<FormFieldResult> = Vec::new();
    for (page_index, dicts) in &all_annots {
        for dict in dicts {
            if let Some(field) = classify_form_field(dict, page_index.saturating_add(1)) {
                fields.push(field);
            }
        }
    }

    let json = match serde_json::to_string(&fields) {
        Ok(j) => j,
        Err(e) => {
            set_last_error(format!("Failed to serialize form fields: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };

    let c_string = match CString::new(json) {
        Ok(cs) => cs,
        Err(e) => {
            set_last_error(format!("Form fields JSON contains null bytes: {e}"));
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    *out_json = c_string.into_raw();
    ErrorCode::Success as c_int
}

#[cfg(test)]
mod profile_ffi_tests {
    use super::*;
    use std::ffi::CStr;

    /// Minimal-but-realistic PDF fixture: A4 with a title and a paragraph.
    /// Built via the `oxidize_pdf` writer so the partitioner has actual
    /// fragments to classify.
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
        unsafe {
            crate::oxidize_free_string(out);
        }
        assert!(json.starts_with('['));
        assert!(json.contains("element_type"));
        assert!(json.contains("page_number"));
        assert!(json.contains("Introduction"));
    }

    #[test]
    fn oxidize_partition_with_profile_rejects_bad_discriminant() {
        let pdf = sample_pdf();
        let mut out: *mut c_char = std::ptr::null_mut();
        let code =
            unsafe { oxidize_partition_with_profile(pdf.as_ptr(), pdf.len(), 99, &mut out) };
        assert_eq!(code, ErrorCode::InvalidArgument as c_int);
        assert!(out.is_null());
    }

    #[test]
    fn oxidize_partition_with_profile_null_pdf_returns_null_pointer() {
        let mut out: *mut c_char = std::ptr::null_mut();
        let code =
            unsafe { oxidize_partition_with_profile(std::ptr::null(), 0, 0, &mut out) };
        assert_eq!(code, ErrorCode::NullPointer as c_int);
        assert!(out.is_null());
    }

    #[test]
    fn oxidize_partition_with_profile_empty_pdf_returns_parse_error() {
        let pdf: Vec<u8> = Vec::new();
        let mut out: *mut c_char = std::ptr::null_mut();
        // Use a non-null pointer with len=0 so the null-check passes.
        let dummy: u8 = 0;
        let code =
            unsafe { oxidize_partition_with_profile(&dummy, pdf.len(), 0, &mut out) };
        assert_eq!(code, ErrorCode::PdfParseError as c_int);
        assert!(out.is_null());
    }

    #[test]
    fn oxidize_partition_with_profile_accepts_all_valid_profiles() {
        let pdf = sample_pdf();
        for profile in 0u8..=6u8 {
            let mut out: *mut c_char = std::ptr::null_mut();
            let code = unsafe {
                oxidize_partition_with_profile(pdf.as_ptr(), pdf.len(), profile, &mut out)
            };
            assert_eq!(
                code,
                ErrorCode::Success as c_int,
                "profile {profile} should succeed"
            );
            assert!(!out.is_null(), "profile {profile} returned null output");
            unsafe {
                crate::oxidize_free_string(out);
            }
        }
    }

    /// Convert the FFI-emitted JSON into a `serde_json::Value` array so tests
    /// can inspect element_type / text / confidence semantically.
    fn json_to_array(out: *mut c_char) -> Vec<serde_json::Value> {
        let json = unsafe { CStr::from_ptr(out).to_string_lossy().into_owned() };
        unsafe {
            crate::oxidize_free_string(out);
        }
        serde_json::from_str::<Vec<serde_json::Value>>(&json).expect("FFI must emit a JSON array")
    }

    fn call_with_config(pdf: &[u8], cfg_json: &str) -> Vec<serde_json::Value> {
        let cfg_c = std::ffi::CString::new(cfg_json).unwrap();
        let mut out: *mut c_char = std::ptr::null_mut();
        let code = unsafe {
            oxidize_partition_with_config(pdf.as_ptr(), pdf.len(), cfg_c.as_ptr(), &mut out)
        };
        assert_eq!(code, ErrorCode::Success as c_int, "expected success");
        assert!(!out.is_null());
        json_to_array(out)
    }

    #[test]
    fn oxidize_partition_with_config_emits_well_formed_elements() {
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
        let elements = call_with_config(&pdf, cfg);
        assert!(
            !elements.is_empty(),
            "fixture should produce at least one element"
        );

        // Every element must carry the documented schema fields (no smoke test).
        for el in &elements {
            assert!(el.get("element_type").and_then(|v| v.as_str()).is_some());
            assert!(el.get("text").and_then(|v| v.as_str()).is_some());
            assert!(el
                .get("page_number")
                .and_then(|v| v.as_u64())
                .is_some_and(|n| n >= 1));
            assert!(el.get("confidence").and_then(|v| v.as_f64()).is_some());
        }

        // Fixture has no table content, and detect_tables is off — assert the
        // negation directly.
        let has_table = elements
            .iter()
            .any(|el| el.get("element_type").and_then(|v| v.as_str()) == Some("Table"));
        assert!(!has_table, "no Table element expected with detect_tables=false on table-free fixture");

        // Introduction text must be present somewhere.
        let has_intro = elements.iter().any(|el| {
            el.get("text")
                .and_then(|v| v.as_str())
                .is_some_and(|t| t.contains("Introduction"))
        });
        assert!(has_intro, "expected fixture title text in output");
    }

    #[test]
    fn oxidize_partition_with_config_accepts_xycut_reading_order() {
        // The XYCut variant carries a payload (`min_gap: f64`) — exercises a
        // different DTO branch than `"Simple"` / `"None"` and proves the FFI
        // honours the System.Text.Json shape `{"XYCut":{"min_gap":N}}` that
        // the C# converter emits.
        let pdf = sample_pdf();
        let cfg = r#"{
            "detect_tables": true,
            "detect_headers_footers": true,
            "title_min_font_ratio": 1.3,
            "header_zone": 0.05,
            "footer_zone": 0.05,
            "reading_order": {"XYCut":{"min_gap":15.0}},
            "min_table_confidence": 0.5
        }"#;
        let elements = call_with_config(&pdf, cfg);
        assert!(!elements.is_empty(), "XYCut should still produce elements");
        let has_intro = elements.iter().any(|el| {
            el.get("text")
                .and_then(|v| v.as_str())
                .is_some_and(|t| t.contains("Introduction"))
        });
        assert!(has_intro);
    }

    #[test]
    fn oxidize_partition_with_config_accepts_integer_min_gap() {
        // System.Text.Json emits whole-number doubles as integer tokens;
        // serde_json must accept them for f64 fields.
        let pdf = sample_pdf();
        let cfg = r#"{
            "detect_tables": true,
            "detect_headers_footers": true,
            "title_min_font_ratio": 1.3,
            "header_zone": 0.05,
            "footer_zone": 0.05,
            "reading_order": {"XYCut":{"min_gap":15}},
            "min_table_confidence": 0.5
        }"#;
        // No assertion on element content — the assertion is "no crash, success
        // code, parseable output" which `call_with_config` already enforces.
        let _ = call_with_config(&pdf, cfg);
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
        assert!(out.is_null());
    }

    #[test]
    fn oxidize_partition_with_config_rejects_missing_fields() {
        let pdf = sample_pdf();
        // Valid JSON but missing required fields — serde should reject.
        let cfg_c = std::ffi::CString::new(r#"{"detect_tables": true}"#).unwrap();
        let mut out: *mut c_char = std::ptr::null_mut();
        let code = unsafe {
            oxidize_partition_with_config(pdf.as_ptr(), pdf.len(), cfg_c.as_ptr(), &mut out)
        };
        assert_eq!(code, ErrorCode::InvalidArgument as c_int);
    }

    #[test]
    fn oxidize_partition_with_config_null_config_returns_null_pointer() {
        let pdf = sample_pdf();
        let mut out: *mut c_char = std::ptr::null_mut();
        let code = unsafe {
            oxidize_partition_with_config(pdf.as_ptr(), pdf.len(), std::ptr::null(), &mut out)
        };
        assert_eq!(code, ErrorCode::NullPointer as c_int);
        assert!(out.is_null());
    }

    // ─── oxidize_rag_chunks_with_profile (Task 8) ───────────────────────────

    fn call_rag_chunks_with_profile(pdf: &[u8], profile: u8) -> Vec<serde_json::Value> {
        let mut out: *mut c_char = std::ptr::null_mut();
        let code =
            unsafe { oxidize_rag_chunks_with_profile(pdf.as_ptr(), pdf.len(), profile, &mut out) };
        assert_eq!(code, ErrorCode::Success as c_int);
        assert!(!out.is_null());
        json_to_array(out)
    }

    #[test]
    fn oxidize_rag_chunks_with_profile_returns_well_formed_chunks() {
        let pdf = sample_pdf();
        let chunks = call_rag_chunks_with_profile(&pdf, 6 /* Rag */);
        assert!(!chunks.is_empty(), "fixture should produce at least one chunk");

        // Every chunk must carry the documented RagChunkResult schema (no smoke test).
        for (i, c) in chunks.iter().enumerate() {
            let idx = c
                .get("chunk_index")
                .and_then(|v| v.as_u64())
                .expect("chunk_index missing");
            assert_eq!(idx as usize, i, "chunk_index must be sequential 0..n");
            assert!(c.get("text").and_then(|v| v.as_str()).is_some());
            assert!(c.get("full_text").and_then(|v| v.as_str()).is_some());
            assert!(c.get("page_numbers").and_then(|v| v.as_array()).is_some());
            assert!(c.get("element_types").and_then(|v| v.as_array()).is_some());
            assert!(c.get("token_estimate").and_then(|v| v.as_u64()).is_some());
            assert!(c.get("is_oversized").and_then(|v| v.as_bool()).is_some());
            // heading_context may be null — just check key presence.
            assert!(c.get("heading_context").is_some());
        }

        // Page numbers must be 1-based (FFI converts the 0-based core values).
        for c in &chunks {
            for p in c.get("page_numbers").unwrap().as_array().unwrap() {
                assert!(
                    p.as_u64().unwrap() >= 1,
                    "page_numbers must be 1-based (FFI contract)"
                );
            }
        }

        // The fixture's title text must surface in at least one chunk's text.
        let has_intro = chunks.iter().any(|c| {
            c.get("text")
                .and_then(|v| v.as_str())
                .is_some_and(|t| t.contains("Introduction"))
        });
        assert!(has_intro);
    }

    #[test]
    fn oxidize_rag_chunks_with_profile_accepts_all_valid_profiles() {
        let pdf = sample_pdf();
        for profile in 0u8..=6u8 {
            let chunks = call_rag_chunks_with_profile(&pdf, profile);
            assert!(
                !chunks.is_empty(),
                "profile {profile} must produce chunks for the fixture"
            );
        }
    }

    #[test]
    fn oxidize_rag_chunks_with_profile_rejects_bad_discriminant() {
        let pdf = sample_pdf();
        let mut out: *mut c_char = std::ptr::null_mut();
        let code =
            unsafe { oxidize_rag_chunks_with_profile(pdf.as_ptr(), pdf.len(), 99, &mut out) };
        assert_eq!(code, ErrorCode::InvalidArgument as c_int);
        assert!(out.is_null());
    }

    #[test]
    fn oxidize_rag_chunks_with_profile_null_pdf_returns_null_pointer() {
        let mut out: *mut c_char = std::ptr::null_mut();
        let code =
            unsafe { oxidize_rag_chunks_with_profile(std::ptr::null(), 0, 0, &mut out) };
        assert_eq!(code, ErrorCode::NullPointer as c_int);
        assert!(out.is_null());
    }

    #[test]
    fn oxidize_rag_chunks_with_profile_empty_pdf_returns_parse_error() {
        let dummy: u8 = 0;
        let mut out: *mut c_char = std::ptr::null_mut();
        let code = unsafe { oxidize_rag_chunks_with_profile(&dummy, 0, 0, &mut out) };
        assert_eq!(code, ErrorCode::PdfParseError as c_int);
        assert!(out.is_null());
    }

    // ─── oxidize_rag_chunks_with_config (Task 9) ─────────────────────────────

    /// Calls `oxidize_rag_chunks_with_config` with the supplied optional configs
    /// (pass `None` to use the upstream default for that parameter), asserts a
    /// successful response, and returns the parsed JSON array.
    fn call_rag_chunks_with_config(
        pdf: &[u8],
        partition_cfg: Option<&str>,
        hybrid_cfg: Option<&str>,
    ) -> Vec<serde_json::Value> {
        let p_owned = partition_cfg.map(|s| std::ffi::CString::new(s).unwrap());
        let h_owned = hybrid_cfg.map(|s| std::ffi::CString::new(s).unwrap());
        let p_ptr = p_owned.as_ref().map_or(std::ptr::null(), |c| c.as_ptr());
        let h_ptr = h_owned.as_ref().map_or(std::ptr::null(), |c| c.as_ptr());
        let mut out: *mut c_char = std::ptr::null_mut();
        let code = unsafe {
            oxidize_rag_chunks_with_config(pdf.as_ptr(), pdf.len(), p_ptr, h_ptr, &mut out)
        };
        assert_eq!(code, ErrorCode::Success as c_int);
        assert!(!out.is_null());
        json_to_array(out)
    }

    #[test]
    fn oxidize_rag_chunks_with_config_both_null_uses_defaults() {
        let pdf = sample_pdf();
        let chunks = call_rag_chunks_with_config(&pdf, None, None);
        assert!(!chunks.is_empty());
        // Default chunk schema must be present.
        assert!(chunks[0].get("chunk_index").is_some());
        assert!(chunks[0].get("full_text").is_some());
    }

    #[test]
    fn oxidize_rag_chunks_with_config_only_hybrid_supplied() {
        // Default partition, custom hybrid — the common case for size tuning.
        let pdf = sample_pdf();
        let hybrid = r#"{
            "max_tokens": 16,
            "overlap_tokens": 0,
            "merge_adjacent": false,
            "propagate_headings": true,
            "merge_policy": "SameTypeOnly"
        }"#;
        let chunks = call_rag_chunks_with_config(&pdf, None, Some(hybrid));
        assert!(!chunks.is_empty());
        for chunk in &chunks {
            assert!(chunk.get("chunk_index").is_some());
            assert!(chunk.get("token_estimate").and_then(|v| v.as_u64()).is_some());
        }
    }

    #[test]
    fn oxidize_rag_chunks_with_config_only_partition_supplied() {
        // Custom partition, default hybrid.
        let pdf = sample_pdf();
        let partition = r#"{
            "detect_tables": false,
            "detect_headers_footers": false,
            "title_min_font_ratio": 1.3,
            "header_zone": 0.05,
            "footer_zone": 0.05,
            "reading_order": "Simple",
            "min_table_confidence": 0.5
        }"#;
        let chunks = call_rag_chunks_with_config(&pdf, Some(partition), None);
        assert!(!chunks.is_empty());
        // 1-based page numbers contract.
        for c in &chunks {
            for p in c.get("page_numbers").unwrap().as_array().unwrap() {
                assert!(p.as_u64().unwrap() >= 1);
            }
        }
    }

    #[test]
    fn oxidize_rag_chunks_with_config_both_supplied() {
        let pdf = sample_pdf();
        let partition = r#"{
            "detect_tables": true,
            "detect_headers_footers": true,
            "title_min_font_ratio": 1.3,
            "header_zone": 0.05,
            "footer_zone": 0.05,
            "reading_order": {"XYCut":{"min_gap":15.0}},
            "min_table_confidence": 0.5
        }"#;
        let hybrid = r#"{
            "max_tokens": 64,
            "overlap_tokens": 0,
            "merge_adjacent": true,
            "propagate_headings": true,
            "merge_policy": "AnyInlineContent"
        }"#;
        let chunks = call_rag_chunks_with_config(&pdf, Some(partition), Some(hybrid));
        assert!(!chunks.is_empty());
    }

    #[test]
    fn oxidize_rag_chunks_with_config_rejects_bad_partition_json() {
        let pdf = sample_pdf();
        let bad = std::ffi::CString::new("{not json").unwrap();
        let mut out: *mut c_char = std::ptr::null_mut();
        let code = unsafe {
            oxidize_rag_chunks_with_config(
                pdf.as_ptr(),
                pdf.len(),
                bad.as_ptr(),
                std::ptr::null(),
                &mut out,
            )
        };
        assert_eq!(code, ErrorCode::InvalidArgument as c_int);
        assert!(out.is_null());
    }

    #[test]
    fn oxidize_rag_chunks_with_config_rejects_bad_hybrid_json() {
        let pdf = sample_pdf();
        let bad = std::ffi::CString::new("{not json").unwrap();
        let mut out: *mut c_char = std::ptr::null_mut();
        let code = unsafe {
            oxidize_rag_chunks_with_config(
                pdf.as_ptr(),
                pdf.len(),
                std::ptr::null(),
                bad.as_ptr(),
                &mut out,
            )
        };
        assert_eq!(code, ErrorCode::InvalidArgument as c_int);
        assert!(out.is_null());
    }

    #[test]
    fn oxidize_rag_chunks_with_config_null_pdf_returns_null_pointer() {
        let mut out: *mut c_char = std::ptr::null_mut();
        let code = unsafe {
            oxidize_rag_chunks_with_config(
                std::ptr::null(),
                0,
                std::ptr::null(),
                std::ptr::null(),
                &mut out,
            )
        };
        assert_eq!(code, ErrorCode::NullPointer as c_int);
        assert!(out.is_null());
    }

    #[test]
    fn oxidize_rag_chunks_with_config_empty_pdf_returns_parse_error() {
        let dummy: u8 = 0;
        let mut out: *mut c_char = std::ptr::null_mut();
        let code = unsafe {
            oxidize_rag_chunks_with_config(
                &dummy,
                0,
                std::ptr::null(),
                std::ptr::null(),
                &mut out,
            )
        };
        assert_eq!(code, ErrorCode::PdfParseError as c_int);
        assert!(out.is_null());
    }

    // ─── oxidize_semantic_chunks (Task 10) ────────────────────────────────────

    fn call_semantic_chunks(
        pdf: &[u8],
        partition_cfg: Option<&str>,
        semantic_cfg: &str,
    ) -> Vec<serde_json::Value> {
        let p_owned = partition_cfg.map(|s| std::ffi::CString::new(s).unwrap());
        let p_ptr = p_owned.as_ref().map_or(std::ptr::null(), |c| c.as_ptr());
        let s_owned = std::ffi::CString::new(semantic_cfg).unwrap();
        let mut out: *mut c_char = std::ptr::null_mut();
        let code = unsafe {
            oxidize_semantic_chunks(pdf.as_ptr(), pdf.len(), p_ptr, s_owned.as_ptr(), &mut out)
        };
        assert_eq!(code, ErrorCode::Success as c_int);
        assert!(!out.is_null());
        json_to_array(out)
    }

    #[test]
    fn oxidize_semantic_chunks_returns_well_formed_chunks() {
        let pdf = sample_pdf();
        let sem = r#"{"max_tokens":64,"overlap_tokens":10,"respect_element_boundaries":true}"#;
        let chunks = call_semantic_chunks(&pdf, None, sem);
        assert!(!chunks.is_empty());

        // Schema validation — every documented field present, correctly typed,
        // sequential chunk_index, 1-based page_numbers.
        for (i, c) in chunks.iter().enumerate() {
            assert_eq!(
                c.get("chunk_index").and_then(|v| v.as_u64()).unwrap() as usize,
                i,
                "chunk_index must be sequential 0..n"
            );
            assert!(c.get("text").and_then(|v| v.as_str()).is_some());
            assert!(c.get("token_estimate").and_then(|v| v.as_u64()).is_some());
            assert!(c.get("is_oversized").and_then(|v| v.as_bool()).is_some());
            for p in c.get("page_numbers").unwrap().as_array().unwrap() {
                assert!(p.as_u64().unwrap() >= 1, "page_numbers must be 1-based");
            }
        }

        // Fixture's title text should appear in some chunk.
        let has_intro = chunks.iter().any(|c| {
            c.get("text")
                .and_then(|v| v.as_str())
                .is_some_and(|t| t.contains("Introduction"))
        });
        assert!(has_intro);
    }

    #[test]
    fn oxidize_semantic_chunks_accepts_custom_partition_config() {
        let pdf = sample_pdf();
        let partition = r#"{
            "detect_tables": false,
            "detect_headers_footers": false,
            "title_min_font_ratio": 1.3,
            "header_zone": 0.05,
            "footer_zone": 0.05,
            "reading_order": "Simple",
            "min_table_confidence": 0.5
        }"#;
        let sem = r#"{"max_tokens":128,"overlap_tokens":0,"respect_element_boundaries":true}"#;
        let chunks = call_semantic_chunks(&pdf, Some(partition), sem);
        assert!(!chunks.is_empty());
    }

    #[test]
    fn oxidize_semantic_chunks_rejects_null_semantic_config() {
        // Unlike the partition config (which is optional), the semantic
        // config is REQUIRED — there's no default we want to silently apply.
        let pdf = sample_pdf();
        let mut out: *mut c_char = std::ptr::null_mut();
        let code = unsafe {
            oxidize_semantic_chunks(
                pdf.as_ptr(),
                pdf.len(),
                std::ptr::null(),
                std::ptr::null(),
                &mut out,
            )
        };
        assert_eq!(code, ErrorCode::NullPointer as c_int);
        assert!(out.is_null());
    }

    #[test]
    fn oxidize_semantic_chunks_rejects_bad_semantic_json() {
        let pdf = sample_pdf();
        let bad = std::ffi::CString::new("{not json").unwrap();
        let mut out: *mut c_char = std::ptr::null_mut();
        let code = unsafe {
            oxidize_semantic_chunks(
                pdf.as_ptr(),
                pdf.len(),
                std::ptr::null(),
                bad.as_ptr(),
                &mut out,
            )
        };
        assert_eq!(code, ErrorCode::InvalidArgument as c_int);
        assert!(out.is_null());
    }

    #[test]
    fn oxidize_semantic_chunks_rejects_bad_partition_json() {
        let pdf = sample_pdf();
        let bad_partition = std::ffi::CString::new("{not json").unwrap();
        let sem = std::ffi::CString::new(
            r#"{"max_tokens":64,"overlap_tokens":10,"respect_element_boundaries":true}"#,
        )
        .unwrap();
        let mut out: *mut c_char = std::ptr::null_mut();
        let code = unsafe {
            oxidize_semantic_chunks(
                pdf.as_ptr(),
                pdf.len(),
                bad_partition.as_ptr(),
                sem.as_ptr(),
                &mut out,
            )
        };
        assert_eq!(code, ErrorCode::InvalidArgument as c_int);
        assert!(out.is_null());
    }

    #[test]
    fn oxidize_semantic_chunks_null_pdf_returns_null_pointer() {
        let sem = std::ffi::CString::new(
            r#"{"max_tokens":64,"overlap_tokens":10,"respect_element_boundaries":true}"#,
        )
        .unwrap();
        let mut out: *mut c_char = std::ptr::null_mut();
        let code = unsafe {
            oxidize_semantic_chunks(
                std::ptr::null(),
                0,
                std::ptr::null(),
                sem.as_ptr(),
                &mut out,
            )
        };
        assert_eq!(code, ErrorCode::NullPointer as c_int);
        assert!(out.is_null());
    }

    #[test]
    fn oxidize_semantic_chunks_empty_pdf_returns_parse_error() {
        let dummy: u8 = 0;
        let sem = std::ffi::CString::new(
            r#"{"max_tokens":64,"overlap_tokens":10,"respect_element_boundaries":true}"#,
        )
        .unwrap();
        let mut out: *mut c_char = std::ptr::null_mut();
        let code = unsafe {
            oxidize_semantic_chunks(&dummy, 0, std::ptr::null(), sem.as_ptr(), &mut out)
        };
        assert_eq!(code, ErrorCode::PdfParseError as c_int);
        assert!(out.is_null());
    }

    // ─── oxidize_to_markdown_with_options (Task 10b, RAG-012) ────────────────

    fn call_to_markdown(pdf: &[u8], opts_json: &str) -> String {
        let opts_c = std::ffi::CString::new(opts_json).unwrap();
        let mut out: *mut c_char = std::ptr::null_mut();
        let code = unsafe {
            oxidize_to_markdown_with_options(pdf.as_ptr(), pdf.len(), opts_c.as_ptr(), &mut out)
        };
        assert_eq!(code, ErrorCode::Success as c_int, "expected Success");
        assert!(!out.is_null());
        let md = unsafe { CStr::from_ptr(out).to_string_lossy().into_owned() };
        unsafe {
            crate::oxidize_free_string(out);
        }
        assert!(!md.is_empty(), "markdown output must not be empty");
        md
    }

    #[test]
    fn oxidize_to_markdown_with_options_metadata_true_emits_yaml_frontmatter() {
        let pdf = sample_pdf();
        let md = call_to_markdown(
            &pdf,
            r#"{"include_metadata":true,"include_page_numbers":false}"#,
        );
        // YAML frontmatter contract from MarkdownExporter::export_with_metadata.
        assert!(md.starts_with("---\n"), "metadata=true must start with YAML frontmatter");
        assert!(md.contains("title:"));
        assert!(md.contains("pages:"));
        assert!(md.contains("---\n\n# "), "must close frontmatter and emit title heading");
        // No per-page markers when include_page_numbers=false.
        assert!(!md.contains("**Page "), "page markers must NOT appear when include_page_numbers=false");
    }

    #[test]
    fn oxidize_to_markdown_with_options_metadata_false_no_yaml() {
        let pdf = sample_pdf();
        let md = call_to_markdown(
            &pdf,
            r#"{"include_metadata":false,"include_page_numbers":false}"#,
        );
        assert!(!md.starts_with("---\n"), "metadata=false must NOT emit YAML frontmatter");
        assert!(!md.contains("title:"));
        assert!(!md.contains("**Page "));
    }

    #[test]
    fn oxidize_to_markdown_with_options_pages_only_emits_page_markers_no_yaml() {
        let pdf = sample_pdf();
        let md = call_to_markdown(
            &pdf,
            r#"{"include_metadata":false,"include_page_numbers":true}"#,
        );
        assert!(!md.starts_with("---\n"), "no YAML when metadata=false");
        assert!(md.contains("**Page 1**"), "must emit per-page marker for page 1");
    }

    #[test]
    fn oxidize_to_markdown_with_options_both_true_emits_yaml_and_page_markers() {
        let pdf = sample_pdf();
        let md = call_to_markdown(
            &pdf,
            r#"{"include_metadata":true,"include_page_numbers":true}"#,
        );
        assert!(md.starts_with("---\n"), "YAML frontmatter when metadata=true");
        assert!(md.contains("title:"));
        assert!(md.contains("**Page 1**"), "page marker when include_page_numbers=true");
    }

    #[test]
    fn oxidize_to_markdown_with_options_distinct_flag_combinations_produce_distinct_output() {
        // The four flag combinations must produce four distinct outputs.
        let pdf = sample_pdf();
        let combos = [
            (false, false),
            (true, false),
            (false, true),
            (true, true),
        ];
        let outputs: Vec<String> = combos
            .iter()
            .map(|(meta, pages)| {
                call_to_markdown(
                    &pdf,
                    &format!(
                        r#"{{"include_metadata":{meta},"include_page_numbers":{pages}}}"#
                    ),
                )
            })
            .collect();
        for i in 0..outputs.len() {
            for j in (i + 1)..outputs.len() {
                assert_ne!(
                    outputs[i], outputs[j],
                    "combos {:?} and {:?} produced identical markdown — options ignored",
                    combos[i], combos[j]
                );
            }
        }
    }

    #[test]
    fn oxidize_to_markdown_with_options_rejects_bad_json() {
        let pdf = sample_pdf();
        let bad = std::ffi::CString::new("{not json").unwrap();
        let mut out: *mut c_char = std::ptr::null_mut();
        let code = unsafe {
            oxidize_to_markdown_with_options(pdf.as_ptr(), pdf.len(), bad.as_ptr(), &mut out)
        };
        assert_eq!(code, ErrorCode::InvalidArgument as c_int);
        assert!(out.is_null());
    }

    #[test]
    fn oxidize_to_markdown_with_options_null_options_returns_null_pointer() {
        let pdf = sample_pdf();
        let mut out: *mut c_char = std::ptr::null_mut();
        let code = unsafe {
            oxidize_to_markdown_with_options(pdf.as_ptr(), pdf.len(), std::ptr::null(), &mut out)
        };
        assert_eq!(code, ErrorCode::NullPointer as c_int);
        assert!(out.is_null());
    }

    #[test]
    fn oxidize_to_markdown_with_options_empty_pdf_returns_parse_error() {
        let dummy: u8 = 0;
        let opts = std::ffi::CString::new(r#"{"include_metadata":true,"include_page_numbers":true}"#).unwrap();
        let mut out: *mut c_char = std::ptr::null_mut();
        let code = unsafe {
            oxidize_to_markdown_with_options(&dummy, 0, opts.as_ptr(), &mut out)
        };
        assert_eq!(code, ErrorCode::PdfParseError as c_int);
        assert!(out.is_null());
    }

    // ─── oxidize_estimate_tokens / oxidize_chunk_text (Task 10c) ───────────
    // RAG-008 (chunk standalone text) + RAG-009 (token estimator).

    #[test]
    fn oxidize_estimate_tokens_uses_word_count_x_1_33() {
        // Upstream `DocumentChunker::estimate_tokens` returns
        // `(words * 1.33) as usize`. Lock the contract here so a silent
        // upstream change to the formula breaks this test rather than
        // silently shifting downstream chunk-count assumptions.
        let cases = [
            ("hello world from oxidize", 4 /*words*/, 5 /*tokens, floor(4*1.33)=5*/),
            ("one", 1, 1 /*floor(1.33)=1*/),
            ("a b c d e f g h", 8, 10 /*floor(10.64)=10*/),
            ("", 0, 0),
        ];
        for (text, words, expected) in cases {
            let cs = std::ffi::CString::new(text).unwrap();
            let mut out: usize = usize::MAX;
            let code = unsafe { oxidize_estimate_tokens(cs.as_ptr(), &mut out) };
            assert_eq!(code, ErrorCode::Success as c_int);
            assert_eq!(
                out, expected,
                "input={:?} words={} expected={} got={}",
                text, words, expected, out
            );
        }
    }

    #[test]
    fn oxidize_estimate_tokens_null_text_returns_null_pointer() {
        let mut out: usize = 0;
        let code = unsafe { oxidize_estimate_tokens(std::ptr::null(), &mut out) };
        assert_eq!(code, ErrorCode::NullPointer as c_int);
    }

    #[test]
    fn oxidize_estimate_tokens_null_out_returns_null_pointer() {
        let cs = std::ffi::CString::new("text").unwrap();
        let code = unsafe { oxidize_estimate_tokens(cs.as_ptr(), std::ptr::null_mut()) };
        assert_eq!(code, ErrorCode::NullPointer as c_int);
    }

    fn call_chunk_text(text: &str, chunk_size: usize, overlap: usize) -> Vec<serde_json::Value> {
        let cs = std::ffi::CString::new(text).unwrap();
        let mut out: *mut c_char = std::ptr::null_mut();
        let code =
            unsafe { oxidize_chunk_text(cs.as_ptr(), chunk_size, overlap, &mut out) };
        assert_eq!(code, ErrorCode::Success as c_int);
        assert!(!out.is_null());
        json_to_array(out)
    }

    #[test]
    fn oxidize_chunk_text_splits_long_input_with_expected_schema() {
        let long = "word ".repeat(200);
        let chunks = call_chunk_text(&long, 50, 5);

        // chunk_size=50, overlap=5 → step=45 → 200 tokens / 45 ≈ 5 chunks.
        assert!(chunks.len() >= 2, "expected multiple chunks for 200-word/size=50 input");

        // Schema validation: every documented field present, correctly typed.
        for (i, c) in chunks.iter().enumerate() {
            let id = c.get("id").and_then(|v| v.as_str()).expect("id missing");
            assert!(id.starts_with("chunk_"), "id must follow `chunk_N` format, got {id}");
            assert!(c.get("content").and_then(|v| v.as_str()).is_some());
            assert!(c.get("tokens").and_then(|v| v.as_u64()).is_some());
            assert!(c.get("page_numbers").and_then(|v| v.as_array()).is_some());
            assert_eq!(
                c.get("chunk_index").and_then(|v| v.as_u64()).unwrap() as usize,
                i,
                "chunk_index must be sequential 0..n"
            );
        }
    }

    #[test]
    fn oxidize_chunk_text_overlap_produces_shared_words_between_consecutive_chunks() {
        // With chunk_size=10 and overlap=3 over 30 distinct numbered words,
        // chunks 0 and 1 must share at least one word — the overlap parameter
        // must affect content, not just chunk boundaries.
        let words: Vec<String> = (0..30).map(|i| format!("w{i}")).collect();
        let text = words.join(" ");
        let chunks = call_chunk_text(&text, 10, 3);
        assert!(chunks.len() >= 2);

        let c0 = chunks[0].get("content").and_then(|v| v.as_str()).unwrap();
        let c1 = chunks[1].get("content").and_then(|v| v.as_str()).unwrap();
        let c0_words: std::collections::HashSet<&str> = c0.split_whitespace().collect();
        let c1_words: std::collections::HashSet<&str> = c1.split_whitespace().collect();
        let shared: Vec<&&str> = c0_words.intersection(&c1_words).collect();
        assert!(
            !shared.is_empty(),
            "consecutive chunks must share words when overlap > 0; \
             c0={:?}, c1={:?}",
            c0_words,
            c1_words
        );
    }

    #[test]
    fn oxidize_chunk_text_short_input_single_chunk() {
        // 5-word input with chunk_size=100 must produce exactly one chunk.
        let chunks = call_chunk_text("one two three four five", 100, 10);
        assert_eq!(chunks.len(), 1, "short input must produce exactly one chunk");
        assert_eq!(chunks[0].get("chunk_index").unwrap(), 0);
    }

    #[test]
    fn oxidize_chunk_text_rejects_overlap_ge_size() {
        let cs = std::ffi::CString::new("some text").unwrap();
        let mut out: *mut c_char = std::ptr::null_mut();
        let code = unsafe { oxidize_chunk_text(cs.as_ptr(), 10, 10, &mut out) };
        assert_eq!(code, ErrorCode::InvalidArgument as c_int);
        assert!(out.is_null());

        // overlap > size is also invalid.
        let mut out2: *mut c_char = std::ptr::null_mut();
        let code2 = unsafe { oxidize_chunk_text(cs.as_ptr(), 5, 10, &mut out2) };
        assert_eq!(code2, ErrorCode::InvalidArgument as c_int);
        assert!(out2.is_null());
    }

    #[test]
    fn oxidize_chunk_text_rejects_zero_chunk_size() {
        let cs = std::ffi::CString::new("some text").unwrap();
        let mut out: *mut c_char = std::ptr::null_mut();
        let code = unsafe { oxidize_chunk_text(cs.as_ptr(), 0, 0, &mut out) };
        assert_eq!(code, ErrorCode::InvalidArgument as c_int);
        assert!(out.is_null());
    }

    #[test]
    fn oxidize_chunk_text_null_text_returns_null_pointer() {
        let mut out: *mut c_char = std::ptr::null_mut();
        let code = unsafe { oxidize_chunk_text(std::ptr::null(), 10, 0, &mut out) };
        assert_eq!(code, ErrorCode::NullPointer as c_int);
        assert!(out.is_null());
    }

    #[test]
    fn oxidize_chunk_text_null_out_returns_null_pointer() {
        let cs = std::ffi::CString::new("text").unwrap();
        let code = unsafe { oxidize_chunk_text(cs.as_ptr(), 10, 0, std::ptr::null_mut()) };
        assert_eq!(code, ErrorCode::NullPointer as c_int);
    }
}

#[cfg(test)]
mod form_tests {
    #[test]
    fn negative_ff_does_not_activate_flags() {
        let raw: i64 = -1;
        let ff_fixed = raw.max(0) as u32;
        assert_eq!(ff_fixed, 0, "negative Ff must clamp to 0");
        assert_eq!(ff_fixed & 1, 0, "read-only must not activate");
        assert_eq!((ff_fixed >> 1) & 1, 0, "required must not activate");
    }

    #[test]
    fn page_index_saturating_add_does_not_overflow() {
        let max_page: u32 = u32::MAX;
        assert_eq!(max_page.saturating_add(1), u32::MAX);
    }
}
