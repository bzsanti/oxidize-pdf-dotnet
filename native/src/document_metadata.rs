//! FFI for document-level metadata: open actions, viewer preferences,
//! named destinations, page labels, and save-with-`WriterConfig` (milestone M1).
//!
//! These are write-path entry points: they mutate the `oxidize_pdf::Document`
//! behind a [`DocumentHandle`] (created via `oxidize_document_create`) and the
//! result is observed by serializing the document to PDF bytes. Complex inputs
//! cross the boundary as JSON.

use std::ffi::CStr;
use std::os::raw::{c_char, c_int};
use std::ptr;

use oxidize_pdf::actions::Action;
use oxidize_pdf::structure::{Destination, NamedDestinations, PageDestination};
use oxidize_pdf::viewer_preferences::{
    Duplex as CoreDuplex, PageLayout as CorePageLayout, PageMode as CorePageMode,
    PrintScaling as CorePrintScaling, ViewerPreferences,
};
use oxidize_pdf::writer::WriterConfig;
use oxidize_pdf::{PageLabel, PageLabelStyle, PageLabelTree};
use serde::Deserialize;

use crate::document::DocumentHandle;
use crate::{clear_last_error, set_last_error, ErrorCode};

// ── Shared destination payload (used by DOC-014 and DOC-017) ─────────────────

#[derive(Deserialize)]
struct DestinationJson {
    page: u32,
    fit: u8,
    left: Option<f64>,
    top: Option<f64>,
    zoom: Option<f64>,
}

impl DestinationJson {
    fn to_core(&self) -> Destination {
        let page = PageDestination::PageNumber(self.page);
        match self.fit {
            0 => Destination::xyz(page, self.left, self.top, self.zoom),
            1 => Destination::fit(page),
            2 => Destination::fit_h(page, self.top),
            3 => Destination::fit_v(page, self.left),
            5 => Destination::fit_b(page),
            _ => Destination::fit(page),
        }
    }
}

// ── DOC-014: Open action ─────────────────────────────────────────────────────

#[derive(Deserialize)]
struct OpenActionJson {
    kind: String,
    destination: Option<DestinationJson>,
    uri: Option<String>,
}

/// Set the document open action (GoTo a destination, or open a URI) from JSON.
///
/// JSON shapes: `{"kind":"goto","destination":{page,fit,left?,top?,zoom?}}` or
/// `{"kind":"uri","uri":"..."}`. `fit`: 0=XYZ, 1=Fit, 2=FitH, 3=FitV, 5=FitB.
///
/// # Safety
/// - `handle` must be a valid pointer from `oxidize_document_create`.
/// - `json` must be a NUL-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_set_open_action_json(
    handle: *mut DocumentHandle,
    json: *const c_char,
) -> c_int {
    clear_last_error();
    if handle.is_null() || json.is_null() {
        set_last_error("Null pointer to oxidize_document_set_open_action_json");
        return ErrorCode::NullPointer as c_int;
    }
    let s = match CStr::from_ptr(json).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in open action JSON");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    let payload: OpenActionJson = match serde_json::from_str(s) {
        Ok(v) => v,
        Err(e) => {
            set_last_error(format!("Invalid open action JSON: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };

    let action = match payload.kind.as_str() {
        "goto" => match payload.destination {
            Some(dest) => Action::goto(dest.to_core()),
            None => {
                set_last_error("goto open action requires 'destination'");
                return ErrorCode::InvalidArgument as c_int;
            }
        },
        "uri" => match payload.uri {
            Some(uri) => Action::uri(uri),
            None => {
                set_last_error("uri open action requires 'uri'");
                return ErrorCode::InvalidArgument as c_int;
            }
        },
        other => {
            set_last_error(format!("Unknown open action kind: {other}"));
            return ErrorCode::InvalidArgument as c_int;
        }
    };

    (*handle).inner.set_open_action(action);
    ErrorCode::Success as c_int
}

// ── DOC-015: Viewer preferences ──────────────────────────────────────────────

#[derive(Deserialize)]
struct ViewerPrefsJson {
    hide_toolbar: Option<bool>,
    hide_menubar: Option<bool>,
    hide_window_ui: Option<bool>,
    fit_window: Option<bool>,
    center_window: Option<bool>,
    display_doc_title: Option<bool>,
    page_layout: Option<u8>,
    page_mode: Option<u8>,
    print_scaling: Option<u8>,
    duplex: Option<u8>,
    num_copies: Option<u32>,
    pick_tray_by_pdf_size: Option<bool>,
}

impl ViewerPrefsJson {
    fn to_core(&self) -> ViewerPreferences {
        let mut prefs = ViewerPreferences::new();
        if let Some(v) = self.hide_toolbar {
            prefs = prefs.hide_toolbar(v);
        }
        if let Some(v) = self.hide_menubar {
            prefs = prefs.hide_menubar(v);
        }
        if let Some(v) = self.hide_window_ui {
            prefs = prefs.hide_window_ui(v);
        }
        if let Some(v) = self.fit_window {
            prefs = prefs.fit_window(v);
        }
        if let Some(v) = self.center_window {
            prefs = prefs.center_window(v);
        }
        if let Some(v) = self.display_doc_title {
            prefs = prefs.display_doc_title(v);
        }
        if let Some(v) = self.page_layout {
            let layout = match v {
                0 => CorePageLayout::SinglePage,
                1 => CorePageLayout::OneColumn,
                2 => CorePageLayout::TwoColumnLeft,
                3 => CorePageLayout::TwoColumnRight,
                4 => CorePageLayout::TwoPageLeft,
                5 => CorePageLayout::TwoPageRight,
                _ => CorePageLayout::SinglePage,
            };
            prefs = prefs.page_layout(layout);
        }
        if let Some(v) = self.page_mode {
            let mode = match v {
                0 => CorePageMode::UseNone,
                1 => CorePageMode::UseOutlines,
                2 => CorePageMode::UseThumbs,
                3 => CorePageMode::FullScreen,
                4 => CorePageMode::UseOC,
                5 => CorePageMode::UseAttachments,
                _ => CorePageMode::UseNone,
            };
            prefs = prefs.page_mode(mode);
        }
        if let Some(v) = self.print_scaling {
            let scaling = match v {
                1 => CorePrintScaling::None,
                _ => CorePrintScaling::AppDefault,
            };
            prefs = prefs.print_scaling(scaling);
        }
        if let Some(v) = self.duplex {
            let duplex = match v {
                1 => CoreDuplex::DuplexFlipShortEdge,
                2 => CoreDuplex::DuplexFlipLongEdge,
                _ => CoreDuplex::Simplex,
            };
            prefs = prefs.duplex(duplex);
        }
        if let Some(v) = self.num_copies {
            prefs = prefs.num_copies(v);
        }
        if let Some(v) = self.pick_tray_by_pdf_size {
            prefs = prefs.pick_tray_by_pdf_size(v);
        }
        prefs
    }
}

/// Set the document viewer preferences from a JSON payload. Enum fields are
/// integer discriminants (see `PdfPageLayout`/`PdfPageMode`/`PdfPrintScaling`/
/// `PdfDuplex` on the C# side).
///
/// # Safety
/// - `handle` must be a valid pointer from `oxidize_document_create`.
/// - `json` must be a NUL-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_set_viewer_preferences_json(
    handle: *mut DocumentHandle,
    json: *const c_char,
) -> c_int {
    clear_last_error();
    if handle.is_null() || json.is_null() {
        set_last_error("Null pointer to oxidize_document_set_viewer_preferences_json");
        return ErrorCode::NullPointer as c_int;
    }
    let s = match CStr::from_ptr(json).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in viewer preferences JSON");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    let payload: ViewerPrefsJson = match serde_json::from_str(s) {
        Ok(v) => v,
        Err(e) => {
            set_last_error(format!("Invalid viewer preferences JSON: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };
    (*handle).inner.set_viewer_preferences(payload.to_core());
    ErrorCode::Success as c_int
}

// ── DOC-017: Named destinations ──────────────────────────────────────────────

#[derive(Deserialize)]
struct NamedDestJson {
    name: String,
    destination: DestinationJson,
}

/// Register a named destination `{ "name": "...", "destination": { ... } }`.
/// Re-adding an existing name overwrites it (last write wins).
///
/// # Safety
/// - `handle` must be a valid pointer from `oxidize_document_create`.
/// - `json` must be a NUL-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_add_named_destination_json(
    handle: *mut DocumentHandle,
    json: *const c_char,
) -> c_int {
    clear_last_error();
    if handle.is_null() || json.is_null() {
        set_last_error("Null pointer to oxidize_document_add_named_destination_json");
        return ErrorCode::NullPointer as c_int;
    }
    let s = match CStr::from_ptr(json).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in named destination JSON");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    let payload: NamedDestJson = match serde_json::from_str(s) {
        Ok(v) => v,
        Err(e) => {
            set_last_error(format!("Invalid named destination JSON: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };
    if payload.name.is_empty() {
        set_last_error("Named destination name must not be empty");
        return ErrorCode::InvalidArgument as c_int;
    }

    let doc = &mut (*handle).inner;
    if doc.named_destinations().is_none() {
        doc.set_named_destinations(NamedDestinations::new());
    }
    let dests = doc
        .named_destinations_mut()
        .expect("named_destinations was just set");
    let array = payload.destination.to_core().to_array();
    dests.add_destination(payload.name, array);
    ErrorCode::Success as c_int
}

// ── DOC-018: Page labels ─────────────────────────────────────────────────────

#[derive(Deserialize)]
struct PageLabelRangeJson {
    start_page: u32,
    style: u8,
    prefix: Option<String>,
    start_at: Option<u32>,
}

#[derive(Deserialize)]
struct PageLabelsJson {
    ranges: Vec<PageLabelRangeJson>,
}

impl PageLabelRangeJson {
    fn to_core_label(&self) -> PageLabel {
        let mut label = match self.style {
            0 => PageLabel::decimal(),
            1 => PageLabel::roman_lowercase(),
            2 => PageLabel::roman_uppercase(),
            3 => PageLabel::letters_lowercase(),
            4 => PageLabel::letters_uppercase(),
            5 => PageLabel::new(PageLabelStyle::None),
            _ => PageLabel::decimal(),
        };
        if let Some(prefix) = &self.prefix {
            label = label.with_prefix(prefix.clone());
        }
        if let Some(start) = self.start_at {
            label = label.starting_at(start);
        }
        label
    }
}

/// Set the document page-label ranges (custom page numbering) from JSON
/// `{ "ranges": [ { start_page, style, prefix?, start_at? }, ... ] }`.
/// `style`: 0=decimal, 1=lowercase-roman, 2=uppercase-roman,
/// 3=lowercase-letters, 4=uppercase-letters, 5=none. At least one range is
/// required.
///
/// # Safety
/// - `handle` must be a valid pointer from `oxidize_document_create`.
/// - `json` must be a NUL-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_set_page_labels_json(
    handle: *mut DocumentHandle,
    json: *const c_char,
) -> c_int {
    clear_last_error();
    if handle.is_null() || json.is_null() {
        set_last_error("Null pointer to oxidize_document_set_page_labels_json");
        return ErrorCode::NullPointer as c_int;
    }
    let s = match CStr::from_ptr(json).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in page labels JSON");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    let payload: PageLabelsJson = match serde_json::from_str(s) {
        Ok(v) => v,
        Err(e) => {
            set_last_error(format!("Invalid page labels JSON: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };
    if payload.ranges.is_empty() {
        set_last_error("Page labels require at least one range");
        return ErrorCode::InvalidArgument as c_int;
    }

    let mut tree = PageLabelTree::new();
    for range in &payload.ranges {
        tree.add_range(range.start_page, range.to_core_label());
    }
    (*handle).inner.set_page_labels(tree);
    ErrorCode::Success as c_int
}

// ── DOC-020: Save with WriterConfig ──────────────────────────────────────────

/// Serialize the document to PDF bytes using a custom writer configuration
/// (PDF version, xref streams, object streams, stream compression).
///
/// The returned buffer is heap-allocated and ownership transfers to the caller;
/// free it with `oxidize_free_bytes`. `incremental_update` is always `false`
/// (full serialization).
///
/// # Safety
/// - `handle` must be a valid pointer from `oxidize_document_create`.
/// - `pdf_version` must be a NUL-terminated UTF-8 string.
/// - `out_ptr` and `out_len` must be writable pointers.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_save_to_bytes_with_config(
    handle: *mut DocumentHandle,
    use_xref_streams: c_int,
    use_object_streams: c_int,
    pdf_version: *const c_char,
    compress_streams: c_int,
    out_ptr: *mut *mut u8,
    out_len: *mut usize,
) -> c_int {
    clear_last_error();
    if handle.is_null() || pdf_version.is_null() || out_ptr.is_null() || out_len.is_null() {
        set_last_error("Null pointer to oxidize_document_save_to_bytes_with_config");
        return ErrorCode::NullPointer as c_int;
    }
    *out_ptr = ptr::null_mut();
    *out_len = 0;

    let version = match CStr::from_ptr(pdf_version).to_str() {
        Ok(v) => v.to_string(),
        Err(_) => {
            set_last_error("Invalid UTF-8 in pdf_version");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    let config = WriterConfig {
        use_xref_streams: use_xref_streams != 0,
        use_object_streams: use_object_streams != 0,
        pdf_version: version,
        compress_streams: compress_streams != 0,
        incremental_update: false,
    };

    let bytes = match (*handle).inner.to_bytes_with_config(config) {
        Ok(b) => b,
        Err(e) => {
            set_last_error(format!("Failed to serialize document: {e}"));
            return ErrorCode::IoError as c_int;
        }
    };

    let len = bytes.len();
    let mut boxed = bytes.into_boxed_slice();
    *out_ptr = boxed.as_mut_ptr();
    *out_len = len;
    std::mem::forget(boxed);

    ErrorCode::Success as c_int
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::ErrorCode;
    use oxidize_pdf::Page;
    use std::ffi::CString;
    use std::os::raw::{c_char, c_int};

    /// Build a one-page document handle for serialization tests.
    unsafe fn one_page_handle() -> *mut DocumentHandle {
        let handle = crate::document::oxidize_document_create();
        (*handle).inner.add_page(Page::a4());
        handle
    }

    /// Serialize via the save-with-config FFI and return the bytes (freeing the
    /// FFI buffer). Asserts the call succeeded.
    unsafe fn save_with_config(
        handle: *mut DocumentHandle,
        xref: c_int,
        objstm: c_int,
        version: &str,
        compress: c_int,
    ) -> Vec<u8> {
        let ver = CString::new(version).unwrap();
        let mut out_ptr: *mut u8 = std::ptr::null_mut();
        let mut out_len: usize = 0;
        let code = oxidize_document_save_to_bytes_with_config(
            handle,
            xref,
            objstm,
            ver.as_ptr(),
            compress,
            &mut out_ptr,
            &mut out_len,
        );
        assert_eq!(code, ErrorCode::Success as c_int);
        assert!(!out_ptr.is_null());
        let bytes = std::slice::from_raw_parts(out_ptr, out_len).to_vec();
        crate::oxidize_free_bytes(out_ptr, out_len);
        bytes
    }

    #[test]
    fn save_with_modern_config_produces_pdf15_with_xref_stream() {
        unsafe {
            let handle = one_page_handle();
            let bytes = save_with_config(handle, 1, 1, "1.5", 1);
            crate::document::oxidize_document_free(handle);
            assert!(
                bytes.starts_with(b"%PDF-1.5"),
                "expected %PDF-1.5 header, got: {:?}",
                String::from_utf8_lossy(&bytes[..bytes.len().min(12)])
            );
            assert!(
                bytes
                    .windows(10)
                    .any(|w| w == b"/Type /XRe" || w == b"Type /XRef"),
                "modern config must emit an XRef stream object"
            );
        }
    }

    #[test]
    fn save_with_legacy_config_produces_pdf14_without_xref_stream() {
        unsafe {
            let handle = one_page_handle();
            let bytes = save_with_config(handle, 0, 0, "1.4", 1);
            crate::document::oxidize_document_free(handle);
            assert!(bytes.starts_with(b"%PDF-1.4"), "expected %PDF-1.4 header");
            let s = String::from_utf8_lossy(&bytes);
            assert!(
                !s.contains("/Type /XRef"),
                "legacy config must use a classic xref table, not an xref stream"
            );
        }
    }

    /// Serialize a handle's document to bytes via the core writer (default config).
    unsafe fn to_bytes(handle: *mut DocumentHandle) -> Vec<u8> {
        (*handle).inner.to_bytes().unwrap()
    }

    unsafe fn call_open_action(handle: *mut DocumentHandle, json: &str) -> c_int {
        let c = CString::new(json).unwrap();
        oxidize_document_set_open_action_json(handle, c.as_ptr())
    }

    #[test]
    fn open_action_goto_fit_emits_goto_in_catalog() {
        unsafe {
            let handle = one_page_handle();
            let code = call_open_action(
                handle,
                r#"{"kind":"goto","destination":{"page":0,"fit":1}}"#,
            );
            assert_eq!(code, ErrorCode::Success as c_int);
            let s = String::from_utf8_lossy(&to_bytes(handle)).into_owned();
            crate::document::oxidize_document_free(handle);
            assert!(s.contains("/OpenAction"), "missing /OpenAction");
            assert!(s.contains("/S /GoTo"), "missing /S /GoTo");
            assert!(s.contains("/Fit"), "missing /Fit destination");
        }
    }

    #[test]
    fn open_action_uri_emits_uri_action() {
        unsafe {
            let handle = one_page_handle();
            let code = call_open_action(handle, r#"{"kind":"uri","uri":"https://example.com/"}"#);
            assert_eq!(code, ErrorCode::Success as c_int);
            let s = String::from_utf8_lossy(&to_bytes(handle)).into_owned();
            crate::document::oxidize_document_free(handle);
            assert!(s.contains("/S /URI"), "missing /S /URI");
            assert!(s.contains("https://example.com/"), "missing URI string");
        }
    }

    #[test]
    fn open_action_goto_without_destination_returns_invalid_argument() {
        unsafe {
            let handle = one_page_handle();
            let code = call_open_action(handle, r#"{"kind":"goto"}"#);
            crate::document::oxidize_document_free(handle);
            assert_eq!(code, ErrorCode::InvalidArgument as c_int);
        }
    }

    #[test]
    fn open_action_null_handle_returns_null_pointer() {
        unsafe {
            let c = CString::new("{}").unwrap();
            let code = oxidize_document_set_open_action_json(std::ptr::null_mut(), c.as_ptr());
            assert_eq!(code, ErrorCode::NullPointer as c_int);
        }
    }

    #[test]
    fn viewer_prefs_hide_toolbar_and_fit_window_emit_dict() {
        unsafe {
            let handle = one_page_handle();
            let c = CString::new(r#"{"hide_toolbar":true,"fit_window":true}"#).unwrap();
            let code = oxidize_document_set_viewer_preferences_json(handle, c.as_ptr());
            assert_eq!(code, ErrorCode::Success as c_int);
            let s = String::from_utf8_lossy(&to_bytes(handle)).into_owned();
            crate::document::oxidize_document_free(handle);
            assert!(
                s.contains("/ViewerPreferences"),
                "missing /ViewerPreferences"
            );
            assert!(s.contains("/HideToolbar true"), "missing /HideToolbar true");
            assert!(s.contains("/FitWindow true"), "missing /FitWindow true");
        }
    }

    #[test]
    fn viewer_prefs_page_layout_two_column_left() {
        unsafe {
            let handle = one_page_handle();
            let c = CString::new(r#"{"page_layout":2}"#).unwrap();
            let code = oxidize_document_set_viewer_preferences_json(handle, c.as_ptr());
            assert_eq!(code, ErrorCode::Success as c_int);
            let s = String::from_utf8_lossy(&to_bytes(handle)).into_owned();
            crate::document::oxidize_document_free(handle);
            assert!(
                s.contains("/PageLayout /TwoColumnLeft"),
                "missing TwoColumnLeft layout"
            );
        }
    }

    #[test]
    fn named_destination_single_emits_name_tree_entry() {
        unsafe {
            let handle = one_page_handle();
            let c =
                CString::new(r#"{"name":"chapter-1","destination":{"page":0,"fit":1}}"#).unwrap();
            let code = oxidize_document_add_named_destination_json(handle, c.as_ptr());
            assert_eq!(code, ErrorCode::Success as c_int);
            let s = String::from_utf8_lossy(&to_bytes(handle)).into_owned();
            crate::document::oxidize_document_free(handle);
            assert!(s.contains("/Names"), "missing /Names dictionary");
            assert!(s.contains("chapter-1"), "missing named destination key");
        }
    }

    #[test]
    fn named_destination_duplicate_name_last_write_wins() {
        unsafe {
            let handle = one_page_handle();
            let c1 = CString::new(r#"{"name":"target","destination":{"page":0,"fit":0,"left":111.0,"top":222.0,"zoom":3.5}}"#).unwrap();
            assert_eq!(
                oxidize_document_add_named_destination_json(handle, c1.as_ptr()),
                ErrorCode::Success as c_int
            );
            let c2 = CString::new(r#"{"name":"target","destination":{"page":0,"fit":0,"left":333.0,"top":444.0,"zoom":4.5}}"#).unwrap();
            assert_eq!(
                oxidize_document_add_named_destination_json(handle, c2.as_ptr()),
                ErrorCode::Success as c_int
            );
            let s = String::from_utf8_lossy(&to_bytes(handle)).into_owned();
            crate::document::oxidize_document_free(handle);
            // Anchor on the full destination array (`/XYZ l t z`) rather than bare
            // integers, which can collide with xref offsets / object IDs.
            assert!(
                !s.contains("/XYZ 111 222 3.5"),
                "first write's destination must be overwritten"
            );
            assert!(
                s.contains("/XYZ 333 444 4.5"),
                "last write's destination must be present"
            );
        }
    }

    #[test]
    fn named_destination_empty_name_returns_invalid_argument() {
        unsafe {
            let handle = one_page_handle();
            let c = CString::new(r#"{"name":"","destination":{"page":0,"fit":1}}"#).unwrap();
            let code = oxidize_document_add_named_destination_json(handle, c.as_ptr());
            crate::document::oxidize_document_free(handle);
            assert_eq!(code, ErrorCode::InvalidArgument as c_int);
        }
    }

    unsafe fn n_page_handle(n: usize) -> *mut DocumentHandle {
        let handle = crate::document::oxidize_document_create();
        for _ in 0..n {
            (*handle).inner.add_page(Page::a4());
        }
        handle
    }

    #[test]
    fn page_labels_roman_then_decimal_emits_page_labels_dict() {
        unsafe {
            let handle = n_page_handle(6);
            let json = r#"{"ranges":[{"start_page":0,"style":1,"prefix":null,"start_at":1},{"start_page":3,"style":0,"prefix":null,"start_at":1}]}"#;
            let c = CString::new(json).unwrap();
            let code = oxidize_document_set_page_labels_json(handle, c.as_ptr());
            assert_eq!(code, ErrorCode::Success as c_int);
            let s = String::from_utf8_lossy(&to_bytes(handle)).into_owned();
            crate::document::oxidize_document_free(handle);
            assert!(s.contains("/PageLabels"), "missing /PageLabels");
            assert!(s.contains("/Nums"), "missing /Nums number tree");
            assert!(s.contains("/S /r"), "missing lowercase-roman style");
            assert!(s.contains("/S /D"), "missing decimal style");
        }
    }

    #[test]
    fn page_labels_with_prefix_and_start_emits_prefix_and_st() {
        unsafe {
            let handle = n_page_handle(2);
            let json =
                r#"{"ranges":[{"start_page":0,"style":0,"prefix":"Chapter ","start_at":5}]}"#;
            let c = CString::new(json).unwrap();
            let code = oxidize_document_set_page_labels_json(handle, c.as_ptr());
            assert_eq!(code, ErrorCode::Success as c_int);
            let s = String::from_utf8_lossy(&to_bytes(handle)).into_owned();
            crate::document::oxidize_document_free(handle);
            assert!(s.contains("/P (Chapter "), "missing /P (Chapter ) prefix");
            assert!(s.contains("/St 5"), "missing /St 5 start value");
            assert!(s.contains("/S /D"), "missing decimal style");
        }
    }

    #[test]
    fn page_labels_empty_ranges_returns_invalid_argument() {
        unsafe {
            let handle = n_page_handle(1);
            let c = CString::new(r#"{"ranges":[]}"#).unwrap();
            let code = oxidize_document_set_page_labels_json(handle, c.as_ptr());
            crate::document::oxidize_document_free(handle);
            assert_eq!(code, ErrorCode::InvalidArgument as c_int);
        }
    }

    #[test]
    fn page_labels_null_handle_returns_null_pointer() {
        unsafe {
            let c = CString::new(r#"{"ranges":[]}"#).unwrap();
            let code = oxidize_document_set_page_labels_json(std::ptr::null_mut(), c.as_ptr());
            assert_eq!(code, ErrorCode::NullPointer as c_int);
        }
    }

    #[test]
    fn save_with_config_null_handle_returns_null_pointer() {
        unsafe {
            let ver = CString::new("1.7").unwrap();
            let mut out_ptr: *mut u8 = std::ptr::null_mut();
            let mut out_len: usize = 0;
            let code = oxidize_document_save_to_bytes_with_config(
                std::ptr::null_mut(),
                1,
                1,
                ver.as_ptr(),
                1,
                &mut out_ptr,
                &mut out_len,
            );
            assert_eq!(code, ErrorCode::NullPointer as c_int);
            assert!(out_ptr.is_null());
            let _ = std::convert::identity::<*const c_char>(ver.as_ptr());
        }
    }
}
