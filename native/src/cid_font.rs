//! FFI for CID-keyed positioned glyph runs (upstream issue #358, added in 3.0.0).
//!
//! Two entry points mirror the upstream write path:
//! - [`oxidize_document_add_cid_keyed_font`] registers a TrueType font drawn by
//!   glyph id (CID = GID under `CIDToGIDMap = Identity`), with the CID→GID and
//!   CID→Unicode maps supplied as JSON.
//! - [`oxidize_page_show_cid_array`] selects that font on the page's graphics
//!   context and draws a positioned glyph run (a `TJ` array).
//!
//! The caller supplies an already-shaped run (e.g. from `rustybuzz`); the core
//! performs no shaping. Text stays extractable via the emitted `ToUnicode` CMap.

use std::collections::HashMap;
use std::ffi::CStr;
use std::os::raw::{c_char, c_int};

use crate::document::DocumentHandle;
use crate::page::PageHandle;
use crate::{clear_last_error, set_last_error, ErrorCode};

/// Register a CID-keyed (CID = GID) TrueType font on the document.
///
/// `mapping_json` schema (`cid_to_unicode` / `cid_to_unicode_str` optional):
/// ```json
/// { "cid_to_gid": {"1": 1, "2": 5},
///   "cid_to_unicode": {"1": 65},
///   "cid_to_unicode_str": {"3": "fi"} }
/// ```
/// Object keys are the decimal CID values. `max_cid` is derived from the keys.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `name` and `mapping_json` must be valid null-terminated UTF-8 strings.
/// - `font_bytes` must point to `font_len` readable bytes.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_add_cid_keyed_font(
    handle: *mut DocumentHandle,
    name: *const c_char,
    font_bytes: *const u8,
    font_len: usize,
    mapping_json: *const c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if handle.is_null() || name.is_null() || font_bytes.is_null() || mapping_json.is_null() {
            set_last_error("Null pointer provided to oxidize_document_add_cid_keyed_font");
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
        let json = match CStr::from_ptr(mapping_json).to_str() {
            Ok(v) => v,
            Err(_) => {
                set_last_error("Invalid UTF-8 in CID mapping JSON");
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };

        #[derive(serde::Deserialize)]
        struct MappingDto {
            cid_to_gid: HashMap<u16, u16>,
            #[serde(default)]
            cid_to_unicode: HashMap<u16, u32>,
            #[serde(default)]
            cid_to_unicode_str: HashMap<u16, String>,
        }

        let dto: MappingDto = match serde_json::from_str(json) {
            Ok(v) => v,
            Err(e) => {
                set_last_error(format!("Invalid CID mapping JSON: {e}"));
                return ErrorCode::PdfParseError as c_int;
            }
        };

        // Derive max_cid from the supplied CIDs so callers need not track it.
        let max_cid = dto
            .cid_to_gid
            .keys()
            .chain(dto.cid_to_unicode.keys())
            .chain(dto.cid_to_unicode_str.keys())
            .copied()
            .max()
            .unwrap_or(0);

        let mut mapping = oxidize_pdf::fonts::CidMapping::new();
        mapping.cid_to_gid = dto.cid_to_gid;
        mapping.cid_to_unicode = dto.cid_to_unicode;
        mapping.cid_to_unicode_str = dto.cid_to_unicode_str;
        mapping.max_cid = max_cid;

        let data = std::slice::from_raw_parts(font_bytes, font_len).to_vec();
        if let Err(e) = (*handle).inner.add_cid_keyed_font(font_name, data, mapping) {
            set_last_error(format!("Failed to add CID-keyed font: {e}"));
            return ErrorCode::InvalidArgument as c_int;
        }
        ErrorCode::Success as c_int
    })
}

/// Draw a positioned glyph run over a CID-keyed font on the page.
///
/// Selects `font_name` (registered via [`oxidize_document_add_cid_keyed_font`])
/// at `size` on the page's graphics context, then emits a `TJ` array at
/// `(x, y)`. `elements_json` schema (`adjust` / `x_offset` default `0.0`):
/// ```json
/// [ {"cid": 1, "adjust": 0.0, "x_offset": 0.0}, {"cid": 2, "adjust": -15.0} ]
/// ```
///
/// # Safety
/// - `page` must be a valid pointer returned by a page factory.
/// - `font_name` and `elements_json` must be valid null-terminated UTF-8 strings.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_show_cid_array(
    page: *mut PageHandle,
    font_name: *const c_char,
    size: f64,
    elements_json: *const c_char,
    x: f64,
    y: f64,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if page.is_null() || font_name.is_null() || elements_json.is_null() {
            set_last_error("Null pointer provided to oxidize_page_show_cid_array");
            return ErrorCode::NullPointer as c_int;
        }
        let name = match CStr::from_ptr(font_name).to_str() {
            Ok(v) => v,
            Err(_) => {
                set_last_error("Invalid UTF-8 in font name");
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };
        let json = match CStr::from_ptr(elements_json).to_str() {
            Ok(v) => v,
            Err(_) => {
                set_last_error("Invalid UTF-8 in CID elements JSON");
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };

        #[derive(serde::Deserialize)]
        struct ElementDto {
            cid: u16,
            #[serde(default)]
            adjust: f32,
            #[serde(default)]
            x_offset: f32,
        }

        let dtos: Vec<ElementDto> = match serde_json::from_str(json) {
            Ok(v) => v,
            Err(e) => {
                set_last_error(format!("Invalid CID elements JSON: {e}"));
                return ErrorCode::PdfParseError as c_int;
            }
        };

        let elements: Vec<oxidize_pdf::graphics::CidShowElement> = dtos
            .iter()
            .map(|d| {
                oxidize_pdf::graphics::CidShowElement::new(d.cid, d.adjust)
                    .with_x_offset(d.x_offset)
            })
            .collect();

        let g = (*page).inner.graphics();
        g.set_custom_font(name, size);
        g.show_cid_array(&elements, x, y);
        ErrorCode::Success as c_int
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::document::{
        oxidize_document_add_page, oxidize_document_create, oxidize_document_free,
        oxidize_document_new_page_a4, oxidize_document_save_to_bytes,
    };
    use crate::oxidize_free_bytes;
    use oxidize_pdf::text::fonts::truetype::{CmapSubtable, TrueTypeFont};
    use std::ffi::CString;
    use std::path::PathBuf;
    use std::ptr;

    fn sample_ttf_bytes() -> Vec<u8> {
        let p = PathBuf::from(env!("CARGO_MANIFEST_DIR"))
            .join("../dotnet/OxidizePdf.NET.Tests/fixtures/fonts/sample.ttf");
        std::fs::read(&p).expect("fixture sample.ttf must be readable")
    }

    fn gid_for(ttf: &TrueTypeFont, ch: char) -> u16 {
        let tables = ttf.parse_cmap().expect("cmap must parse");
        let cmap = CmapSubtable::select_best_or_first(&tables).expect("a usable cmap subtable");
        *cmap
            .mappings
            .get(&(ch as u32))
            .unwrap_or_else(|| panic!("sample.ttf must have a glyph for {ch:?}"))
    }

    fn contains(haystack: &[u8], needle: &str) -> bool {
        let n = needle.as_bytes();
        haystack.windows(n.len()).any(|w| w == n)
    }

    /// End-to-end: register a CID-keyed font and draw a positioned glyph run
    /// through the FFI, then re-inspect the generated PDF bytes. The writer must
    /// emit a Type0 / CIDFontType2 font and a `ToUnicode` CMap mapping each CID
    /// back to its Unicode code point, and the content must carry the glyph CIDs.
    #[test]
    fn cid_keyed_font_roundtrip_emits_type0_cidfonttype2_and_tounicode() {
        let data = sample_ttf_bytes();
        let ttf = TrueTypeFont::parse(data.clone()).expect("sample.ttf must parse");
        let gid_a = gid_for(&ttf, 'A');
        let gid_b = gid_for(&ttf, 'B');
        assert_ne!(gid_a, gid_b, "distinct glyphs expected");

        let mapping_json = format!(
            r#"{{"cid_to_gid":{{"{ga}":{ga},"{gb}":{gb}}},"cid_to_unicode":{{"{ga}":65,"{gb}":66}}}}"#,
            ga = gid_a,
            gb = gid_b
        );
        let elements_json = format!(
            r#"[{{"cid":{ga}}},{{"cid":{gb},"adjust":-15.0}}]"#,
            ga = gid_a,
            gb = gid_b
        );

        unsafe {
            let handle = oxidize_document_create();
            assert!(!handle.is_null());
            // Deterministic, inspectable bytes (same-crate access; not under test).
            (*handle).inner.set_compress(false);

            let name = CString::new("ShapedSample").unwrap();
            let mj = CString::new(mapping_json).unwrap();
            let rc = oxidize_document_add_cid_keyed_font(
                handle,
                name.as_ptr(),
                data.as_ptr(),
                data.len(),
                mj.as_ptr(),
            );
            assert_eq!(rc, ErrorCode::Success as c_int, "registration must succeed");

            let page = oxidize_document_new_page_a4(handle);
            assert!(!page.is_null());
            let ej = CString::new(elements_json).unwrap();
            let rc2 =
                oxidize_page_show_cid_array(page, name.as_ptr(), 24.0, ej.as_ptr(), 100.0, 700.0);
            assert_eq!(rc2, ErrorCode::Success as c_int, "draw must succeed");

            assert_eq!(
                oxidize_document_add_page(handle, page),
                ErrorCode::Success as c_int
            );

            let mut out_bytes: *mut u8 = ptr::null_mut();
            let mut out_len: usize = 0;
            let rc3 = oxidize_document_save_to_bytes(handle, &mut out_bytes, &mut out_len);
            assert_eq!(rc3, ErrorCode::Success as c_int, "save must succeed");
            let pdf = std::slice::from_raw_parts(out_bytes, out_len).to_vec();
            oxidize_free_bytes(out_bytes, out_len);
            crate::page::oxidize_page_free(page);
            oxidize_document_free(handle);

            assert!(
                contains(&pdf, "/Subtype /Type0"),
                "Type0 font wrapper expected"
            );
            assert!(
                contains(&pdf, "/Subtype /CIDFontType2"),
                "CIDFontType2 descendant expected"
            );
            let bf_a = format!("<{:04X}> <{:04X}>", gid_a, 'A' as u32);
            let bf_b = format!("<{:04X}> <{:04X}>", gid_b, 'B' as u32);
            assert!(
                contains(&pdf, &bf_a),
                "ToUnicode must map CID {gid_a} to U+0041 ('{bf_a}')"
            );
            assert!(
                contains(&pdf, &bf_b),
                "ToUnicode must map CID {gid_b} to U+0042 ('{bf_b}')"
            );
        }
    }

    /// `oxidize_page_show_cid_array` must emit a `TJ` show-text-array carrying the
    /// glyph CIDs as a coalesced hex run — distinct from the `ToUnicode` bfchar
    /// entries (which list each CID separately). Registration alone never emits a
    /// `TJ`, so this specifically proves the draw happened.
    #[test]
    fn show_cid_array_emits_tj_with_coalesced_glyph_cids() {
        let data = sample_ttf_bytes();
        let ttf = TrueTypeFont::parse(data.clone()).expect("sample.ttf must parse");
        let gid_a = gid_for(&ttf, 'A');
        let gid_b = gid_for(&ttf, 'B');

        let mapping_json = format!(
            r#"{{"cid_to_gid":{{"{ga}":{ga},"{gb}":{gb}}},"cid_to_unicode":{{"{ga}":65,"{gb}":66}}}}"#,
            ga = gid_a,
            gb = gid_b
        );
        // First glyph has no adjustment, so it coalesces with the second into one
        // hex run before the trailing `adjust`.
        let elements_json = format!(
            r#"[{{"cid":{ga}}},{{"cid":{gb},"adjust":-15.0}}]"#,
            ga = gid_a,
            gb = gid_b
        );

        unsafe {
            let handle = oxidize_document_create();
            (*handle).inner.set_compress(false);
            let name = CString::new("ShapedSample").unwrap();
            let mj = CString::new(mapping_json).unwrap();
            assert_eq!(
                oxidize_document_add_cid_keyed_font(
                    handle,
                    name.as_ptr(),
                    data.as_ptr(),
                    data.len(),
                    mj.as_ptr()
                ),
                ErrorCode::Success as c_int
            );
            let page = oxidize_document_new_page_a4(handle);
            let ej = CString::new(elements_json).unwrap();
            assert_eq!(
                oxidize_page_show_cid_array(page, name.as_ptr(), 24.0, ej.as_ptr(), 100.0, 700.0),
                ErrorCode::Success as c_int
            );
            oxidize_document_add_page(handle, page);

            let mut out_bytes: *mut u8 = ptr::null_mut();
            let mut out_len: usize = 0;
            // Must check the result before `from_raw_parts`: on failure `out_bytes`
            // stays null, and `from_raw_parts` over a null pointer is UB.
            assert_eq!(
                oxidize_document_save_to_bytes(handle, &mut out_bytes, &mut out_len),
                ErrorCode::Success as c_int,
                "save must succeed"
            );
            let pdf = std::slice::from_raw_parts(out_bytes, out_len).to_vec();
            oxidize_free_bytes(out_bytes, out_len);
            crate::page::oxidize_page_free(page);
            oxidize_document_free(handle);

            assert!(
                contains(&pdf, "TJ"),
                "show_cid_array must emit a TJ show-text-array operator"
            );
            let coalesced = format!("{:04X}{:04X}", gid_a, gid_b);
            assert!(
                contains(&pdf, &coalesced),
                "the TJ run must coalesce both glyph CIDs into one hex string ('{coalesced}')"
            );
        }
    }

    /// Malformed mapping JSON is reported as a parse error, not a crash.
    #[test]
    fn add_cid_keyed_font_rejects_malformed_mapping_json() {
        let data = sample_ttf_bytes();
        unsafe {
            let handle = oxidize_document_create();
            let name = CString::new("Bad").unwrap();
            let mj = CString::new(r#"{"cid_to_gid": "not an object"}"#).unwrap();
            let rc = oxidize_document_add_cid_keyed_font(
                handle,
                name.as_ptr(),
                data.as_ptr(),
                data.len(),
                mj.as_ptr(),
            );
            oxidize_document_free(handle);
            assert_eq!(
                rc,
                ErrorCode::PdfParseError as c_int,
                "malformed mapping JSON must return a parse error"
            );
        }
    }

    /// Null arguments are rejected with the null-pointer code, never a deref.
    #[test]
    fn add_cid_keyed_font_rejects_null_mapping_json() {
        let data = sample_ttf_bytes();
        unsafe {
            let handle = oxidize_document_create();
            let name = CString::new("X").unwrap();
            let rc = oxidize_document_add_cid_keyed_font(
                handle,
                name.as_ptr(),
                data.as_ptr(),
                data.len(),
                ptr::null(),
            );
            oxidize_document_free(handle);
            assert_eq!(rc, ErrorCode::NullPointer as c_int);
        }
    }
}
