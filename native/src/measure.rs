use std::ffi::CStr;
use std::os::raw::{c_char, c_int};

use crate::{clear_last_error, set_last_error, ErrorCode};

/// Measure the width and height of a string using an embedded font.
///
/// This is a stateless function — it parses the font from bytes each time.
/// For repeated measurements with the same font, consider caching font bytes
/// on the calling side.
///
/// # Safety
/// - `font_bytes` must be a valid pointer to `font_len` bytes of TTF/OTF data.
/// - `text` must be a valid null-terminated UTF-8 string.
/// - `out_width` and `out_height` must be valid non-null pointers.
#[no_mangle]
pub unsafe extern "C" fn oxidize_measure_text(
    font_bytes: *const u8,
    font_len: usize,
    font_size: f32,
    text: *const c_char,
    out_width: *mut f32,
    out_height: *mut f32,
) -> c_int {
    clear_last_error();
    if font_bytes.is_null() || text.is_null() || out_width.is_null() || out_height.is_null() {
        set_last_error("Null pointer provided to oxidize_measure_text");
        return ErrorCode::NullPointer as c_int;
    }
    *out_width = 0.0;
    *out_height = 0.0;

    if font_len == 0 {
        set_last_error("Font data is empty");
        return ErrorCode::IoError as c_int;
    }

    let data = std::slice::from_raw_parts(font_bytes, font_len).to_vec();
    // The "__measure__" name is a private placeholder used only for this stateless
    // measurement call. It is never registered against any `Document`'s
    // `FontMetricsStore` (post-2.8.0) nor against the legacy global registry, so
    // there is no risk of collision with caller-registered font names. The local
    // `Font` instance lives only for the duration of this function.
    let font = match oxidize_pdf::fonts::Font::from_bytes("__measure__", data) {
        Ok(f) => f,
        Err(e) => {
            set_last_error(format!("Failed to parse font for measurement: {e}"));
            return ErrorCode::IoError as c_int;
        }
    };

    let s = match CStr::from_ptr(text).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in text");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    let m = font.measure_text(s, font_size);
    *out_width = m.width;
    *out_height = m.height;
    ErrorCode::Success as c_int
}
