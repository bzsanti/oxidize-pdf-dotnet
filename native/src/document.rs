use std::ffi::CStr;
use std::os::raw::{c_char, c_int};
use std::ptr;

use crate::{clear_last_error, set_last_error, ErrorCode};

/// Opaque handle wrapping an `oxidize_pdf::Document`.
pub struct DocumentHandle {
    pub(crate) inner: oxidize_pdf::Document,
}

/// Create a new empty document.
///
/// # Safety
/// - Returns a heap-allocated `DocumentHandle` pointer that must be freed with
///   `oxidize_document_free`.
/// - Returns null on allocation failure (sets last error).
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_create() -> *mut DocumentHandle {
    clear_last_error();
    let handle = Box::new(DocumentHandle {
        inner: oxidize_pdf::Document::new(),
    });
    Box::into_raw(handle)
}

/// Free a document handle previously created by `oxidize_document_create`.
///
/// # Safety
/// - `handle` must have been returned by `oxidize_document_create`.
/// - `handle` must not have been freed previously.
/// - After calling this function, `handle` must not be used again.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_free(handle: *mut DocumentHandle) {
    if handle.is_null() {
        return;
    }
    drop(Box::from_raw(handle));
}

/// Set the document title metadata.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `text` must be a valid null-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_set_title(
    handle: *mut DocumentHandle,
    text: *const c_char,
) -> c_int {
    clear_last_error();
    if handle.is_null() || text.is_null() {
        set_last_error("Null pointer provided to oxidize_document_set_title");
        return ErrorCode::NullPointer as c_int;
    }
    let s = match CStr::from_ptr(text).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in title");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    (*handle).inner.set_title(s);
    ErrorCode::Success as c_int
}

/// Set the document author metadata.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `text` must be a valid null-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_set_author(
    handle: *mut DocumentHandle,
    text: *const c_char,
) -> c_int {
    clear_last_error();
    if handle.is_null() || text.is_null() {
        set_last_error("Null pointer provided to oxidize_document_set_author");
        return ErrorCode::NullPointer as c_int;
    }
    let s = match CStr::from_ptr(text).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in author");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    (*handle).inner.set_author(s);
    ErrorCode::Success as c_int
}

/// Set the document subject metadata.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `text` must be a valid null-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_set_subject(
    handle: *mut DocumentHandle,
    text: *const c_char,
) -> c_int {
    clear_last_error();
    if handle.is_null() || text.is_null() {
        set_last_error("Null pointer provided to oxidize_document_set_subject");
        return ErrorCode::NullPointer as c_int;
    }
    let s = match CStr::from_ptr(text).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in subject");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    (*handle).inner.set_subject(s);
    ErrorCode::Success as c_int
}

/// Set the document keywords metadata.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `text` must be a valid null-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_set_keywords(
    handle: *mut DocumentHandle,
    text: *const c_char,
) -> c_int {
    clear_last_error();
    if handle.is_null() || text.is_null() {
        set_last_error("Null pointer provided to oxidize_document_set_keywords");
        return ErrorCode::NullPointer as c_int;
    }
    let s = match CStr::from_ptr(text).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in keywords");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    (*handle).inner.set_keywords(s);
    ErrorCode::Success as c_int
}

/// Set the document creator metadata.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `text` must be a valid null-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_set_creator(
    handle: *mut DocumentHandle,
    text: *const c_char,
) -> c_int {
    clear_last_error();
    if handle.is_null() || text.is_null() {
        set_last_error("Null pointer provided to oxidize_document_set_creator");
        return ErrorCode::NullPointer as c_int;
    }
    let s = match CStr::from_ptr(text).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in creator");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    (*handle).inner.set_creator(s);
    ErrorCode::Success as c_int
}

/// Add a page to the document. The page is cloned internally; the caller retains ownership.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `page_handle` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_add_page(
    handle: *mut DocumentHandle,
    page_handle: *const crate::page::PageHandle,
) -> c_int {
    clear_last_error();
    if handle.is_null() || page_handle.is_null() {
        set_last_error("Null pointer provided to oxidize_document_add_page");
        return ErrorCode::NullPointer as c_int;
    }
    (*handle).inner.add_page((*page_handle).inner.clone());
    ErrorCode::Success as c_int
}

/// Serialize the document to PDF bytes.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `out_bytes` must be a valid pointer to a mutable pointer; on success it will point to a
///   heap-allocated byte array that must be freed with `oxidize_free_bytes`.
/// - `out_len` must be a valid pointer to a `usize` that will receive the byte count.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_save_to_bytes(
    handle: *mut DocumentHandle,
    out_bytes: *mut *mut u8,
    out_len: *mut usize,
) -> c_int {
    clear_last_error();
    if handle.is_null() || out_bytes.is_null() || out_len.is_null() {
        set_last_error("Null pointer provided to oxidize_document_save_to_bytes");
        return ErrorCode::NullPointer as c_int;
    }
    *out_bytes = ptr::null_mut();
    *out_len = 0;

    let bytes = match (*handle).inner.to_bytes() {
        Ok(b) => b,
        Err(e) => {
            set_last_error(format!("Failed to serialize document: {e}"));
            return ErrorCode::IoError as c_int;
        }
    };

    let len = bytes.len();
    let mut boxed = bytes.into_boxed_slice();
    *out_bytes = boxed.as_mut_ptr();
    *out_len = len;
    std::mem::forget(boxed);

    ErrorCode::Success as c_int
}

/// Get the number of pages in the document.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `out_count` must be a valid pointer to a `usize`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_page_count(
    handle: *const DocumentHandle,
    out_count: *mut usize,
) -> c_int {
    clear_last_error();
    if handle.is_null() || out_count.is_null() {
        set_last_error("Null pointer provided to oxidize_document_page_count");
        return ErrorCode::NullPointer as c_int;
    }
    *out_count = (*handle).inner.page_count();
    ErrorCode::Success as c_int
}

/// Register a custom font from byte data (e.g., TTF/OTF).
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `name` must be a valid null-terminated UTF-8 string.
/// - `font_bytes` must be a valid pointer to `font_len` bytes.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_add_font_from_bytes(
    handle: *mut DocumentHandle,
    name: *const c_char,
    font_bytes: *const u8,
    font_len: usize,
) -> c_int {
    clear_last_error();
    if handle.is_null() || name.is_null() || font_bytes.is_null() {
        set_last_error("Null pointer provided to oxidize_document_add_font_from_bytes");
        return ErrorCode::NullPointer as c_int;
    }
    if font_len == 0 {
        set_last_error("Font data is empty (0 bytes)");
        return ErrorCode::IoError as c_int;
    }
    let font_name = match CStr::from_ptr(name).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in font name");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    let data = std::slice::from_raw_parts(font_bytes, font_len).to_vec();
    if let Err(e) = (*handle).inner.add_font_from_bytes(font_name, data) {
        set_last_error(format!("Failed to add font: {e}"));
        return ErrorCode::IoError as c_int;
    }
    ErrorCode::Success as c_int
}
