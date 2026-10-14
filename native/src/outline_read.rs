//! Bounded outline reading. Flatten the hierarchy to keep JSON depth constant.
use crate::{clear_last_error, set_last_error, ErrorCode};
use oxidize_pdf::structure::{DestinationType, OutlineItem, PageDestination};
use serde_json::{json, Value};
use std::ffi::CString;
use std::os::raw::{c_char, c_int};

fn flatten(items: &[OutlineItem]) -> Result<Vec<Value>, String> {
    let mut output = Vec::new();
    let mut pending: Vec<_> = items
        .iter()
        .rev()
        .map(|item| (item, None::<usize>))
        .collect();
    while let Some((item, parent)) = pending.pop() {
        let destination = if let Some(dest) = &item.destination {
            let page = match dest.page {
                PageDestination::PageNumber(page) => page,
                PageDestination::PageRef(_) => {
                    return Err("Unresolved bookmark page reference".into())
                }
            };
            let mut value = json!({"page_index": page});
            let mode = match &dest.dest_type {
                DestinationType::XYZ { left, top, zoom } => {
                    value["left"] = json!(left);
                    value["top"] = json!(top);
                    value["zoom"] = json!(zoom);
                    "XYZ"
                }
                DestinationType::Fit => "Fit",
                DestinationType::FitH { top } => {
                    value["top"] = json!(top);
                    "FitH"
                }
                DestinationType::FitV { left } => {
                    value["left"] = json!(left);
                    "FitV"
                }
                DestinationType::FitR { rect } => {
                    value["left"] = json!(rect.lower_left.x);
                    value["bottom"] = json!(rect.lower_left.y);
                    value["right"] = json!(rect.upper_right.x);
                    value["top"] = json!(rect.upper_right.y);
                    "FitR"
                }
                DestinationType::FitB => "FitB",
                DestinationType::FitBH { top } => {
                    value["top"] = json!(top);
                    "FitBH"
                }
                DestinationType::FitBV { left } => {
                    value["left"] = json!(left);
                    "FitBV"
                }
            };
            value["view_mode"] = json!(mode);
            value
        } else {
            Value::Null
        };
        let color = item.color.map(|c| match c.to_rgb() {
            oxidize_pdf::graphics::Color::Rgb(r, g, b) => [r, g, b],
            _ => unreachable!("to_rgb always returns RGB"),
        });
        let index = output.len();
        output.push(json!({"title": item.title, "parent_index": parent,
            "destination": destination, "color": color, "is_bold": item.flags.bold,
            "is_italic": item.flags.italic, "is_open": item.open}));
        pending.extend(item.children.iter().rev().map(|child| (child, Some(index))));
    }
    Ok(output)
}

/// Read bookmarks as a preorder list with parent indexes and resolved destinations.
///
/// # Safety
/// `bytes` must reference `len` readable bytes; `out_json` must be writable.
/// Free the returned JSON with `oxidize_free_string`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_read_bookmarks(
    bytes: *const u8,
    len: usize,
    max_items: usize,
    max_depth: usize,
    max_named_destinations: usize,
    max_name_tree_nodes: usize,
    out_json: *mut *mut c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if bytes.is_null() || out_json.is_null() {
            set_last_error("Null pointer in bookmark reading");
            return ErrorCode::NullPointer as c_int;
        }
        *out_json = std::ptr::null_mut();
        if len == 0
            || max_items == 0
            || max_depth == 0
            || max_depth > 256
            || max_named_destinations == 0
            || max_name_tree_nodes == 0
        {
            set_last_error("Invalid bookmark limits (depth must be 1..256) or empty PDF");
            return ErrorCode::InvalidArgument as c_int;
        }
        let result = (|| -> Result<String, String> {
            let reader = crate::parser::open_lenient(std::slice::from_raw_parts(bytes, len))?;
            let doc = oxidize_pdf::parser::PdfDocument::new(reader);
            let options = oxidize_pdf::parser::OutlineReadOptions {
                max_items,
                max_depth,
                max_named_destinations,
                max_name_tree_nodes,
            };
            let tree = doc
                .outline_with_options(&options)
                .map_err(|e| e.to_string())?;
            let items = match tree {
                Some(tree) => flatten(&tree.items)?,
                None => Vec::new(),
            };
            serde_json::to_string(&items).map_err(|e| e.to_string())
        })();
        match result {
            Ok(json) => match CString::new(json) {
                Ok(value) => {
                    *out_json = value.into_raw();
                    ErrorCode::Success as c_int
                }
                Err(e) => {
                    set_last_error(e.to_string());
                    ErrorCode::SerializationError as c_int
                }
            },
            Err(e) => {
                set_last_error(e);
                ErrorCode::PdfParseError as c_int
            }
        }
    })
}
