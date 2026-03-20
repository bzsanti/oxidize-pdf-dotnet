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
}

/// Free an image handle.
///
/// # Safety
/// - `handle` must have been returned by `oxidize_image_from_jpeg`, `oxidize_image_from_png`, or `oxidize_image_from_file`.
/// - `handle` must not have been freed previously.
#[no_mangle]
pub unsafe extern "C" fn oxidize_image_free(handle: *mut ImageHandle) {
    if handle.is_null() {
        return;
    }
    drop(Box::from_raw(handle));
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
    clear_last_error();
    if handle.is_null() || out_width.is_null() {
        set_last_error("Null pointer provided to oxidize_image_get_width");
        return ErrorCode::NullPointer as c_int;
    }
    *out_width = (*handle).inner.width();
    ErrorCode::Success as c_int
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
    clear_last_error();
    if handle.is_null() || out_height.is_null() {
        set_last_error("Null pointer provided to oxidize_image_get_height");
        return ErrorCode::NullPointer as c_int;
    }
    *out_height = (*handle).inner.height();
    ErrorCode::Success as c_int
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
}
