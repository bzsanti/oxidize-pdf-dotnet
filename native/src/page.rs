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

/// Register an ICC color space with an embedded profile under `name`.
///
/// `data` / `data_len`: raw ICC binary bytes. Must be non-null and non-empty.
/// `color_space_kind`: 1=Gray, 3=Rgb, 4=Cmyk (maps to `IccColorSpace` variants).
///
/// This is the .NET-superset path (embedded profile); the Python bridge
/// does not expose this. For the inline ICCBased path (no binary),
/// use `oxidize_page_add_color_space_icc_based` instead.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
/// - `name` must be a valid non-null, null-terminated UTF-8 C string.
/// - `data` must be a valid pointer to `data_len` bytes, or null when
///   `data_len` is 0 (the function rejects the null/empty case).
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_add_icc_color_space(
    page: *mut PageHandle,
    name: *const c_char,
    data: *const u8,
    data_len: usize,
    color_space_kind: c_int,
) -> c_int {
    clear_last_error();
    if page.is_null() || name.is_null() {
        set_last_error("Null pointer provided to oxidize_page_add_icc_color_space");
        return ErrorCode::NullPointer as c_int;
    }
    if data.is_null() || data_len == 0 {
        set_last_error("ICC profile data must not be empty");
        return ErrorCode::InvalidArgument as c_int;
    }
    let name_str = match CStr::from_ptr(name).to_str() {
        Ok(s) => s.to_owned(),
        Err(_) => {
            set_last_error("Invalid UTF-8 in ICC color space name");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    let icc_cs = match color_space_kind {
        1 => oxidize_pdf::graphics::IccColorSpace::Gray,
        3 => oxidize_pdf::graphics::IccColorSpace::Rgb,
        4 => oxidize_pdf::graphics::IccColorSpace::Cmyk,
        _ => {
            set_last_error(format!("Unknown ICC color space kind: {color_space_kind}"));
            return ErrorCode::InvalidArgument as c_int;
        }
    };
    let profile_data = std::slice::from_raw_parts(data, data_len).to_vec();
    let profile = oxidize_pdf::graphics::IccProfile::new(name_str.clone(), profile_data, icc_cs);
    match (*page).inner.add_icc_color_space(name_str, &profile) {
        Ok(()) => ErrorCode::Success as c_int,
        Err(e) => {
            set_last_error(format!("add_icc_color_space failed: {e}"));
            ErrorCode::InvalidArgument as c_int
        }
    }
}

/// Register an inline ICCBased color space (N components + Alternate device
/// space, no embedded binary) under `name`. Mirrors the Python bridge's
/// `PageColorSpace.icc_based(n, alternate)`. For the embedded-profile path use
/// `oxidize_page_add_icc_color_space`.
///
/// `n`: 1=Gray, 3=RGB/Lab, 4=CMYK — any other value returns `InvalidArgument`.
/// `alternate`: e.g. "DeviceRGB" / "DeviceGray" / "DeviceCMYK".
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
/// - `name` must be a valid non-null, null-terminated UTF-8 C string.
/// - `alternate` must be a valid non-null, null-terminated UTF-8 C string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_add_color_space_icc_based(
    page: *mut PageHandle,
    name: *const c_char,
    n: c_int,
    alternate: *const c_char,
) -> c_int {
    clear_last_error();
    if page.is_null() || name.is_null() || alternate.is_null() {
        set_last_error("Null pointer provided to oxidize_page_add_color_space_icc_based");
        return ErrorCode::NullPointer as c_int;
    }
    if !matches!(n, 1 | 3 | 4) {
        set_last_error(format!(
            "Invalid N value {n}: must be 1 (Gray), 3 (RGB/Lab), or 4 (CMYK)"
        ));
        return ErrorCode::InvalidArgument as c_int;
    }
    let name_str = match CStr::from_ptr(name).to_str() {
        Ok(s) => s.to_owned(),
        Err(_) => {
            set_last_error("Invalid UTF-8 in color space name");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    let alternate_str = match CStr::from_ptr(alternate).to_str() {
        Ok(s) => s.to_owned(),
        Err(_) => {
            set_last_error("Invalid UTF-8 in alternate color space name");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    use oxidize_pdf::graphics::{PageColorSpace, ParameterisedFamily};
    use oxidize_pdf::objects::{Dictionary, Object};
    let mut params = Dictionary::new();
    params.set("N", Object::Integer(n as i64));
    params.set("Alternate", Object::Name(alternate_str));
    let cs = PageColorSpace::Parameterised {
        family: ParameterisedFamily::IccBased,
        params,
    };
    match (*page).inner.add_color_space(name_str, cs) {
        Ok(()) => ErrorCode::Success as c_int,
        Err(e) => {
            set_last_error(format!("add_color_space (icc_based) failed: {e}"));
            ErrorCode::InvalidArgument as c_int
        }
    }
}

#[cfg(test)]
mod add_icc_color_space_ffi_tests {
    use super::*;

    #[test]
    fn add_icc_color_space_null_page_returns_error() {
        unsafe {
            let name = std::ffi::CString::new("ICCGray").unwrap();
            let data = [0u8; 64];
            // IccColorSpace::Gray = 1
            let result = oxidize_page_add_icc_color_space(
                std::ptr::null_mut(),
                name.as_ptr(),
                data.as_ptr(),
                64,
                1,
            );
            assert_eq!(result, 1, "expected ErrorCode::NullPointer (1)");
        }
    }

    #[test]
    fn add_icc_color_space_empty_data_returns_invalid_argument() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            assert!(!page.is_null());
            let name = std::ffi::CString::new("ICCGray").unwrap();
            let result =
                oxidize_page_add_icc_color_space(page, name.as_ptr(), std::ptr::null(), 0, 1);
            assert_eq!(result, 9, "empty ICC data must return InvalidArgument (9)");
            oxidize_page_free(page);
        }
    }
}

#[cfg(test)]
mod add_color_space_icc_based_ffi_tests {
    use super::*;

    #[test]
    fn icc_based_valid_n3_device_rgb_returns_success() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            assert!(!page.is_null());
            let name = std::ffi::CString::new("ICCCS1").unwrap();
            let alternate = std::ffi::CString::new("DeviceRGB").unwrap();
            let result =
                oxidize_page_add_color_space_icc_based(page, name.as_ptr(), 3, alternate.as_ptr());
            assert_eq!(result, 0, "expected ErrorCode::Success (0)");
            oxidize_page_free(page);
        }
    }

    #[test]
    fn icc_based_null_page_returns_null_pointer() {
        unsafe {
            let name = std::ffi::CString::new("ICCCS1").unwrap();
            let alternate = std::ffi::CString::new("DeviceRGB").unwrap();
            let result = oxidize_page_add_color_space_icc_based(
                std::ptr::null_mut(),
                name.as_ptr(),
                3,
                alternate.as_ptr(),
            );
            assert_eq!(result, 1, "expected ErrorCode::NullPointer (1)");
        }
    }

    #[test]
    fn icc_based_invalid_n_returns_invalid_argument() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            assert!(!page.is_null());
            let name = std::ffi::CString::new("ICCCS1").unwrap();
            let alternate = std::ffi::CString::new("DeviceRGB").unwrap();
            let result =
                oxidize_page_add_color_space_icc_based(page, name.as_ptr(), 2, alternate.as_ptr());
            assert_eq!(result, 9, "n=2 must return InvalidArgument (9)");
            oxidize_page_free(page);
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
