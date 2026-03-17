use std::ffi::CStr;
use std::os::raw::{c_char, c_int};

use crate::page::PageHandle;
use crate::{clear_last_error, set_last_error, ErrorCode};

/// Ordered list style — C-compatible enum for FFI.
#[repr(C)]
#[derive(Clone, Copy)]
pub enum OrderedListStyle {
    Decimal = 0,
    LowerAlpha = 1,
    UpperAlpha = 2,
    LowerRoman = 3,
    UpperRoman = 4,
}

impl OrderedListStyle {
    fn to_oxidize(self) -> oxidize_pdf::text::OrderedListStyle {
        match self {
            OrderedListStyle::Decimal => oxidize_pdf::text::OrderedListStyle::Decimal,
            OrderedListStyle::LowerAlpha => oxidize_pdf::text::OrderedListStyle::LowerAlpha,
            OrderedListStyle::UpperAlpha => oxidize_pdf::text::OrderedListStyle::UpperAlpha,
            OrderedListStyle::LowerRoman => oxidize_pdf::text::OrderedListStyle::LowerRoman,
            OrderedListStyle::UpperRoman => oxidize_pdf::text::OrderedListStyle::UpperRoman,
        }
    }
}

/// Bullet style — C-compatible enum for FFI.
#[repr(C)]
#[derive(Clone, Copy)]
pub enum BulletStyle {
    Disc = 0,
    Circle = 1,
    Square = 2,
    Dash = 3,
}

impl BulletStyle {
    fn to_oxidize(self) -> oxidize_pdf::text::BulletStyle {
        match self {
            BulletStyle::Disc => oxidize_pdf::text::BulletStyle::Disc,
            BulletStyle::Circle => oxidize_pdf::text::BulletStyle::Circle,
            BulletStyle::Square => oxidize_pdf::text::BulletStyle::Square,
            BulletStyle::Dash => oxidize_pdf::text::BulletStyle::Dash,
        }
    }
}

/// Add a quick ordered list to a page. Items are passed as a JSON array of strings.
///
/// # Safety
/// - `page` must be a valid page handle.
/// - `items_json` must be a valid null-terminated UTF-8 string containing a JSON array.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_add_ordered_list(
    page: *mut PageHandle,
    items_json: *const c_char,
    x: f64,
    y: f64,
    style: OrderedListStyle,
) -> c_int {
    clear_last_error();
    if page.is_null() || items_json.is_null() {
        set_last_error("Null pointer provided to oxidize_page_add_ordered_list");
        return ErrorCode::NullPointer as c_int;
    }
    let json_str = match CStr::from_ptr(items_json).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in items_json");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    let items: Vec<String> = match serde_json::from_str(json_str) {
        Ok(v) => v,
        Err(e) => {
            set_last_error(format!("Failed to parse items_json: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };

    use oxidize_pdf::PageLists;
    if let Err(e) = (*page)
        .inner
        .add_quick_ordered_list(items, x, y, style.to_oxidize())
    {
        set_last_error(format!("Failed to add ordered list: {e}"));
        return ErrorCode::PdfParseError as c_int;
    }
    ErrorCode::Success as c_int
}

/// Add a quick unordered list to a page. Items are passed as a JSON array of strings.
///
/// # Safety
/// - `page` must be a valid page handle.
/// - `items_json` must be a valid null-terminated UTF-8 string containing a JSON array.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_add_unordered_list(
    page: *mut PageHandle,
    items_json: *const c_char,
    x: f64,
    y: f64,
    style: BulletStyle,
) -> c_int {
    clear_last_error();
    if page.is_null() || items_json.is_null() {
        set_last_error("Null pointer provided to oxidize_page_add_unordered_list");
        return ErrorCode::NullPointer as c_int;
    }
    let json_str = match CStr::from_ptr(items_json).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in items_json");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    let items: Vec<String> = match serde_json::from_str(json_str) {
        Ok(v) => v,
        Err(e) => {
            set_last_error(format!("Failed to parse items_json: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };

    use oxidize_pdf::PageLists;
    if let Err(e) = (*page)
        .inner
        .add_quick_unordered_list(items, x, y, style.to_oxidize())
    {
        set_last_error(format!("Failed to add unordered list: {e}"));
        return ErrorCode::PdfParseError as c_int;
    }
    ErrorCode::Success as c_int
}
