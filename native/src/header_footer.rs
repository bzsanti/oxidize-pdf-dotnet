use std::ffi::CStr;
use std::os::raw::{c_char, c_int};

use crate::page::PageHandle;
use crate::types::StandardFont;
use crate::{clear_last_error, set_last_error, ErrorCode};

/// Set a header on a page with content, font, and size.
///
/// # Safety
/// - `page` must be a valid page handle.
/// - `content` must be a valid null-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_header(
    page: *mut PageHandle,
    content: *const c_char,
    font: StandardFont,
    size: f64,
) -> c_int {
    clear_last_error();
    if page.is_null() || content.is_null() {
        set_last_error("Null pointer provided to oxidize_page_set_header");
        return ErrorCode::NullPointer as c_int;
    }
    let s = match CStr::from_ptr(content).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in header content");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    let header = oxidize_pdf::text::HeaderFooter::new_header(s).with_font(font.to_oxidize(), size);
    (*page).inner.set_header(header);
    ErrorCode::Success as c_int
}

/// Set a footer on a page with content, font, and size.
///
/// # Safety
/// - `page` must be a valid page handle.
/// - `content` must be a valid null-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_set_footer(
    page: *mut PageHandle,
    content: *const c_char,
    font: StandardFont,
    size: f64,
) -> c_int {
    clear_last_error();
    if page.is_null() || content.is_null() {
        set_last_error("Null pointer provided to oxidize_page_set_footer");
        return ErrorCode::NullPointer as c_int;
    }
    let s = match CStr::from_ptr(content).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in footer content");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    let footer = oxidize_pdf::text::HeaderFooter::new_footer(s).with_font(font.to_oxidize(), size);
    (*page).inner.set_footer(footer);
    ErrorCode::Success as c_int
}
