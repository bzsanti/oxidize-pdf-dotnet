use std::ffi::CStr;
use std::os::raw::{c_char, c_int};

use crate::page::PageHandle;
use crate::types::{StandardFont, TextAlign, TextRenderingMode};
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
pub unsafe extern "C" fn oxidize_page_set_leading(page: *mut PageHandle, leading: f64) -> c_int {
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

/// Set a custom (embedded) font on a page by name.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
/// - `font_name` must be a valid null-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_custom_font(
    page: *mut PageHandle,
    font_name: *const c_char,
    size: f64,
) -> c_int {
    clear_last_error();
    if page.is_null() || font_name.is_null() {
        set_last_error("Null pointer provided to oxidize_page_set_custom_font");
        return ErrorCode::NullPointer as c_int;
    }
    let name = match CStr::from_ptr(font_name).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in font name");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    (*page)
        .inner
        .text()
        .set_font(oxidize_pdf::text::Font::Custom(name.to_string()), size);
    ErrorCode::Success as c_int
}

// ── Advanced text operations ────────────────────────────────────────────────

/// Set horizontal scaling for subsequent text operations.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_horizontal_scaling(
    page: *mut PageHandle,
    scale: f64,
) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_set_horizontal_scaling");
        return ErrorCode::NullPointer as c_int;
    }
    (*page).inner.text().set_horizontal_scaling(scale);
    ErrorCode::Success as c_int
}

/// Set text rise (vertical offset) for subsequent text operations.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_text_rise(page: *mut PageHandle, rise: f64) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_set_text_rise");
        return ErrorCode::NullPointer as c_int;
    }
    (*page).inner.text().set_text_rise(rise);
    ErrorCode::Success as c_int
}

/// Set the text rendering mode.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_rendering_mode(
    page: *mut PageHandle,
    mode: TextRenderingMode,
) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_set_rendering_mode");
        return ErrorCode::NullPointer as c_int;
    }
    (*page).inner.text().set_rendering_mode(mode.to_oxidize());
    ErrorCode::Success as c_int
}

// ── TextFlow operations ─────────────────────────────────────────────────────

/// Opaque handle wrapping an `oxidize_pdf::text::TextFlowContext`.
pub struct TextFlowHandle {
    pub(crate) inner: oxidize_pdf::text::TextFlowContext,
}

/// Create a new text flow context from a page handle.
/// The text flow inherits the page's dimensions and margins.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
/// - Returns a heap-allocated `TextFlowHandle` that must be freed with
///   `oxidize_text_flow_free`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_text_flow_create(page: *const PageHandle) -> *mut TextFlowHandle {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_text_flow_create");
        return std::ptr::null_mut();
    }
    let flow = (*page).inner.text_flow();
    let handle = Box::new(TextFlowHandle { inner: flow });
    Box::into_raw(handle)
}

/// Free a text flow handle.
///
/// # Safety
/// - `handle` must have been returned by `oxidize_text_flow_create`.
/// - `handle` must not have been freed previously.
#[no_mangle]
pub unsafe extern "C" fn oxidize_text_flow_free(handle: *mut TextFlowHandle) {
    if handle.is_null() {
        return;
    }
    drop(Box::from_raw(handle));
}

/// Set the font and size on a text flow context.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_text_flow_create`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_text_flow_set_font(
    handle: *mut TextFlowHandle,
    font: StandardFont,
    size: f64,
) -> c_int {
    clear_last_error();
    if handle.is_null() {
        set_last_error("Null pointer provided to oxidize_text_flow_set_font");
        return ErrorCode::NullPointer as c_int;
    }
    (*handle).inner.set_font(font.to_oxidize(), size);
    ErrorCode::Success as c_int
}

/// Set the text alignment on a text flow context.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_text_flow_create`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_text_flow_set_alignment(
    handle: *mut TextFlowHandle,
    alignment: TextAlign,
) -> c_int {
    clear_last_error();
    if handle.is_null() {
        set_last_error("Null pointer provided to oxidize_text_flow_set_alignment");
        return ErrorCode::NullPointer as c_int;
    }
    (*handle).inner.set_alignment(alignment.to_oxidize());
    ErrorCode::Success as c_int
}

/// Write wrapped text into a text flow context.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_text_flow_create`.
/// - `text` must be a valid null-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_text_flow_write_wrapped(
    handle: *mut TextFlowHandle,
    text: *const c_char,
) -> c_int {
    clear_last_error();
    if handle.is_null() || text.is_null() {
        set_last_error("Null pointer provided to oxidize_text_flow_write_wrapped");
        return ErrorCode::NullPointer as c_int;
    }
    let s = match CStr::from_ptr(text).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in text argument");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    if let Err(e) = (*handle).inner.write_wrapped(s) {
        set_last_error(format!("Failed to write wrapped text: {e}"));
        return ErrorCode::PdfParseError as c_int;
    }
    ErrorCode::Success as c_int
}

/// Add a text flow's operations to a page.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
/// - `flow` must be a valid pointer returned by `oxidize_text_flow_create`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_add_text_flow(
    page: *mut PageHandle,
    flow: *const TextFlowHandle,
) -> c_int {
    clear_last_error();
    if page.is_null() || flow.is_null() {
        set_last_error("Null pointer provided to oxidize_page_add_text_flow");
        return ErrorCode::NullPointer as c_int;
    }
    (*page).inner.add_text_flow(&(*flow).inner);
    ErrorCode::Success as c_int
}
