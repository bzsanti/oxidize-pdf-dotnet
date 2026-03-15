use std::cell::RefCell;
use std::ffi::CString;
use std::os::raw::{c_char, c_int};
use std::ptr;

// ── Sub-modules ───────────────────────────────────────────────────────────────

pub mod document;
pub mod graphics;
pub mod operations;
pub mod page;
pub mod parser;
pub mod security;
pub mod text;
pub mod types;

// ── Error infrastructure ──────────────────────────────────────────────────────

// Thread-local storage for the last error message.
thread_local! {
    static LAST_ERROR: RefCell<Option<String>> = const { RefCell::new(None) };
}

/// Store an error message for later retrieval via `oxidize_get_last_error`.
pub(crate) fn set_last_error<S: Into<String>>(msg: S) {
    LAST_ERROR.with(|e| *e.borrow_mut() = Some(msg.into()));
}

/// Clear any previously stored error message.
pub(crate) fn clear_last_error() {
    LAST_ERROR.with(|e| *e.borrow_mut() = None);
}

/// Find the nearest valid UTF-8 char boundary at or after `index`.
/// Prevents panics when slicing strings at arbitrary byte positions.
pub(crate) fn find_char_boundary(s: &str, mut index: usize) -> usize {
    if index >= s.len() {
        return s.len();
    }
    while index < s.len() && !s.is_char_boundary(index) {
        index += 1;
    }
    index
}

/// Error codes returned by all FFI functions.
#[repr(C)]
pub enum ErrorCode {
    Success = 0,
    NullPointer = 1,
    InvalidUtf8 = 2,
    PdfParseError = 3,
    AllocationError = 4,
    SerializationError = 5,
    IoError = 6,
    EncryptionError = 7,
    PermissionError = 8,
}

// ── Free helpers ──────────────────────────────────────────────────────────────

/// Free a C string previously allocated by an `oxidize_*` function.
///
/// # Safety
/// - `ptr` must have been returned by a previous call to an `oxidize_*` function.
/// - `ptr` must not have been freed previously.
/// - After calling this function, `ptr` must not be used again.
#[no_mangle]
pub unsafe extern "C" fn oxidize_free_string(ptr: *mut c_char) {
    if ptr.is_null() {
        return;
    }
    drop(CString::from_raw(ptr));
}

/// Free a byte array previously allocated by an `oxidize_*` function.
///
/// # Safety
/// - `ptr` must have been set by a previous call to an `oxidize_*` function that returns bytes.
/// - `len` must match the length reported by that same call.
/// - `ptr` must not have been freed previously.
/// - After calling this function, `ptr` must not be used again.
#[no_mangle]
pub unsafe extern "C" fn oxidize_free_bytes(ptr: *mut u8, len: usize) {
    if ptr.is_null() {
        return;
    }
    drop(Vec::from_raw_parts(ptr, len, len));
}

// ── Introspection ─────────────────────────────────────────────────────────────

/// Retrieve the last error message set by any `oxidize_*` function on this thread.
///
/// # Safety
/// - `out_error` must be a valid pointer to a mutable pointer location.
/// - The returned string must be freed with `oxidize_free_string`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_get_last_error(out_error: *mut *mut c_char) -> c_int {
    if out_error.is_null() {
        return ErrorCode::NullPointer as c_int;
    }

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
        _ => ErrorCode::Success as c_int,
    }
}

/// Return the library version string.
///
/// # Safety
/// - `out_version` must be a valid pointer to a mutable pointer location.
/// - The returned string must be freed with `oxidize_free_string`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_version(out_version: *mut *mut c_char) -> c_int {
    if out_version.is_null() {
        return ErrorCode::NullPointer as c_int;
    }

    *out_version = ptr::null_mut();

    let version = format!("oxidize-pdf-ffi v{}", env!("CARGO_PKG_VERSION"));

    let c_string = match CString::new(version) {
        Ok(s) => s,
        Err(_) => return ErrorCode::InvalidUtf8 as c_int,
    };

    *out_version = c_string.into_raw();
    ErrorCode::Success as c_int
}

// ── Tests ─────────────────────────────────────────────────────────────────────

#[cfg(test)]
mod tests {
    use super::*;
    use std::ffi::CStr;

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
        let result = unsafe { parser::oxidize_extract_text(ptr::null(), 0, ptr::null_mut()) };
        assert_eq!(result, ErrorCode::NullPointer as c_int);
    }
}
