use std::ffi::CStr;
use std::os::raw::{c_char, c_int};
use std::ptr;

use crate::{clear_last_error, set_last_error, ErrorCode};

/// Opaque handle wrapping an `oxidize_pdf::Document`.
pub struct DocumentHandle {
    pub(crate) inner: oxidize_pdf::Document,
}

/// Create a new empty document.
///
/// # Safety
/// - Returns a heap-allocated `DocumentHandle` pointer that must be freed with
///   `oxidize_document_free`.
/// - Returns null on allocation failure (sets last error).
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_create() -> *mut DocumentHandle {
    clear_last_error();
    let handle = Box::new(DocumentHandle {
        inner: oxidize_pdf::Document::new(),
    });
    Box::into_raw(handle)
}

/// Free a document handle previously created by `oxidize_document_create`.
///
/// # Safety
/// - `handle` must have been returned by `oxidize_document_create`.
/// - `handle` must not have been freed previously.
/// - After calling this function, `handle` must not be used again.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_free(handle: *mut DocumentHandle) {
    if handle.is_null() {
        return;
    }
    drop(Box::from_raw(handle));
}

/// Set the document title metadata.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `text` must be a valid null-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_set_title(
    handle: *mut DocumentHandle,
    text: *const c_char,
) -> c_int {
    clear_last_error();
    if handle.is_null() || text.is_null() {
        set_last_error("Null pointer provided to oxidize_document_set_title");
        return ErrorCode::NullPointer as c_int;
    }
    let s = match CStr::from_ptr(text).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in title");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    (*handle).inner.set_title(s);
    ErrorCode::Success as c_int
}

/// Set the document author metadata.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `text` must be a valid null-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_set_author(
    handle: *mut DocumentHandle,
    text: *const c_char,
) -> c_int {
    clear_last_error();
    if handle.is_null() || text.is_null() {
        set_last_error("Null pointer provided to oxidize_document_set_author");
        return ErrorCode::NullPointer as c_int;
    }
    let s = match CStr::from_ptr(text).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in author");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    (*handle).inner.set_author(s);
    ErrorCode::Success as c_int
}

/// Set the document subject metadata.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `text` must be a valid null-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_set_subject(
    handle: *mut DocumentHandle,
    text: *const c_char,
) -> c_int {
    clear_last_error();
    if handle.is_null() || text.is_null() {
        set_last_error("Null pointer provided to oxidize_document_set_subject");
        return ErrorCode::NullPointer as c_int;
    }
    let s = match CStr::from_ptr(text).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in subject");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    (*handle).inner.set_subject(s);
    ErrorCode::Success as c_int
}

/// Set the document keywords metadata.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `text` must be a valid null-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_set_keywords(
    handle: *mut DocumentHandle,
    text: *const c_char,
) -> c_int {
    clear_last_error();
    if handle.is_null() || text.is_null() {
        set_last_error("Null pointer provided to oxidize_document_set_keywords");
        return ErrorCode::NullPointer as c_int;
    }
    let s = match CStr::from_ptr(text).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in keywords");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    (*handle).inner.set_keywords(s);
    ErrorCode::Success as c_int
}

/// Set the document creator metadata.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `text` must be a valid null-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_set_creator(
    handle: *mut DocumentHandle,
    text: *const c_char,
) -> c_int {
    clear_last_error();
    if handle.is_null() || text.is_null() {
        set_last_error("Null pointer provided to oxidize_document_set_creator");
        return ErrorCode::NullPointer as c_int;
    }
    let s = match CStr::from_ptr(text).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in creator");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    (*handle).inner.set_creator(s);
    ErrorCode::Success as c_int
}

/// Set the document producer metadata.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `text` must be a valid null-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_set_producer(
    handle: *mut DocumentHandle,
    text: *const c_char,
) -> c_int {
    clear_last_error();
    if handle.is_null() || text.is_null() {
        set_last_error("Null pointer provided to oxidize_document_set_producer");
        return ErrorCode::NullPointer as c_int;
    }
    let s = match CStr::from_ptr(text).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in producer");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    (*handle).inner.set_producer(s);
    ErrorCode::Success as c_int
}

/// Set the document creation date from a Unix timestamp (seconds since epoch).
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_set_creation_date(
    handle: *mut DocumentHandle,
    unix_timestamp_secs: i64,
) -> c_int {
    clear_last_error();
    if handle.is_null() {
        set_last_error("Null pointer provided to oxidize_document_set_creation_date");
        return ErrorCode::NullPointer as c_int;
    }
    let date = match chrono::DateTime::from_timestamp(unix_timestamp_secs, 0) {
        Some(d) => d,
        None => {
            set_last_error(format!("Invalid Unix timestamp: {unix_timestamp_secs}"));
            return ErrorCode::PdfParseError as c_int;
        }
    };
    (*handle).inner.set_creation_date(date);
    ErrorCode::Success as c_int
}

/// Set the document modification date from a Unix timestamp (seconds since epoch).
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_set_modification_date(
    handle: *mut DocumentHandle,
    unix_timestamp_secs: i64,
) -> c_int {
    clear_last_error();
    if handle.is_null() {
        set_last_error("Null pointer provided to oxidize_document_set_modification_date");
        return ErrorCode::NullPointer as c_int;
    }
    let date = match chrono::DateTime::from_timestamp(unix_timestamp_secs, 0) {
        Some(d) => d,
        None => {
            set_last_error(format!("Invalid Unix timestamp: {unix_timestamp_secs}"));
            return ErrorCode::PdfParseError as c_int;
        }
    };
    (*handle).inner.set_modification_date(date);
    ErrorCode::Success as c_int
}

/// Save the document to a file at the given path.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `path` must be a valid null-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_save_to_file(
    handle: *mut DocumentHandle,
    path: *const c_char,
) -> c_int {
    clear_last_error();
    if handle.is_null() || path.is_null() {
        set_last_error("Null pointer provided to oxidize_document_save_to_file");
        return ErrorCode::NullPointer as c_int;
    }
    let p = match CStr::from_ptr(path).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in file path");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    if let Err(e) = (*handle).inner.save(p) {
        set_last_error(format!("Failed to save document to file: {e}"));
        return ErrorCode::IoError as c_int;
    }
    ErrorCode::Success as c_int
}

/// Create a new A4 page bound to this document's `FontMetricsStore`.
///
/// **Use this instead of `oxidize_page_create_preset(A4)` when the page will
/// draw text in a `Font::Custom(_)` registered via
/// `oxidize_document_add_font_from_bytes`.** Pages produced by this factory
/// share the document's per-instance metrics store, so measurement helpers
/// (text wrapping in `TextFlowContext`, table layout, header / footer width)
/// resolve custom-font widths against the real font metrics rather than the
/// default 500/em fallback that pages constructed standalone receive in
/// oxidize-pdf 2.8.0+.
///
/// The returned handle must be freed with `oxidize_page_free`. Custom fonts
/// can be added to the document either before or after this call — the
/// store is `Arc`-shared, so subsequent registrations are visible to the
/// page automatically.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - The returned pointer must be freed with `oxidize_page_free`.
/// - Returns null and sets the last-error message on failure.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_new_page_a4(
    handle: *const DocumentHandle,
) -> *mut crate::page::PageHandle {
    clear_last_error();
    if handle.is_null() {
        set_last_error("Null pointer provided to oxidize_document_new_page_a4");
        return ptr::null_mut();
    }
    let page = (*handle).inner.new_page_a4();
    Box::into_raw(Box::new(crate::page::PageHandle { inner: page }))
}

/// Create a new US Letter page bound to this document's `FontMetricsStore`.
/// See [`oxidize_document_new_page_a4`] for the rationale.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - The returned pointer must be freed with `oxidize_page_free`.
/// - Returns null and sets the last-error message on failure.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_new_page_letter(
    handle: *const DocumentHandle,
) -> *mut crate::page::PageHandle {
    clear_last_error();
    if handle.is_null() {
        set_last_error("Null pointer provided to oxidize_document_new_page_letter");
        return ptr::null_mut();
    }
    let page = (*handle).inner.new_page_letter();
    Box::into_raw(Box::new(crate::page::PageHandle { inner: page }))
}

/// Create a new page with explicit dimensions bound to this document's
/// `FontMetricsStore`. See [`oxidize_document_new_page_a4`] for the
/// rationale.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - The returned pointer must be freed with `oxidize_page_free`.
/// - `width` and `height` are interpreted as PDF points and must be finite
///   and positive. Non-finite or non-positive values are rejected with a
///   null return and last-error message.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_new_page(
    handle: *const DocumentHandle,
    width: f64,
    height: f64,
) -> *mut crate::page::PageHandle {
    clear_last_error();
    if handle.is_null() {
        set_last_error("Null pointer provided to oxidize_document_new_page");
        return ptr::null_mut();
    }
    if !width.is_finite() || !height.is_finite() || width <= 0.0 || height <= 0.0 {
        set_last_error(format!(
            "Invalid page dimensions for oxidize_document_new_page: \
             width={width}, height={height} (must be finite and positive)"
        ));
        return ptr::null_mut();
    }
    let page = (*handle).inner.new_page(width, height);
    Box::into_raw(Box::new(crate::page::PageHandle { inner: page }))
}

/// Add a page to the document. The page is cloned internally; the caller retains ownership.
///
/// # FontMetricsStore binding (oxidize-pdf 2.8.0+)
///
/// Upstream `Document::add_page` injects the document's `FontMetricsStore`
/// into the cloned page if the page does not already carry one. The
/// document-side copy therefore measures `Font::Custom(_)` against the
/// per-Document store; the caller's `PageHandle` is unmodified and its
/// inner `Page` retains whatever store binding it had before this call.
///
/// **For pages that will draw `Font::Custom(_)` text via measurement-heavy
/// flows (`TextFlowContext`, table layout, header / footer width), construct
/// the page via [`oxidize_document_new_page_a4`] /
/// [`oxidize_document_new_page_letter`] / [`oxidize_document_new_page`]
/// instead of [`crate::page::oxidize_page_create_preset`] /
/// [`crate::page::oxidize_page_create`].** Pages from those factories carry
/// the document's metrics store from creation, so drawing operations that
/// run before this call already see the real font widths.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `page_handle` must be a valid pointer returned by `oxidize_page_create` or
///   `oxidize_page_create_preset`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_add_page(
    handle: *mut DocumentHandle,
    page_handle: *const crate::page::PageHandle,
) -> c_int {
    clear_last_error();
    if handle.is_null() || page_handle.is_null() {
        set_last_error("Null pointer provided to oxidize_document_add_page");
        return ErrorCode::NullPointer as c_int;
    }
    (*handle).inner.add_page((*page_handle).inner.clone());
    ErrorCode::Success as c_int
}

/// Serialize the document to PDF bytes.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `out_bytes` must be a valid pointer to a mutable pointer; on success it will point to a
///   heap-allocated byte array that must be freed with `oxidize_free_bytes`.
/// - `out_len` must be a valid pointer to a `usize` that will receive the byte count.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_save_to_bytes(
    handle: *mut DocumentHandle,
    out_bytes: *mut *mut u8,
    out_len: *mut usize,
) -> c_int {
    clear_last_error();
    if handle.is_null() || out_bytes.is_null() || out_len.is_null() {
        set_last_error("Null pointer provided to oxidize_document_save_to_bytes");
        return ErrorCode::NullPointer as c_int;
    }
    *out_bytes = ptr::null_mut();
    *out_len = 0;

    let bytes = match (*handle).inner.to_bytes() {
        Ok(b) => b,
        Err(e) => {
            set_last_error(format!("Failed to serialize document: {e}"));
            return ErrorCode::IoError as c_int;
        }
    };

    let len = bytes.len();
    let mut boxed = bytes.into_boxed_slice();
    *out_bytes = boxed.as_mut_ptr();
    *out_len = len;
    std::mem::forget(boxed);

    ErrorCode::Success as c_int
}

/// Get the number of pages in the document.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `out_count` must be a valid pointer to a `usize`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_page_count(
    handle: *const DocumentHandle,
    out_count: *mut usize,
) -> c_int {
    clear_last_error();
    if handle.is_null() || out_count.is_null() {
        set_last_error("Null pointer provided to oxidize_document_page_count");
        return ErrorCode::NullPointer as c_int;
    }
    *out_count = (*handle).inner.page_count();
    ErrorCode::Success as c_int
}

/// Register a custom font from byte data (e.g., TTF/OTF).
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `name` must be a valid null-terminated UTF-8 string.
/// - `font_bytes` must be a valid pointer to `font_len` bytes.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_add_font_from_bytes(
    handle: *mut DocumentHandle,
    name: *const c_char,
    font_bytes: *const u8,
    font_len: usize,
) -> c_int {
    clear_last_error();
    if handle.is_null() || name.is_null() || font_bytes.is_null() {
        set_last_error("Null pointer provided to oxidize_document_add_font_from_bytes");
        return ErrorCode::NullPointer as c_int;
    }
    if font_len == 0 {
        set_last_error("Font data is empty (0 bytes)");
        return ErrorCode::IoError as c_int;
    }
    let font_name = match CStr::from_ptr(name).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in font name");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    let data = std::slice::from_raw_parts(font_bytes, font_len).to_vec();
    if let Err(e) = (*handle).inner.add_font_from_bytes(font_name, data) {
        set_last_error(format!("Failed to add font: {e}"));
        return ErrorCode::IoError as c_int;
    }
    ErrorCode::Success as c_int
}

/// Set the document outline (bookmarks/table of contents) from a JSON tree.
///
/// The JSON must conform to:
/// `{ "items": [ { "title": string, "page": u32, "bold": bool, "italic": bool,
///                 "open": bool, "children": [...] } ] }`
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `outline_json` must be a valid null-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_set_outline(
    handle: *mut DocumentHandle,
    outline_json: *const c_char,
) -> c_int {
    clear_last_error();
    if handle.is_null() || outline_json.is_null() {
        set_last_error("Null pointer provided to oxidize_document_set_outline");
        return ErrorCode::NullPointer as c_int;
    }
    let json_str = match CStr::from_ptr(outline_json).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in outline JSON");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    #[derive(serde::Deserialize)]
    struct ItemJson {
        title: String,
        page: u32,
        #[serde(default)]
        bold: bool,
        #[serde(default)]
        italic: bool,
        #[serde(default = "default_true")]
        open: bool,
        #[serde(default)]
        children: Vec<ItemJson>,
    }

    fn default_true() -> bool {
        true
    }

    #[derive(serde::Deserialize)]
    struct OutlineJson {
        items: Vec<ItemJson>,
    }

    fn build_item(json: ItemJson) -> oxidize_pdf::OutlineItem {
        let dest =
            oxidize_pdf::Destination::fit(oxidize_pdf::PageDestination::PageNumber(json.page));
        let mut item = oxidize_pdf::OutlineItem::new(json.title).with_destination(dest);
        if json.bold {
            item = item.bold();
        }
        if json.italic {
            item = item.italic();
        }
        if !json.open {
            item = item.closed();
        }
        for child in json.children {
            item.add_child(build_item(child));
        }
        item
    }

    let parsed: OutlineJson = match serde_json::from_str(json_str) {
        Ok(v) => v,
        Err(e) => {
            set_last_error(format!("Failed to parse outline JSON: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };

    let mut tree = oxidize_pdf::OutlineTree::new();
    for item_json in parsed.items {
        tree.add_item(build_item(item_json));
    }

    (*handle).inner.set_outline(tree);
    ErrorCode::Success as c_int
}

/// Register a custom font from a file path (TTF/OTF).
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `name` and `path` must be valid null-terminated UTF-8 strings.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_add_font_from_file(
    handle: *mut DocumentHandle,
    name: *const c_char,
    path: *const c_char,
) -> c_int {
    clear_last_error();
    if handle.is_null() || name.is_null() || path.is_null() {
        set_last_error("Null pointer provided to oxidize_document_add_font_from_file");
        return ErrorCode::NullPointer as c_int;
    }
    let font_name = match CStr::from_ptr(name).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in font name");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    let font_path = match CStr::from_ptr(path).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in font path");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    if let Err(e) = (*handle).inner.add_font(font_name, font_path) {
        set_last_error(format!("Failed to add font from file: {e}"));
        return ErrorCode::IoError as c_int;
    }
    ErrorCode::Success as c_int
}

#[cfg(test)]
mod fontmetricsstore_binding_tests {
    //! Regression suite for the per-`Document` `FontMetricsStore` binding introduced upstream in oxidize-pdf 2.8.0 (issue #230).
    //!
    //! The legacy FFI flow (`oxidize_page_create_preset` / `oxidize_page_create` + `oxidize_page_set_custom_font` + draw + `oxidize_document_add_page`) exposes a behavioural gap: the page used for drawing is constructed standalone and never receives the document's `FontMetricsStore`. When `TextFlowContext::write_wrapped` (and the other measurement-driven emitters) run on that page they fall back to default widths from `text::metrics::create_default_custom_metrics` for any `Font::Custom(name)` not present in the legacy global registry — which 2.8.0 no longer writes to from `add_font_from_bytes`.
    //!
    //! The fix is to construct the drawing page via the new `oxidize_document_new_page_a4` / `_letter` / `_(w, h)` FFI factories (which call upstream `Document::new_page_*`). Those factories pre-bind the document's `FontMetricsStore` to the page; the binding is `Arc`-shared, so subsequent `add_font_from_bytes` registrations on the document are visible to the page automatically.
    //!
    //! These tests verify the full chain at the upstream Rust API level — the same surface the FFI thin-wraps. They are the rigorous regression detector for this fix; the .NET test layer cannot inspect glyph-level widths because the upstream Type0/CID font width array (`/W`) is not understood by `text::extraction::calculate_text_width`, which falls back to `text.len() * font_size * 0.5` regardless of whether the renderer used real or default metrics.

    use oxidize_pdf::text::metrics::measure_text_with;
    use oxidize_pdf::text::Font;
    use oxidize_pdf::Document;
    use std::path::PathBuf;

    fn fixtures_sample_ttf_bytes() -> Vec<u8> {
        let p = PathBuf::from(env!("CARGO_MANIFEST_DIR"))
            .join("../dotnet/OxidizePdf.NET.Tests/fixtures/fonts/sample.ttf");
        std::fs::read(&p).expect("fixture sample.ttf must be readable")
    }

    #[test]
    fn add_font_from_bytes_populates_document_font_metrics_store() {
        let bytes = fixtures_sample_ttf_bytes();
        let mut doc = Document::new();
        doc.add_font_from_bytes("narrow", bytes)
            .expect("add_font_from_bytes must succeed for sample.ttf");
        assert!(
            doc.font_metrics().get("narrow").is_some(),
            "Document::font_metrics() must contain 'narrow' after add_font_from_bytes"
        );
        let m = doc.font_metrics().get("narrow").unwrap();
        // 'i' should not be the default width — sample.ttf has glyph data for 'i'.
        let w = m.char_width('i');
        assert!(
            w > 0 && w < 500,
            "char_width('i') in sample.ttf metrics must be a real positive value below the \
             500/em default; got {w}"
        );
    }

    /// `Document::new_page(w, h)` (and the `_a4` / `_letter` variants) must
    /// return a page that carries the document's `FontMetricsStore`. The
    /// store is `Arc`-shared, so fonts registered on the document before or
    /// after the page is created are visible to the page automatically.
    #[test]
    fn document_new_page_propagates_metrics_store_to_page() {
        let mut doc = Document::new();
        doc.add_font_from_bytes("narrow", fixtures_sample_ttf_bytes())
            .unwrap();
        let page = doc.new_page(400.0, 600.0);
        let store = page.font_metrics_store().expect(
            "page.font_metrics_store() must be Some when constructed via Document::new_page",
        );
        assert!(
            store.get("narrow").is_some(),
            "page.font_metrics_store() must see fonts registered on the document"
        );
    }

    /// Substantive regression assertion. The diagnostic is:
    /// `measure_text_with(text, custom, size, Some(store))` must equal the
    /// oracle (`Font.measure_text` direct), and must differ from
    /// `measure_text_with(text, custom, size, None)` (fallback profile).
    /// A `text/flow.rs` measurement that returns the fallback value when the
    /// page is bound is what the architectural fix prevents.
    #[test]
    fn measure_text_with_real_metrics_differs_from_fallback() {
        let mut doc = Document::new();
        doc.add_font_from_bytes("narrow", fixtures_sample_ttf_bytes())
            .unwrap();
        let store = doc.font_metrics();
        let text = "iiiiiiiiiiiiiiiiiiii"; // 20 narrow chars
        let size = 24.0;
        let real = measure_text_with(text, &Font::Custom("narrow".into()), size, Some(store));
        let fallback = measure_text_with(text, &Font::Custom("narrow".into()), size, None);

        // Oracle: the same per-em widths reach a different code path
        // (`Font::from_bytes` -> `measure_text`) that bypasses the
        // `FontMetricsStore` entirely. They must agree.
        let oracle_font =
            oxidize_pdf::fonts::Font::from_bytes("oracle", fixtures_sample_ttf_bytes()).unwrap();
        let oracle = oracle_font.measure_text(text, size as f32).width as f64;
        assert!(
            (real - oracle).abs() < 0.5,
            "measure_text_with(store) ({real}) must match the oracle Font.measure_text \
             ({oracle}) — both consult the same per-Document widths."
        );

        // The fallback path differs measurably from the real path. If it
        // didn't, the test couldn't detect the regression.
        assert!(
            (real - fallback).abs() > 5.0,
            "real measurement ({real}) and fallback ({fallback}) must differ by more \
             than 5 points; pick a longer text or a different font fixture if they're \
             too close to discriminate."
        );

        // The exact upstream fallback for "i" is 222/em (Helvetica profile in
        // `create_default_custom_metrics`). 20 * 222/1000 * 24 = 106.56. Lock
        // it so a future upstream change to the default profile fails this
        // test and forces a deliberate review.
        let expected_fallback = 20.0 * 222.0 / 1000.0 * 24.0;
        assert!(
            (fallback - expected_fallback).abs() < 0.5,
            "upstream fallback for 'i' is expected to be {expected_fallback} \
             (Helvetica-i in `create_default_custom_metrics`); got {fallback}. \
             If this fails, the upstream default profile changed."
        );
    }
}
