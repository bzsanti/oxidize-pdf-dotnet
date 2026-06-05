use std::ffi::CStr;
use std::os::raw::{c_char, c_int};

use crate::page::PageHandle;
use crate::types::{BlendMode, LineCap, LineJoin};
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
}

// ── Line style ────────────────────────────────────────────────────────────────

/// Set the stroke line width.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_line_width(page: *mut PageHandle, width: f64) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_set_line_width");
        return ErrorCode::NullPointer as c_int;
    }
    (*page).inner.graphics().set_line_width(width);
    ErrorCode::Success as c_int
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
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_set_fill_opacity");
        return ErrorCode::NullPointer as c_int;
    }
    (*page).inner.graphics().set_fill_opacity(opacity);
    ErrorCode::Success as c_int
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
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_set_stroke_opacity");
        return ErrorCode::NullPointer as c_int;
    }
    (*page).inner.graphics().set_stroke_opacity(opacity);
    ErrorCode::Success as c_int
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
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_rect");
        return ErrorCode::NullPointer as c_int;
    }
    (*page).inner.graphics().rect(x, y, width, height);
    ErrorCode::Success as c_int
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
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_circle");
        return ErrorCode::NullPointer as c_int;
    }
    (*page).inner.graphics().circle(cx, cy, radius);
    ErrorCode::Success as c_int
}

// ── Path construction ─────────────────────────────────────────────────────────

/// Move the current point to (x, y) without drawing.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_move_to(page: *mut PageHandle, x: f64, y: f64) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_move_to");
        return ErrorCode::NullPointer as c_int;
    }
    (*page).inner.graphics().move_to(x, y);
    ErrorCode::Success as c_int
}

/// Draw a line from the current point to (x, y).
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_line_to(page: *mut PageHandle, x: f64, y: f64) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_line_to");
        return ErrorCode::NullPointer as c_int;
    }
    (*page).inner.graphics().line_to(x, y);
    ErrorCode::Success as c_int
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
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_curve_to");
        return ErrorCode::NullPointer as c_int;
    }
    (*page).inner.graphics().curve_to(x1, y1, x2, y2, x3, y3);
    ErrorCode::Success as c_int
}

/// Close the current path by drawing a line back to its starting point.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_close_path(page: *mut PageHandle) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_close_path");
        return ErrorCode::NullPointer as c_int;
    }
    (*page).inner.graphics().close_path();
    ErrorCode::Success as c_int
}

// ── Paint ─────────────────────────────────────────────────────────────────────

/// Fill the current path.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_fill(page: *mut PageHandle) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_fill");
        return ErrorCode::NullPointer as c_int;
    }
    (*page).inner.graphics().fill();
    ErrorCode::Success as c_int
}

/// Stroke the current path.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_stroke(page: *mut PageHandle) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_stroke");
        return ErrorCode::NullPointer as c_int;
    }
    (*page).inner.graphics().stroke();
    ErrorCode::Success as c_int
}

/// Fill and then stroke the current path.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_fill_and_stroke(page: *mut PageHandle) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_fill_and_stroke");
        return ErrorCode::NullPointer as c_int;
    }
    (*page).inner.graphics().fill_stroke();
    ErrorCode::Success as c_int
}

// ── Line style (advanced) ────────────────────────────────────────────────────

/// Set the line cap style for stroke operations.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_line_cap(page: *mut PageHandle, cap: LineCap) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_set_line_cap");
        return ErrorCode::NullPointer as c_int;
    }
    (*page).inner.graphics().set_line_cap(cap.to_oxidize());
    ErrorCode::Success as c_int
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
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_set_line_join");
        return ErrorCode::NullPointer as c_int;
    }
    (*page).inner.graphics().set_line_join(join.to_oxidize());
    ErrorCode::Success as c_int
}

/// Set the miter limit for stroke joins.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_miter_limit(page: *mut PageHandle, limit: f64) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_set_miter_limit");
        return ErrorCode::NullPointer as c_int;
    }
    (*page).inner.graphics().set_miter_limit(limit);
    ErrorCode::Success as c_int
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
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_set_dash_pattern");
        return ErrorCode::NullPointer as c_int;
    }
    let pattern = oxidize_pdf::graphics::LineDashPattern::dashed(dash_length, gap_length);
    (*page).inner.graphics().set_line_dash_pattern(pattern);
    ErrorCode::Success as c_int
}

/// Reset stroke to solid line (no dash pattern).
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_line_solid(page: *mut PageHandle) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_set_line_solid");
        return ErrorCode::NullPointer as c_int;
    }
    (*page).inner.graphics().set_line_solid();
    ErrorCode::Success as c_int
}

// ── Graphics state ───────────────────────────────────────────────────────────

/// Save the current graphics state onto an internal stack.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_save_state(page: *mut PageHandle) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_save_state");
        return ErrorCode::NullPointer as c_int;
    }
    (*page).inner.graphics().save_state();
    ErrorCode::Success as c_int
}

/// Restore the most recently saved graphics state.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_restore_state(page: *mut PageHandle) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_restore_state");
        return ErrorCode::NullPointer as c_int;
    }
    (*page).inner.graphics().restore_state();
    ErrorCode::Success as c_int
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
}

/// Clear all clipping regions.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_clear_clipping(page: *mut PageHandle) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_clear_clipping");
        return ErrorCode::NullPointer as c_int;
    }
    (*page).inner.graphics().clear_clipping();
    ErrorCode::Success as c_int
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
}

// ── Coordinate transforms ─────────────────────────────────────────────────────

/// Translate the current coordinate system.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_translate(page: *mut PageHandle, tx: f64, ty: f64) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_translate");
        return ErrorCode::NullPointer as c_int;
    }
    (*page).inner.graphics().translate(tx, ty);
    ErrorCode::Success as c_int
}

/// Scale the current coordinate system.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_scale(page: *mut PageHandle, sx: f64, sy: f64) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_scale");
        return ErrorCode::NullPointer as c_int;
    }
    (*page).inner.graphics().scale(sx, sy);
    ErrorCode::Success as c_int
}

/// Rotate the coordinate system by the given angle in radians.
///
/// # Safety
/// - `page` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_rotate_radians(page: *mut PageHandle, angle: f64) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_rotate_radians");
        return ErrorCode::NullPointer as c_int;
    }
    (*page).inner.graphics().rotate(angle);
    ErrorCode::Success as c_int
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
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_transform");
        return ErrorCode::NullPointer as c_int;
    }
    (*page).inner.graphics().transform(a, b, c, d, e, f);
    ErrorCode::Success as c_int
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
