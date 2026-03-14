use std::os::raw::c_int;

use crate::types::PagePreset;
use crate::{clear_last_error, set_last_error, ErrorCode};

/// Opaque handle wrapping an `oxidize_pdf::Page`.
pub struct PageHandle {
    pub(crate) inner: oxidize_pdf::Page,
}

/// Create a new page with explicit dimensions (in PDF points).
///
/// # Safety
/// - Returns a heap-allocated `PageHandle` pointer that must be freed with `oxidize_page_free`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_create(width: f64, height: f64) -> *mut PageHandle {
    clear_last_error();
    let handle = Box::new(PageHandle {
        inner: oxidize_pdf::Page::new(width, height),
    });
    Box::into_raw(handle)
}

/// Create a new page from a size preset.
///
/// # Safety
/// - Returns a heap-allocated `PageHandle` pointer that must be freed with `oxidize_page_free`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_create_preset(preset: PagePreset) -> *mut PageHandle {
    clear_last_error();
    let page = match preset {
        PagePreset::A4 => oxidize_pdf::Page::a4(),
        PagePreset::A4Landscape => oxidize_pdf::Page::a4_landscape(),
        PagePreset::Letter => oxidize_pdf::Page::letter(),
        PagePreset::LetterLandscape => oxidize_pdf::Page::letter_landscape(),
        PagePreset::Legal => oxidize_pdf::Page::legal(),
        PagePreset::LegalLandscape => oxidize_pdf::Page::legal_landscape(),
    };
    let handle = Box::new(PageHandle { inner: page });
    Box::into_raw(handle)
}

/// Free a page handle previously created by `oxidize_page_create` or
/// `oxidize_page_create_preset`.
///
/// # Safety
/// - `handle` must have been returned by `oxidize_page_create` or `oxidize_page_create_preset`.
/// - `handle` must not have been freed previously.
/// - After calling this function, `handle` must not be used again.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_free(handle: *mut PageHandle) {
    if handle.is_null() {
        return;
    }
    drop(Box::from_raw(handle));
}

/// Set page margins.  All values are in PDF points.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_margins(
    handle: *mut PageHandle,
    top: f64,
    right: f64,
    bottom: f64,
    left: f64,
) -> c_int {
    clear_last_error();
    if handle.is_null() {
        set_last_error("Null pointer provided to oxidize_page_set_margins");
        return ErrorCode::NullPointer as c_int;
    }
    (*handle).inner.set_margins(left, right, top, bottom);
    ErrorCode::Success as c_int
}

/// Get the page width in PDF points.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
/// - `out_value` must be a valid pointer to an `f64`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_get_width(
    handle: *const PageHandle,
    out_value: *mut f64,
) -> c_int {
    clear_last_error();
    if handle.is_null() || out_value.is_null() {
        set_last_error("Null pointer provided to oxidize_page_get_width");
        return ErrorCode::NullPointer as c_int;
    }
    *out_value = (*handle).inner.width();
    ErrorCode::Success as c_int
}

/// Get the page height in PDF points.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
/// - `out_value` must be a valid pointer to an `f64`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_get_height(
    handle: *const PageHandle,
    out_value: *mut f64,
) -> c_int {
    clear_last_error();
    if handle.is_null() || out_value.is_null() {
        set_last_error("Null pointer provided to oxidize_page_get_height");
        return ErrorCode::NullPointer as c_int;
    }
    *out_value = (*handle).inner.height();
    ErrorCode::Success as c_int
}
