use std::ffi::CStr;
use std::os::raw::{c_char, c_int};

use crate::page::PageHandle;
use crate::{clear_last_error, set_last_error, ErrorCode};

/// Opaque handle wrapping an `AdvancedTableBuilder`.
/// Uses Option to support take/replace pattern for consuming builder methods.
pub struct TableBuilderHandle {
    pub(crate) inner: Option<oxidize_pdf::advanced_tables::AdvancedTableBuilder>,
}

/// Create a new table builder with equal-width columns.
///
/// `headers_json` is a JSON array of column header strings, e.g. `["Name", "Age", "City"]`.
///
/// # Safety
/// - `headers_json` must be a valid null-terminated UTF-8 string.
/// - `out_handle` must be a valid pointer.
#[no_mangle]
pub unsafe extern "C" fn oxidize_table_builder_create(
    headers_json: *const c_char,
    total_width: f64,
    out_handle: *mut *mut TableBuilderHandle,
) -> c_int {
    clear_last_error();
    if headers_json.is_null() || out_handle.is_null() {
        set_last_error("Null pointer provided to oxidize_table_builder_create");
        return ErrorCode::NullPointer as c_int;
    }
    *out_handle = std::ptr::null_mut();

    let json_str = match CStr::from_ptr(headers_json).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in headers_json");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    let headers: Vec<String> = match serde_json::from_str(json_str) {
        Ok(v) => v,
        Err(e) => {
            set_last_error(format!("Failed to parse headers_json: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };

    let header_refs: Vec<&str> = headers.iter().map(|s| s.as_str()).collect();
    let builder = oxidize_pdf::advanced_tables::AdvancedTableBuilder::new()
        .columns_equal_width(header_refs, total_width);

    *out_handle = Box::into_raw(Box::new(TableBuilderHandle {
        inner: Some(builder),
    }));
    ErrorCode::Success as c_int
}

/// Free a table builder handle.
///
/// # Safety
/// - `handle` must have been returned by `oxidize_table_builder_create`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_table_builder_free(handle: *mut TableBuilderHandle) {
    if handle.is_null() {
        return;
    }
    drop(Box::from_raw(handle));
}

/// Set the table position.
///
/// # Safety
/// - `handle` must be a valid table builder handle.
#[no_mangle]
pub unsafe extern "C" fn oxidize_table_builder_set_position(
    handle: *mut TableBuilderHandle,
    x: f64,
    y: f64,
) -> c_int {
    clear_last_error();
    if handle.is_null() {
        set_last_error("Null pointer provided to oxidize_table_builder_set_position");
        return ErrorCode::NullPointer as c_int;
    }
    let h = &mut *handle;
    let builder = match h.inner.take() {
        Some(b) => b,
        None => {
            set_last_error("Table builder has already been consumed");
            return ErrorCode::PdfParseError as c_int;
        }
    };
    h.inner = Some(builder.position(x, y));
    ErrorCode::Success as c_int
}

/// Add a data row to the table. `cells_json` is a JSON array of strings.
///
/// # Safety
/// - `handle` must be a valid table builder handle.
/// - `cells_json` must be a valid null-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_table_builder_add_row(
    handle: *mut TableBuilderHandle,
    cells_json: *const c_char,
) -> c_int {
    clear_last_error();
    if handle.is_null() || cells_json.is_null() {
        set_last_error("Null pointer provided to oxidize_table_builder_add_row");
        return ErrorCode::NullPointer as c_int;
    }
    let json_str = match CStr::from_ptr(cells_json).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in cells_json");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    let cells: Vec<String> = match serde_json::from_str(json_str) {
        Ok(v) => v,
        Err(e) => {
            set_last_error(format!("Failed to parse cells_json: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };

    let h = &mut *handle;
    let builder = match h.inner.take() {
        Some(b) => b,
        None => {
            set_last_error("Table builder has already been consumed");
            return ErrorCode::PdfParseError as c_int;
        }
    };
    let cell_refs: Vec<&str> = cells.iter().map(|s| s.as_str()).collect();
    h.inner = Some(builder.add_row(cell_refs));
    ErrorCode::Success as c_int
}

/// Build the table and add it to a page. The builder is consumed by this call.
///
/// # Safety
/// - `page` must be a valid page handle.
/// - `builder` must be a valid table builder handle. It is consumed (freed) by this call.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_add_table(
    page: *mut PageHandle,
    builder: *mut TableBuilderHandle,
) -> c_int {
    clear_last_error();
    if page.is_null() || builder.is_null() {
        set_last_error("Null pointer provided to oxidize_page_add_table");
        return ErrorCode::NullPointer as c_int;
    }

    // Consume the builder handle
    let builder_box = Box::from_raw(builder);
    let builder_inner = match builder_box.inner {
        Some(b) => b,
        None => {
            set_last_error("Table builder has already been consumed");
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let table = match builder_inner.build() {
        Ok(t) => t,
        Err(e) => {
            set_last_error(format!("Failed to build table: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    use oxidize_pdf::advanced_tables::AdvancedTableExt;
    if let Err(e) = (*page).inner.add_advanced_table_auto(&table) {
        set_last_error(format!("Failed to add table to page: {e}"));
        return ErrorCode::PdfParseError as c_int;
    }
    ErrorCode::Success as c_int
}
