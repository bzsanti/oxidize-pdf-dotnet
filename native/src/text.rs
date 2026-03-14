use std::ffi::CStr;
use std::os::raw::{c_char, c_int};

use crate::page::PageHandle;
use crate::types::StandardFont;
use crate::{clear_last_error, set_last_error, ErrorCode};

/// Set the current font and size on a page.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_font(
    page: *mut PageHandle,
    font: StandardFont,
    size: f64,
) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_set_font");
        return ErrorCode::NullPointer as c_int;
    }
    (*page).inner.text().set_font(font.to_oxidize(), size);
    ErrorCode::Success as c_int
}

/// Set the text fill color using RGB components (each in 0.0–1.0).
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_text_color_rgb(
    page: *mut PageHandle,
    r: f64,
    g: f64,
    b: f64,
) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_set_text_color_rgb");
        return ErrorCode::NullPointer as c_int;
    }
    (*page)
        .inner
        .text()
        .set_fill_color(oxidize_pdf::Color::rgb(r, g, b));
    ErrorCode::Success as c_int
}

/// Set the text fill color using a gray value (0.0 = black, 1.0 = white).
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_text_color_gray(
    page: *mut PageHandle,
    value: f64,
) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_set_text_color_gray");
        return ErrorCode::NullPointer as c_int;
    }
    (*page)
        .inner
        .text()
        .set_fill_color(oxidize_pdf::Color::gray(value));
    ErrorCode::Success as c_int
}

/// Set the text fill color using CMYK components (each in 0.0–1.0).
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_text_color_cmyk(
    page: *mut PageHandle,
    c: f64,
    m: f64,
    y: f64,
    k: f64,
) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_set_text_color_cmyk");
        return ErrorCode::NullPointer as c_int;
    }
    (*page)
        .inner
        .text()
        .set_fill_color(oxidize_pdf::Color::cmyk(c, m, y, k));
    ErrorCode::Success as c_int
}

/// Set character spacing for subsequent text operations.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_character_spacing(
    page: *mut PageHandle,
    spacing: f64,
) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_set_character_spacing");
        return ErrorCode::NullPointer as c_int;
    }
    (*page).inner.text().set_character_spacing(spacing);
    ErrorCode::Success as c_int
}

/// Set word spacing for subsequent text operations.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_word_spacing(
    page: *mut PageHandle,
    spacing: f64,
) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_set_word_spacing");
        return ErrorCode::NullPointer as c_int;
    }
    (*page).inner.text().set_word_spacing(spacing);
    ErrorCode::Success as c_int
}

/// Set text leading (line spacing) for subsequent text operations.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_leading(
    page: *mut PageHandle,
    leading: f64,
) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_set_leading");
        return ErrorCode::NullPointer as c_int;
    }
    (*page).inner.text().set_leading(leading);
    ErrorCode::Success as c_int
}

/// Write text at the given position on the page.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
/// - `text` must be a valid null-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_text_at(
    page: *mut PageHandle,
    x: f64,
    y: f64,
    text: *const c_char,
) -> c_int {
    clear_last_error();
    if page.is_null() || text.is_null() {
        set_last_error("Null pointer provided to oxidize_page_text_at");
        return ErrorCode::NullPointer as c_int;
    }
    let s = match CStr::from_ptr(text).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in text argument");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    if let Err(e) = (*page).inner.text().at(x, y).write(s) {
        set_last_error(format!("Failed to write text: {e}"));
        return ErrorCode::PdfParseError as c_int;
    }
    ErrorCode::Success as c_int
}
