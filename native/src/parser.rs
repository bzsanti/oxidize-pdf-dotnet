use base64::Engine as _;
use oxidize_pdf::parser::{ParseOptions, PdfDocument, PdfReader};
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
                page_number: page_index + 1, // 0-based to 1-based
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
