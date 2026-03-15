use oxidize_pdf::parser::{PdfDocument, PdfReader};
use serde::{Deserialize, Serialize};
use std::ffi::{CStr, CString};
use std::io::Cursor;
use std::os::raw::{c_char, c_int};
use std::ptr;
use std::slice;

use crate::{clear_last_error, find_char_boundary, set_last_error, ErrorCode};

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
    let cursor = Cursor::new(bytes);
    let reader = match PdfReader::new(cursor) {
        Ok(r) => r,
        Err(e) => {
            set_last_error(format!("Failed to parse PDF: {e}"));
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
    let cursor = Cursor::new(bytes);
    let reader = match PdfReader::new(cursor) {
        Ok(r) => r,
        Err(e) => {
            set_last_error(format!("Failed to parse PDF: {e}"));
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
    let cursor = Cursor::new(bytes);

    let reader = match PdfReader::new(cursor) {
        Ok(r) => r,
        Err(e) => {
            set_last_error(format!("Failed to parse PDF: {e}"));
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

    *out_count = text_pages.len();
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
    let cursor = Cursor::new(bytes);

    let reader = match PdfReader::new(cursor) {
        Ok(r) => r,
        Err(e) => {
            set_last_error(format!("Failed to parse PDF: {e}"));
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
    let cursor = Cursor::new(bytes);

    let reader = match PdfReader::new(cursor) {
        Ok(r) => r,
        Err(e) => {
            set_last_error(format!("Failed to parse PDF: {e}"));
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
    let cursor = Cursor::new(bytes);
    let reader = match PdfReader::new(cursor) {
        Ok(r) => r,
        Err(e) => {
            set_last_error(format!("Failed to parse PDF: {e}"));
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
    let cursor = Cursor::new(bytes);
    let mut reader = match PdfReader::new(cursor) {
        Ok(r) => r,
        Err(e) => {
            set_last_error(format!("Failed to parse PDF: {e}"));
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
    let cursor = Cursor::new(bytes);
    let reader = match PdfReader::new(cursor) {
        Ok(r) => r,
        Err(e) => {
            set_last_error(format!("Failed to parse PDF: {e}"));
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
    let cursor = Cursor::new(bytes);
    let reader = match PdfReader::new(cursor) {
        Ok(r) => r,
        Err(e) => {
            set_last_error(format!("Failed to parse PDF: {e}"));
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
