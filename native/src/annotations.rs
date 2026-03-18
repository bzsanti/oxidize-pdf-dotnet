use std::ffi::CStr;
use std::os::raw::{c_char, c_int};

use oxidize_pdf::annotations::{
    CircleAnnotation, LineAnnotation, LinkAction, LinkAnnotation, MarkupAnnotation,
    SquareAnnotation, StampAnnotation, TextAnnotation,
};
use oxidize_pdf::geometry::{Point, Rectangle};
use oxidize_pdf::Color;

use crate::page::PageHandle;
use crate::types::{StampNameFFI, TextAnnotationIcon};
use crate::{clear_last_error, set_last_error, ErrorCode};

fn make_rect(x: f64, y: f64, width: f64, height: f64) -> Rectangle {
    Rectangle::new(Point::new(x, y), Point::new(x + width, y + height))
}

// ── ANN-001: Link annotations ────────────────────────────────────────────────

/// Add a URI link annotation to a page.
///
/// # Safety
/// - `page` must be a valid `PageHandle` pointer.
/// - `uri` must be a valid null-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_add_link_uri(
    page: *mut PageHandle,
    x: f64,
    y: f64,
    width: f64,
    height: f64,
    uri: *const c_char,
) -> c_int {
    clear_last_error();
    if page.is_null() || uri.is_null() {
        set_last_error("Null pointer provided to oxidize_page_add_link_uri");
        return ErrorCode::NullPointer as c_int;
    }
    let uri_str = match CStr::from_ptr(uri).to_str() {
        Ok(s) => s,
        Err(_) => {
            set_last_error("Invalid UTF-8 in URI");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    let rect = make_rect(x, y, width, height);
    let link = LinkAnnotation::to_uri(rect, uri_str);
    (*page).inner.add_annotation(link.to_annotation());
    ErrorCode::Success as c_int
}

/// Add a named destination link annotation to a page.
///
/// # Safety
/// - `page` must be a valid `PageHandle` pointer.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_add_link_goto(
    page: *mut PageHandle,
    x: f64,
    y: f64,
    width: f64,
    height: f64,
    target_page: u32,
) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_add_link_goto");
        return ErrorCode::NullPointer as c_int;
    }
    let rect = make_rect(x, y, width, height);
    // Use Named action with page number as destination name.
    // ObjectReference is not available at page-construction time.
    let action = LinkAction::Named {
        name: format!("page-{target_page}"),
    };
    let link = LinkAnnotation::new(rect, action);
    (*page).inner.add_annotation(link.to_annotation());
    ErrorCode::Success as c_int
}

// ── ANN-002: Markup annotations ──────────────────────────────────────────────

/// Add a highlight annotation to a page.
///
/// # Safety
/// - `page` must be a valid `PageHandle` pointer.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_add_highlight(
    page: *mut PageHandle,
    x: f64,
    y: f64,
    width: f64,
    height: f64,
    r: f64,
    g: f64,
    b: f64,
) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_add_highlight");
        return ErrorCode::NullPointer as c_int;
    }
    let rect = make_rect(x, y, width, height);
    let markup = MarkupAnnotation::highlight(rect).with_color(Color::Rgb(r, g, b));
    (*page).inner.add_annotation(markup.to_annotation());
    ErrorCode::Success as c_int
}

/// Add an underline annotation to a page.
///
/// # Safety
/// - `page` must be a valid `PageHandle` pointer.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_add_underline(
    page: *mut PageHandle,
    x: f64,
    y: f64,
    width: f64,
    height: f64,
) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_add_underline");
        return ErrorCode::NullPointer as c_int;
    }
    let rect = make_rect(x, y, width, height);
    let markup = MarkupAnnotation::underline(rect);
    (*page).inner.add_annotation(markup.to_annotation());
    ErrorCode::Success as c_int
}

/// Add a strikeout annotation to a page.
///
/// # Safety
/// - `page` must be a valid `PageHandle` pointer.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_add_strikeout(
    page: *mut PageHandle,
    x: f64,
    y: f64,
    width: f64,
    height: f64,
) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_add_strikeout");
        return ErrorCode::NullPointer as c_int;
    }
    let rect = make_rect(x, y, width, height);
    let markup = MarkupAnnotation::strikeout(rect);
    (*page).inner.add_annotation(markup.to_annotation());
    ErrorCode::Success as c_int
}

// ── ANN-003: Text note (sticky note) ────────────────────────────────────────

/// Add a text note (sticky note) annotation to a page.
///
/// # Safety
/// - `page` must be a valid `PageHandle` pointer.
/// - `contents` must be a valid null-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_add_text_note(
    page: *mut PageHandle,
    x: f64,
    y: f64,
    contents: *const c_char,
    icon: TextAnnotationIcon,
    open: u8,
) -> c_int {
    clear_last_error();
    if page.is_null() || contents.is_null() {
        set_last_error("Null pointer provided to oxidize_page_add_text_note");
        return ErrorCode::NullPointer as c_int;
    }
    let contents_str = match CStr::from_ptr(contents).to_str() {
        Ok(s) => s,
        Err(_) => {
            set_last_error("Invalid UTF-8 in contents");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    let mut note = TextAnnotation::new(Point::new(x, y))
        .with_icon(icon.to_oxidize())
        .with_contents(contents_str);
    if open != 0 {
        note = note.open();
    }
    (*page).inner.add_annotation(note.to_annotation());
    ErrorCode::Success as c_int
}

// ── ANN-004: Stamp annotation ────────────────────────────────────────────────

/// Add a stamp annotation to a page.
///
/// # Safety
/// - `page` must be a valid `PageHandle` pointer.
/// - `custom_name` must be a valid null-terminated UTF-8 string if `stamp == Custom`; can be null otherwise.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_add_stamp(
    page: *mut PageHandle,
    x: f64,
    y: f64,
    width: f64,
    height: f64,
    stamp: c_int,
    custom_name: *const c_char,
) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_add_stamp");
        return ErrorCode::NullPointer as c_int;
    }

    let stamp = match StampNameFFI::from_c_int(stamp) {
        Some(s) => s,
        None => {
            set_last_error(format!("Invalid stamp name discriminant: {stamp}"));
            return ErrorCode::InvalidArgument as c_int;
        }
    };

    let custom = if !custom_name.is_null() {
        match CStr::from_ptr(custom_name).to_str() {
            Ok(s) => Some(s.to_string()),
            Err(_) => {
                set_last_error("Invalid UTF-8 in custom stamp name");
                return ErrorCode::InvalidUtf8 as c_int;
            }
        }
    } else {
        None
    };

    // Custom stamp requires a name
    if matches!(stamp, StampNameFFI::Custom) && custom.is_none() {
        set_last_error("Custom stamp requires a non-null custom_name");
        return ErrorCode::NullPointer as c_int;
    }

    let rect = make_rect(x, y, width, height);
    let stamp_annot = StampAnnotation::new(rect, stamp.to_oxidize(custom));
    (*page).inner.add_annotation(stamp_annot.to_annotation());
    ErrorCode::Success as c_int
}

// ── ANN-005: Geometric annotations ──────────────────────────────────────────

/// Add a line annotation to a page.
///
/// # Safety
/// - `page` must be a valid `PageHandle` pointer.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_add_annotation_line(
    page: *mut PageHandle,
    x1: f64,
    y1: f64,
    x2: f64,
    y2: f64,
    r: f64,
    g: f64,
    b: f64,
) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_add_annotation_line");
        return ErrorCode::NullPointer as c_int;
    }
    let line = LineAnnotation::new(Point::new(x1, y1), Point::new(x2, y2));
    let annot = line.to_annotation().with_color(Color::Rgb(r, g, b));
    (*page).inner.add_annotation(annot);
    ErrorCode::Success as c_int
}

/// Add a rectangle annotation to a page.
///
/// # Safety
/// - `page` must be a valid `PageHandle` pointer.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_add_annotation_rect(
    page: *mut PageHandle,
    x: f64,
    y: f64,
    width: f64,
    height: f64,
    stroke_r: f64,
    stroke_g: f64,
    stroke_b: f64,
    fill_r: f64,
    fill_g: f64,
    fill_b: f64,
    has_fill: u8,
) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_add_annotation_rect");
        return ErrorCode::NullPointer as c_int;
    }
    let rect = make_rect(x, y, width, height);
    let mut sq = SquareAnnotation::new(rect);
    if has_fill != 0 {
        sq = sq.with_interior_color(Color::Rgb(fill_r, fill_g, fill_b));
    }
    let annot = sq
        .to_annotation()
        .with_color(Color::Rgb(stroke_r, stroke_g, stroke_b));
    (*page).inner.add_annotation(annot);
    ErrorCode::Success as c_int
}

/// Add a circle annotation to a page.
///
/// # Safety
/// - `page` must be a valid `PageHandle` pointer.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_add_annotation_circle(
    page: *mut PageHandle,
    x: f64,
    y: f64,
    width: f64,
    height: f64,
    stroke_r: f64,
    stroke_g: f64,
    stroke_b: f64,
    fill_r: f64,
    fill_g: f64,
    fill_b: f64,
    has_fill: u8,
) -> c_int {
    clear_last_error();
    if page.is_null() {
        set_last_error("Null pointer provided to oxidize_page_add_annotation_circle");
        return ErrorCode::NullPointer as c_int;
    }
    let rect = make_rect(x, y, width, height);
    let mut circle = CircleAnnotation::new(rect);
    if has_fill != 0 {
        circle = circle.with_interior_color(Color::Rgb(fill_r, fill_g, fill_b));
    }
    let annot = circle
        .to_annotation()
        .with_color(Color::Rgb(stroke_r, stroke_g, stroke_b));
    (*page).inner.add_annotation(annot);
    ErrorCode::Success as c_int
}
