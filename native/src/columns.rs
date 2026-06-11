//! TXT-014 — Multi-column text layout.
//!
//! Bridges `oxidize_pdf`'s `ColumnLayout` so a .NET caller can flow a block of
//! text across N columns on a page with a single JSON call. The text is emitted
//! as real `BT`/`Td`/`Tj`/`ET` operators per column into the page content
//! stream via `GraphicsContext::render_column_layout`.

use std::ffi::CStr;
use std::os::raw::{c_char, c_int};

use serde::Deserialize;

use oxidize_pdf::graphics::Color;
use oxidize_pdf::text::{ColumnContent, ColumnLayout, ColumnOptions, Font, TextAlign};

use crate::page::PageHandle;
use crate::{clear_last_error, set_last_error, ErrorCode};

#[derive(Debug, Deserialize)]
struct ColumnLayoutDto {
    text: String,
    column_count: usize,
    total_width: f64,
    column_gap: f64,
    /// Optional explicit per-column widths; when present, overrides
    /// `column_count` / `total_width` (uses `ColumnLayout::with_custom_widths`).
    #[serde(default)]
    custom_widths: Option<Vec<f64>>,
    start_x: f64,
    start_y: f64,
    column_height: f64,
    /// Standard font index (matches the .NET `StandardFont` enum / preset list).
    #[serde(default)]
    font: Option<String>,
    #[serde(default)]
    font_size: Option<f64>,
    #[serde(default)]
    line_height: Option<f64>,
    /// "left" | "right" | "center" | "justified" (case-insensitive).
    #[serde(default)]
    text_align: Option<String>,
    #[serde(default)]
    balance_columns: Option<bool>,
    #[serde(default)]
    show_separators: Option<bool>,
    /// Text color as [r, g, b] in 0.0..=1.0.
    #[serde(default)]
    color: Option<[f64; 3]>,
}

fn font_from_name(name: &str) -> Font {
    match name {
        "HelveticaBold" => Font::HelveticaBold,
        "HelveticaOblique" => Font::HelveticaOblique,
        "HelveticaBoldOblique" => Font::HelveticaBoldOblique,
        "TimesRoman" => Font::TimesRoman,
        "TimesBold" => Font::TimesBold,
        "TimesItalic" => Font::TimesItalic,
        "TimesBoldItalic" => Font::TimesBoldItalic,
        "Courier" => Font::Courier,
        "CourierBold" => Font::CourierBold,
        "CourierOblique" => Font::CourierOblique,
        "CourierBoldOblique" => Font::CourierBoldOblique,
        _ => Font::Helvetica,
    }
}

fn align_from_name(name: &str) -> TextAlign {
    match name.to_ascii_lowercase().as_str() {
        "right" => TextAlign::Right,
        "center" => TextAlign::Center,
        "justified" | "justify" => TextAlign::Justified,
        _ => TextAlign::Left,
    }
}

/// TXT-014 — Flow `text` across columns on `page`, emitting positioned text per
/// column into the content stream.
///
/// # Safety
/// - `page` must be a valid pointer returned by an `oxidize_page_*` constructor.
/// - `json` must be a valid non-null, null-terminated UTF-8 C string matching
///   the column-layout schema.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_render_columns_json(
    page: *mut PageHandle,
    json: *const c_char,
) -> c_int {
    clear_last_error();
    if page.is_null() || json.is_null() {
        set_last_error("Null pointer provided to oxidize_page_render_columns_json");
        return ErrorCode::NullPointer as c_int;
    }
    let json_str = match CStr::from_ptr(json).to_str() {
        Ok(s) => s,
        Err(_) => {
            set_last_error("Invalid UTF-8 in column-layout JSON");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    let dto: ColumnLayoutDto = match serde_json::from_str(json_str) {
        Ok(d) => d,
        Err(e) => {
            set_last_error(format!("Failed to parse column-layout JSON: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };

    let mut layout = match &dto.custom_widths {
        Some(widths) if !widths.is_empty() => {
            ColumnLayout::with_custom_widths(widths.clone(), dto.column_gap)
        }
        _ => {
            if dto.column_count == 0 {
                set_last_error("column_count must be >= 1");
                return ErrorCode::InvalidArgument as c_int;
            }
            ColumnLayout::new(dto.column_count, dto.total_width, dto.column_gap)
        }
    };

    let mut options = ColumnOptions::default();
    if let Some(f) = &dto.font {
        options.font = font_from_name(f);
    }
    if let Some(sz) = dto.font_size {
        options.font_size = sz;
    }
    if let Some(lh) = dto.line_height {
        options.line_height = lh;
    }
    if let Some(a) = &dto.text_align {
        options.text_align = align_from_name(a);
    }
    if let Some(b) = dto.balance_columns {
        options.balance_columns = b;
    }
    if let Some(s) = dto.show_separators {
        options.show_separators = s;
    }
    if let Some([r, g, b]) = dto.color {
        options.text_color = Color::rgb(r, g, b);
    }
    layout.set_options(options);

    let content = ColumnContent::new(dto.text);

    match (*page).inner.graphics().render_column_layout(
        &layout,
        &content,
        dto.start_x,
        dto.start_y,
        dto.column_height,
    ) {
        Ok(()) => ErrorCode::Success as c_int,
        Err(e) => {
            set_last_error(format!("render_column_layout failed: {e}"));
            ErrorCode::InvalidArgument as c_int
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::page::{oxidize_page_create, oxidize_page_free};
    use oxidize_pdf::{Document, Page};
    use std::ffi::CString;

    fn render_and_dump(json: &str, width: f64, height: f64) -> String {
        unsafe {
            let page = oxidize_page_create(width, height);
            let cjson = CString::new(json).unwrap();
            let rc = oxidize_page_render_columns_json(page, cjson.as_ptr());
            assert_eq!(rc, 0, "render must succeed");
            let inner = std::mem::replace(&mut (*page).inner, Page::new(1.0, 1.0));
            oxidize_page_free(page);
            let mut doc = Document::new();
            doc.set_compress(false);
            doc.add_page(inner);
            let bytes = doc.to_bytes().unwrap();
            String::from_utf8_lossy(&bytes).into_owned()
        }
    }

    #[test]
    fn render_columns_emits_text_in_multiple_columns() {
        // Long text across 2 columns must produce >= 2 BT text blocks at
        // distinct X positions (one per column).
        let json = r#"{
            "text": "Lorem ipsum dolor sit amet consectetur adipiscing elit sed do eiusmod tempor incididunt ut labore et dolore magna aliqua ut enim ad minim veniam quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat duis aute irure dolor in reprehenderit",
            "column_count": 2,
            "total_width": 400.0,
            "column_gap": 20.0,
            "start_x": 50.0,
            "start_y": 750.0,
            "column_height": 600.0,
            "font": "Helvetica",
            "font_size": 10.0,
            "text_align": "left"
        }"#;
        let pdf = render_and_dump(json, 595.0, 842.0);

        // Real text emission: at least one Tj operator and multiple BT blocks.
        assert!(pdf.contains("Tj"), "expected text-show operators");
        let bt_count = pdf.matches("BT").count();
        assert!(
            bt_count >= 2,
            "expected >= 2 text blocks across columns, found {bt_count}"
        );
        // A word from the source text must be present in the stream.
        assert!(
            pdf.contains("Lorem") || pdf.contains("ipsum"),
            "source text must be emitted"
        );
    }

    #[test]
    fn render_columns_zero_count_returns_invalid_argument() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            let json = CString::new(
                r#"{"text":"x","column_count":0,"total_width":400.0,"column_gap":20.0,"start_x":50.0,"start_y":750.0,"column_height":600.0}"#,
            )
            .unwrap();
            let rc = oxidize_page_render_columns_json(page, json.as_ptr());
            assert_eq!(rc, 9, "column_count=0 must return InvalidArgument (9)");
            oxidize_page_free(page);
        }
    }

    #[test]
    fn render_columns_null_returns_null_pointer() {
        unsafe {
            let json = CString::new("{}").unwrap();
            assert_eq!(
                oxidize_page_render_columns_json(std::ptr::null_mut(), json.as_ptr()),
                1
            );
        }
    }
}
