use std::ffi::{CStr, CString};
use std::fs;
use std::os::raw::{c_char, c_int};
use std::path::PathBuf;
use std::ptr;
use std::time::{SystemTime, UNIX_EPOCH};

use crate::{clear_last_error, set_last_error, ErrorCode};
use base64::Engine as _;

// ── Helpers ───────────────────────────────────────────────────────────────────

/// Generate a unique temp file path using timestamp + thread id to avoid collisions.
fn temp_pdf_path(suffix: &str) -> PathBuf {
    let ts = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|d| d.as_nanos())
        .unwrap_or(0);
    let tid = std::thread::current().id();
    let name = format!("oxidize_{ts}_{tid:?}{suffix}.pdf");
    std::env::temp_dir().join(name)
}

/// Write bytes to a temp file, run `f`, then delete the file.
fn with_temp_input<F, T>(bytes: &[u8], f: F) -> Result<T, String>
where
    F: FnOnce(&str) -> Result<T, String>,
{
    let path = temp_pdf_path("_in");
    fs::write(&path, bytes).map_err(|e| format!("Failed to write temp input: {e}"))?;
    let path_str = path
        .to_str()
        .ok_or_else(|| "Temp path is not valid UTF-8".to_string())?;
    let result = f(path_str);
    let _ = fs::remove_file(&path);
    result
}

/// Run `f` with a temp output path, then read the result bytes and delete the file.
fn with_temp_output<F>(f: F) -> Result<Vec<u8>, String>
where
    F: FnOnce(&str) -> Result<(), String>,
{
    let path = temp_pdf_path("_out");
    let path_str = path
        .to_str()
        .ok_or_else(|| "Temp path is not valid UTF-8".to_string())?;
    f(path_str)?;
    let bytes = fs::read(&path).map_err(|e| format!("Failed to read temp output: {e}"))?;
    let _ = fs::remove_file(&path);
    Ok(bytes)
}

/// Allocate a byte buffer on the heap for the caller.  The caller must free it with
/// `oxidize_free_bytes`.
unsafe fn set_out_bytes(bytes: Vec<u8>, out_bytes: *mut *mut u8, out_len: *mut usize) {
    let len = bytes.len();
    let mut boxed = bytes.into_boxed_slice();
    *out_bytes = boxed.as_mut_ptr();
    *out_len = len;
    std::mem::forget(boxed);
}

// ── split ─────────────────────────────────────────────────────────────────────

/// Split a PDF (supplied as bytes) into individual single-page PDFs.
///
/// Returns a JSON array of base64-encoded PDF strings, one per page.
/// The JSON string must be freed with `oxidize_free_string`.
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `out_json` must be a valid pointer to a mutable pointer that will receive the JSON string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_split_pdf_bytes(
    pdf_bytes: *const u8,
    pdf_len: usize,
    out_json: *mut *mut c_char,
) -> c_int {
    clear_last_error();
    if pdf_bytes.is_null() || out_json.is_null() {
        set_last_error("Null pointer provided to oxidize_split_pdf_bytes");
        return ErrorCode::NullPointer as c_int;
    }
    *out_json = ptr::null_mut();

    if pdf_len == 0 {
        set_last_error("PDF data is empty (0 bytes)");
        return ErrorCode::PdfParseError as c_int;
    }

    let input_bytes = std::slice::from_raw_parts(pdf_bytes, pdf_len);

    let result = with_temp_input(input_bytes, |input_path| {
        let out_dir = std::env::temp_dir();
        let ts = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .map(|d| d.as_nanos())
            .unwrap_or(0);
        let pattern = format!("{}/oxidize_split_{}_page_{{}}.pdf", out_dir.display(), ts);

        let paths = oxidize_pdf::operations::split_into_pages(input_path, &pattern)
            .map_err(|e| format!("split_into_pages failed: {e}"))?;

        let mut encoded_pages = Vec::with_capacity(paths.len());
        for path in &paths {
            let page_bytes =
                fs::read(path).map_err(|e| format!("Failed to read split page: {e}"))?;
            encoded_pages.push(base64::engine::general_purpose::STANDARD.encode(&page_bytes));
            let _ = fs::remove_file(path);
        }

        serde_json::to_string(&encoded_pages).map_err(|e| format!("JSON serialization failed: {e}"))
    });

    match result {
        Ok(json) => match CString::new(json) {
            Ok(cs) => {
                *out_json = cs.into_raw();
                ErrorCode::Success as c_int
            }
            Err(_) => {
                set_last_error("JSON output contains null bytes");
                ErrorCode::SerializationError as c_int
            }
        },
        Err(msg) => {
            set_last_error(msg);
            ErrorCode::IoError as c_int
        }
    }
}

// ── merge ─────────────────────────────────────────────────────────────────────

/// Merge multiple PDFs (supplied as a JSON array of base64 strings) into one PDF.
///
/// `pdfs_json` must be a null-terminated JSON array of base64-encoded PDF strings.
/// On success, `out_bytes` / `out_len` receive the merged PDF bytes (free with
/// `oxidize_free_bytes`).
///
/// # Safety
/// - `pdfs_json` must be a valid null-terminated UTF-8 string containing a JSON array.
/// - `out_bytes` and `out_len` must be valid non-null pointers.
#[no_mangle]
pub unsafe extern "C" fn oxidize_merge_pdfs_bytes(
    pdfs_json: *const c_char,
    out_bytes: *mut *mut u8,
    out_len: *mut usize,
) -> c_int {
    clear_last_error();
    if pdfs_json.is_null() || out_bytes.is_null() || out_len.is_null() {
        set_last_error("Null pointer provided to oxidize_merge_pdfs_bytes");
        return ErrorCode::NullPointer as c_int;
    }
    *out_bytes = ptr::null_mut();
    *out_len = 0;

    let json_str = match CStr::from_ptr(pdfs_json).to_str() {
        Ok(s) => s,
        Err(_) => {
            set_last_error("Invalid UTF-8 in pdfs_json");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    let encoded: Vec<String> = match serde_json::from_str(json_str) {
        Ok(v) => v,
        Err(e) => {
            set_last_error(format!("Failed to parse pdfs_json: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };

    if encoded.is_empty() {
        set_last_error("At least one PDF is required for merge");
        return ErrorCode::PdfParseError as c_int;
    }

    // Decode all base64 PDFs and write to temp files.
    let mut temp_paths: Vec<PathBuf> = Vec::with_capacity(encoded.len());
    for (i, b64) in encoded.iter().enumerate() {
        let decoded = match base64::engine::general_purpose::STANDARD.decode(b64) {
            Ok(d) => d,
            Err(e) => {
                for p in &temp_paths {
                    let _ = fs::remove_file(p);
                }
                set_last_error(format!("Failed to decode PDF #{i}: {e}"));
                return ErrorCode::PdfParseError as c_int;
            }
        };
        let path = temp_pdf_path(&format!("_merge_{i}"));
        if let Err(e) = fs::write(&path, &decoded) {
            for p in &temp_paths {
                let _ = fs::remove_file(p);
            }
            set_last_error(format!("Failed to write temp file for PDF #{i}: {e}"));
            return ErrorCode::IoError as c_int;
        }
        temp_paths.push(path);
    }

    let inputs: Vec<oxidize_pdf::operations::MergeInput> = match temp_paths
        .iter()
        .map(|p| {
            p.to_str()
                .ok_or_else(|| "Temp path is not valid UTF-8".to_string())
                .map(oxidize_pdf::operations::MergeInput::new)
        })
        .collect::<Result<Vec<_>, _>>()
    {
        Ok(v) => v,
        Err(msg) => {
            for p in &temp_paths {
                let _ = fs::remove_file(p);
            }
            set_last_error(msg);
            return ErrorCode::IoError as c_int;
        }
    };

    let merge_result = with_temp_output(|output_path| {
        oxidize_pdf::operations::merge_pdfs(
            inputs,
            output_path,
            oxidize_pdf::operations::MergeOptions::default(),
        )
        .map_err(|e| format!("merge_pdfs failed: {e}"))
    });

    // Clean up temp input files regardless.
    for p in &temp_paths {
        let _ = fs::remove_file(p);
    }

    match merge_result {
        Ok(bytes) => {
            set_out_bytes(bytes, out_bytes, out_len);
            ErrorCode::Success as c_int
        }
        Err(msg) => {
            set_last_error(msg);
            ErrorCode::IoError as c_int
        }
    }
}

// ── rotate ────────────────────────────────────────────────────────────────────

/// Rotate all pages of a PDF (supplied as bytes) by `degrees` (must be 0, 90, 180, or 270).
///
/// On success, `out_bytes` / `out_len` receive the rotated PDF bytes (free with
/// `oxidize_free_bytes`).
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `out_bytes` and `out_len` must be valid non-null pointers.
#[no_mangle]
pub unsafe extern "C" fn oxidize_rotate_pdf_bytes(
    pdf_bytes: *const u8,
    pdf_len: usize,
    degrees: c_int,
    out_bytes: *mut *mut u8,
    out_len: *mut usize,
) -> c_int {
    clear_last_error();
    if pdf_bytes.is_null() || out_bytes.is_null() || out_len.is_null() {
        set_last_error("Null pointer provided to oxidize_rotate_pdf_bytes");
        return ErrorCode::NullPointer as c_int;
    }
    *out_bytes = ptr::null_mut();
    *out_len = 0;

    if pdf_len == 0 {
        set_last_error("PDF data is empty (0 bytes)");
        return ErrorCode::PdfParseError as c_int;
    }

    let angle = match oxidize_pdf::operations::RotationAngle::from_degrees(degrees) {
        Ok(a) => a,
        Err(e) => {
            set_last_error(format!("Invalid rotation angle {degrees}: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let input_bytes = std::slice::from_raw_parts(pdf_bytes, pdf_len);

    let result = with_temp_input(input_bytes, |input_path| {
        with_temp_output(|output_path| {
            oxidize_pdf::operations::rotate_all_pages(input_path, output_path, angle)
                .map_err(|e| format!("rotate_all_pages failed: {e}"))
        })
    });

    match result {
        Ok(bytes) => {
            set_out_bytes(bytes, out_bytes, out_len);
            ErrorCode::Success as c_int
        }
        Err(msg) => {
            set_last_error(msg);
            ErrorCode::IoError as c_int
        }
    }
}

// ── extract pages ─────────────────────────────────────────────────────────────

/// Extract specific pages from a PDF (supplied as bytes).
///
/// `pages_json` must be a null-terminated JSON array of 0-based page indices, e.g. `"[0,2,4]"`.
/// On success, `out_bytes` / `out_len` receive the new PDF bytes (free with `oxidize_free_bytes`).
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `pages_json` must be a valid null-terminated UTF-8 string.
/// - `out_bytes` and `out_len` must be valid non-null pointers.
#[no_mangle]
pub unsafe extern "C" fn oxidize_extract_pages_bytes(
    pdf_bytes: *const u8,
    pdf_len: usize,
    pages_json: *const c_char,
    out_bytes: *mut *mut u8,
    out_len: *mut usize,
) -> c_int {
    clear_last_error();
    if pdf_bytes.is_null() || pages_json.is_null() || out_bytes.is_null() || out_len.is_null() {
        set_last_error("Null pointer provided to oxidize_extract_pages_bytes");
        return ErrorCode::NullPointer as c_int;
    }
    *out_bytes = ptr::null_mut();
    *out_len = 0;

    if pdf_len == 0 {
        set_last_error("PDF data is empty (0 bytes)");
        return ErrorCode::PdfParseError as c_int;
    }

    let json_str = match CStr::from_ptr(pages_json).to_str() {
        Ok(s) => s,
        Err(_) => {
            set_last_error("Invalid UTF-8 in pages_json");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    let indices: Vec<usize> = match serde_json::from_str(json_str) {
        Ok(v) => v,
        Err(e) => {
            set_last_error(format!("Failed to parse pages_json: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };

    if indices.is_empty() {
        set_last_error("At least one page index is required");
        return ErrorCode::PdfParseError as c_int;
    }

    let input_bytes = std::slice::from_raw_parts(pdf_bytes, pdf_len);

    let result = with_temp_input(input_bytes, |input_path| {
        with_temp_output(|output_path| {
            oxidize_pdf::operations::extract_pages_to_file(input_path, &indices, output_path)
                .map_err(|e| format!("extract_pages_to_file failed: {e}"))
        })
    });

    match result {
        Ok(bytes) => {
            set_out_bytes(bytes, out_bytes, out_len);
            ErrorCode::Success as c_int
        }
        Err(msg) => {
            set_last_error(msg);
            ErrorCode::IoError as c_int
        }
    }
}

// ── reorder pages ────────────────────────────────────────────────────────────

/// Reorder pages of a PDF according to a new order (JSON array of 0-based indices).
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `order_json` must be a valid null-terminated UTF-8 string containing a JSON array.
/// - `out_bytes` and `out_len` must be valid non-null pointers.
#[no_mangle]
pub unsafe extern "C" fn oxidize_reorder_pages_bytes(
    pdf_bytes: *const u8,
    pdf_len: usize,
    order_json: *const c_char,
    out_bytes: *mut *mut u8,
    out_len: *mut usize,
) -> c_int {
    clear_last_error();
    if pdf_bytes.is_null() || order_json.is_null() || out_bytes.is_null() || out_len.is_null() {
        set_last_error("Null pointer provided to oxidize_reorder_pages_bytes");
        return ErrorCode::NullPointer as c_int;
    }
    *out_bytes = ptr::null_mut();
    *out_len = 0;

    if pdf_len == 0 {
        set_last_error("PDF data is empty (0 bytes)");
        return ErrorCode::PdfParseError as c_int;
    }

    let json_str = match CStr::from_ptr(order_json).to_str() {
        Ok(s) => s,
        Err(_) => {
            set_last_error("Invalid UTF-8 in order_json");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    let order: Vec<usize> = match serde_json::from_str(json_str) {
        Ok(v) => v,
        Err(e) => {
            set_last_error(format!("Failed to parse order_json: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };

    if order.is_empty() {
        set_last_error("Page order array cannot be empty");
        return ErrorCode::PdfParseError as c_int;
    }

    let input_bytes = std::slice::from_raw_parts(pdf_bytes, pdf_len);

    let result = with_temp_input(input_bytes, |input_path| {
        with_temp_output(|output_path| {
            oxidize_pdf::operations::reorder_pdf_pages(input_path, output_path, order.clone())
                .map_err(|e| format!("reorder_pdf_pages failed: {e}"))
        })
    });

    match result {
        Ok(bytes) => {
            set_out_bytes(bytes, out_bytes, out_len);
            ErrorCode::Success as c_int
        }
        Err(msg) => {
            set_last_error(msg);
            ErrorCode::IoError as c_int
        }
    }
}

// ── swap pages ───────────────────────────────────────────────────────────────

/// Swap two pages in a PDF by their 0-based indices.
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `out_bytes` and `out_len` must be valid non-null pointers.
#[no_mangle]
pub unsafe extern "C" fn oxidize_swap_pages_bytes(
    pdf_bytes: *const u8,
    pdf_len: usize,
    page_a: usize,
    page_b: usize,
    out_bytes: *mut *mut u8,
    out_len: *mut usize,
) -> c_int {
    clear_last_error();
    if pdf_bytes.is_null() || out_bytes.is_null() || out_len.is_null() {
        set_last_error("Null pointer provided to oxidize_swap_pages_bytes");
        return ErrorCode::NullPointer as c_int;
    }
    *out_bytes = ptr::null_mut();
    *out_len = 0;

    if pdf_len == 0 {
        set_last_error("PDF data is empty (0 bytes)");
        return ErrorCode::PdfParseError as c_int;
    }

    let input_bytes = std::slice::from_raw_parts(pdf_bytes, pdf_len);

    let result = with_temp_input(input_bytes, |input_path| {
        with_temp_output(|output_path| {
            oxidize_pdf::operations::swap_pdf_pages(input_path, output_path, page_a, page_b)
                .map_err(|e| format!("swap_pdf_pages failed: {e}"))
        })
    });

    match result {
        Ok(bytes) => {
            set_out_bytes(bytes, out_bytes, out_len);
            ErrorCode::Success as c_int
        }
        Err(msg) => {
            set_last_error(msg);
            ErrorCode::IoError as c_int
        }
    }
}

// ── move page ────────────────────────────────────────────────────────────────

/// Move a page from one position to another (0-based indices).
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `out_bytes` and `out_len` must be valid non-null pointers.
#[no_mangle]
pub unsafe extern "C" fn oxidize_move_page_bytes(
    pdf_bytes: *const u8,
    pdf_len: usize,
    from_index: usize,
    to_index: usize,
    out_bytes: *mut *mut u8,
    out_len: *mut usize,
) -> c_int {
    clear_last_error();
    if pdf_bytes.is_null() || out_bytes.is_null() || out_len.is_null() {
        set_last_error("Null pointer provided to oxidize_move_page_bytes");
        return ErrorCode::NullPointer as c_int;
    }
    *out_bytes = ptr::null_mut();
    *out_len = 0;

    if pdf_len == 0 {
        set_last_error("PDF data is empty (0 bytes)");
        return ErrorCode::PdfParseError as c_int;
    }

    let input_bytes = std::slice::from_raw_parts(pdf_bytes, pdf_len);

    let result = with_temp_input(input_bytes, |input_path| {
        with_temp_output(|output_path| {
            oxidize_pdf::operations::move_pdf_page(input_path, output_path, from_index, to_index)
                .map_err(|e| format!("move_pdf_page failed: {e}"))
        })
    });

    match result {
        Ok(bytes) => {
            set_out_bytes(bytes, out_bytes, out_len);
            ErrorCode::Success as c_int
        }
        Err(msg) => {
            set_last_error(msg);
            ErrorCode::IoError as c_int
        }
    }
}

// ── reverse pages ────────────────────────────────────────────────────────────

/// Reverse the order of all pages in a PDF.
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `out_bytes` and `out_len` must be valid non-null pointers.
#[no_mangle]
pub unsafe extern "C" fn oxidize_reverse_pages_bytes(
    pdf_bytes: *const u8,
    pdf_len: usize,
    out_bytes: *mut *mut u8,
    out_len: *mut usize,
) -> c_int {
    clear_last_error();
    if pdf_bytes.is_null() || out_bytes.is_null() || out_len.is_null() {
        set_last_error("Null pointer provided to oxidize_reverse_pages_bytes");
        return ErrorCode::NullPointer as c_int;
    }
    *out_bytes = ptr::null_mut();
    *out_len = 0;

    if pdf_len == 0 {
        set_last_error("PDF data is empty (0 bytes)");
        return ErrorCode::PdfParseError as c_int;
    }

    let input_bytes = std::slice::from_raw_parts(pdf_bytes, pdf_len);

    let result = with_temp_input(input_bytes, |input_path| {
        with_temp_output(|output_path| {
            oxidize_pdf::operations::reverse_pdf_pages(input_path, output_path)
                .map_err(|e| format!("reverse_pdf_pages failed: {e}"))
        })
    });

    match result {
        Ok(bytes) => {
            set_out_bytes(bytes, out_bytes, out_len);
            ErrorCode::Success as c_int
        }
        Err(msg) => {
            set_last_error(msg);
            ErrorCode::IoError as c_int
        }
    }
}

// ── split with options ────────────────────────────────────────────────────────

/// Options JSON tag for split mode dispatch.
#[derive(serde::Deserialize)]
#[serde(tag = "mode")]
enum SplitOptionsJson {
    SinglePages,
    ChunkSize { chunk_size: usize },
    Ranges { ranges: Vec<[usize; 2]> },
    SplitAt { split_at: Vec<usize> },
}

/// Split a PDF (supplied as bytes) with configurable split options.
///
/// `options_json` must be a null-terminated JSON object with a `mode` tag,
/// e.g. `{"mode":"ChunkSize","chunk_size":3}`.
/// Returns a JSON array of base64-encoded PDF strings.
/// The JSON string must be freed with `oxidize_free_string`.
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `options_json` must be a valid null-terminated UTF-8 string.
/// - `out_json` must be a valid pointer to a mutable pointer.
#[no_mangle]
pub unsafe extern "C" fn oxidize_split_pdf_bytes_with_options(
    pdf_bytes: *const u8,
    pdf_len: usize,
    options_json: *const c_char,
    out_json: *mut *mut c_char,
) -> c_int {
    clear_last_error();
    if pdf_bytes.is_null() || options_json.is_null() || out_json.is_null() {
        set_last_error("Null pointer provided to oxidize_split_pdf_bytes_with_options");
        return ErrorCode::NullPointer as c_int;
    }
    *out_json = ptr::null_mut();

    if pdf_len == 0 {
        set_last_error("PDF data is empty (0 bytes)");
        return ErrorCode::PdfParseError as c_int;
    }

    let opts_str = match CStr::from_ptr(options_json).to_str() {
        Ok(s) => s,
        Err(_) => {
            set_last_error("Invalid UTF-8 in options_json");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    let opts_json: SplitOptionsJson = match serde_json::from_str(opts_str) {
        Ok(v) => v,
        Err(e) => {
            set_last_error(format!("Failed to parse options_json: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };

    let split_mode = match opts_json {
        SplitOptionsJson::SinglePages => oxidize_pdf::operations::SplitMode::SinglePages,
        SplitOptionsJson::ChunkSize { chunk_size } => {
            oxidize_pdf::operations::SplitMode::ChunkSize(chunk_size)
        }
        SplitOptionsJson::Ranges { ranges } => {
            let page_ranges: Vec<oxidize_pdf::operations::PageRange> = ranges
                .iter()
                .map(|&[from, to]| oxidize_pdf::operations::PageRange::Range(from, to))
                .collect();
            oxidize_pdf::operations::SplitMode::Ranges(page_ranges)
        }
        SplitOptionsJson::SplitAt { split_at } => {
            oxidize_pdf::operations::SplitMode::SplitAt(split_at)
        }
    };

    let input_bytes = std::slice::from_raw_parts(pdf_bytes, pdf_len);

    let result = with_temp_input(input_bytes, |input_path| {
        let out_dir = std::env::temp_dir();
        let ts = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .map(|d| d.as_nanos())
            .unwrap_or(0);
        let pattern = format!(
            "{}/oxidize_split_opts_{}_chunk_{{}}.pdf",
            out_dir.display(),
            ts
        );

        let options = oxidize_pdf::operations::SplitOptions {
            mode: split_mode,
            output_pattern: pattern,
            ..Default::default()
        };

        let paths = oxidize_pdf::operations::split_pdf(input_path, options)
            .map_err(|e| format!("split_pdf failed: {e}"))?;

        let mut encoded_chunks = Vec::with_capacity(paths.len());
        for path in &paths {
            let chunk_bytes =
                fs::read(path).map_err(|e| format!("Failed to read split chunk: {e}"))?;
            encoded_chunks.push(base64::engine::general_purpose::STANDARD.encode(&chunk_bytes));
            let _ = fs::remove_file(path);
        }

        serde_json::to_string(&encoded_chunks)
            .map_err(|e| format!("JSON serialization failed: {e}"))
    });

    match result {
        Ok(json) => match CString::new(json) {
            Ok(cs) => {
                *out_json = cs.into_raw();
                ErrorCode::Success as c_int
            }
            Err(_) => {
                set_last_error("JSON output contains null bytes");
                ErrorCode::SerializationError as c_int
            }
        },
        Err(msg) => {
            set_last_error(msg);
            ErrorCode::IoError as c_int
        }
    }
}

// ── merge with page ranges ─────────────────────────────────────────────────────

/// JSON representation of a page range for merge inputs.
#[derive(serde::Deserialize)]
#[serde(tag = "kind")]
enum PageRangeJson {
    All,
    Single { index: usize },
    Range { from: usize, to: usize },
    List { indices: Vec<usize> },
}

impl PageRangeJson {
    fn into_core(self) -> oxidize_pdf::operations::PageRange {
        match self {
            PageRangeJson::All => oxidize_pdf::operations::PageRange::All,
            PageRangeJson::Single { index } => oxidize_pdf::operations::PageRange::Single(index),
            PageRangeJson::Range { from, to } => {
                oxidize_pdf::operations::PageRange::Range(from, to)
            }
            PageRangeJson::List { indices } => oxidize_pdf::operations::PageRange::List(indices),
        }
    }
}

/// JSON representation of a single merge input.
#[derive(serde::Deserialize)]
struct MergeInputJson {
    /// Base64-encoded PDF bytes.
    pdf: String,
    /// Optional page range selector.
    pages: Option<PageRangeJson>,
}

/// Merge multiple PDFs with per-input page range selection.
///
/// `inputs_json` must be a null-terminated JSON array of objects with shape
/// `{"pdf":"<base64>","pages":null}` or `{"pdf":"<base64>","pages":{"kind":"Range","from":0,"to":2}}`.
/// On success, `out_bytes` / `out_len` receive the merged PDF bytes (free with `oxidize_free_bytes`).
///
/// # Safety
/// - `inputs_json` must be a valid null-terminated UTF-8 string.
/// - `out_bytes` and `out_len` must be valid non-null pointers.
#[no_mangle]
pub unsafe extern "C" fn oxidize_merge_pdfs_with_ranges(
    inputs_json: *const c_char,
    out_bytes: *mut *mut u8,
    out_len: *mut usize,
) -> c_int {
    clear_last_error();
    if inputs_json.is_null() || out_bytes.is_null() || out_len.is_null() {
        set_last_error("Null pointer provided to oxidize_merge_pdfs_with_ranges");
        return ErrorCode::NullPointer as c_int;
    }
    *out_bytes = ptr::null_mut();
    *out_len = 0;

    let json_str = match CStr::from_ptr(inputs_json).to_str() {
        Ok(s) => s,
        Err(_) => {
            set_last_error("Invalid UTF-8 in inputs_json");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    let inputs_data: Vec<MergeInputJson> = match serde_json::from_str(json_str) {
        Ok(v) => v,
        Err(e) => {
            set_last_error(format!("Failed to parse inputs_json: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };

    if inputs_data.is_empty() {
        set_last_error("At least one PDF is required for merge");
        return ErrorCode::PdfParseError as c_int;
    }

    // Decode all base64 PDFs and write to temp files.
    let mut temp_paths: Vec<PathBuf> = Vec::with_capacity(inputs_data.len());

    for (i, input) in inputs_data.iter().enumerate() {
        let decoded = match base64::engine::general_purpose::STANDARD.decode(&input.pdf) {
            Ok(d) => d,
            Err(e) => {
                for p in &temp_paths {
                    let _ = fs::remove_file(p);
                }
                set_last_error(format!("Failed to decode PDF #{i}: {e}"));
                return ErrorCode::PdfParseError as c_int;
            }
        };
        let path = temp_pdf_path(&format!("_merge_ranges_{i}"));
        if let Err(e) = fs::write(&path, &decoded) {
            for p in &temp_paths {
                let _ = fs::remove_file(p);
            }
            set_last_error(format!("Failed to write temp file for PDF #{i}: {e}"));
            return ErrorCode::IoError as c_int;
        }
        temp_paths.push(path);
    }

    // Build MergeInput list with optional page ranges.
    let merge_inputs: Vec<oxidize_pdf::operations::MergeInput> = temp_paths
        .iter()
        .zip(inputs_data)
        .filter_map(|(path, data)| {
            path.to_str().map(|path_str| match data.pages {
                Some(range) => {
                    oxidize_pdf::operations::MergeInput::with_pages(path_str, range.into_core())
                }
                None => oxidize_pdf::operations::MergeInput::new(path_str),
            })
        })
        .collect();

    if merge_inputs.len() != temp_paths.len() {
        for p in &temp_paths {
            let _ = fs::remove_file(p);
        }
        set_last_error("One or more temp paths are not valid UTF-8");
        return ErrorCode::IoError as c_int;
    }

    let merge_result = with_temp_output(|output_path| {
        oxidize_pdf::operations::merge_pdfs(
            merge_inputs,
            output_path,
            oxidize_pdf::operations::MergeOptions::default(),
        )
        .map_err(|e| format!("merge_pdfs failed: {e}"))
    });

    for p in &temp_paths {
        let _ = fs::remove_file(p);
    }

    match merge_result {
        Ok(bytes) => {
            set_out_bytes(bytes, out_bytes, out_len);
            ErrorCode::Success as c_int
        }
        Err(msg) => {
            set_last_error(msg);
            ErrorCode::IoError as c_int
        }
    }
}

// ── rotate selected pages ─────────────────────────────────────────────────────

/// Rotate specific pages (or all pages) of a PDF.
///
/// `pages_json` must be a null-terminated JSON object with a `kind` tag describing the
/// page range, e.g. `{"kind":"All"}`, `{"kind":"Range","from":0,"to":2}`, or
/// `{"kind":"Single","index":1}`. If `pages_json` is null, all pages are rotated.
/// On success, `out_bytes` / `out_len` receive the rotated PDF bytes (free with `oxidize_free_bytes`).
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `pages_json` may be null (meaning "all pages") or a valid null-terminated UTF-8 string.
/// - `out_bytes` and `out_len` must be valid non-null pointers.
#[no_mangle]
pub unsafe extern "C" fn oxidize_rotate_pages_bytes(
    pdf_bytes: *const u8,
    pdf_len: usize,
    degrees: c_int,
    pages_json: *const c_char,
    out_bytes: *mut *mut u8,
    out_len: *mut usize,
) -> c_int {
    clear_last_error();
    if pdf_bytes.is_null() || out_bytes.is_null() || out_len.is_null() {
        set_last_error("Null pointer provided to oxidize_rotate_pages_bytes");
        return ErrorCode::NullPointer as c_int;
    }
    *out_bytes = ptr::null_mut();
    *out_len = 0;

    if pdf_len == 0 {
        set_last_error("PDF data is empty (0 bytes)");
        return ErrorCode::PdfParseError as c_int;
    }

    let angle = match oxidize_pdf::operations::RotationAngle::from_degrees(degrees) {
        Ok(a) => a,
        Err(e) => {
            set_last_error(format!("Invalid rotation angle {degrees}: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let page_range = if pages_json.is_null() {
        oxidize_pdf::operations::PageRange::All
    } else {
        let range_str = match CStr::from_ptr(pages_json).to_str() {
            Ok(s) => s,
            Err(_) => {
                set_last_error("Invalid UTF-8 in pages_json");
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };
        let range_json: PageRangeJson = match serde_json::from_str(range_str) {
            Ok(v) => v,
            Err(e) => {
                set_last_error(format!("Failed to parse pages_json: {e}"));
                return ErrorCode::SerializationError as c_int;
            }
        };
        range_json.into_core()
    };

    let input_bytes = std::slice::from_raw_parts(pdf_bytes, pdf_len);

    let result = with_temp_input(input_bytes, |input_path| {
        with_temp_output(|output_path| {
            let options = oxidize_pdf::operations::RotateOptions {
                pages: page_range,
                angle,
                preserve_page_size: false,
            };
            oxidize_pdf::operations::rotate_pdf_pages(input_path, output_path, options)
                .map_err(|e| format!("rotate_pdf_pages failed: {e}"))
        })
    });

    match result {
        Ok(bytes) => {
            set_out_bytes(bytes, out_bytes, out_len);
            ErrorCode::Success as c_int
        }
        Err(msg) => {
            set_last_error(msg);
            ErrorCode::IoError as c_int
        }
    }
}

// ── overlay ──────────────────────────────────────────────────────────────────

/// Overlay one PDF on top of another using default options.
///
/// # Safety
/// - `base_bytes` must be a valid pointer to `base_len` bytes.
/// - `overlay_bytes` must be a valid pointer to `overlay_len` bytes.
/// - `out_bytes` and `out_len` must be valid non-null pointers.
#[no_mangle]
pub unsafe extern "C" fn oxidize_overlay_pdf_bytes(
    base_bytes: *const u8,
    base_len: usize,
    overlay_bytes: *const u8,
    overlay_len: usize,
    out_bytes: *mut *mut u8,
    out_len: *mut usize,
) -> c_int {
    clear_last_error();
    if base_bytes.is_null() || overlay_bytes.is_null() || out_bytes.is_null() || out_len.is_null() {
        set_last_error("Null pointer provided to oxidize_overlay_pdf_bytes");
        return ErrorCode::NullPointer as c_int;
    }
    *out_bytes = ptr::null_mut();
    *out_len = 0;

    if base_len == 0 || overlay_len == 0 {
        set_last_error("PDF data is empty (0 bytes)");
        return ErrorCode::PdfParseError as c_int;
    }

    let base_data = std::slice::from_raw_parts(base_bytes, base_len);
    let overlay_data = std::slice::from_raw_parts(overlay_bytes, overlay_len);

    let base_path = temp_pdf_path("_overlay_base");
    let overlay_path = temp_pdf_path("_overlay_top");

    if let Err(e) = fs::write(&base_path, base_data) {
        set_last_error(format!("Failed to write base temp file: {e}"));
        return ErrorCode::IoError as c_int;
    }
    if let Err(e) = fs::write(&overlay_path, overlay_data) {
        let _ = fs::remove_file(&base_path);
        set_last_error(format!("Failed to write overlay temp file: {e}"));
        return ErrorCode::IoError as c_int;
    }

    let result = with_temp_output(|output_path| {
        oxidize_pdf::operations::overlay_pdf(
            &base_path,
            &overlay_path,
            output_path,
            oxidize_pdf::operations::OverlayOptions::default(),
        )
        .map_err(|e| format!("overlay_pdf failed: {e}"))
    });

    let _ = fs::remove_file(&base_path);
    let _ = fs::remove_file(&overlay_path);

    match result {
        Ok(bytes) => {
            set_out_bytes(bytes, out_bytes, out_len);
            ErrorCode::Success as c_int
        }
        Err(msg) => {
            set_last_error(msg);
            ErrorCode::IoError as c_int
        }
    }
}

// ── extract images ───────────────────────────────────────────────────────────

/// Extract all images from a PDF (supplied as bytes).
///
/// On success, `out_json` receives a null-terminated JSON array of image objects:
/// `[{"page_number":0,"image_index":0,"width":100,"height":100,"format":"jpeg","data":"<base64>"},...]`
/// The string must be freed with `oxidize_free_string`.
///
/// # Safety
/// - `pdf_bytes` must be a valid pointer to `pdf_len` bytes.
/// - `out_json` must be a valid pointer to a mutable pointer.
#[no_mangle]
pub unsafe extern "C" fn oxidize_extract_images_bytes(
    pdf_bytes: *const u8,
    pdf_len: usize,
    out_json: *mut *mut c_char,
) -> c_int {
    clear_last_error();
    if pdf_bytes.is_null() || out_json.is_null() {
        set_last_error("Null pointer provided to oxidize_extract_images_bytes");
        return ErrorCode::NullPointer as c_int;
    }
    *out_json = ptr::null_mut();

    if pdf_len == 0 {
        set_last_error("PDF data is empty (0 bytes)");
        return ErrorCode::PdfParseError as c_int;
    }

    let input_bytes = std::slice::from_raw_parts(pdf_bytes, pdf_len);

    let result = with_temp_input(input_bytes, |input_path| {
        let ts = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .map(|d| d.as_nanos())
            .unwrap_or(0);
        let tid = std::thread::current().id();
        let out_dir = std::env::temp_dir().join(format!("oxidize_imgs_{ts}_{tid:?}"));

        let options = oxidize_pdf::operations::ExtractImagesOptions {
            output_dir: out_dir.clone(),
            create_dir: true,
            ..Default::default()
        };

        let images = oxidize_pdf::operations::extract_images_from_pdf(input_path, options)
            .map_err(|e| format!("extract_images_from_pdf failed: {e}"))?;

        let mut json_items: Vec<String> = Vec::with_capacity(images.len());
        for img in &images {
            let img_bytes = match fs::read(&img.file_path) {
                Ok(b) => b,
                Err(e) => {
                    let _ = fs::remove_dir_all(&out_dir);
                    return Err(format!("Failed to read extracted image: {e}"));
                }
            };
            let data_b64 = base64::engine::general_purpose::STANDARD.encode(&img_bytes);
            let format_str = match img.format {
                oxidize_pdf::ImageFormat::Jpeg => "jpeg",
                oxidize_pdf::ImageFormat::Png => "png",
                oxidize_pdf::ImageFormat::Tiff => "tiff",
                oxidize_pdf::ImageFormat::Raw => "raw",
            };
            json_items.push(format!(
                r#"{{"page_number":{page},"image_index":{idx},"width":{w},"height":{h},"format":"{fmt}","data":"{b64}"}}"#,
                page = img.page_number,
                idx = img.image_index,
                w = img.width,
                h = img.height,
                fmt = format_str,
                b64 = data_b64,
            ));
        }

        let _ = fs::remove_dir_all(&out_dir);

        Ok(format!("[{}]", json_items.join(",")))
    });

    match result {
        Ok(json) => match CString::new(json) {
            Ok(cs) => {
                *out_json = cs.into_raw();
                ErrorCode::Success as c_int
            }
            Err(_) => {
                set_last_error("JSON output contains null bytes");
                ErrorCode::SerializationError as c_int
            }
        },
        Err(msg) => {
            set_last_error(msg);
            ErrorCode::IoError as c_int
        }
    }
}
