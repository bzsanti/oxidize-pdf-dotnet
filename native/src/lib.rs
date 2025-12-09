use oxidize_pdf::parser::{PdfDocument, PdfReader};
use serde::{Deserialize, Serialize};
use std::cell::RefCell;
use std::ffi::CString;
use std::io::Cursor;
use std::os::raw::{c_char, c_int};
use std::ptr;
use std::slice;

// Thread-local storage for last error message
thread_local! {
    static LAST_ERROR: RefCell<Option<String>> = const { RefCell::new(None) };
}

/// Set the last error message (internal use)
fn set_last_error<S: Into<String>>(msg: S) {
    LAST_ERROR.with(|e| *e.borrow_mut() = Some(msg.into()));
}

/// Clear the last error message (internal use)
fn clear_last_error() {
    LAST_ERROR.with(|e| *e.borrow_mut() = None);
}

/// Find the nearest valid char boundary at or after the given byte index.
/// This prevents panics when slicing UTF-8 strings at arbitrary byte positions.
fn find_char_boundary(s: &str, mut index: usize) -> usize {
    if index >= s.len() {
        return s.len();
    }

    // Move forward until we find a valid char boundary
    while index < s.len() && !s.is_char_boundary(index) {
        index += 1;
    }

    index
}

/// Error codes for C# interop
#[repr(C)]
pub enum ErrorCode {
    Success = 0,
    NullPointer = 1,
    InvalidUtf8 = 2,
    PdfParseError = 3,
    AllocationError = 4,
    SerializationError = 5,
}

/// Document chunk for RAG/LLM pipelines
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

/// Chunk options from C#
#[repr(C)]
#[derive(Copy, Clone)]
pub struct ChunkOptions {
    pub max_chunk_size: usize,
    pub overlap: usize,
    pub preserve_sentence_boundaries: bool,
    pub include_metadata: bool,
}

/// Free a C string allocated by Rust
///
/// # Safety
/// - `ptr` must have been returned by a previous call to an oxidize_* function
/// - `ptr` must not have been freed previously
/// - After calling this function, `ptr` must not be used again
#[no_mangle]
pub unsafe extern "C" fn oxidize_free_string(ptr: *mut c_char) {
    if ptr.is_null() {
        return;
    }
    let _ = CString::from_raw(ptr);
}

/// Get the last error message
///
/// # Safety
/// - `out_error` must be a valid pointer to a mutable pointer location
/// - The returned string must be freed with `oxidize_free_string`
/// - Returns ErrorCode::Success if there was an error message, or if no error was set
#[no_mangle]
pub unsafe extern "C" fn oxidize_get_last_error(out_error: *mut *mut c_char) -> c_int {
    if out_error.is_null() {
        return ErrorCode::NullPointer as c_int;
    }

    // Initialize output to null
    *out_error = ptr::null_mut();

    let error_msg = LAST_ERROR.with(|e| e.borrow().clone());

    match error_msg {
        Some(msg) if !msg.is_empty() => match CString::new(msg) {
            Ok(c_string) => {
                *out_error = c_string.into_raw();
                ErrorCode::Success as c_int
            }
            Err(_) => ErrorCode::InvalidUtf8 as c_int,
        },
        _ => ErrorCode::Success as c_int, // No error to report
    }
}

/// Extract plain text from PDF bytes
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes
/// - `out_text` will be allocated by this function and must be freed with `oxidize_free_string`
/// - Returns ErrorCode::Success on success
#[no_mangle]
pub unsafe extern "C" fn oxidize_extract_text(
    pdf_bytes: *const u8,
    pdf_len: usize,
    out_text: *mut *mut c_char,
) -> c_int {
    // Clear any previous error
    clear_last_error();

    // Validate inputs
    if pdf_bytes.is_null() || out_text.is_null() {
        set_last_error("Null pointer provided to oxidize_extract_text");
        return ErrorCode::NullPointer as c_int;
    }

    // CRITICAL: Initialize output to null on entry (safety)
    *out_text = ptr::null_mut();

    // Validate PDF length
    if pdf_len == 0 {
        set_last_error("PDF data is empty (0 bytes)");
        return ErrorCode::PdfParseError as c_int;
    }

    // Convert to Rust slice
    let bytes = slice::from_raw_parts(pdf_bytes, pdf_len);

    // Parse PDF
    let cursor = Cursor::new(bytes);
    let reader = match PdfReader::new(cursor) {
        Ok(r) => r,
        Err(e) => {
            set_last_error(format!("Failed to parse PDF: {}", e));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let document = PdfDocument::new(reader);

    // Extract text
    let text_pages = match document.extract_text() {
        Ok(pages) => pages,
        Err(e) => {
            set_last_error(format!("Failed to extract text from PDF: {}", e));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    // Combine all pages into single string
    let text = text_pages
        .iter()
        .map(|p| p.text.as_str())
        .collect::<Vec<_>>()
        .join("\n\n");

    // Convert to C string
    let c_string = match CString::new(text) {
        Ok(s) => s,
        Err(e) => {
            set_last_error(format!("Text contains invalid UTF-8: {}", e));
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    // Transfer ownership to caller
    *out_text = c_string.into_raw();

    ErrorCode::Success as c_int
}

/// Extract text chunks optimized for RAG/LLM pipelines
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes
/// - `options` can be null (will use defaults)
/// - `out_json` will contain JSON array of DocumentChunk, must be freed with `oxidize_free_string`
/// - Returns ErrorCode::Success on success
#[no_mangle]
pub unsafe extern "C" fn oxidize_extract_chunks(
    pdf_bytes: *const u8,
    pdf_len: usize,
    options: *const ChunkOptions,
    out_json: *mut *mut c_char,
) -> c_int {
    // Clear any previous error
    clear_last_error();

    // Validate inputs
    if pdf_bytes.is_null() || out_json.is_null() {
        set_last_error("Null pointer provided to oxidize_extract_chunks");
        return ErrorCode::NullPointer as c_int;
    }

    // CRITICAL: Initialize output to null on entry (safety)
    *out_json = ptr::null_mut();

    // Validate PDF length
    if pdf_len == 0 {
        set_last_error("PDF data is empty (0 bytes)");
        return ErrorCode::PdfParseError as c_int;
    }

    // Parse options or use defaults
    let chunk_opts = if options.is_null() {
        ChunkOptions {
            max_chunk_size: 512,
            overlap: 50,
            preserve_sentence_boundaries: true,
            include_metadata: true,
        }
    } else {
        *options
    };

    // Convert to Rust slice
    let bytes = slice::from_raw_parts(pdf_bytes, pdf_len);

    // Parse PDF
    let cursor = Cursor::new(bytes);
    let reader = match PdfReader::new(cursor) {
        Ok(r) => r,
        Err(e) => {
            set_last_error(format!("Failed to parse PDF: {}", e));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let document = PdfDocument::new(reader);

    // Extract text from all pages
    let text_pages = match document.extract_text() {
        Ok(pages) => pages,
        Err(e) => {
            set_last_error(format!("Failed to extract text from PDF: {}", e));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    // Create chunks from pages
    let mut chunks = Vec::new();
    let mut chunk_index = 0;

    for (page_num, page_text) in text_pages.iter().enumerate() {
        let page_content = &page_text.text;

        // Simple chunking: split by max_chunk_size with overlap
        let mut byte_start = 0;

        while byte_start < page_content.len() {
            // Ensure we're at a valid char boundary
            let start = find_char_boundary(page_content, byte_start);
            if start >= page_content.len() {
                break;
            }

            let raw_end = (start + chunk_opts.max_chunk_size).min(page_content.len());
            let end = find_char_boundary(page_content, raw_end);

            // Try to find sentence boundary near end
            let chunk_end = if chunk_opts.preserve_sentence_boundaries && end < page_content.len() {
                // Look for sentence boundary (. ! ?) within last 20% of chunk
                let raw_search_start =
                    start + (chunk_opts.max_chunk_size * 4 / 5).min(end.saturating_sub(start));
                let search_start = find_char_boundary(page_content, raw_search_start);

                if search_start < end {
                    page_content[search_start..end]
                        .rfind(&['.', '!', '?'][..])
                        .map(|i| {
                            // Ensure the result is at a char boundary
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
                    index: chunk_index,
                    page_number: page_num + 1,
                    text: chunk_text,
                    confidence: 1.0,
                    x: 0.0,
                    y: 0.0,
                    width: 0.0,
                    height: 0.0,
                });
                chunk_index += 1;
            }

            // Move to next chunk with overlap
            let next_start = chunk_end.saturating_sub(chunk_opts.overlap);
            // Break if no progress (prevents infinite loop)
            if next_start <= byte_start || chunk_end >= page_content.len() {
                break;
            }
            byte_start = next_start;
        }
    }

    // Serialize to JSON
    let json = match serde_json::to_string(&chunks) {
        Ok(j) => j,
        Err(e) => {
            set_last_error(format!("Failed to serialize chunks to JSON: {}", e));
            return ErrorCode::SerializationError as c_int;
        }
    };

    // Convert to C string
    let c_string = match CString::new(json) {
        Ok(s) => s,
        Err(e) => {
            set_last_error(format!("JSON contains invalid UTF-8: {}", e));
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    // Transfer ownership to caller
    *out_json = c_string.into_raw();

    ErrorCode::Success as c_int
}

/// Get the number of pages in a PDF
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes
/// - `out_count` must be a valid pointer to store the page count
/// - Returns ErrorCode::Success on success
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
            set_last_error(format!("Failed to parse PDF: {}", e));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let document = PdfDocument::new(reader);
    let text_pages = match document.extract_text() {
        Ok(pages) => pages,
        Err(e) => {
            set_last_error(format!("Failed to extract text from PDF: {}", e));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    *out_count = text_pages.len();
    ErrorCode::Success as c_int
}

/// Extract plain text from a specific page of a PDF
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes
/// - `page_number` is 1-based (first page = 1)
/// - `out_text` will be allocated by this function and must be freed with `oxidize_free_string`
/// - Returns ErrorCode::Success on success
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
            set_last_error(format!("Failed to parse PDF: {}", e));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let document = PdfDocument::new(reader);
    let text_pages = match document.extract_text() {
        Ok(pages) => pages,
        Err(e) => {
            set_last_error(format!("Failed to extract text from PDF: {}", e));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let page_index = page_number - 1;
    if page_index >= text_pages.len() {
        set_last_error(format!(
            "Page number {} is out of range (PDF has {} pages)",
            page_number,
            text_pages.len()
        ));
        return ErrorCode::PdfParseError as c_int;
    }

    let text = &text_pages[page_index].text;

    let c_string = match CString::new(text.as_str()) {
        Ok(s) => s,
        Err(e) => {
            set_last_error(format!("Text contains invalid UTF-8: {}", e));
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    *out_text = c_string.into_raw();
    ErrorCode::Success as c_int
}

/// Extract text chunks from a specific page of a PDF
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes
/// - `page_number` is 1-based (first page = 1)
/// - `options` can be null (will use defaults)
/// - `out_json` will contain JSON array of DocumentChunk, must be freed with `oxidize_free_string`
/// - Returns ErrorCode::Success on success
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
        ChunkOptions {
            max_chunk_size: 512,
            overlap: 50,
            preserve_sentence_boundaries: true,
            include_metadata: true,
        }
    } else {
        *options
    };

    let bytes = slice::from_raw_parts(pdf_bytes, pdf_len);
    let cursor = Cursor::new(bytes);

    let reader = match PdfReader::new(cursor) {
        Ok(r) => r,
        Err(e) => {
            set_last_error(format!("Failed to parse PDF: {}", e));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let document = PdfDocument::new(reader);
    let text_pages = match document.extract_text() {
        Ok(pages) => pages,
        Err(e) => {
            set_last_error(format!("Failed to extract text from PDF: {}", e));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let page_index = page_number - 1;
    if page_index >= text_pages.len() {
        set_last_error(format!(
            "Page number {} is out of range (PDF has {} pages)",
            page_number,
            text_pages.len()
        ));
        return ErrorCode::PdfParseError as c_int;
    }

    let page_text = &text_pages[page_index];
    let page_content = &page_text.text;

    let mut chunks = Vec::new();
    let mut chunk_index = 0;
    let mut byte_start = 0;

    while byte_start < page_content.len() {
        let start = find_char_boundary(page_content, byte_start);
        if start >= page_content.len() {
            break;
        }

        let raw_end = (start + chunk_opts.max_chunk_size).min(page_content.len());
        let end = find_char_boundary(page_content, raw_end);

        let chunk_end = if chunk_opts.preserve_sentence_boundaries && end < page_content.len() {
            let raw_search_start =
                start + (chunk_opts.max_chunk_size * 4 / 5).min(end.saturating_sub(start));
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
                index: chunk_index,
                page_number,
                text: chunk_text,
                confidence: 1.0,
                x: 0.0,
                y: 0.0,
                width: 0.0,
                height: 0.0,
            });
            chunk_index += 1;
        }

        let next_start = chunk_end.saturating_sub(chunk_opts.overlap);
        if next_start <= byte_start || chunk_end >= page_content.len() {
            break;
        }
        byte_start = next_start;
    }

    let json = match serde_json::to_string(&chunks) {
        Ok(j) => j,
        Err(e) => {
            set_last_error(format!("Failed to serialize chunks to JSON: {}", e));
            return ErrorCode::SerializationError as c_int;
        }
    };

    let c_string = match CString::new(json) {
        Ok(s) => s,
        Err(e) => {
            set_last_error(format!("JSON contains invalid UTF-8: {}", e));
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    *out_json = c_string.into_raw();
    ErrorCode::Success as c_int
}

/// Get version string
///
/// # Safety
/// - `out_version` must be a valid pointer to a mutable pointer location
/// - The returned string must be freed with `oxidize_free_string`
#[no_mangle]
pub unsafe extern "C" fn oxidize_version(out_version: *mut *mut c_char) -> c_int {
    if out_version.is_null() {
        return ErrorCode::NullPointer as c_int;
    }

    // CRITICAL: Initialize output to null on entry (safety)
    *out_version = ptr::null_mut();

    let version = format!(
        "oxidize-pdf-ffi v{} (oxidize-pdf v1.6.4)",
        env!("CARGO_PKG_VERSION")
    );

    let c_string = match CString::new(version) {
        Ok(s) => s,
        Err(_) => return ErrorCode::InvalidUtf8 as c_int,
    };

    *out_version = c_string.into_raw();

    ErrorCode::Success as c_int
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::ffi::CStr;
    use std::ptr;

    #[test]
    fn test_version() {
        let mut version_ptr: *mut c_char = ptr::null_mut();
        let result = unsafe { oxidize_version(&mut version_ptr as *mut *mut c_char) };

        assert_eq!(result, ErrorCode::Success as c_int);
        assert!(!version_ptr.is_null());

        unsafe {
            let version = CStr::from_ptr(version_ptr).to_string_lossy();
            assert!(version.contains("oxidize-pdf-ffi"));
            oxidize_free_string(version_ptr);
        }
    }

    #[test]
    fn test_null_pointer_handling() {
        let result = unsafe { oxidize_extract_text(ptr::null(), 0, ptr::null_mut()) };
        assert_eq!(result, ErrorCode::NullPointer as c_int);
    }
}
