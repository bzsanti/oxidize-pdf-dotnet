use std::os::raw::c_int;

use crate::page::PageHandle;
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
pub unsafe extern "C" fn oxidize_page_set_line_width(
    page: *mut PageHandle,
    width: f64,
) -> c_int {
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
pub unsafe extern "C" fn oxidize_page_move_to(
    page: *mut PageHandle,
    x: f64,
    y: f64,
) -> c_int {
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
pub unsafe extern "C" fn oxidize_page_line_to(
    page: *mut PageHandle,
    x: f64,
    y: f64,
) -> c_int {
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
