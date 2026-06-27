use std::ffi::CStr;
use std::os::raw::{c_char, c_int};

use crate::page::PageHandle;
use crate::{clear_last_error, set_last_error, ErrorCode};

/// Opaque handle wrapping an `oxidize_pdf::Image`.
pub struct ImageHandle {
    pub(crate) inner: oxidize_pdf::Image,
}

/// Create an image from JPEG byte data.
///
/// # Safety
/// - `data` must be a valid pointer to `data_len` bytes of JPEG data.
/// - `out_handle` must be a valid pointer to receive the new handle.
/// - The returned handle must be freed with `oxidize_image_free`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_image_from_jpeg(
    data: *const u8,
    data_len: usize,
    out_handle: *mut *mut ImageHandle,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if data.is_null() || out_handle.is_null() {
            set_last_error("Null pointer provided to oxidize_image_from_jpeg");
            return ErrorCode::NullPointer as c_int;
        }
        *out_handle = std::ptr::null_mut();

        if data_len == 0 {
            set_last_error("Image data is empty (0 bytes)");
            return ErrorCode::PdfParseError as c_int;
        }

        let bytes = std::slice::from_raw_parts(data, data_len).to_vec();
        match oxidize_pdf::Image::from_jpeg_data(bytes) {
            Ok(img) => {
                *out_handle = Box::into_raw(Box::new(ImageHandle { inner: img }));
                ErrorCode::Success as c_int
            }
            Err(e) => {
                set_last_error(format!("Failed to create image from JPEG: {e}"));
                ErrorCode::PdfParseError as c_int
            }
        }
    })
}

/// Create an image from PNG byte data.
///
/// # Safety
/// - `data` must be a valid pointer to `data_len` bytes of PNG data.
/// - `out_handle` must be a valid pointer to receive the new handle.
/// - The returned handle must be freed with `oxidize_image_free`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_image_from_png(
    data: *const u8,
    data_len: usize,
    out_handle: *mut *mut ImageHandle,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if data.is_null() || out_handle.is_null() {
            set_last_error("Null pointer provided to oxidize_image_from_png");
            return ErrorCode::NullPointer as c_int;
        }
        *out_handle = std::ptr::null_mut();

        if data_len == 0 {
            set_last_error("Image data is empty (0 bytes)");
            return ErrorCode::PdfParseError as c_int;
        }

        let bytes = std::slice::from_raw_parts(data, data_len).to_vec();
        match oxidize_pdf::Image::from_png_data(bytes) {
            Ok(img) => {
                *out_handle = Box::into_raw(Box::new(ImageHandle { inner: img }));
                ErrorCode::Success as c_int
            }
            Err(e) => {
                set_last_error(format!("Failed to create image from PNG: {e}"));
                ErrorCode::PdfParseError as c_int
            }
        }
    })
}

/// Create an image from a file path (auto-detects JPEG/PNG/TIFF).
///
/// # Safety
/// - `path` must be a valid null-terminated UTF-8 string.
/// - `out_handle` must be a valid pointer to receive the new handle.
/// - The returned handle must be freed with `oxidize_image_free`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_image_from_file(
    path: *const c_char,
    out_handle: *mut *mut ImageHandle,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if path.is_null() || out_handle.is_null() {
            set_last_error("Null pointer provided to oxidize_image_from_file");
            return ErrorCode::NullPointer as c_int;
        }
        *out_handle = std::ptr::null_mut();

        let p = match CStr::from_ptr(path).to_str() {
            Ok(v) => v,
            Err(_) => {
                set_last_error("Invalid UTF-8 in file path");
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };
        match oxidize_pdf::Image::from_file(p) {
            Ok(img) => {
                *out_handle = Box::into_raw(Box::new(ImageHandle { inner: img }));
                ErrorCode::Success as c_int
            }
            Err(e) => {
                set_last_error(format!("Failed to load image from file: {e}"));
                ErrorCode::IoError as c_int
            }
        }
    })
}

/// Free an image handle.
///
/// # Safety
/// - `handle` must have been returned by `oxidize_image_from_jpeg`, `oxidize_image_from_png`, or `oxidize_image_from_file`.
/// - `handle` must not have been freed previously.
#[no_mangle]
pub unsafe extern "C" fn oxidize_image_free(handle: *mut ImageHandle) {
    crate::ffi_guard_unit(move || {
        if handle.is_null() {
            return;
        }
        drop(Box::from_raw(handle));
    })
}

/// Get the image width in pixels.
///
/// # Safety
/// - `handle` must be a valid image handle.
/// - `out_width` must be a valid pointer to a `u32`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_image_get_width(
    handle: *const ImageHandle,
    out_width: *mut u32,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if handle.is_null() || out_width.is_null() {
            set_last_error("Null pointer provided to oxidize_image_get_width");
            return ErrorCode::NullPointer as c_int;
        }
        *out_width = (*handle).inner.width();
        ErrorCode::Success as c_int
    })
}

/// Get the image height in pixels.
///
/// # Safety
/// - `handle` must be a valid image handle.
/// - `out_height` must be a valid pointer to a `u32`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_image_get_height(
    handle: *const ImageHandle,
    out_height: *mut u32,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if handle.is_null() || out_height.is_null() {
            set_last_error("Null pointer provided to oxidize_image_get_height");
            return ErrorCode::NullPointer as c_int;
        }
        *out_height = (*handle).inner.height();
        ErrorCode::Success as c_int
    })
}

/// Add an image to a page by name (clones the image internally).
///
/// # Safety
/// - `page` must be a valid page handle.
/// - `name` must be a valid null-terminated UTF-8 string.
/// - `image` must be a valid image handle.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_add_image(
    page: *mut PageHandle,
    name: *const c_char,
    image: *const ImageHandle,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() || name.is_null() || image.is_null() {
            set_last_error("Null pointer provided to oxidize_page_add_image");
            return ErrorCode::NullPointer as c_int;
        }
        let s = match CStr::from_ptr(name).to_str() {
            Ok(v) => v,
            Err(_) => {
                set_last_error("Invalid UTF-8 in image name");
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };
        (*page).inner.add_image(s, (*image).inner.clone());
        ErrorCode::Success as c_int
    })
}

/// Draw a previously added image at the specified position and dimensions.
///
/// # Safety
/// - `page` must be a valid page handle.
/// - `name` must be a valid null-terminated UTF-8 string matching a previously added image.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_draw_image(
    page: *mut PageHandle,
    name: *const c_char,
    x: f64,
    y: f64,
    width: f64,
    height: f64,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() || name.is_null() {
            set_last_error("Null pointer provided to oxidize_page_draw_image");
            return ErrorCode::NullPointer as c_int;
        }
        let s = match CStr::from_ptr(name).to_str() {
            Ok(v) => v,
            Err(_) => {
                set_last_error("Invalid UTF-8 in image name");
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };
        if let Err(e) = (*page).inner.draw_image(s, x, y, width, height) {
            set_last_error(format!("Failed to draw image: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
        ErrorCode::Success as c_int
    })
}

/// Draw a previously added image through the graphics context (GFX-023),
/// optionally masked by a soft mask. Emits `q`, a `width 0 0 height x y cm`
/// placement matrix, `/image_name Do`, and `Q`. When `mask_name` is non-null it
/// also brackets the draw with a luminosity-soft-mask ExtGState (`/GSx gs` …
/// reset to `/None`); the writer resolves the mask name to the indirect `/G`
/// reference of a Form XObject registered on the page via
/// `oxidize_page_add_form_xobject`.
///
/// The image must already be registered with `oxidize_page_add_image` and the
/// mask form (when given) with `oxidize_page_add_form_xobject`, before saving.
///
/// # Safety
/// - `page` must be a valid page handle.
/// - `image_name` must be a valid non-null, null-terminated UTF-8 C string
///   matching a previously added image.
/// - `mask_name` is nullable; when non-null it must be a valid null-terminated
///   UTF-8 C string naming a Form XObject registered on the page.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_draw_image_with_transparency(
    page: *mut PageHandle,
    image_name: *const c_char,
    x: f64,
    y: f64,
    width: f64,
    height: f64,
    mask_name: *const c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() || image_name.is_null() {
            set_last_error("Null pointer provided to oxidize_page_draw_image_with_transparency");
            return ErrorCode::NullPointer as c_int;
        }
        let image = match CStr::from_ptr(image_name).to_str() {
            Ok(v) => v,
            Err(_) => {
                set_last_error("Invalid UTF-8 in image name");
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };
        let mask: Option<String> = if mask_name.is_null() {
            None
        } else {
            match CStr::from_ptr(mask_name).to_str() {
                Ok(v) => Some(v.to_owned()),
                Err(_) => {
                    set_last_error("Invalid UTF-8 in soft mask name");
                    return ErrorCode::InvalidUtf8 as c_int;
                }
            }
        };
        (*page).inner.graphics().draw_image_with_transparency(
            image,
            x,
            y,
            width,
            height,
            mask.as_deref(),
        );
        ErrorCode::Success as c_int
    })
}

#[cfg(test)]
mod image_transparency_ffi_tests {
    use super::*;
    use crate::page::{oxidize_page_create, oxidize_page_free};
    use std::ffi::CString;

    #[test]
    fn draw_image_with_transparency_with_mask_emits_cm_do_and_gs() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            let img = CString::new("Img1").unwrap();
            let mask = CString::new("Mask1").unwrap();
            let rc = oxidize_page_draw_image_with_transparency(
                page,
                img.as_ptr(),
                50.0,
                600.0,
                200.0,
                100.0,
                mask.as_ptr(),
            );
            assert_eq!(rc, 0, "expected ErrorCode::Success (0)");
            let ops = (*page).inner.graphics().operations();
            assert!(
                ops.contains("200.00 0.00 0.00 100.00 50.00 600.00 cm"),
                "expected image placement matrix\n{ops}"
            );
            assert!(ops.contains("/Img1 Do"), "expected image invocation\n{ops}");
            assert!(ops.contains(" gs"), "expected soft-mask gs operator\n{ops}");
            oxidize_page_free(page);
        }
    }

    #[test]
    fn draw_image_with_transparency_no_mask_emits_cm_do_without_gs() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            let img = CString::new("Img1").unwrap();
            let rc = oxidize_page_draw_image_with_transparency(
                page,
                img.as_ptr(),
                0.0,
                0.0,
                10.0,
                10.0,
                std::ptr::null(),
            );
            assert_eq!(rc, 0, "expected ErrorCode::Success (0)");
            let ops = (*page).inner.graphics().operations();
            assert!(ops.contains("/Img1 Do"), "expected image invocation\n{ops}");
            assert!(
                !ops.contains(" gs"),
                "no mask supplied → no soft-mask gs operator\n{ops}"
            );
            oxidize_page_free(page);
        }
    }

    #[test]
    fn draw_image_with_transparency_null_image_returns_null_pointer_error() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            let rc = oxidize_page_draw_image_with_transparency(
                page,
                std::ptr::null(),
                0.0,
                0.0,
                10.0,
                10.0,
                std::ptr::null(),
            );
            assert_eq!(rc, 1, "expected ErrorCode::NullPointer (1)");
            oxidize_page_free(page);
        }
    }
}
