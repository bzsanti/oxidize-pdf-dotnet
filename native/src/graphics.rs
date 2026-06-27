use std::ffi::CStr;
use std::os::raw::{c_char, c_int};

use crate::page::PageHandle;
use crate::types::{BlendMode, LineCap, LineJoin, StandardFont};
use crate::{clear_last_error, set_last_error, ErrorCode};

// ── Fill color ────────────────────────────────────────────────────────────────

/// Set the graphics fill color using RGB components (each in 0.0–1.0).
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_fill_color_rgb(
    page: *mut PageHandle,
    r: f64,
    g: f64,
    b: f64,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_set_fill_color_rgb");
            return ErrorCode::NullPointer as c_int;
        }
        (*page)
            .inner
            .graphics()
            .set_fill_color(oxidize_pdf::Color::rgb(r, g, b));
        ErrorCode::Success as c_int
    })
}

/// Set the graphics fill color using a gray value (0.0 = black, 1.0 = white).
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_fill_color_gray(
    page: *mut PageHandle,
    value: f64,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_set_fill_color_gray");
            return ErrorCode::NullPointer as c_int;
        }
        (*page)
            .inner
            .graphics()
            .set_fill_color(oxidize_pdf::Color::gray(value));
        ErrorCode::Success as c_int
    })
}

/// Set the graphics fill color using CMYK components (each in 0.0–1.0).
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_fill_color_cmyk(
    page: *mut PageHandle,
    c: f64,
    m: f64,
    y: f64,
    k: f64,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_set_fill_color_cmyk");
            return ErrorCode::NullPointer as c_int;
        }
        (*page)
            .inner
            .graphics()
            .set_fill_color(oxidize_pdf::Color::cmyk(c, m, y, k));
        ErrorCode::Success as c_int
    })
}

// ── Stroke color ──────────────────────────────────────────────────────────────

/// Set the graphics stroke color using RGB components (each in 0.0–1.0).
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_stroke_color_rgb(
    page: *mut PageHandle,
    r: f64,
    g: f64,
    b: f64,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_set_stroke_color_rgb");
            return ErrorCode::NullPointer as c_int;
        }
        (*page)
            .inner
            .graphics()
            .set_stroke_color(oxidize_pdf::Color::rgb(r, g, b));
        ErrorCode::Success as c_int
    })
}

/// Set the graphics stroke color using a gray value (0.0 = black, 1.0 = white).
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_stroke_color_gray(
    page: *mut PageHandle,
    value: f64,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_set_stroke_color_gray");
            return ErrorCode::NullPointer as c_int;
        }
        (*page)
            .inner
            .graphics()
            .set_stroke_color(oxidize_pdf::Color::gray(value));
        ErrorCode::Success as c_int
    })
}

/// Set the graphics stroke color using CMYK components (each in 0.0–1.0).
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_stroke_color_cmyk(
    page: *mut PageHandle,
    c: f64,
    m: f64,
    y: f64,
    k: f64,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_set_stroke_color_cmyk");
            return ErrorCode::NullPointer as c_int;
        }
        (*page)
            .inner
            .graphics()
            .set_stroke_color(oxidize_pdf::Color::cmyk(c, m, y, k));
        ErrorCode::Success as c_int
    })
}

// ── Line style ────────────────────────────────────────────────────────────────

/// Set the stroke line width.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_line_width(page: *mut PageHandle, width: f64) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_set_line_width");
            return ErrorCode::NullPointer as c_int;
        }
        (*page).inner.graphics().set_line_width(width);
        ErrorCode::Success as c_int
    })
}

/// Set fill opacity (0.0 = fully transparent, 1.0 = fully opaque).
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_fill_opacity(
    page: *mut PageHandle,
    opacity: f64,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_set_fill_opacity");
            return ErrorCode::NullPointer as c_int;
        }
        (*page).inner.graphics().set_fill_opacity(opacity);
        ErrorCode::Success as c_int
    })
}

/// Set stroke opacity (0.0 = fully transparent, 1.0 = fully opaque).
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_stroke_opacity(
    page: *mut PageHandle,
    opacity: f64,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_set_stroke_opacity");
            return ErrorCode::NullPointer as c_int;
        }
        (*page).inner.graphics().set_stroke_opacity(opacity);
        ErrorCode::Success as c_int
    })
}

// ── Shape drawing ─────────────────────────────────────────────────────────────

/// Add a rectangle to the current path (does not fill or stroke automatically).
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_rect(
    page: *mut PageHandle,
    x: f64,
    y: f64,
    width: f64,
    height: f64,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_rect");
            return ErrorCode::NullPointer as c_int;
        }
        (*page).inner.graphics().rect(x, y, width, height);
        ErrorCode::Success as c_int
    })
}

/// Add a circle to the current path.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_circle(
    page: *mut PageHandle,
    cx: f64,
    cy: f64,
    radius: f64,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_circle");
            return ErrorCode::NullPointer as c_int;
        }
        (*page).inner.graphics().circle(cx, cy, radius);
        ErrorCode::Success as c_int
    })
}

// ── Path construction ─────────────────────────────────────────────────────────

/// Move the current point to (x, y) without drawing.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_move_to(page: *mut PageHandle, x: f64, y: f64) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_move_to");
            return ErrorCode::NullPointer as c_int;
        }
        (*page).inner.graphics().move_to(x, y);
        ErrorCode::Success as c_int
    })
}

/// Draw a line from the current point to (x, y).
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_line_to(page: *mut PageHandle, x: f64, y: f64) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_line_to");
            return ErrorCode::NullPointer as c_int;
        }
        (*page).inner.graphics().line_to(x, y);
        ErrorCode::Success as c_int
    })
}

/// Draw a cubic Bézier curve from the current point using two control points and an endpoint.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_curve_to(
    page: *mut PageHandle,
    x1: f64,
    y1: f64,
    x2: f64,
    y2: f64,
    x3: f64,
    y3: f64,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_curve_to");
            return ErrorCode::NullPointer as c_int;
        }
        (*page).inner.graphics().curve_to(x1, y1, x2, y2, x3, y3);
        ErrorCode::Success as c_int
    })
}

/// Close the current path by drawing a line back to its starting point.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_close_path(page: *mut PageHandle) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_close_path");
            return ErrorCode::NullPointer as c_int;
        }
        (*page).inner.graphics().close_path();
        ErrorCode::Success as c_int
    })
}

// ── Paint ─────────────────────────────────────────────────────────────────────

/// Fill the current path.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_fill(page: *mut PageHandle) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_fill");
            return ErrorCode::NullPointer as c_int;
        }
        (*page).inner.graphics().fill();
        ErrorCode::Success as c_int
    })
}

/// Stroke the current path.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_stroke(page: *mut PageHandle) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_stroke");
            return ErrorCode::NullPointer as c_int;
        }
        (*page).inner.graphics().stroke();
        ErrorCode::Success as c_int
    })
}

/// Fill and then stroke the current path.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_fill_and_stroke(page: *mut PageHandle) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_fill_and_stroke");
            return ErrorCode::NullPointer as c_int;
        }
        (*page).inner.graphics().fill_stroke();
        ErrorCode::Success as c_int
    })
}

// ── Line style (advanced) ────────────────────────────────────────────────────

/// Set the line cap style for stroke operations.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_line_cap(page: *mut PageHandle, cap: LineCap) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_set_line_cap");
            return ErrorCode::NullPointer as c_int;
        }
        (*page).inner.graphics().set_line_cap(cap.to_oxidize());
        ErrorCode::Success as c_int
    })
}

/// Set the line join style for stroke operations.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_line_join(
    page: *mut PageHandle,
    join: LineJoin,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_set_line_join");
            return ErrorCode::NullPointer as c_int;
        }
        (*page).inner.graphics().set_line_join(join.to_oxidize());
        ErrorCode::Success as c_int
    })
}

/// Set the miter limit for stroke joins.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_miter_limit(page: *mut PageHandle, limit: f64) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_set_miter_limit");
            return ErrorCode::NullPointer as c_int;
        }
        (*page).inner.graphics().set_miter_limit(limit);
        ErrorCode::Success as c_int
    })
}

/// Set a dash pattern for stroke operations.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_dash_pattern(
    page: *mut PageHandle,
    dash_length: f64,
    gap_length: f64,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_set_dash_pattern");
            return ErrorCode::NullPointer as c_int;
        }
        let pattern = oxidize_pdf::graphics::LineDashPattern::dashed(dash_length, gap_length);
        (*page).inner.graphics().set_line_dash_pattern(pattern);
        ErrorCode::Success as c_int
    })
}

/// Reset stroke to solid line (no dash pattern).
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_line_solid(page: *mut PageHandle) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_set_line_solid");
            return ErrorCode::NullPointer as c_int;
        }
        (*page).inner.graphics().set_line_solid();
        ErrorCode::Success as c_int
    })
}

// ── Graphics state ───────────────────────────────────────────────────────────

/// Save the current graphics state onto an internal stack.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_save_state(page: *mut PageHandle) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_save_state");
            return ErrorCode::NullPointer as c_int;
        }
        (*page).inner.graphics().save_state();
        ErrorCode::Success as c_int
    })
}

/// Restore the most recently saved graphics state.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_restore_state(page: *mut PageHandle) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_restore_state");
            return ErrorCode::NullPointer as c_int;
        }
        (*page).inner.graphics().restore_state();
        ErrorCode::Success as c_int
    })
}

// ── Clipping ─────────────────────────────────────────────────────────────────

/// Set a rectangular clipping region.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_clip_rect(
    page: *mut PageHandle,
    x: f64,
    y: f64,
    width: f64,
    height: f64,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_clip_rect");
            return ErrorCode::NullPointer as c_int;
        }
        if let Err(e) = (*page).inner.graphics().clip_rect(x, y, width, height) {
            set_last_error(format!("Failed to set clipping rect: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
        ErrorCode::Success as c_int
    })
}

/// Set a circular clipping region.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_clip_circle(
    page: *mut PageHandle,
    cx: f64,
    cy: f64,
    radius: f64,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_clip_circle");
            return ErrorCode::NullPointer as c_int;
        }
        if let Err(e) = (*page).inner.graphics().clip_circle(cx, cy, radius) {
            set_last_error(format!("Failed to set clipping circle: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
        ErrorCode::Success as c_int
    })
}

/// Clear all clipping regions.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_clear_clipping(page: *mut PageHandle) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_clear_clipping");
            return ErrorCode::NullPointer as c_int;
        }
        (*page).inner.graphics().clear_clipping();
        ErrorCode::Success as c_int
    })
}

// ── Blend mode ───────────────────────────────────────────────────────────────

/// Set the blend mode for compositing.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_blend_mode(
    page: *mut PageHandle,
    mode: BlendMode,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_set_blend_mode");
            return ErrorCode::NullPointer as c_int;
        }
        if let Err(e) = (*page).inner.graphics().set_blend_mode(mode.to_oxidize()) {
            set_last_error(format!("Failed to set blend mode: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
        ErrorCode::Success as c_int
    })
}

// ── Coordinate transforms ─────────────────────────────────────────────────────

/// Translate the current coordinate system.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_translate(page: *mut PageHandle, tx: f64, ty: f64) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_translate");
            return ErrorCode::NullPointer as c_int;
        }
        (*page).inner.graphics().translate(tx, ty);
        ErrorCode::Success as c_int
    })
}

/// Scale the current coordinate system.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_scale(page: *mut PageHandle, sx: f64, sy: f64) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_scale");
            return ErrorCode::NullPointer as c_int;
        }
        (*page).inner.graphics().scale(sx, sy);
        ErrorCode::Success as c_int
    })
}

/// Rotate the coordinate system by the given angle in radians.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_rotate_radians(page: *mut PageHandle, angle: f64) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_rotate_radians");
            return ErrorCode::NullPointer as c_int;
        }
        (*page).inner.graphics().rotate(angle);
        ErrorCode::Success as c_int
    })
}

/// Apply a full 6-element transformation matrix [a b c d e f].
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_transform(
    page: *mut PageHandle,
    a: f64,
    b: f64,
    c: f64,
    d: f64,
    e: f64,
    f: f64,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_transform");
            return ErrorCode::NullPointer as c_int;
        }
        (*page).inner.graphics().transform(a, b, c, d, e, f);
        ErrorCode::Success as c_int
    })
}

// ── ICC draw ──────────────────────────────────────────────────────────────────

/// Set fill color using an ICC-based color space registered under `name`.
///
/// `components` must be non-null and non-empty. The function enforces this
/// in ALL builds (the upstream `debug_assert!` is compiled out in release;
/// this FFI layer catches it instead and returns `ErrorCode::InvalidArgument`).
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
/// - `name` must be a valid non-null, null-terminated UTF-8 C string.
/// - `components` must be a valid pointer to `components_len` `f64` values, or
///   null when `components_len` is 0 (the function rejects the null case).
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_fill_color_icc(
    page: *mut PageHandle,
    name: *const c_char,
    components: *const f64,
    components_len: usize,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() || name.is_null() {
            set_last_error("Null pointer provided to oxidize_page_set_fill_color_icc");
            return ErrorCode::NullPointer as c_int;
        }
        if components.is_null() || components_len == 0 {
            set_last_error("ICC fill color components must not be empty");
            return ErrorCode::InvalidArgument as c_int;
        }
        let name_str = match CStr::from_ptr(name).to_str() {
            Ok(s) => s.to_owned(),
            Err(_) => {
                set_last_error("Invalid UTF-8 in ICC color space name");
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };
        let comps = std::slice::from_raw_parts(components, components_len).to_vec();
        (*page).inner.graphics().set_fill_color_icc(name_str, comps);
        ErrorCode::Success as c_int
    })
}

/// Set stroke color using an ICC-based color space registered under `name`.
///
/// See `oxidize_page_set_fill_color_icc` for parameter and safety notes.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
/// - `name` must be a valid non-null, null-terminated UTF-8 C string.
/// - `components` must be a valid pointer to `components_len` `f64` values, or
///   null when `components_len` is 0 (the function rejects the null case).
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_stroke_color_icc(
    page: *mut PageHandle,
    name: *const c_char,
    components: *const f64,
    components_len: usize,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() || name.is_null() {
            set_last_error("Null pointer provided to oxidize_page_set_stroke_color_icc");
            return ErrorCode::NullPointer as c_int;
        }
        if components.is_null() || components_len == 0 {
            set_last_error("ICC stroke color components must not be empty");
            return ErrorCode::InvalidArgument as c_int;
        }
        let name_str = match CStr::from_ptr(name).to_str() {
            Ok(s) => s.to_owned(),
            Err(_) => {
                set_last_error("Invalid UTF-8 in ICC color space name");
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };
        let comps = std::slice::from_raw_parts(components, components_len).to_vec();
        (*page)
            .inner
            .graphics()
            .set_stroke_color_icc(name_str, comps);
        ErrorCode::Success as c_int
    })
}

// ── CalGray color (hardcoded name "CalGray1" via upstream) ────────────────────

/// Set the graphics fill color using a calibrated gray color space.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_fill_color_cal_gray(
    page: *mut PageHandle,
    value: f64,
    wp_x: f64,
    wp_y: f64,
    wp_z: f64,
    bp_x: f64,
    bp_y: f64,
    bp_z: f64,
    gamma: f64,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_set_fill_color_cal_gray");
            return ErrorCode::NullPointer as c_int;
        }
        use oxidize_pdf::graphics::{CalGrayColorSpace, CalibratedColor};
        let cs = CalGrayColorSpace::new()
            .with_white_point([wp_x, wp_y, wp_z])
            .with_black_point([bp_x, bp_y, bp_z])
            .with_gamma(gamma);
        let color = CalibratedColor::cal_gray(value, cs);
        (*page).inner.graphics().set_fill_color_calibrated(color);
        ErrorCode::Success as c_int
    })
}

/// Set the graphics stroke color using a calibrated gray color space.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_stroke_color_cal_gray(
    page: *mut PageHandle,
    value: f64,
    wp_x: f64,
    wp_y: f64,
    wp_z: f64,
    bp_x: f64,
    bp_y: f64,
    bp_z: f64,
    gamma: f64,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_set_stroke_color_cal_gray");
            return ErrorCode::NullPointer as c_int;
        }
        use oxidize_pdf::graphics::{CalGrayColorSpace, CalibratedColor};
        let cs = CalGrayColorSpace::new()
            .with_white_point([wp_x, wp_y, wp_z])
            .with_black_point([bp_x, bp_y, bp_z])
            .with_gamma(gamma);
        let color = CalibratedColor::cal_gray(value, cs);
        (*page).inner.graphics().set_stroke_color_calibrated(color);
        ErrorCode::Success as c_int
    })
}

#[cfg(test)]
mod cal_gray_ffi_tests {
    use super::*;
    use crate::page::{oxidize_page_create, oxidize_page_free};

    #[test]
    fn fill_cal_gray_valid_params_returns_success() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            assert!(!page.is_null());
            let result = oxidize_page_set_fill_color_cal_gray(
                page, 0.5, 0.9505, 1.0, 1.0890, 0.0, 0.0, 0.0, 1.0,
            );
            assert_eq!(result, 0, "expected ErrorCode::Success (0)");
            oxidize_page_free(page);
        }
    }

    #[test]
    fn stroke_cal_gray_null_page_returns_null_pointer_error() {
        unsafe {
            let result = oxidize_page_set_stroke_color_cal_gray(
                std::ptr::null_mut(),
                0.5,
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
}

// ── Lab color (hardcoded name "Lab1" via upstream) ────────────────────────────

/// Set the graphics fill color using a CIE L*a*b* color space.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_fill_color_lab(
    page: *mut PageHandle,
    l: f64,
    a: f64,
    b: f64,
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
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_set_fill_color_lab");
            return ErrorCode::NullPointer as c_int;
        }
        use oxidize_pdf::graphics::{LabColor, LabColorSpace};
        let cs = LabColorSpace::new()
            .with_white_point([wp_x, wp_y, wp_z])
            .with_black_point([bp_x, bp_y, bp_z])
            .with_range(range_amin, range_amax, range_bmin, range_bmax);
        let color = LabColor::new(l, a, b, cs);
        (*page).inner.graphics().set_fill_color_lab(color);
        ErrorCode::Success as c_int
    })
}

/// Set the graphics stroke color using a CIE L*a*b* color space.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_stroke_color_lab(
    page: *mut PageHandle,
    l: f64,
    a: f64,
    b: f64,
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
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_set_stroke_color_lab");
            return ErrorCode::NullPointer as c_int;
        }
        use oxidize_pdf::graphics::{LabColor, LabColorSpace};
        let cs = LabColorSpace::new()
            .with_white_point([wp_x, wp_y, wp_z])
            .with_black_point([bp_x, bp_y, bp_z])
            .with_range(range_amin, range_amax, range_bmin, range_bmax);
        let color = LabColor::new(l, a, b, cs);
        (*page).inner.graphics().set_stroke_color_lab(color);
        ErrorCode::Success as c_int
    })
}

#[cfg(test)]
mod lab_ffi_tests {
    use super::*;
    use crate::page::{oxidize_page_create, oxidize_page_free};

    #[test]
    fn fill_lab_valid_params_returns_success() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            assert!(!page.is_null());
            let result = oxidize_page_set_fill_color_lab(
                page, 50.0, 0.0, 0.0, 0.9642, 1.0, 0.8251, 0.0, 0.0, 0.0, -128.0, 127.0, -128.0,
                127.0,
            );
            assert_eq!(result, 0, "expected ErrorCode::Success (0)");
            oxidize_page_free(page);
        }
    }

    #[test]
    fn stroke_lab_null_page_returns_null_pointer_error() {
        unsafe {
            let result = oxidize_page_set_stroke_color_lab(
                std::ptr::null_mut(),
                50.0,
                0.0,
                0.0,
                0.9642,
                1.0,
                0.8251,
                0.0,
                0.0,
                0.0,
                -128.0,
                127.0,
                -128.0,
                127.0,
            );
            assert_eq!(result, 1, "expected ErrorCode::NullPointer (1)");
        }
    }
}

// ── CalRGB color (hardcoded name "CalRGB1" via upstream) ─────────────────────

/// Set the graphics fill color using a calibrated RGB color space.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_fill_color_cal_rgb(
    page: *mut PageHandle,
    r: f64,
    g: f64,
    b: f64,
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
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_set_fill_color_cal_rgb");
            return ErrorCode::NullPointer as c_int;
        }
        use oxidize_pdf::graphics::{CalRgbColorSpace, CalibratedColor};
        let cs = CalRgbColorSpace::new()
            .with_white_point([wp_x, wp_y, wp_z])
            .with_black_point([bp_x, bp_y, bp_z])
            .with_gamma([gamma_r, gamma_g, gamma_b])
            .with_matrix([m0, m1, m2, m3, m4, m5, m6, m7, m8]);
        let color = CalibratedColor::cal_rgb([r, g, b], cs);
        (*page).inner.graphics().set_fill_color_calibrated(color);
        ErrorCode::Success as c_int
    })
}

/// Set the graphics stroke color using a calibrated RGB color space.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_stroke_color_cal_rgb(
    page: *mut PageHandle,
    r: f64,
    g: f64,
    b: f64,
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
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_set_stroke_color_cal_rgb");
            return ErrorCode::NullPointer as c_int;
        }
        use oxidize_pdf::graphics::{CalRgbColorSpace, CalibratedColor};
        let cs = CalRgbColorSpace::new()
            .with_white_point([wp_x, wp_y, wp_z])
            .with_black_point([bp_x, bp_y, bp_z])
            .with_gamma([gamma_r, gamma_g, gamma_b])
            .with_matrix([m0, m1, m2, m3, m4, m5, m6, m7, m8]);
        let color = CalibratedColor::cal_rgb([r, g, b], cs);
        (*page).inner.graphics().set_stroke_color_calibrated(color);
        ErrorCode::Success as c_int
    })
}

#[cfg(test)]
mod cal_rgb_ffi_tests {
    use super::*;
    use crate::page::{oxidize_page_create, oxidize_page_free};

    #[test]
    fn fill_cal_rgb_valid_params_returns_success() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            assert!(!page.is_null());
            let result = oxidize_page_set_fill_color_cal_rgb(
                page, 0.5, 0.3, 0.8, 0.9505, 1.0, 1.0890, 0.0, 0.0, 0.0, 2.2, 2.2, 2.2, 1.0, 0.0,
                0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0,
            );
            assert_eq!(result, 0, "expected ErrorCode::Success (0)");
            oxidize_page_free(page);
        }
    }

    #[test]
    fn stroke_cal_rgb_null_page_returns_null_pointer_error() {
        unsafe {
            let result = oxidize_page_set_stroke_color_cal_rgb(
                std::ptr::null_mut(),
                0.5,
                0.3,
                0.8,
                0.9505,
                1.0,
                1.0890,
                0.0,
                0.0,
                0.0,
                2.2,
                2.2,
                2.2,
                1.0,
                0.0,
                0.0,
                0.0,
                1.0,
                0.0,
                0.0,
                0.0,
                1.0,
            );
            assert_eq!(result, 1, "expected ErrorCode::NullPointer (1)");
        }
    }
}

// ── CalGray named ─────────────────────────────────────────────────────────────

/// Set the graphics fill color using a named calibrated gray color space.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
/// - `name` must be a valid non-null, null-terminated UTF-8 C string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_fill_color_cal_gray_named(
    page: *mut PageHandle,
    name: *const c_char,
    value: f64,
    wp_x: f64,
    wp_y: f64,
    wp_z: f64,
    bp_x: f64,
    bp_y: f64,
    bp_z: f64,
    gamma: f64,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() || name.is_null() {
            set_last_error("Null pointer provided to oxidize_page_set_fill_color_cal_gray_named");
            return ErrorCode::NullPointer as c_int;
        }
        let name_str = match CStr::from_ptr(name).to_str() {
            Ok(s) => s.to_owned(),
            Err(_) => {
                set_last_error("Invalid UTF-8 in color space name");
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };
        use oxidize_pdf::graphics::{CalGrayColorSpace, CalibratedColor};
        let cs = CalGrayColorSpace::new()
            .with_white_point([wp_x, wp_y, wp_z])
            .with_black_point([bp_x, bp_y, bp_z])
            .with_gamma(gamma);
        let color = CalibratedColor::cal_gray(value, cs);
        (*page)
            .inner
            .graphics()
            .set_fill_color_calibrated_named(name_str, color);
        ErrorCode::Success as c_int
    })
}

/// Set the graphics stroke color using a named calibrated gray color space.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
/// - `name` must be a valid non-null, null-terminated UTF-8 C string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_stroke_color_cal_gray_named(
    page: *mut PageHandle,
    name: *const c_char,
    value: f64,
    wp_x: f64,
    wp_y: f64,
    wp_z: f64,
    bp_x: f64,
    bp_y: f64,
    bp_z: f64,
    gamma: f64,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() || name.is_null() {
            set_last_error("Null pointer provided to oxidize_page_set_stroke_color_cal_gray_named");
            return ErrorCode::NullPointer as c_int;
        }
        let name_str = match CStr::from_ptr(name).to_str() {
            Ok(s) => s.to_owned(),
            Err(_) => {
                set_last_error("Invalid UTF-8 in color space name");
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };
        use oxidize_pdf::graphics::{CalGrayColorSpace, CalibratedColor};
        let cs = CalGrayColorSpace::new()
            .with_white_point([wp_x, wp_y, wp_z])
            .with_black_point([bp_x, bp_y, bp_z])
            .with_gamma(gamma);
        let color = CalibratedColor::cal_gray(value, cs);
        (*page)
            .inner
            .graphics()
            .set_stroke_color_calibrated_named(name_str, color);
        ErrorCode::Success as c_int
    })
}

// ── CalRGB named ──────────────────────────────────────────────────────────────

/// Set the graphics fill color using a named calibrated RGB color space.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
/// - `name` must be a valid non-null, null-terminated UTF-8 C string.
#[allow(clippy::too_many_arguments)]
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_fill_color_cal_rgb_named(
    page: *mut PageHandle,
    name: *const c_char,
    r: f64,
    g: f64,
    b: f64,
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
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() || name.is_null() {
            set_last_error("Null pointer provided to oxidize_page_set_fill_color_cal_rgb_named");
            return ErrorCode::NullPointer as c_int;
        }
        let name_str = match CStr::from_ptr(name).to_str() {
            Ok(s) => s.to_owned(),
            Err(_) => {
                set_last_error("Invalid UTF-8 in color space name");
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };
        use oxidize_pdf::graphics::{CalRgbColorSpace, CalibratedColor};
        let cs = CalRgbColorSpace::new()
            .with_white_point([wp_x, wp_y, wp_z])
            .with_black_point([bp_x, bp_y, bp_z])
            .with_gamma([gamma_r, gamma_g, gamma_b])
            .with_matrix([m0, m1, m2, m3, m4, m5, m6, m7, m8]);
        let color = CalibratedColor::cal_rgb([r, g, b], cs);
        (*page)
            .inner
            .graphics()
            .set_fill_color_calibrated_named(name_str, color);
        ErrorCode::Success as c_int
    })
}

/// Set the graphics stroke color using a named calibrated RGB color space.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
/// - `name` must be a valid non-null, null-terminated UTF-8 C string.
#[allow(clippy::too_many_arguments)]
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_stroke_color_cal_rgb_named(
    page: *mut PageHandle,
    name: *const c_char,
    r: f64,
    g: f64,
    b: f64,
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
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() || name.is_null() {
            set_last_error("Null pointer provided to oxidize_page_set_stroke_color_cal_rgb_named");
            return ErrorCode::NullPointer as c_int;
        }
        let name_str = match CStr::from_ptr(name).to_str() {
            Ok(s) => s.to_owned(),
            Err(_) => {
                set_last_error("Invalid UTF-8 in color space name");
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };
        use oxidize_pdf::graphics::{CalRgbColorSpace, CalibratedColor};
        let cs = CalRgbColorSpace::new()
            .with_white_point([wp_x, wp_y, wp_z])
            .with_black_point([bp_x, bp_y, bp_z])
            .with_gamma([gamma_r, gamma_g, gamma_b])
            .with_matrix([m0, m1, m2, m3, m4, m5, m6, m7, m8]);
        let color = CalibratedColor::cal_rgb([r, g, b], cs);
        (*page)
            .inner
            .graphics()
            .set_stroke_color_calibrated_named(name_str, color);
        ErrorCode::Success as c_int
    })
}

// ── Lab named ─────────────────────────────────────────────────────────────────

/// Set the graphics fill color using a named CIE L*a*b* color space.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
/// - `name` must be a valid non-null, null-terminated UTF-8 C string.
#[allow(clippy::too_many_arguments)]
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_fill_color_lab_named(
    page: *mut PageHandle,
    name: *const c_char,
    l: f64,
    a: f64,
    b: f64,
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
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() || name.is_null() {
            set_last_error("Null pointer provided to oxidize_page_set_fill_color_lab_named");
            return ErrorCode::NullPointer as c_int;
        }
        let name_str = match CStr::from_ptr(name).to_str() {
            Ok(s) => s.to_owned(),
            Err(_) => {
                set_last_error("Invalid UTF-8 in color space name");
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };
        use oxidize_pdf::graphics::{LabColor, LabColorSpace};
        let cs = LabColorSpace::new()
            .with_white_point([wp_x, wp_y, wp_z])
            .with_black_point([bp_x, bp_y, bp_z])
            .with_range(range_amin, range_amax, range_bmin, range_bmax);
        let color = LabColor::new(l, a, b, cs);
        (*page)
            .inner
            .graphics()
            .set_fill_color_lab_named(name_str, color);
        ErrorCode::Success as c_int
    })
}

/// Set the graphics stroke color using a named CIE L*a*b* color space.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
/// - `name` must be a valid non-null, null-terminated UTF-8 C string.
#[allow(clippy::too_many_arguments)]
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_stroke_color_lab_named(
    page: *mut PageHandle,
    name: *const c_char,
    l: f64,
    a: f64,
    b: f64,
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
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() || name.is_null() {
            set_last_error("Null pointer provided to oxidize_page_set_stroke_color_lab_named");
            return ErrorCode::NullPointer as c_int;
        }
        let name_str = match CStr::from_ptr(name).to_str() {
            Ok(s) => s.to_owned(),
            Err(_) => {
                set_last_error("Invalid UTF-8 in color space name");
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };
        use oxidize_pdf::graphics::{LabColor, LabColorSpace};
        let cs = LabColorSpace::new()
            .with_white_point([wp_x, wp_y, wp_z])
            .with_black_point([bp_x, bp_y, bp_z])
            .with_range(range_amin, range_amax, range_bmin, range_bmax);
        let color = LabColor::new(l, a, b, cs);
        (*page)
            .inner
            .graphics()
            .set_stroke_color_lab_named(name_str, color);
        ErrorCode::Success as c_int
    })
}

#[cfg(test)]
mod icc_draw_ffi_tests {
    use super::*;
    use crate::page::{oxidize_page_create, oxidize_page_free};

    #[test]
    fn fill_icc_valid_params_returns_success() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            assert!(!page.is_null());
            let name = std::ffi::CString::new("ICCRGB1").unwrap();
            let components: [f64; 3] = [0.5, 0.3, 0.8];
            let result =
                oxidize_page_set_fill_color_icc(page, name.as_ptr(), components.as_ptr(), 3);
            assert_eq!(result, 0, "expected ErrorCode::Success (0)");
            oxidize_page_free(page);
        }
    }

    #[test]
    fn fill_icc_null_page_returns_error() {
        unsafe {
            let name = std::ffi::CString::new("ICCRGB1").unwrap();
            let components: [f64; 3] = [0.5, 0.3, 0.8];
            let result = oxidize_page_set_fill_color_icc(
                std::ptr::null_mut(),
                name.as_ptr(),
                components.as_ptr(),
                3,
            );
            assert_eq!(result, 1, "expected ErrorCode::NullPointer (1)");
        }
    }

    #[test]
    fn fill_icc_empty_components_returns_invalid_argument() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            assert!(!page.is_null());
            let name = std::ffi::CString::new("ICCRGB1").unwrap();
            let result = oxidize_page_set_fill_color_icc(page, name.as_ptr(), std::ptr::null(), 0);
            // ErrorCode::InvalidArgument = 9
            assert_eq!(
                result, 9,
                "empty components must return InvalidArgument (9)"
            );
            oxidize_page_free(page);
        }
    }
}

#[cfg(test)]
mod cal_gray_named_ffi_tests {
    use super::*;
    use crate::page::{oxidize_page_create, oxidize_page_free};

    #[test]
    fn fill_cal_gray_named_valid_params_returns_success() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            assert!(!page.is_null());
            let name = std::ffi::CString::new("MyCalGray").unwrap();
            let result = oxidize_page_set_fill_color_cal_gray_named(
                page,
                name.as_ptr(),
                0.5,
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

    #[test]
    fn fill_cal_gray_named_null_page_returns_error() {
        unsafe {
            let name = std::ffi::CString::new("MyCalGray").unwrap();
            let result = oxidize_page_set_fill_color_cal_gray_named(
                std::ptr::null_mut(),
                name.as_ptr(),
                0.5,
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
    fn fill_cal_gray_named_null_name_returns_error() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            assert!(!page.is_null());
            let result = oxidize_page_set_fill_color_cal_gray_named(
                page,
                std::ptr::null(),
                0.5,
                0.9505,
                1.0,
                1.0890,
                0.0,
                0.0,
                0.0,
                1.0,
            );
            assert_eq!(result, 1, "expected ErrorCode::NullPointer (1)");
            oxidize_page_free(page);
        }
    }
}

// ── Tiling patterns (GFX-016) ──────────────────────────────────────────────────

/// Register a tiling pattern on the page under `name`.
///
/// `paint_type`: 1 = Colored, 2 = Uncolored.
/// `tiling_type`: 1 = ConstantSpacing, 2 = NoDistortion, 3 = ConstantSpacingFaster.
/// The pattern cell bounding box is given as position + size (`bbox_x`, `bbox_y`,
/// `bbox_w`, `bbox_h`) and converted to the PDF `[x0 y0 x1 y1]` BBox internally.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create`/`_preset`.
/// - `name` must be a valid non-null, null-terminated UTF-8 C string.
/// - `content`/`content_len` must describe a valid byte buffer (the tile's
///   content-stream operators); `content` may be null only when `content_len` is 0.
/// - `matrix` is nullable; if non-null it must point to exactly 6 `f64` values.
#[no_mangle]
#[allow(clippy::too_many_arguments)]
pub unsafe extern "C" fn oxidize_page_add_tiling_pattern(
    page: *mut PageHandle,
    name: *const c_char,
    paint_type: c_int,
    tiling_type: c_int,
    bbox_x: f64,
    bbox_y: f64,
    bbox_w: f64,
    bbox_h: f64,
    x_step: f64,
    y_step: f64,
    content: *const u8,
    content_len: usize,
    matrix: *const f64,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() || name.is_null() {
            set_last_error("Null pointer provided to oxidize_page_add_tiling_pattern");
            return ErrorCode::NullPointer as c_int;
        }
        let name_str = match CStr::from_ptr(name).to_str() {
            Ok(s) => s.to_owned(),
            Err(_) => {
                set_last_error("Invalid UTF-8 in pattern name");
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };
        use oxidize_pdf::graphics::{PaintType, PatternMatrix, TilingPattern, TilingType};
        let paint = match paint_type {
            1 => PaintType::Colored,
            2 => PaintType::Uncolored,
            _ => {
                set_last_error("Invalid paint_type (expected 1=Colored, 2=Uncolored)");
                return ErrorCode::InvalidArgument as c_int;
            }
        };
        let tiling = match tiling_type {
            1 => TilingType::ConstantSpacing,
            2 => TilingType::NoDistortion,
            3 => TilingType::ConstantSpacingFaster,
            _ => {
                set_last_error("Invalid tiling_type (expected 1, 2, or 3)");
                return ErrorCode::InvalidArgument as c_int;
            }
        };
        if x_step <= 0.0 || y_step <= 0.0 {
            set_last_error("Tiling pattern x_step and y_step must be positive");
            return ErrorCode::InvalidArgument as c_int;
        }
        let bbox = [bbox_x, bbox_y, bbox_x + bbox_w, bbox_y + bbox_h];
        let content_vec = if content.is_null() || content_len == 0 {
            Vec::new()
        } else {
            std::slice::from_raw_parts(content, content_len).to_vec()
        };
        let mut pattern = TilingPattern::new(name_str.clone(), paint, tiling, bbox, x_step, y_step)
            .with_content_stream(content_vec);
        if !matrix.is_null() {
            let m = std::slice::from_raw_parts(matrix, 6);
            pattern = pattern.with_matrix(PatternMatrix {
                matrix: [m[0], m[1], m[2], m[3], m[4], m[5]],
            });
        }
        if let Err(e) = (*page).inner.add_pattern(name_str, pattern) {
            set_last_error(format!("Failed to add tiling pattern: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
        ErrorCode::Success as c_int
    })
}

/// Select a previously registered tiling pattern as the fill color.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create`/`_preset`.
/// - `name` must be a valid non-null, null-terminated UTF-8 C string matching a
///   pattern registered via `oxidize_page_add_tiling_pattern`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_fill_pattern(
    page: *mut PageHandle,
    name: *const c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() || name.is_null() {
            set_last_error("Null pointer provided to oxidize_page_set_fill_pattern");
            return ErrorCode::NullPointer as c_int;
        }
        let name_str = match CStr::from_ptr(name).to_str() {
            Ok(s) => s,
            Err(_) => {
                set_last_error("Invalid UTF-8 in pattern name");
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };
        use oxidize_pdf::graphics::PatternGraphicsContext;
        if let Err(e) = (*page).inner.graphics().set_fill_pattern(name_str) {
            set_last_error(format!("Failed to set fill pattern: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
        ErrorCode::Success as c_int
    })
}

/// Select a previously registered tiling pattern as the stroke color.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create`/`_preset`.
/// - `name` must be a valid non-null, null-terminated UTF-8 C string matching a
///   pattern registered via `oxidize_page_add_tiling_pattern`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_stroke_pattern(
    page: *mut PageHandle,
    name: *const c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() || name.is_null() {
            set_last_error("Null pointer provided to oxidize_page_set_stroke_pattern");
            return ErrorCode::NullPointer as c_int;
        }
        let name_str = match CStr::from_ptr(name).to_str() {
            Ok(s) => s,
            Err(_) => {
                set_last_error("Invalid UTF-8 in pattern name");
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };
        use oxidize_pdf::graphics::PatternGraphicsContext;
        if let Err(e) = (*page).inner.graphics().set_stroke_pattern(name_str) {
            set_last_error(format!("Failed to set stroke pattern: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
        ErrorCode::Success as c_int
    })
}

#[cfg(test)]
mod tiling_pattern_ffi_tests {
    use super::*;
    use crate::page::{oxidize_page_create, oxidize_page_free};
    use std::ffi::CString;

    #[test]
    fn add_tiling_pattern_valid_returns_success() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            assert!(!page.is_null());
            let name = CString::new("P1").unwrap();
            let content = b"1.0 0.0 0.0 rg\n0 0 10 10 re\nf\n";
            let result = oxidize_page_add_tiling_pattern(
                page,
                name.as_ptr(),
                1,
                1,
                0.0,
                0.0,
                10.0,
                10.0,
                10.0,
                10.0,
                content.as_ptr(),
                content.len(),
                std::ptr::null(),
            );
            assert_eq!(result, 0, "expected ErrorCode::Success (0)");
            let sel = oxidize_page_set_fill_pattern(page, name.as_ptr());
            assert_eq!(sel, 0, "expected ErrorCode::Success (0)");
            oxidize_page_free(page);
        }
    }

    #[test]
    fn add_tiling_pattern_null_page_returns_null_pointer_error() {
        unsafe {
            let name = CString::new("P1").unwrap();
            let result = oxidize_page_add_tiling_pattern(
                std::ptr::null_mut(),
                name.as_ptr(),
                1,
                1,
                0.0,
                0.0,
                10.0,
                10.0,
                10.0,
                10.0,
                std::ptr::null(),
                0,
                std::ptr::null(),
            );
            assert_eq!(result, 1, "expected ErrorCode::NullPointer (1)");
        }
    }

    #[test]
    fn add_tiling_pattern_invalid_paint_type_returns_invalid_argument() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            let name = CString::new("P1").unwrap();
            let result = oxidize_page_add_tiling_pattern(
                page,
                name.as_ptr(),
                99,
                1,
                0.0,
                0.0,
                10.0,
                10.0,
                10.0,
                10.0,
                std::ptr::null(),
                0,
                std::ptr::null(),
            );
            assert_eq!(result, 9, "expected ErrorCode::InvalidArgument (9)");
            oxidize_page_free(page);
        }
    }
}

// ── Form XObjects (GFX-018) ─────────────────────────────────────────────────────

/// Register a reusable Form XObject (template) on the page under `name`.
///
/// The form's bounding box is given as position + size (`x`, `y`, `width`, `height`).
/// `content`/`content_len` is the form's content-stream operators.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create`/`_preset`.
/// - `name` must be a valid non-null, null-terminated UTF-8 C string.
/// - `content` may be null only when `content_len` is 0.
/// - `matrix` is nullable; if non-null it must point to exactly 6 `f64` values.
/// - `group_color_space` is nullable; non-null attaches a transparency group
///   (GFX-020) with the given colour space and the `isolated`/`knockout` flags
///   (0 = false, non-zero = true). Null means no transparency group.
#[no_mangle]
#[allow(clippy::too_many_arguments)]
pub unsafe extern "C" fn oxidize_page_add_form_xobject(
    page: *mut PageHandle,
    name: *const c_char,
    x: f64,
    y: f64,
    width: f64,
    height: f64,
    content: *const u8,
    content_len: usize,
    matrix: *const f64,
    group_color_space: *const c_char,
    isolated: c_int,
    knockout: c_int,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() || name.is_null() {
            set_last_error("Null pointer provided to oxidize_page_add_form_xobject");
            return ErrorCode::NullPointer as c_int;
        }
        let name_str = match CStr::from_ptr(name).to_str() {
            Ok(s) => s.to_owned(),
            Err(_) => {
                set_last_error("Invalid UTF-8 in form XObject name");
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };
        use oxidize_pdf::graphics::FormXObject;
        use oxidize_pdf::Rectangle;
        let bbox = Rectangle::from_position_and_size(x, y, width, height);
        let content_vec = if content.is_null() || content_len == 0 {
            Vec::new()
        } else {
            std::slice::from_raw_parts(content, content_len).to_vec()
        };
        let mut form = FormXObject::new(bbox).with_content(content_vec);
        if !matrix.is_null() {
            let m = std::slice::from_raw_parts(matrix, 6);
            form = form.with_matrix([m[0], m[1], m[2], m[3], m[4], m[5]]);
        }
        if !group_color_space.is_null() {
            let cs = match CStr::from_ptr(group_color_space).to_str() {
                Ok(s) => s.to_owned(),
                Err(_) => {
                    set_last_error("Invalid UTF-8 in transparency group color space");
                    return ErrorCode::InvalidUtf8 as c_int;
                }
            };
            use oxidize_pdf::graphics::FormTransparencyGroup;
            form = form.with_transparency_group(FormTransparencyGroup {
                color_space: cs,
                isolated: isolated != 0,
                knockout: knockout != 0,
            });
        }
        if let Err(e) = (*page).inner.add_form_xobject(name_str, form) {
            set_last_error(format!("Failed to add form XObject: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
        ErrorCode::Success as c_int
    })
}

/// Invoke (paint) a previously registered Form XObject, emitting `/name Do`.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create`/`_preset`.
/// - `name` must be a valid non-null, null-terminated UTF-8 C string matching a
///   form registered via `oxidize_page_add_form_xobject`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_invoke_xobject(
    page: *mut PageHandle,
    name: *const c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() || name.is_null() {
            set_last_error("Null pointer provided to oxidize_page_invoke_xobject");
            return ErrorCode::NullPointer as c_int;
        }
        let name_str = match CStr::from_ptr(name).to_str() {
            Ok(s) => s,
            Err(_) => {
                set_last_error("Invalid UTF-8 in form XObject name");
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };
        (*page)
            .inner
            .graphics()
            .add_command(&format!("/{name_str} Do"));
        ErrorCode::Success as c_int
    })
}

/// Apply a soft mask (GFX-021) to the page via an ExtGState `/SMask` entry
/// (ISO 32000-1 §11.6.4.3), emitting the corresponding `/GSx gs` operator.
///
/// `mask_type`: 0 = None (disable masking), 1 = Alpha, 2 = Luminosity.
/// For Alpha/Luminosity, `group_ref` must be the name of a Form XObject
/// registered on this page via `oxidize_page_add_form_xobject` — the writer
/// resolves it to an indirect `/G` reference at save time. For None,
/// `group_ref` is ignored and may be null.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create`/`_preset`.
/// - `group_ref`, when required, must be a valid null-terminated UTF-8 C string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_apply_soft_mask(
    page: *mut PageHandle,
    mask_type: c_int,
    group_ref: *const c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_apply_soft_mask");
            return ErrorCode::NullPointer as c_int;
        }
        use oxidize_pdf::graphics::{ExtGState, SoftMask};
        let mask = match mask_type {
            0 => SoftMask::none(),
            1 | 2 => {
                if group_ref.is_null() {
                    set_last_error(
                        "Alpha/Luminosity soft mask requires a non-null group reference",
                    );
                    return ErrorCode::NullPointer as c_int;
                }
                let name = match CStr::from_ptr(group_ref).to_str() {
                    Ok(s) => s.to_owned(),
                    Err(_) => {
                        set_last_error("Invalid UTF-8 in soft mask group reference");
                        return ErrorCode::InvalidUtf8 as c_int;
                    }
                };
                if mask_type == 1 {
                    SoftMask::alpha(name)
                } else {
                    SoftMask::luminosity(name)
                }
            }
            _ => {
                set_last_error("Invalid soft mask type (expected 0=None, 1=Alpha, 2=Luminosity)");
                return ErrorCode::InvalidArgument as c_int;
            }
        };
        let mut state = ExtGState::new();
        state.set_soft_mask(mask);
        if let Err(e) = (*page).inner.graphics().apply_extgstate(state) {
            set_last_error(format!("Failed to apply soft mask: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
        ErrorCode::Success as c_int
    })
}

/// Draw text through the page's graphics context (GFX-022), emitting a
/// `BT /Font size Tf x y Td (text) Tj ET` sequence. Unlike the text-layout
/// entry point (`oxidize_page_text_at`), this participates in the graphics
/// state stack — fill colour, clipping paths, soft masks, transparency groups
/// and the CTM set on the graphics context all apply to the drawn text.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create`/`_preset`.
/// - `text` must be a valid non-null, null-terminated UTF-8 C string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_draw_text_at(
    page: *mut PageHandle,
    font: StandardFont,
    size: f64,
    x: f64,
    y: f64,
    text: *const c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() || text.is_null() {
            set_last_error("Null pointer provided to oxidize_page_draw_text_at");
            return ErrorCode::NullPointer as c_int;
        }
        let text_str = match CStr::from_ptr(text).to_str() {
            Ok(s) => s,
            Err(_) => {
                set_last_error("Invalid UTF-8 in text");
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };
        let result = (*page)
            .inner
            .graphics()
            .set_font(font.to_oxidize(), size)
            .draw_text(text_str, x, y);
        if let Err(e) = result {
            set_last_error(format!("Failed to draw text: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
        ErrorCode::Success as c_int
    })
}

/// Intersect the current clipping region with an ellipse (GFX-024), emitting the
/// path construction (`m`, four `c` Bézier quarters, `h`) followed by `W n`.
/// The ellipse is centred at (`cx`, `cy`) with horizontal radius `rx` and
/// vertical radius `ry`; both radii must be strictly positive.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create`/`_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_clip_ellipse(
    page: *mut PageHandle,
    cx: f64,
    cy: f64,
    rx: f64,
    ry: f64,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_clip_ellipse");
            return ErrorCode::NullPointer as c_int;
        }
        if rx <= 0.0 || ry <= 0.0 || rx.is_nan() || ry.is_nan() {
            set_last_error("Ellipse radii (rx, ry) must be strictly positive");
            return ErrorCode::InvalidArgument as c_int;
        }
        if let Err(e) = (*page).inner.graphics().clip_ellipse(cx, cy, rx, ry) {
            set_last_error(format!("Failed to set elliptical clip: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
        ErrorCode::Success as c_int
    })
}

// ── Shadings / gradients (GFX-017, upstream `sh` operator added in 2.14.0) ─────

/// One gradient color stop: a position in `[0.0, 1.0]` and an RGB color
/// (components in `[0.0, 1.0]`).
#[derive(serde::Deserialize)]
struct ShadingStopJson {
    position: f64,
    /// RGB color components `[r, g, b]`, each in `[0.0, 1.0]`.
    color: [f64; 3],
}

/// Axial or radial shading definition. `kind` selects the geometry fields:
/// `axial` uses `start`/`end`; `radial` uses `start_center`/`start_radius`/
/// `end_center`/`end_radius`. Both use `stops` (≥2) and the `extend_*` flags.
#[derive(serde::Deserialize)]
struct ShadingJson {
    kind: String,
    // axial
    start: Option<[f64; 2]>,
    end: Option<[f64; 2]>,
    // radial
    start_center: Option<[f64; 2]>,
    start_radius: Option<f64>,
    end_center: Option<[f64; 2]>,
    end_radius: Option<f64>,
    // both
    stops: Vec<ShadingStopJson>,
    #[serde(default)]
    extend_start: bool,
    #[serde(default)]
    extend_end: bool,
}

fn color_stops_from(stops: &[ShadingStopJson]) -> Vec<oxidize_pdf::graphics::ColorStop> {
    use oxidize_pdf::graphics::{Color, ColorStop};
    stops
        .iter()
        .map(|s| ColorStop::new(s.position, Color::Rgb(s.color[0], s.color[1], s.color[2])))
        .collect()
}

/// Register an axial (linear) or radial gradient shading on the page under
/// `name`, from a JSON definition. The writer emits it as an indirect
/// dictionary under `/Resources/Shading/<name>`; paint it with
/// [`oxidize_page_paint_shading`].
///
/// JSON shapes:
/// - axial:  `{"kind":"axial","start":[x,y],"end":[x,y],"stops":[{"position":p,"color":[r,g,b]}],"extend_start":bool,"extend_end":bool}`
/// - radial: `{"kind":"radial","start_center":[x,y],"start_radius":r0,"end_center":[x,y],"end_radius":r1,"stops":[...]}`
///
/// Requires at least two color stops.
///
/// # Safety
/// - `page` must be a valid pointer from `oxidize_page_create`/`_preset`.
/// - `name` and `json` must be valid null-terminated UTF-8 C strings.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_add_shading_json(
    page: *mut PageHandle,
    name: *const c_char,
    json: *const c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() || name.is_null() || json.is_null() {
            set_last_error("Null pointer provided to oxidize_page_add_shading_json");
            return ErrorCode::NullPointer as c_int;
        }
        let name_str = match CStr::from_ptr(name).to_str() {
            Ok(s) => s.to_owned(),
            Err(_) => {
                set_last_error("Invalid UTF-8 in shading name");
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };
        let json_str = match CStr::from_ptr(json).to_str() {
            Ok(s) => s,
            Err(_) => {
                set_last_error("Invalid UTF-8 in shading JSON");
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };
        let dto: ShadingJson = match serde_json::from_str(json_str) {
            Ok(v) => v,
            Err(e) => {
                set_last_error(format!("Invalid shading JSON: {e}"));
                return ErrorCode::SerializationError as c_int;
            }
        };
        if dto.stops.len() < 2 {
            set_last_error("Shading requires at least two color stops");
            return ErrorCode::InvalidArgument as c_int;
        }

        use oxidize_pdf::graphics::{
            AxialShading, Point as ShadingPoint, RadialShading, ShadingDefinition,
        };
        let stops = color_stops_from(&dto.stops);

        let definition = match dto.kind.as_str() {
            "axial" => {
                let (Some(start), Some(end)) = (dto.start, dto.end) else {
                    set_last_error("axial shading requires 'start' and 'end'");
                    return ErrorCode::InvalidArgument as c_int;
                };
                let shading = AxialShading::new(
                    name_str.clone(),
                    ShadingPoint::new(start[0], start[1]),
                    ShadingPoint::new(end[0], end[1]),
                    stops,
                )
                .with_extend(dto.extend_start, dto.extend_end);
                ShadingDefinition::Axial(shading)
            }
            "radial" => {
                let (Some(sc), Some(sr), Some(ec), Some(er)) = (
                    dto.start_center,
                    dto.start_radius,
                    dto.end_center,
                    dto.end_radius,
                ) else {
                    set_last_error(
                        "radial shading requires 'start_center', 'start_radius', \
                     'end_center', 'end_radius'",
                    );
                    return ErrorCode::InvalidArgument as c_int;
                };
                let shading = RadialShading::new(
                    name_str.clone(),
                    ShadingPoint::new(sc[0], sc[1]),
                    sr,
                    ShadingPoint::new(ec[0], ec[1]),
                    er,
                    stops,
                )
                .with_extend(dto.extend_start, dto.extend_end);
                ShadingDefinition::Radial(shading)
            }
            other => {
                set_last_error(format!("Unknown shading kind: {other}"));
                return ErrorCode::InvalidArgument as c_int;
            }
        };

        if let Err(e) = (*page).inner.add_shading(name_str, definition) {
            set_last_error(format!("Failed to add shading: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
        ErrorCode::Success as c_int
    })
}

/// Paint a registered shading with the `sh` operator (ISO 32000-1 §8.7.4.2).
///
/// `name` must match a shading registered via [`oxidize_page_add_shading_json`].
/// `sh` fills the current clip region (the whole page if unclipped), so callers
/// typically wrap it as `save_state → clip_rect → paint_shading → restore_state`
/// to bound the gradient.
///
/// # Safety
/// - `page` must be a valid pointer from `oxidize_page_create`/`_preset`.
/// - `name` must be a valid null-terminated UTF-8 C string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_paint_shading(
    page: *mut PageHandle,
    name: *const c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() || name.is_null() {
            set_last_error("Null pointer provided to oxidize_page_paint_shading");
            return ErrorCode::NullPointer as c_int;
        }
        let name_str = match CStr::from_ptr(name).to_str() {
            Ok(s) => s.to_owned(),
            Err(_) => {
                set_last_error("Invalid UTF-8 in shading name");
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };
        (*page).inner.graphics().paint_shading(name_str);
        ErrorCode::Success as c_int
    })
}

/// End the current path without filling or stroking (the `n` operator).
///
/// Required to terminate a manually constructed clipping path: the canonical
/// sequence is `<path> W n` (ISO 32000-1 §8.5.4) before painting a shading into
/// the clipped region. For rectangular clips, prefer `oxidize_page_clip_rect`,
/// which emits the whole `re W n` sequence.
///
/// # Safety
/// - `page` must be a valid pointer from `oxidize_page_create`/`_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_end_path(page: *mut PageHandle) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() {
            set_last_error("Null pointer provided to oxidize_page_end_path");
            return ErrorCode::NullPointer as c_int;
        }
        (*page).inner.graphics().end_path();
        ErrorCode::Success as c_int
    })
}

#[cfg(test)]
mod form_xobject_ffi_tests {
    use super::*;
    use crate::page::{oxidize_page_create, oxidize_page_free};
    use std::ffi::CString;

    #[test]
    fn add_form_xobject_valid_returns_success() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            assert!(!page.is_null());
            let name = CString::new("Fm1").unwrap();
            let content = b"0.0 0.0 1.0 rg\n0 0 50 50 re\nf\n";
            let result = oxidize_page_add_form_xobject(
                page,
                name.as_ptr(),
                0.0,
                0.0,
                50.0,
                50.0,
                content.as_ptr(),
                content.len(),
                std::ptr::null(),
                std::ptr::null(),
                0,
                0,
            );
            assert_eq!(result, 0, "expected ErrorCode::Success (0)");
            let inv = oxidize_page_invoke_xobject(page, name.as_ptr());
            assert_eq!(inv, 0, "expected ErrorCode::Success (0)");
            oxidize_page_free(page);
        }
    }

    #[test]
    fn add_form_xobject_with_transparency_group_marks_form_as_transparent() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            assert!(!page.is_null());
            let name = CString::new("Fm1").unwrap();
            let cs = CString::new("DeviceRGB").unwrap();
            let content = b"0.0 0.0 1.0 rg\n0 0 50 50 re\nf\n";
            let result = oxidize_page_add_form_xobject(
                page,
                name.as_ptr(),
                0.0,
                0.0,
                50.0,
                50.0,
                content.as_ptr(),
                content.len(),
                std::ptr::null(),
                cs.as_ptr(),
                1, // isolated
                1, // knockout
            );
            assert_eq!(result, 0, "expected ErrorCode::Success (0)");
            // Real behavioural check: the registered form carries a transparency
            // group, so to_stream() will emit a `/Group /S /Transparency` dict.
            let form = (*page)
                .inner
                .form_xobjects()
                .get("Fm1")
                .expect("form Fm1 must be registered");
            assert!(
                form.has_transparency(),
                "form must carry a transparency group when one was supplied"
            );
            oxidize_page_free(page);
        }
    }

    #[test]
    fn add_form_xobject_null_page_returns_null_pointer_error() {
        unsafe {
            let name = CString::new("Fm1").unwrap();
            let result = oxidize_page_add_form_xobject(
                std::ptr::null_mut(),
                name.as_ptr(),
                0.0,
                0.0,
                50.0,
                50.0,
                std::ptr::null(),
                0,
                std::ptr::null(),
                std::ptr::null(),
                0,
                0,
            );
            assert_eq!(result, 1, "expected ErrorCode::NullPointer (1)");
        }
    }

    #[test]
    fn invoke_xobject_null_page_returns_null_pointer_error() {
        unsafe {
            let name = CString::new("Fm1").unwrap();
            let result = oxidize_page_invoke_xobject(std::ptr::null_mut(), name.as_ptr());
            assert_eq!(result, 1, "expected ErrorCode::NullPointer (1)");
        }
    }

    #[test]
    fn apply_soft_mask_luminosity_registers_extgstate_with_soft_mask() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            let group = CString::new("Mask1").unwrap();
            let result = oxidize_page_apply_soft_mask(page, 2, group.as_ptr());
            assert_eq!(result, 0, "expected ErrorCode::Success (0)");
            // Real behavioural check: an ExtGState carrying a soft mask is now
            // registered, so the writer will emit an `/SMask` entry for it.
            let states = (*page)
                .inner
                .get_extgstate_resources()
                .expect("page must expose ExtGState resources after apply_soft_mask");
            assert!(
                states.values().any(|s| s.soft_mask.is_some()),
                "a registered ExtGState must carry a soft mask"
            );
            oxidize_page_free(page);
        }
    }

    #[test]
    fn apply_soft_mask_invalid_type_returns_invalid_argument() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            let result = oxidize_page_apply_soft_mask(page, 7, std::ptr::null());
            assert_eq!(result, 9, "expected ErrorCode::InvalidArgument (9)");
            oxidize_page_free(page);
        }
    }

    #[test]
    fn apply_soft_mask_null_page_returns_null_pointer_error() {
        unsafe {
            let result = oxidize_page_apply_soft_mask(std::ptr::null_mut(), 2, std::ptr::null());
            assert_eq!(result, 1, "expected ErrorCode::NullPointer (1)");
        }
    }

    #[test]
    fn draw_text_at_emits_text_operators_in_graphics_content() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            let text = CString::new("Hello").unwrap();
            let rc = oxidize_page_draw_text_at(
                page,
                StandardFont::Helvetica,
                14.0,
                72.0,
                700.0,
                text.as_ptr(),
            );
            assert_eq!(rc, 0, "expected ErrorCode::Success (0)");
            // Real behavioural check on the graphics-context content stream.
            let ops = (*page).inner.graphics().operations();
            assert!(ops.contains("BT"), "expected BeginText (BT)\n{ops}");
            assert!(ops.contains("ET"), "expected EndText (ET)\n{ops}");
            assert!(
                ops.contains("Tj"),
                "expected show-text operator (Tj)\n{ops}"
            );
            assert!(ops.contains("Tf"), "expected set-font operator (Tf)\n{ops}");
            assert!(ops.contains("Helvetica"), "expected font name\n{ops}");
            assert!(ops.contains("Hello"), "expected literal text\n{ops}");
            oxidize_page_free(page);
        }
    }

    #[test]
    fn draw_text_at_null_text_returns_null_pointer_error() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            let rc = oxidize_page_draw_text_at(
                page,
                StandardFont::Helvetica,
                14.0,
                0.0,
                0.0,
                std::ptr::null(),
            );
            assert_eq!(rc, 1, "expected ErrorCode::NullPointer (1)");
            oxidize_page_free(page);
        }
    }

    #[test]
    fn clip_ellipse_emits_path_and_clip_operators() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            let rc = oxidize_page_clip_ellipse(page, 300.0, 400.0, 100.0, 50.0);
            assert_eq!(rc, 0, "expected ErrorCode::Success (0)");
            let ops = (*page).inner.graphics().operations();
            // Path starts at the top of the ellipse (cy + ry = 450).
            assert!(ops.contains("300.000 450.000 m"), "expected MoveTo\n{ops}");
            assert!(ops.contains(" c\n"), "expected Bézier curves\n{ops}");
            assert!(ops.contains("W\n"), "expected clip operator W\n{ops}");
            assert!(ops.contains("n\n"), "expected end-path operator n\n{ops}");
            oxidize_page_free(page);
        }
    }

    #[test]
    fn clip_ellipse_zero_radius_returns_invalid_argument() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            let rc = oxidize_page_clip_ellipse(page, 300.0, 400.0, 0.0, 50.0);
            assert_eq!(rc, 9, "expected ErrorCode::InvalidArgument (9)");
            oxidize_page_free(page);
        }
    }

    #[test]
    fn clip_ellipse_negative_radius_returns_invalid_argument() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            let rc = oxidize_page_clip_ellipse(page, 300.0, 400.0, 100.0, -5.0);
            assert_eq!(rc, 9, "expected ErrorCode::InvalidArgument (9)");
            oxidize_page_free(page);
        }
    }

    #[test]
    fn clip_ellipse_null_page_returns_null_pointer_error() {
        unsafe {
            let rc = oxidize_page_clip_ellipse(std::ptr::null_mut(), 0.0, 0.0, 10.0, 10.0);
            assert_eq!(rc, 1, "expected ErrorCode::NullPointer (1)");
        }
    }
}

#[cfg(test)]
mod shading_ffi_tests {
    use super::*;
    use crate::document::{
        oxidize_document_add_page, oxidize_document_create, oxidize_document_free,
    };
    use crate::page::{oxidize_page_create, oxidize_page_free};
    use std::ffi::CString;

    /// Build a one-page document whose single page defines `name` as a shading
    /// and paints it bounded by a clip rect, then serialize WITHOUT stream
    /// compression so the content-stream `sh` operator is greppable. The
    /// shading dictionary itself is an indirect object (always greppable).
    unsafe fn build_gradient_pdf(name: &str, shading_json: &str) -> String {
        let handle = oxidize_document_create();
        let page = oxidize_page_create(595.0, 842.0);

        let c_name = CString::new(name).unwrap();
        let c_json = CString::new(shading_json).unwrap();
        assert_eq!(
            oxidize_page_add_shading_json(page, c_name.as_ptr(), c_json.as_ptr()),
            ErrorCode::Success as c_int,
            "add_shading_json failed"
        );

        // q <rect> re W n  /Grad sh  Q
        assert_eq!(oxidize_page_save_state(page), ErrorCode::Success as c_int);
        assert_eq!(
            oxidize_page_clip_rect(page, 50.0, 50.0, 200.0, 100.0),
            ErrorCode::Success as c_int
        );
        assert_eq!(
            oxidize_page_paint_shading(page, c_name.as_ptr()),
            ErrorCode::Success as c_int
        );
        assert_eq!(
            oxidize_page_restore_state(page),
            ErrorCode::Success as c_int
        );

        assert_eq!(
            oxidize_document_add_page(handle, page),
            ErrorCode::Success as c_int
        );
        oxidize_page_free(page);

        let config = oxidize_pdf::writer::WriterConfig {
            use_xref_streams: false,
            use_object_streams: false,
            pdf_version: "1.7".to_string(),
            compress_streams: false,
            incremental_update: false,
        };
        let bytes = (*handle).inner.to_bytes_with_config(config).unwrap();
        oxidize_document_free(handle);
        String::from_utf8_lossy(&bytes).into_owned()
    }

    #[test]
    fn axial_shading_emits_type2_dict_and_sh_operator() {
        unsafe {
            let s = build_gradient_pdf(
                "Grad1",
                r#"{"kind":"axial","start":[50,50],"end":[250,50],
                   "stops":[{"position":0.0,"color":[1.0,0.0,0.0]},
                            {"position":1.0,"color":[0.0,0.0,1.0]}]}"#,
            );
            assert!(
                s.contains("/ShadingType 2"),
                "axial shading must emit /ShadingType 2; got none"
            );
            assert!(
                s.contains("/Coords"),
                "axial shading must emit /Coords; got none"
            );
            assert!(
                s.contains("/Grad1 sh"),
                "content stream must paint the shading with `/Grad1 sh`"
            );
        }
    }

    #[test]
    fn radial_shading_emits_type3_dict() {
        unsafe {
            let s = build_gradient_pdf(
                "Glow",
                r#"{"kind":"radial","start_center":[150,150],"start_radius":0.0,
                   "end_center":[150,150],"end_radius":80.0,
                   "stops":[{"position":0.0,"color":[1.0,1.0,1.0]},
                            {"position":1.0,"color":[0.0,0.0,0.0]}]}"#,
            );
            assert!(
                s.contains("/ShadingType 3"),
                "radial shading must emit /ShadingType 3"
            );
            assert!(s.contains("/Glow sh"), "content must paint `/Glow sh`");
        }
    }

    #[test]
    fn shading_resource_is_referenced_from_page_resources() {
        unsafe {
            let s = build_gradient_pdf(
                "G",
                r#"{"kind":"axial","start":[0,0],"end":[100,0],
                   "stops":[{"position":0.0,"color":[0.0,0.0,0.0]},
                            {"position":1.0,"color":[1.0,1.0,1.0]}]}"#,
            );
            // The page /Resources must expose a /Shading subdictionary keyed by
            // the registered name so the `sh` operator can resolve it.
            assert!(
                s.contains("/Shading"),
                "page resources must contain a /Shading entry"
            );
        }
    }

    #[test]
    fn add_shading_unknown_kind_returns_invalid_argument() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            let name = CString::new("X").unwrap();
            let json = CString::new(
                r#"{"kind":"conic","start":[0,0],"end":[1,1],"stops":[{"position":0.0,"color":[0,0,0]},{"position":1.0,"color":[1,1,1]}]}"#,
            )
            .unwrap();
            let rc = oxidize_page_add_shading_json(page, name.as_ptr(), json.as_ptr());
            oxidize_page_free(page);
            assert_eq!(rc, ErrorCode::InvalidArgument as c_int);
        }
    }

    #[test]
    fn add_shading_too_few_stops_returns_invalid_argument() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            let name = CString::new("X").unwrap();
            let json = CString::new(
                r#"{"kind":"axial","start":[0,0],"end":[1,1],"stops":[{"position":0.0,"color":[0,0,0]}]}"#,
            )
            .unwrap();
            let rc = oxidize_page_add_shading_json(page, name.as_ptr(), json.as_ptr());
            oxidize_page_free(page);
            assert_eq!(rc, ErrorCode::InvalidArgument as c_int);
        }
    }

    #[test]
    fn axial_missing_coords_returns_invalid_argument() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            let name = CString::new("X").unwrap();
            // 'axial' without start/end.
            let json = CString::new(
                r#"{"kind":"axial","stops":[{"position":0.0,"color":[0,0,0]},{"position":1.0,"color":[1,1,1]}]}"#,
            )
            .unwrap();
            let rc = oxidize_page_add_shading_json(page, name.as_ptr(), json.as_ptr());
            oxidize_page_free(page);
            assert_eq!(rc, ErrorCode::InvalidArgument as c_int);
        }
    }

    #[test]
    fn add_shading_null_page_returns_null_pointer() {
        unsafe {
            let name = CString::new("X").unwrap();
            let json =
                CString::new(r#"{"kind":"axial","start":[0,0],"end":[1,1],"stops":[]}"#).unwrap();
            let rc =
                oxidize_page_add_shading_json(std::ptr::null_mut(), name.as_ptr(), json.as_ptr());
            assert_eq!(rc, ErrorCode::NullPointer as c_int);
        }
    }

    #[test]
    fn paint_shading_null_page_returns_null_pointer() {
        unsafe {
            let name = CString::new("X").unwrap();
            let rc = oxidize_page_paint_shading(std::ptr::null_mut(), name.as_ptr());
            assert_eq!(rc, ErrorCode::NullPointer as c_int);
        }
    }
}
