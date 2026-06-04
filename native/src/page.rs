use std::ffi::CStr;
use std::os::raw::{c_char, c_int};

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

/// Set the page rotation in degrees (0, 90, 180, or 270).
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_rotation(
    handle: *mut PageHandle,
    degrees: c_int,
) -> c_int {
    clear_last_error();
    if handle.is_null() {
        set_last_error("Null pointer provided to oxidize_page_set_rotation");
        return ErrorCode::NullPointer as c_int;
    }
    (*handle).inner.set_rotation(degrees);
    ErrorCode::Success as c_int
}

/// Get the page rotation in degrees.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
/// - `out_degrees` must be a valid pointer to a `c_int`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_get_rotation(
    handle: *const PageHandle,
    out_degrees: *mut c_int,
) -> c_int {
    clear_last_error();
    if handle.is_null() || out_degrees.is_null() {
        set_last_error("Null pointer provided to oxidize_page_get_rotation");
        return ErrorCode::NullPointer as c_int;
    }
    *out_degrees = (*handle).inner.get_rotation() as c_int;
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

/// Get all four page margins in PDF points.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
/// - All out pointers must be valid non-null `f64` pointers.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_get_margins(
    handle: *const PageHandle,
    out_top: *mut f64,
    out_right: *mut f64,
    out_bottom: *mut f64,
    out_left: *mut f64,
) -> c_int {
    clear_last_error();
    if handle.is_null()
        || out_top.is_null()
        || out_right.is_null()
        || out_bottom.is_null()
        || out_left.is_null()
    {
        set_last_error("Null pointer provided to oxidize_page_get_margins");
        return ErrorCode::NullPointer as c_int;
    }
    let m = (*handle).inner.margins();
    *out_top = m.top;
    *out_right = m.right;
    *out_bottom = m.bottom;
    *out_left = m.left;
    ErrorCode::Success as c_int
}

// ── Color space registration ──────────────────────────────────────────────────

/// Register a CalGray color space under `name` on this page.
///
/// Required before drawing with `oxidize_page_set_fill_color_cal_gray_named`
/// or `oxidize_page_set_stroke_color_cal_gray_named`.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
/// - `name` must be a valid non-null, null-terminated UTF-8 C string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_add_color_space_cal_gray(
    page: *mut PageHandle,
    name: *const c_char,
    wp_x: f64,
    wp_y: f64,
    wp_z: f64,
    bp_x: f64,
    bp_y: f64,
    bp_z: f64,
    gamma: f64,
) -> c_int {
    clear_last_error();
    if page.is_null() || name.is_null() {
        set_last_error("Null pointer provided to oxidize_page_add_color_space_cal_gray");
        return ErrorCode::NullPointer as c_int;
    }
    let name_str = match CStr::from_ptr(name).to_str() {
        Ok(s) => s.to_owned(),
        Err(_) => {
            set_last_error("Invalid UTF-8 in color space name");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    use oxidize_pdf::graphics::{CalGrayColorSpace, PageColorSpace};
    let cs = CalGrayColorSpace::new()
        .with_white_point([wp_x, wp_y, wp_z])
        .with_black_point([bp_x, bp_y, bp_z])
        .with_gamma(gamma);
    match (*page)
        .inner
        .add_color_space(name_str, PageColorSpace::from(&cs))
    {
        Ok(()) => ErrorCode::Success as c_int,
        Err(e) => {
            set_last_error(format!("add_color_space failed: {e}"));
            ErrorCode::InvalidArgument as c_int
        }
    }
}

/// Register a CalRGB color space under `name` on this page.
///
/// Required before drawing with `oxidize_page_set_fill_color_cal_rgb_named`
/// or `oxidize_page_set_stroke_color_cal_rgb_named`.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
/// - `name` must be a valid non-null, null-terminated UTF-8 C string.
#[allow(clippy::too_many_arguments)]
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_add_color_space_cal_rgb(
    page: *mut PageHandle,
    name: *const c_char,
    wp_x: f64,
    wp_y: f64,
    wp_z: f64,
    bp_x: f64,
    bp_y: f64,
    bp_z: f64,
    gamma_r: f64,
    gamma_g: f64,
    gamma_b: f64,
    m0: f64,
    m1: f64,
    m2: f64,
    m3: f64,
    m4: f64,
    m5: f64,
    m6: f64,
    m7: f64,
    m8: f64,
) -> c_int {
    clear_last_error();
    if page.is_null() || name.is_null() {
        set_last_error("Null pointer provided to oxidize_page_add_color_space_cal_rgb");
        return ErrorCode::NullPointer as c_int;
    }
    let name_str = match CStr::from_ptr(name).to_str() {
        Ok(s) => s.to_owned(),
        Err(_) => {
            set_last_error("Invalid UTF-8 in color space name");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    use oxidize_pdf::graphics::{CalRgbColorSpace, PageColorSpace};
    let cs = CalRgbColorSpace::new()
        .with_white_point([wp_x, wp_y, wp_z])
        .with_black_point([bp_x, bp_y, bp_z])
        .with_gamma([gamma_r, gamma_g, gamma_b])
        .with_matrix([m0, m1, m2, m3, m4, m5, m6, m7, m8]);
    match (*page)
        .inner
        .add_color_space(name_str, PageColorSpace::from(&cs))
    {
        Ok(()) => ErrorCode::Success as c_int,
        Err(e) => {
            set_last_error(format!("add_color_space failed: {e}"));
            ErrorCode::InvalidArgument as c_int
        }
    }
}

/// Register a Lab color space under `name` on this page.
///
/// Required before drawing with `oxidize_page_set_fill_color_lab_named`
/// or `oxidize_page_set_stroke_color_lab_named`.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
/// - `name` must be a valid non-null, null-terminated UTF-8 C string.
#[allow(clippy::too_many_arguments)]
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_add_color_space_lab(
    page: *mut PageHandle,
    name: *const c_char,
    wp_x: f64,
    wp_y: f64,
    wp_z: f64,
    bp_x: f64,
    bp_y: f64,
    bp_z: f64,
    range_amin: f64,
    range_amax: f64,
    range_bmin: f64,
    range_bmax: f64,
) -> c_int {
    clear_last_error();
    if page.is_null() || name.is_null() {
        set_last_error("Null pointer provided to oxidize_page_add_color_space_lab");
        return ErrorCode::NullPointer as c_int;
    }
    let name_str = match CStr::from_ptr(name).to_str() {
        Ok(s) => s.to_owned(),
        Err(_) => {
            set_last_error("Invalid UTF-8 in color space name");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    use oxidize_pdf::graphics::{LabColorSpace, PageColorSpace};
    let cs = LabColorSpace::new()
        .with_white_point([wp_x, wp_y, wp_z])
        .with_black_point([bp_x, bp_y, bp_z])
        .with_range(range_amin, range_amax, range_bmin, range_bmax);
    match (*page)
        .inner
        .add_color_space(name_str, PageColorSpace::from(&cs))
    {
        Ok(()) => ErrorCode::Success as c_int,
        Err(e) => {
            set_last_error(format!("add_color_space failed: {e}"));
            ErrorCode::InvalidArgument as c_int
        }
    }
}

#[cfg(test)]
mod add_color_space_ffi_tests {
    use super::*;

    #[test]
    fn add_color_space_cal_gray_null_page_returns_error() {
        unsafe {
            let name = std::ffi::CString::new("CS1").unwrap();
            let result = oxidize_page_add_color_space_cal_gray(
                std::ptr::null_mut(),
                name.as_ptr(),
                0.9505,
                1.0,
                1.0890,
                0.0,
                0.0,
                0.0,
                1.0,
            );
            assert_eq!(result, 1, "expected ErrorCode::NullPointer (1)");
        }
    }

    #[test]
    fn add_color_space_cal_gray_valid_returns_success() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            assert!(!page.is_null());
            let name = std::ffi::CString::new("CS1").unwrap();
            let result = oxidize_page_add_color_space_cal_gray(
                page,
                name.as_ptr(),
                0.9505,
                1.0,
                1.0890,
                0.0,
                0.0,
                0.0,
                1.0,
            );
            assert_eq!(result, 0, "expected ErrorCode::Success (0)");
            oxidize_page_free(page);
        }
    }
}
