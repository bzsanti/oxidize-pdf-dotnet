use std::ffi::CStr;
use std::os::raw::{c_char, c_int};
use std::sync::Arc;

use crate::document::DocumentHandle;
use crate::image::ImageHandle;
use crate::types::StandardFont;
use crate::{clear_last_error, set_last_error, ErrorCode};

// ── FlowLayout ───────────────────────────────────────────────────────────────

/// Opaque handle wrapping an `oxidize_pdf::layout::FlowLayout`.
/// Stores a copy of `PageConfig` because the field is private on `FlowLayout`.
pub struct FlowLayoutHandle {
    pub(crate) inner: oxidize_pdf::layout::FlowLayout,
    pub(crate) config: oxidize_pdf::layout::PageConfig,
}

/// Create a FlowLayout with A4 page size and default 72pt margins.
///
/// # Safety
/// - `out_handle` must be a valid pointer.
#[no_mangle]
pub unsafe extern "C" fn oxidize_flow_layout_create_a4(
    out_handle: *mut *mut FlowLayoutHandle,
) -> c_int {
    clear_last_error();
    if out_handle.is_null() {
        set_last_error("Null pointer provided to oxidize_flow_layout_create_a4");
        return ErrorCode::NullPointer as c_int;
    }
    *out_handle = std::ptr::null_mut();

    let config = oxidize_pdf::layout::PageConfig::a4();
    let layout = oxidize_pdf::layout::FlowLayout::new(config.clone());
    *out_handle = Box::into_raw(Box::new(FlowLayoutHandle {
        inner: layout,
        config,
    }));
    ErrorCode::Success as c_int
}

/// Create a FlowLayout with custom dimensions and margins.
///
/// # Safety
/// - `out_handle` must be a valid pointer.
#[no_mangle]
pub unsafe extern "C" fn oxidize_flow_layout_create(
    width: f64,
    height: f64,
    margin_left: f64,
    margin_right: f64,
    margin_top: f64,
    margin_bottom: f64,
    out_handle: *mut *mut FlowLayoutHandle,
) -> c_int {
    clear_last_error();
    if out_handle.is_null() {
        set_last_error("Null pointer provided to oxidize_flow_layout_create");
        return ErrorCode::NullPointer as c_int;
    }
    *out_handle = std::ptr::null_mut();

    let config = oxidize_pdf::layout::PageConfig::new(
        width,
        height,
        margin_left,
        margin_right,
        margin_top,
        margin_bottom,
    );
    let layout = oxidize_pdf::layout::FlowLayout::new(config.clone());
    *out_handle = Box::into_raw(Box::new(FlowLayoutHandle {
        inner: layout,
        config,
    }));
    ErrorCode::Success as c_int
}

/// Free a FlowLayout handle.
///
/// # Safety
/// - `handle` must have been returned by `oxidize_flow_layout_create*`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_flow_layout_free(handle: *mut FlowLayoutHandle) {
    if handle.is_null() {
        return;
    }
    drop(Box::from_raw(handle));
}

/// Add a text block with default line height (1.2).
///
/// # Safety
/// - `handle` must be a valid FlowLayout handle.
/// - `text` must be a valid null-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_flow_layout_add_text(
    handle: *mut FlowLayoutHandle,
    text: *const c_char,
    font: StandardFont,
    font_size: f64,
) -> c_int {
    clear_last_error();
    if handle.is_null() || text.is_null() {
        set_last_error("Null pointer provided to oxidize_flow_layout_add_text");
        return ErrorCode::NullPointer as c_int;
    }
    let text_str = match CStr::from_ptr(text).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in text");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    (*handle)
        .inner
        .add_text(text_str, font.to_oxidize(), font_size);
    ErrorCode::Success as c_int
}

/// Add a text block with custom line height.
///
/// # Safety
/// - `handle` must be a valid FlowLayout handle.
/// - `text` must be a valid null-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_flow_layout_add_text_with_line_height(
    handle: *mut FlowLayoutHandle,
    text: *const c_char,
    font: StandardFont,
    font_size: f64,
    line_height: f64,
) -> c_int {
    clear_last_error();
    if handle.is_null() || text.is_null() {
        set_last_error("Null pointer provided to oxidize_flow_layout_add_text_with_line_height");
        return ErrorCode::NullPointer as c_int;
    }
    let text_str = match CStr::from_ptr(text).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in text");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    (*handle)
        .inner
        .add_text_with_line_height(text_str, font.to_oxidize(), font_size, line_height);
    ErrorCode::Success as c_int
}

/// Add vertical spacing in points.
///
/// # Safety
/// - `handle` must be a valid FlowLayout handle.
#[no_mangle]
pub unsafe extern "C" fn oxidize_flow_layout_add_spacer(
    handle: *mut FlowLayoutHandle,
    points: f64,
) -> c_int {
    clear_last_error();
    if handle.is_null() {
        set_last_error("Null pointer provided to oxidize_flow_layout_add_spacer");
        return ErrorCode::NullPointer as c_int;
    }
    (*handle).inner.add_spacer(points);
    ErrorCode::Success as c_int
}

/// Add a simple table to the layout from JSON.
///
/// JSON format: `{"column_widths":[100.0,200.0],"headers":["A","B"],"rows":[["a1","b1"],["a2","b2"]]}`
///
/// # Safety
/// - `handle` must be a valid FlowLayout handle.
/// - `table_json` must be a valid null-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_flow_layout_add_table(
    handle: *mut FlowLayoutHandle,
    table_json: *const c_char,
) -> c_int {
    clear_last_error();
    if handle.is_null() || table_json.is_null() {
        set_last_error("Null pointer provided to oxidize_flow_layout_add_table");
        return ErrorCode::NullPointer as c_int;
    }
    let table = match parse_simple_table(table_json) {
        Ok(t) => t,
        Err(code) => return code,
    };
    (*handle).inner.add_table(table);
    ErrorCode::Success as c_int
}

/// Add rich text (mixed-style single line) from a JSON array of spans.
///
/// JSON format: `[{"text":"...","font":0,"font_size":12.0,"r":0.0,"g":0.0,"b":0.0}, ...]`
///
/// # Safety
/// - `handle` must be a valid FlowLayout handle.
/// - `spans_json` must be a valid null-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_flow_layout_add_rich_text(
    handle: *mut FlowLayoutHandle,
    spans_json: *const c_char,
) -> c_int {
    clear_last_error();
    if handle.is_null() || spans_json.is_null() {
        set_last_error("Null pointer provided to oxidize_flow_layout_add_rich_text");
        return ErrorCode::NullPointer as c_int;
    }
    let rich = match parse_rich_text(spans_json) {
        Ok(r) => r,
        Err(code) => return code,
    };
    (*handle).inner.add_rich_text(rich);
    ErrorCode::Success as c_int
}

/// Add an image to the layout, left-aligned.
///
/// # Safety
/// - `handle` must be a valid FlowLayout handle.
/// - `name` must be a valid null-terminated UTF-8 string.
/// - `image_handle` must be a valid ImageHandle.
#[no_mangle]
pub unsafe extern "C" fn oxidize_flow_layout_add_image(
    handle: *mut FlowLayoutHandle,
    name: *const c_char,
    image_handle: *const ImageHandle,
    max_width: f64,
    max_height: f64,
) -> c_int {
    clear_last_error();
    if handle.is_null() || name.is_null() || image_handle.is_null() {
        set_last_error("Null pointer provided to oxidize_flow_layout_add_image");
        return ErrorCode::NullPointer as c_int;
    }
    let name_str = match CStr::from_ptr(name).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in image name");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    let img = Arc::new((*image_handle).inner.clone());
    (*handle)
        .inner
        .add_image(name_str, img, max_width, max_height);
    ErrorCode::Success as c_int
}

/// Add an image to the layout, centered horizontally.
///
/// # Safety
/// - `handle` must be a valid FlowLayout handle.
/// - `name` must be a valid null-terminated UTF-8 string.
/// - `image_handle` must be a valid ImageHandle.
#[no_mangle]
pub unsafe extern "C" fn oxidize_flow_layout_add_image_centered(
    handle: *mut FlowLayoutHandle,
    name: *const c_char,
    image_handle: *const ImageHandle,
    max_width: f64,
    max_height: f64,
) -> c_int {
    clear_last_error();
    if handle.is_null() || name.is_null() || image_handle.is_null() {
        set_last_error("Null pointer provided to oxidize_flow_layout_add_image_centered");
        return ErrorCode::NullPointer as c_int;
    }
    let name_str = match CStr::from_ptr(name).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in image name");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    let img = Arc::new((*image_handle).inner.clone());
    (*handle)
        .inner
        .add_image_centered(name_str, img, max_width, max_height);
    ErrorCode::Success as c_int
}

/// Build the layout into a document, creating pages as needed.
///
/// # Safety
/// - `handle` must be a valid FlowLayout handle.
/// - `doc` must be a valid DocumentHandle.
#[no_mangle]
pub unsafe extern "C" fn oxidize_flow_layout_build_into(
    handle: *const FlowLayoutHandle,
    doc: *mut DocumentHandle,
) -> c_int {
    clear_last_error();
    if handle.is_null() || doc.is_null() {
        set_last_error("Null pointer provided to oxidize_flow_layout_build_into");
        return ErrorCode::NullPointer as c_int;
    }
    if let Err(e) = (*handle).inner.build_into(&mut (*doc).inner) {
        set_last_error(format!("Failed to build layout into document: {e}"));
        return ErrorCode::PdfParseError as c_int;
    }
    ErrorCode::Success as c_int
}

/// Get the content width (page width minus margins).
///
/// # Safety
/// - `handle` must be a valid FlowLayout handle.
/// - `out_width` must be a valid pointer.
#[no_mangle]
pub unsafe extern "C" fn oxidize_flow_layout_content_width(
    handle: *const FlowLayoutHandle,
    out_width: *mut f64,
) -> c_int {
    clear_last_error();
    if handle.is_null() || out_width.is_null() {
        set_last_error("Null pointer provided to oxidize_flow_layout_content_width");
        return ErrorCode::NullPointer as c_int;
    }
    *out_width = (*handle).config.content_width();
    ErrorCode::Success as c_int
}

/// Get the usable height (page height minus margins).
///
/// # Safety
/// - `handle` must be a valid FlowLayout handle.
/// - `out_height` must be a valid pointer.
#[no_mangle]
pub unsafe extern "C" fn oxidize_flow_layout_usable_height(
    handle: *const FlowLayoutHandle,
    out_height: *mut f64,
) -> c_int {
    clear_last_error();
    if handle.is_null() || out_height.is_null() {
        set_last_error("Null pointer provided to oxidize_flow_layout_usable_height");
        return ErrorCode::NullPointer as c_int;
    }
    *out_height = (*handle).config.usable_height();
    ErrorCode::Success as c_int
}

// ── DocumentBuilder ──────────────────────────────────────────────────────────

/// Opaque handle wrapping an `oxidize_pdf::layout::DocumentBuilder`.
/// Uses Option for take/replace pattern (owned-chaining API).
pub struct DocumentBuilderHandle {
    pub(crate) inner: Option<oxidize_pdf::layout::DocumentBuilder>,
}

/// Create a DocumentBuilder with A4 page size and default 72pt margins.
///
/// # Safety
/// - `out_handle` must be a valid pointer.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_builder_create_a4(
    out_handle: *mut *mut DocumentBuilderHandle,
) -> c_int {
    clear_last_error();
    if out_handle.is_null() {
        set_last_error("Null pointer provided to oxidize_document_builder_create_a4");
        return ErrorCode::NullPointer as c_int;
    }
    *out_handle = std::ptr::null_mut();

    let builder = oxidize_pdf::layout::DocumentBuilder::a4();
    *out_handle = Box::into_raw(Box::new(DocumentBuilderHandle {
        inner: Some(builder),
    }));
    ErrorCode::Success as c_int
}

/// Create a DocumentBuilder with custom dimensions and margins.
///
/// # Safety
/// - `out_handle` must be a valid pointer.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_builder_create(
    width: f64,
    height: f64,
    margin_left: f64,
    margin_right: f64,
    margin_top: f64,
    margin_bottom: f64,
    out_handle: *mut *mut DocumentBuilderHandle,
) -> c_int {
    clear_last_error();
    if out_handle.is_null() {
        set_last_error("Null pointer provided to oxidize_document_builder_create");
        return ErrorCode::NullPointer as c_int;
    }
    *out_handle = std::ptr::null_mut();

    let config = oxidize_pdf::layout::PageConfig::new(
        width,
        height,
        margin_left,
        margin_right,
        margin_top,
        margin_bottom,
    );
    let builder = oxidize_pdf::layout::DocumentBuilder::new(config);
    *out_handle = Box::into_raw(Box::new(DocumentBuilderHandle {
        inner: Some(builder),
    }));
    ErrorCode::Success as c_int
}

/// Free a DocumentBuilder handle.
///
/// # Safety
/// - `handle` must have been returned by `oxidize_document_builder_create*`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_builder_free(handle: *mut DocumentBuilderHandle) {
    if handle.is_null() {
        return;
    }
    drop(Box::from_raw(handle));
}

/// Helper macro for DocumentBuilder take/replace pattern.
macro_rules! builder_take_replace {
    ($handle:expr, $fn_name:expr, $op:expr) => {{
        let h = &mut *$handle;
        let builder = match h.inner.take() {
            Some(b) => b,
            None => {
                set_last_error(concat!(
                    "DocumentBuilder has already been consumed in ",
                    $fn_name
                ));
                return ErrorCode::PdfParseError as c_int;
            }
        };
        h.inner = Some($op(builder));
        ErrorCode::Success as c_int
    }};
}

/// Add a text block with default line height (1.2).
///
/// # Safety
/// - `handle` must be a valid DocumentBuilder handle.
/// - `text` must be a valid null-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_builder_add_text(
    handle: *mut DocumentBuilderHandle,
    text: *const c_char,
    font: StandardFont,
    font_size: f64,
) -> c_int {
    clear_last_error();
    if handle.is_null() || text.is_null() {
        set_last_error("Null pointer provided to oxidize_document_builder_add_text");
        return ErrorCode::NullPointer as c_int;
    }
    let text_str = match CStr::from_ptr(text).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in text");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    builder_take_replace!(
        handle,
        "add_text",
        |b: oxidize_pdf::layout::DocumentBuilder| {
            b.add_text(text_str, font.to_oxidize(), font_size)
        }
    )
}

/// Add a text block with custom line height.
///
/// # Safety
/// - `handle` must be a valid DocumentBuilder handle.
/// - `text` must be a valid null-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_builder_add_text_with_line_height(
    handle: *mut DocumentBuilderHandle,
    text: *const c_char,
    font: StandardFont,
    font_size: f64,
    line_height: f64,
) -> c_int {
    clear_last_error();
    if handle.is_null() || text.is_null() {
        set_last_error(
            "Null pointer provided to oxidize_document_builder_add_text_with_line_height",
        );
        return ErrorCode::NullPointer as c_int;
    }
    let text_str = match CStr::from_ptr(text).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in text");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    builder_take_replace!(
        handle,
        "add_text_with_line_height",
        |b: oxidize_pdf::layout::DocumentBuilder| {
            b.add_text_with_line_height(text_str, font.to_oxidize(), font_size, line_height)
        }
    )
}

/// Add vertical spacing in points.
///
/// # Safety
/// - `handle` must be a valid DocumentBuilder handle.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_builder_add_spacer(
    handle: *mut DocumentBuilderHandle,
    points: f64,
) -> c_int {
    clear_last_error();
    if handle.is_null() {
        set_last_error("Null pointer provided to oxidize_document_builder_add_spacer");
        return ErrorCode::NullPointer as c_int;
    }
    builder_take_replace!(
        handle,
        "add_spacer",
        |b: oxidize_pdf::layout::DocumentBuilder| { b.add_spacer(points) }
    )
}

/// Add a simple table to the builder from JSON.
///
/// JSON format: `{"column_widths":[100.0,200.0],"headers":["A","B"],"rows":[["a1","b1"],["a2","b2"]]}`
///
/// # Safety
/// - `handle` must be a valid DocumentBuilder handle.
/// - `table_json` must be a valid null-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_builder_add_table(
    handle: *mut DocumentBuilderHandle,
    table_json: *const c_char,
) -> c_int {
    clear_last_error();
    if handle.is_null() || table_json.is_null() {
        set_last_error("Null pointer provided to oxidize_document_builder_add_table");
        return ErrorCode::NullPointer as c_int;
    }
    let table = match parse_simple_table(table_json) {
        Ok(t) => t,
        Err(code) => return code,
    };
    builder_take_replace!(
        handle,
        "add_table",
        |b: oxidize_pdf::layout::DocumentBuilder| { b.add_table(table) }
    )
}

/// Add rich text from a JSON array of spans.
///
/// # Safety
/// - `handle` must be a valid DocumentBuilder handle.
/// - `spans_json` must be a valid null-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_builder_add_rich_text(
    handle: *mut DocumentBuilderHandle,
    spans_json: *const c_char,
) -> c_int {
    clear_last_error();
    if handle.is_null() || spans_json.is_null() {
        set_last_error("Null pointer provided to oxidize_document_builder_add_rich_text");
        return ErrorCode::NullPointer as c_int;
    }
    let rich = match parse_rich_text(spans_json) {
        Ok(r) => r,
        Err(code) => return code,
    };
    builder_take_replace!(
        handle,
        "add_rich_text",
        |b: oxidize_pdf::layout::DocumentBuilder| { b.add_rich_text(rich) }
    )
}

/// Add an image to the builder, left-aligned.
///
/// # Safety
/// - `handle` must be a valid DocumentBuilder handle.
/// - `name` must be a valid null-terminated UTF-8 string.
/// - `image_handle` must be a valid ImageHandle.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_builder_add_image(
    handle: *mut DocumentBuilderHandle,
    name: *const c_char,
    image_handle: *const ImageHandle,
    max_width: f64,
    max_height: f64,
) -> c_int {
    clear_last_error();
    if handle.is_null() || name.is_null() || image_handle.is_null() {
        set_last_error("Null pointer provided to oxidize_document_builder_add_image");
        return ErrorCode::NullPointer as c_int;
    }
    let name_str = match CStr::from_ptr(name).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in image name");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    let img = Arc::new((*image_handle).inner.clone());
    builder_take_replace!(
        handle,
        "add_image",
        |b: oxidize_pdf::layout::DocumentBuilder| {
            b.add_image(name_str, img, max_width, max_height)
        }
    )
}

/// Add an image to the builder, centered horizontally.
///
/// # Safety
/// - `handle` must be a valid DocumentBuilder handle.
/// - `name` must be a valid null-terminated UTF-8 string.
/// - `image_handle` must be a valid ImageHandle.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_builder_add_image_centered(
    handle: *mut DocumentBuilderHandle,
    name: *const c_char,
    image_handle: *const ImageHandle,
    max_width: f64,
    max_height: f64,
) -> c_int {
    clear_last_error();
    if handle.is_null() || name.is_null() || image_handle.is_null() {
        set_last_error("Null pointer provided to oxidize_document_builder_add_image_centered");
        return ErrorCode::NullPointer as c_int;
    }
    let name_str = match CStr::from_ptr(name).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in image name");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    let img = Arc::new((*image_handle).inner.clone());
    builder_take_replace!(
        handle,
        "add_image_centered",
        |b: oxidize_pdf::layout::DocumentBuilder| {
            b.add_image_centered(name_str, img, max_width, max_height)
        }
    )
}

/// Build the document, creating pages as needed. The builder is consumed by this call.
///
/// # Safety
/// - `handle` must be a valid DocumentBuilder handle.
/// - `out_doc` must be a valid pointer.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_builder_build(
    handle: *mut DocumentBuilderHandle,
    out_doc: *mut *mut DocumentHandle,
) -> c_int {
    clear_last_error();
    if handle.is_null() || out_doc.is_null() {
        set_last_error("Null pointer provided to oxidize_document_builder_build");
        return ErrorCode::NullPointer as c_int;
    }
    *out_doc = std::ptr::null_mut();

    let h = &mut *handle;
    let builder = match h.inner.take() {
        Some(b) => b,
        None => {
            set_last_error("DocumentBuilder has already been consumed (built)");
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let doc = match builder.build() {
        Ok(d) => d,
        Err(e) => {
            set_last_error(format!("Failed to build document: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    *out_doc = Box::into_raw(Box::new(DocumentHandle {
        inner: doc,
        pending_open_action_pdf: None,
    }));
    ErrorCode::Success as c_int
}

// ── Shared helpers ───────────────────────────────────────────────────────────

/// JSON representation of a simple table for deserialization.
#[derive(serde::Deserialize)]
struct SimpleTableJson {
    column_widths: Vec<f64>,
    #[serde(default)]
    headers: Vec<String>,
    #[serde(default)]
    rows: Vec<Vec<String>>,
}

/// Parse a JSON object into a simple `Table`.
///
/// # Safety
/// - `table_json` must be a valid null-terminated UTF-8 string.
unsafe fn parse_simple_table(table_json: *const c_char) -> Result<oxidize_pdf::text::Table, c_int> {
    let json_str = match CStr::from_ptr(table_json).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in table_json");
            return Err(ErrorCode::InvalidUtf8 as c_int);
        }
    };
    let data: SimpleTableJson = match serde_json::from_str(json_str) {
        Ok(v) => v,
        Err(e) => {
            set_last_error(format!("Failed to parse table_json: {e}"));
            return Err(ErrorCode::SerializationError as c_int);
        }
    };

    let mut table = oxidize_pdf::text::Table::new(data.column_widths);
    if !data.headers.is_empty() {
        if let Err(e) = table.add_header_row(data.headers) {
            set_last_error(format!("Failed to add header row: {e}"));
            return Err(ErrorCode::PdfParseError as c_int);
        }
    }
    for row in data.rows {
        if let Err(e) = table.add_row(row) {
            set_last_error(format!("Failed to add row: {e}"));
            return Err(ErrorCode::PdfParseError as c_int);
        }
    }
    Ok(table)
}

/// JSON representation of a text span for deserialization.
#[derive(serde::Deserialize)]
struct SpanJson {
    text: String,
    font: i32,
    font_size: f64,
    r: f64,
    g: f64,
    b: f64,
}

impl SpanJson {
    fn to_oxidize(&self) -> oxidize_pdf::layout::TextSpan {
        let font = match self.font {
            0 => oxidize_pdf::Font::Helvetica,
            1 => oxidize_pdf::Font::HelveticaBold,
            2 => oxidize_pdf::Font::HelveticaOblique,
            3 => oxidize_pdf::Font::HelveticaBoldOblique,
            4 => oxidize_pdf::Font::TimesRoman,
            5 => oxidize_pdf::Font::TimesBold,
            6 => oxidize_pdf::Font::TimesItalic,
            7 => oxidize_pdf::Font::TimesBoldItalic,
            8 => oxidize_pdf::Font::Courier,
            9 => oxidize_pdf::Font::CourierBold,
            10 => oxidize_pdf::Font::CourierOblique,
            11 => oxidize_pdf::Font::CourierBoldOblique,
            12 => oxidize_pdf::Font::Symbol,
            13 => oxidize_pdf::Font::ZapfDingbats,
            _ => oxidize_pdf::Font::Helvetica,
        };
        let color = oxidize_pdf::Color::Rgb(self.r, self.g, self.b);
        oxidize_pdf::layout::TextSpan::new(&self.text, font, self.font_size, color)
    }
}

/// Parse a JSON array of spans into a RichText.
///
/// # Safety
/// - `spans_json` must be a valid null-terminated UTF-8 string.
unsafe fn parse_rich_text(
    spans_json: *const c_char,
) -> Result<oxidize_pdf::layout::RichText, c_int> {
    let json_str = match CStr::from_ptr(spans_json).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in spans_json");
            return Err(ErrorCode::InvalidUtf8 as c_int);
        }
    };
    let spans_data: Vec<SpanJson> = match serde_json::from_str(json_str) {
        Ok(v) => v,
        Err(e) => {
            set_last_error(format!("Failed to parse spans_json: {e}"));
            return Err(ErrorCode::SerializationError as c_int);
        }
    };
    let spans: Vec<oxidize_pdf::layout::TextSpan> =
        spans_data.iter().map(|s| s.to_oxidize()).collect();
    Ok(oxidize_pdf::layout::RichText::new(spans))
}

// ── Tests ────────────────────────────────────────────────────────────────────

#[cfg(test)]
mod tests {
    use super::*;
    use crate::document::{
        oxidize_document_create, oxidize_document_free, oxidize_document_page_count,
    };
    use std::ffi::CString;
    use std::os::raw::c_int;
    use std::ptr;

    // ── FlowLayout tests ─────────────────────────────────────────────────

    #[test]
    fn test_flow_layout_create_a4_returns_non_null() {
        let mut handle: *mut FlowLayoutHandle = ptr::null_mut();
        let code = unsafe { oxidize_flow_layout_create_a4(&mut handle) };
        assert_eq!(code, ErrorCode::Success as c_int);
        assert!(!handle.is_null());
        unsafe { oxidize_flow_layout_free(handle) };
    }

    #[test]
    fn test_flow_layout_create_null_out_returns_error() {
        let code = unsafe { oxidize_flow_layout_create_a4(ptr::null_mut()) };
        assert_eq!(code, ErrorCode::NullPointer as c_int);
    }

    #[test]
    fn test_flow_layout_create_custom_returns_non_null() {
        let mut handle: *mut FlowLayoutHandle = ptr::null_mut();
        let code = unsafe {
            oxidize_flow_layout_create(595.0, 842.0, 50.0, 50.0, 50.0, 50.0, &mut handle)
        };
        assert_eq!(code, ErrorCode::Success as c_int);
        assert!(!handle.is_null());
        unsafe { oxidize_flow_layout_free(handle) };
    }

    #[test]
    fn test_flow_layout_add_text_returns_success() {
        let mut handle: *mut FlowLayoutHandle = ptr::null_mut();
        unsafe { oxidize_flow_layout_create_a4(&mut handle) };
        let text = CString::new("Hello").unwrap();
        let code = unsafe {
            oxidize_flow_layout_add_text(handle, text.as_ptr(), StandardFont::Helvetica, 12.0)
        };
        assert_eq!(code, ErrorCode::Success as c_int);
        unsafe { oxidize_flow_layout_free(handle) };
    }

    #[test]
    fn test_flow_layout_add_text_null_handle_returns_error() {
        let text = CString::new("Hello").unwrap();
        let code = unsafe {
            oxidize_flow_layout_add_text(
                ptr::null_mut(),
                text.as_ptr(),
                StandardFont::Helvetica,
                12.0,
            )
        };
        assert_eq!(code, ErrorCode::NullPointer as c_int);
    }

    #[test]
    fn test_flow_layout_add_text_with_line_height_success() {
        let mut handle: *mut FlowLayoutHandle = ptr::null_mut();
        unsafe { oxidize_flow_layout_create_a4(&mut handle) };
        let text = CString::new("Hello").unwrap();
        let code = unsafe {
            oxidize_flow_layout_add_text_with_line_height(
                handle,
                text.as_ptr(),
                StandardFont::Helvetica,
                12.0,
                1.5,
            )
        };
        assert_eq!(code, ErrorCode::Success as c_int);
        unsafe { oxidize_flow_layout_free(handle) };
    }

    #[test]
    fn test_flow_layout_add_spacer_success() {
        let mut handle: *mut FlowLayoutHandle = ptr::null_mut();
        unsafe { oxidize_flow_layout_create_a4(&mut handle) };
        let code = unsafe { oxidize_flow_layout_add_spacer(handle, 20.0) };
        assert_eq!(code, ErrorCode::Success as c_int);
        unsafe { oxidize_flow_layout_free(handle) };
    }

    #[test]
    fn test_flow_layout_add_table_success() {
        let mut layout_h: *mut FlowLayoutHandle = ptr::null_mut();
        unsafe { oxidize_flow_layout_create_a4(&mut layout_h) };

        let json = CString::new(
            r#"{"column_widths":[100.0,200.0],"headers":["A","B"],"rows":[["a1","b1"]]}"#,
        )
        .unwrap();
        let code = unsafe { oxidize_flow_layout_add_table(layout_h, json.as_ptr()) };
        assert_eq!(code, ErrorCode::Success as c_int);
        unsafe { oxidize_flow_layout_free(layout_h) };
    }

    #[test]
    fn test_flow_layout_add_rich_text_success() {
        let mut handle: *mut FlowLayoutHandle = ptr::null_mut();
        unsafe { oxidize_flow_layout_create_a4(&mut handle) };
        let json =
            CString::new(r#"[{"text":"Hello","font":0,"font_size":12.0,"r":0.0,"g":0.0,"b":0.0}]"#)
                .unwrap();
        let code = unsafe { oxidize_flow_layout_add_rich_text(handle, json.as_ptr()) };
        assert_eq!(code, ErrorCode::Success as c_int);
        unsafe { oxidize_flow_layout_free(handle) };
    }

    #[test]
    fn test_flow_layout_add_rich_text_invalid_json_returns_error() {
        let mut handle: *mut FlowLayoutHandle = ptr::null_mut();
        unsafe { oxidize_flow_layout_create_a4(&mut handle) };
        let json = CString::new("not valid json").unwrap();
        let code = unsafe { oxidize_flow_layout_add_rich_text(handle, json.as_ptr()) };
        assert_eq!(code, ErrorCode::SerializationError as c_int);
        unsafe { oxidize_flow_layout_free(handle) };
    }

    #[test]
    fn test_flow_layout_build_into_creates_pages() {
        let mut layout_h: *mut FlowLayoutHandle = ptr::null_mut();
        unsafe { oxidize_flow_layout_create_a4(&mut layout_h) };
        let text = CString::new("Test content").unwrap();
        unsafe {
            oxidize_flow_layout_add_text(layout_h, text.as_ptr(), StandardFont::Helvetica, 12.0)
        };

        let doc_h = unsafe { oxidize_document_create() };
        let code = unsafe { oxidize_flow_layout_build_into(layout_h, doc_h) };
        assert_eq!(code, ErrorCode::Success as c_int);

        let mut count: usize = 0;
        unsafe { oxidize_document_page_count(doc_h, &mut count) };
        assert_eq!(count, 1);

        unsafe { oxidize_document_free(doc_h) };
        unsafe { oxidize_flow_layout_free(layout_h) };
    }

    #[test]
    fn test_flow_layout_a4_content_width() {
        let mut handle: *mut FlowLayoutHandle = ptr::null_mut();
        unsafe { oxidize_flow_layout_create_a4(&mut handle) };
        let mut width: f64 = 0.0;
        let code = unsafe { oxidize_flow_layout_content_width(handle, &mut width) };
        assert_eq!(code, ErrorCode::Success as c_int);
        assert!((width - 451.0).abs() < 0.01);
        unsafe { oxidize_flow_layout_free(handle) };
    }

    #[test]
    fn test_flow_layout_a4_usable_height() {
        let mut handle: *mut FlowLayoutHandle = ptr::null_mut();
        unsafe { oxidize_flow_layout_create_a4(&mut handle) };
        let mut height: f64 = 0.0;
        let code = unsafe { oxidize_flow_layout_usable_height(handle, &mut height) };
        assert_eq!(code, ErrorCode::Success as c_int);
        assert!((height - 698.0).abs() < 0.01);
        unsafe { oxidize_flow_layout_free(handle) };
    }

    // ── DocumentBuilder tests ────────────────────────────────────────────

    #[test]
    fn test_document_builder_create_a4_success() {
        let mut handle: *mut DocumentBuilderHandle = ptr::null_mut();
        let code = unsafe { oxidize_document_builder_create_a4(&mut handle) };
        assert_eq!(code, ErrorCode::Success as c_int);
        assert!(!handle.is_null());
        unsafe { oxidize_document_builder_free(handle) };
    }

    #[test]
    fn test_document_builder_create_null_out_returns_error() {
        let code = unsafe { oxidize_document_builder_create_a4(ptr::null_mut()) };
        assert_eq!(code, ErrorCode::NullPointer as c_int);
    }

    #[test]
    fn test_document_builder_add_text_success() {
        let mut handle: *mut DocumentBuilderHandle = ptr::null_mut();
        unsafe { oxidize_document_builder_create_a4(&mut handle) };
        let text = CString::new("Hello").unwrap();
        let code = unsafe {
            oxidize_document_builder_add_text(handle, text.as_ptr(), StandardFont::Helvetica, 12.0)
        };
        assert_eq!(code, ErrorCode::Success as c_int);
        unsafe { oxidize_document_builder_free(handle) };
    }

    #[test]
    fn test_document_builder_add_spacer_success() {
        let mut handle: *mut DocumentBuilderHandle = ptr::null_mut();
        unsafe { oxidize_document_builder_create_a4(&mut handle) };
        let code = unsafe { oxidize_document_builder_add_spacer(handle, 20.0) };
        assert_eq!(code, ErrorCode::Success as c_int);
        unsafe { oxidize_document_builder_free(handle) };
    }

    #[test]
    fn test_document_builder_add_table_success() {
        let mut builder_h: *mut DocumentBuilderHandle = ptr::null_mut();
        unsafe { oxidize_document_builder_create_a4(&mut builder_h) };

        let json = CString::new(
            r#"{"column_widths":[100.0,200.0],"headers":["A","B"],"rows":[["a1","b1"]]}"#,
        )
        .unwrap();
        let code = unsafe { oxidize_document_builder_add_table(builder_h, json.as_ptr()) };
        assert_eq!(code, ErrorCode::Success as c_int);
        unsafe { oxidize_document_builder_free(builder_h) };
    }

    #[test]
    fn test_document_builder_add_rich_text_success() {
        let mut handle: *mut DocumentBuilderHandle = ptr::null_mut();
        unsafe { oxidize_document_builder_create_a4(&mut handle) };
        let json =
            CString::new(r#"[{"text":"Bold","font":1,"font_size":14.0,"r":0.0,"g":0.0,"b":0.0}]"#)
                .unwrap();
        let code = unsafe { oxidize_document_builder_add_rich_text(handle, json.as_ptr()) };
        assert_eq!(code, ErrorCode::Success as c_int);
        unsafe { oxidize_document_builder_free(handle) };
    }

    #[test]
    fn test_document_builder_build_produces_document() {
        let mut builder_h: *mut DocumentBuilderHandle = ptr::null_mut();
        unsafe { oxidize_document_builder_create_a4(&mut builder_h) };
        let text = CString::new("Content").unwrap();
        unsafe {
            oxidize_document_builder_add_text(
                builder_h,
                text.as_ptr(),
                StandardFont::Helvetica,
                12.0,
            )
        };

        let mut doc_h: *mut DocumentHandle = ptr::null_mut();
        let code = unsafe { oxidize_document_builder_build(builder_h, &mut doc_h) };
        assert_eq!(code, ErrorCode::Success as c_int);
        assert!(!doc_h.is_null());

        let mut count: usize = 0;
        unsafe { oxidize_document_page_count(doc_h, &mut count) };
        assert_eq!(count, 1);

        unsafe { oxidize_document_free(doc_h) };
        unsafe { oxidize_document_builder_free(builder_h) };
    }

    #[test]
    fn test_document_builder_build_twice_returns_error() {
        let mut builder_h: *mut DocumentBuilderHandle = ptr::null_mut();
        unsafe { oxidize_document_builder_create_a4(&mut builder_h) };
        let text = CString::new("Content").unwrap();
        unsafe {
            oxidize_document_builder_add_text(
                builder_h,
                text.as_ptr(),
                StandardFont::Helvetica,
                12.0,
            )
        };

        let mut doc_h: *mut DocumentHandle = ptr::null_mut();
        unsafe { oxidize_document_builder_build(builder_h, &mut doc_h) };

        // Second build should fail
        let mut doc_h2: *mut DocumentHandle = ptr::null_mut();
        let code = unsafe { oxidize_document_builder_build(builder_h, &mut doc_h2) };
        assert_eq!(code, ErrorCode::PdfParseError as c_int);
        assert!(doc_h2.is_null());

        unsafe { oxidize_document_free(doc_h) };
        unsafe { oxidize_document_builder_free(builder_h) };
    }
}
