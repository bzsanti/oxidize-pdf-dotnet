//! FFI for document-level metadata: open actions, viewer preferences,
//! named destinations, page labels, save-with-WriterConfig.
//!
//! Module scaffold for Milestone M1 — individual feature functions will be
//! added per-task following the plan at
//! `docs/superpowers/plans/2026-04-21-m1-document-metadata.md`.

use std::ffi::CStr;
use std::os::raw::{c_char, c_int};

use oxidize_pdf::structure::{Destination, PageDestination};
use serde::Deserialize;

use crate::document::DocumentHandle;
use crate::{clear_last_error, set_last_error, ErrorCode};

// ── JSON payload types ────────────────────────────────────────────────────────

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

    /// Render as a PDF destination array string, e.g. `[ 0 0 R /Fit ]`.
    fn to_pdf_array(&self) -> String {
        let page_ref = format!("{} 0 R", self.page);
        match self.fit {
            0 => {
                // XYZ: [ page /XYZ left top zoom ]
                let l = self
                    .left
                    .map_or_else(|| "null".to_string(), |v| format!("{v}"));
                let t = self
                    .top
                    .map_or_else(|| "null".to_string(), |v| format!("{v}"));
                let z = self
                    .zoom
                    .map_or_else(|| "null".to_string(), |v| format!("{v}"));
                format!("[ {page_ref} /XYZ {l} {t} {z} ]")
            }
            1 => format!("[ {page_ref} /Fit ]"),
            2 => {
                let t = self
                    .top
                    .map_or_else(|| "null".to_string(), |v| format!("{v}"));
                format!("[ {page_ref} /FitH {t} ]")
            }
            3 => {
                let l = self
                    .left
                    .map_or_else(|| "null".to_string(), |v| format!("{v}"));
                format!("[ {page_ref} /FitV {l} ]")
            }
            5 => format!("[ {page_ref} /FitB ]"),
            _ => format!("[ {page_ref} /Fit ]"),
        }
    }
}

#[derive(Deserialize)]
struct OpenActionJson {
    kind: String,
    destination: Option<DestinationJson>,
    uri: Option<String>,
}

impl OpenActionJson {
    /// Render as an inline PDF action dictionary string.
    fn to_pdf_dict(&self) -> Option<String> {
        match self.kind.as_str() {
            "goto" => {
                let dest = self.destination.as_ref()?;
                let arr = dest.to_pdf_array();
                Some(format!("<< /Type /Action /S /GoTo /D {arr} >>"))
            }
            "uri" => {
                let uri = self.uri.as_ref()?;
                // Escape parentheses in URI for PDF string literal
                let escaped = uri.replace('(', "\\(").replace(')', "\\)");
                Some(format!("<< /Type /Action /S /URI /URI ({escaped}) >>"))
            }
            _ => None,
        }
    }
}

// ── PDF incremental-update injection ─────────────────────────────────────────

/// Injects an `/OpenAction` into an already-serialized PDF by appending an
/// incremental update section.
///
/// `action_pdf` is the pre-formatted inline PDF action dictionary string,
/// e.g. `<< /Type /Action /S /GoTo /D [ 1 0 R /Fit ] >>`.
///
/// Returns the new PDF bytes with the incremental update appended.
pub(crate) fn inject_open_action_incremental(
    mut pdf: Vec<u8>,
    action_pdf: &str,
) -> Result<Vec<u8>, String> {
    // ── 1. Find startxref offset ──────────────────────────────────────────
    let prev_startxref = find_startxref_offset(&pdf)?;

    // ── 2. Find the /Root object number and generation from the trailer ───
    let (root_num, root_gen) = find_root_ref(&pdf)?;

    // ── 3. Find the catalog object bytes in the PDF ───────────────────────
    let catalog_body = find_object_body(&pdf, root_num, root_gen)?;

    // ── 4. Strip any existing /OpenAction from the catalog body ──────────
    let catalog_body = strip_key_from_dict(&catalog_body, "/OpenAction");

    // ── 5. Determine the new object number for the action ─────────────────
    let xref_size = find_xref_size(&pdf)?;
    let action_num = xref_size;

    // ── 6. Emit incremental update bytes ─────────────────────────────────
    let orig_len = pdf.len();

    // Action object
    let action_obj_offset = orig_len + pdf.len() - orig_len; // == orig_len
    let action_obj = format!(
        "{action_num} 0 obj\n{action_pdf}\nendobj\n",
        action_num = action_num,
        action_pdf = action_pdf,
    );

    // Modified catalog — inject /OpenAction before the closing >>
    let new_catalog_dict =
        inject_key_into_dict(&catalog_body, &format!("/OpenAction {action_num} 0 R"))?;
    let catalog_obj_bytes = format!(
        "{root_num} {root_gen} obj\n{new_catalog_dict}\nendobj\n",
        root_num = root_num,
        root_gen = root_gen,
        new_catalog_dict = new_catalog_dict,
    );

    let mut update = Vec::new();
    update.extend_from_slice(action_obj.as_bytes());
    let catalog_obj_offset = orig_len + update.len();
    update.extend_from_slice(catalog_obj_bytes.as_bytes());

    let action_obj_offset_abs = orig_len;

    // xref section
    let new_size = action_num + 1;
    let xref = format!(
        "xref\n\
         0 1\n\
         0000000000 65535 f \n\
         {action_num} 1\n\
         {a_off:010} 00000 n \n\
         {root_num} 1\n\
         {c_off:010} 00000 n \n",
        action_num = action_num,
        a_off = action_obj_offset_abs,
        root_num = root_num,
        c_off = catalog_obj_offset,
    );
    update.extend_from_slice(xref.as_bytes());

    // trailer
    let new_startxref = orig_len + update.len();
    let trailer = format!(
        "trailer\n<< /Size {new_size} /Root {root_num} {root_gen} R /Prev {prev_startxref} >>\nstartxref\n{new_startxref}\n%%EOF\n",
        new_size = new_size,
        root_num = root_num,
        root_gen = root_gen,
        prev_startxref = prev_startxref,
        new_startxref = new_startxref,
    );
    update.extend_from_slice(trailer.as_bytes());

    pdf.extend_from_slice(&update);
    Ok(pdf)
}

/// Parse `startxref` value from PDF bytes (searches from end).
fn find_startxref_offset(pdf: &[u8]) -> Result<u64, String> {
    // Search for "startxref" near end of file
    let search_window = &pdf[pdf.len().saturating_sub(1024)..];
    let pat = b"startxref";
    let pos = search_window
        .windows(pat.len())
        .rposition(|w| w == pat)
        .ok_or("startxref not found in PDF")?;
    let after = &search_window[pos + pat.len()..];
    // Use lossy conversion — we only care about ASCII digits
    let s = String::from_utf8_lossy(after);
    let s = s.trim_start_matches(|c: char| c == '\n' || c == '\r' || c == ' ');
    let num_end = s
        .find(|c: char| c == '\n' || c == '\r' || c == ' ' || c == '%')
        .unwrap_or(s.len());
    s[..num_end]
        .trim()
        .parse::<u64>()
        .map_err(|e| format!("Invalid startxref value: {e}"))
}

/// Extract `/Root <N> <G> R` from the PDF trailer area.
fn find_root_ref(pdf: &[u8]) -> Result<(u32, u16), String> {
    // Search in the last 4 KB which contains the trailer.
    // Use lossy conversion — the trailer is ASCII; binary content earlier is replaced with U+FFFD.
    let search = &pdf[pdf.len().saturating_sub(4096)..];
    let text = String::from_utf8_lossy(search);

    // Find /Root entry — use rfind so we get the last occurrence (handles incremental updates)
    let root_pos = text.rfind("/Root").ok_or("/Root not found in trailer")?;
    let after = text[root_pos + 5..].trim_start();
    // Parse "<N> <G> R"
    let mut parts = after.split_whitespace();
    let num = parts
        .next()
        .ok_or("Missing Root obj num")?
        .parse::<u32>()
        .map_err(|e| format!("Invalid Root obj num: {e}"))?;
    let gen = parts
        .next()
        .ok_or("Missing Root gen num")?
        .parse::<u16>()
        .map_err(|e| format!("Invalid Root gen num: {e}"))?;
    let r = parts.next().ok_or("Missing 'R' after Root ref")?;
    if r != "R" {
        return Err(format!("Expected 'R' after Root ref, got '{r}'"));
    }
    Ok((num, gen))
}

/// Find the body (the dictionary content between `obj` and `endobj`) of object `<num> <gen>`.
///
/// Works at the byte level to avoid UTF-8 issues with binary PDF content.
/// The catalog dictionary body is always pure ASCII.
fn find_object_body(pdf: &[u8], num: u32, gen: u16) -> Result<String, String> {
    let marker = format!("{num} {gen} obj");
    let marker_bytes = marker.as_bytes();

    // Find `<num> <gen> obj` by scanning for the byte pattern
    let obj_pos = pdf
        .windows(marker_bytes.len())
        .position(|w| w == marker_bytes)
        .ok_or_else(|| format!("Object {num} {gen} obj not found"))?;

    let after_obj = &pdf[obj_pos + marker_bytes.len()..];

    // Find `endobj` after the obj marker
    let endobj_pat = b"endobj";
    let endobj_pos = after_obj
        .windows(endobj_pat.len())
        .position(|w| w == endobj_pat)
        .ok_or_else(|| format!("endobj not found after object {num} {gen}"))?;

    let body_bytes = &after_obj[..endobj_pos];

    // The catalog body is pure ASCII — convert safely
    std::str::from_utf8(body_bytes)
        .map(|s| s.trim().to_string())
        .map_err(|_| format!("Object {num} {gen} body is not valid UTF-8/ASCII"))
}

/// Extract the `/Size` value from the xref trailer to determine the next object number.
fn find_xref_size(pdf: &[u8]) -> Result<u32, String> {
    let search = &pdf[pdf.len().saturating_sub(4096)..];
    let text = String::from_utf8_lossy(search);
    let size_pos = text.rfind("/Size").ok_or("/Size not found in trailer")?;
    let after = text[size_pos + 5..].trim_start();
    let num_end = after
        .find(|c: char| c.is_whitespace() || c == '/')
        .unwrap_or(after.len());
    after[..num_end]
        .trim()
        .parse::<u32>()
        .map_err(|e| format!("Invalid /Size value: {e}"))
}

/// Remove a named key (and its value) from a PDF dictionary string.
fn strip_key_from_dict(dict: &str, key: &str) -> String {
    // Naive: find the key, remove from key start to the next / or >>
    if let Some(pos) = dict.find(key) {
        let after = &dict[pos + key.len()..];
        // Skip whitespace and the value (up to next / or >>)
        let val_end = find_dict_value_end(after);
        let before = dict[..pos].trim_end().to_string();
        let rest = after[val_end..].to_string();
        format!("{before}\n{rest}")
    } else {
        dict.to_string()
    }
}

/// Find where a PDF dictionary value ends (handles nested dicts, arrays, and refs).
fn find_dict_value_end(s: &str) -> usize {
    let s = s.trim_start();
    let mut depth = 0i32;
    let mut i = 0;
    let bytes = s.as_bytes();
    while i < bytes.len() {
        match bytes[i] {
            b'<' if i + 1 < bytes.len() && bytes[i + 1] == b'<' => {
                depth += 1;
                i += 2;
            }
            b'>' if i + 1 < bytes.len() && bytes[i + 1] == b'>' => {
                if depth == 0 {
                    break;
                }
                depth -= 1;
                i += 2;
            }
            b'[' => {
                depth += 1;
                i += 1;
            }
            b']' => {
                depth -= 1;
                i += 1;
            }
            b'/' if depth == 0 && i > 0 => {
                // Next key starts here
                break;
            }
            _ => i += 1,
        }
    }
    // Account for trimmed leading whitespace
    let trimmed_len = s.len() - s.trim_start().len();
    trimmed_len + i
}

/// Inject a key-value string before the closing `>>` of a PDF dictionary.
fn inject_key_into_dict(dict: &str, kv: &str) -> Result<String, String> {
    // Find the last `>>`
    let close_pos = dict
        .rfind(">>")
        .ok_or("Closing >> not found in catalog dictionary")?;
    let before = &dict[..close_pos];
    let after = &dict[close_pos + 2..];
    Ok(format!("{before}\n/{kv}\n>>{after}"))
}

// ── FFI function ──────────────────────────────────────────────────────────────

/// Set the document open action from a JSON payload.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `json` must be a valid null-terminated UTF-8 string.
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
            set_last_error(&format!("Invalid open action JSON: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };

    // Validate the payload and format the action as a PDF dictionary string.
    let action_pdf = match payload.to_pdf_dict() {
        Some(s) => s,
        None => {
            set_last_error(&format!(
                "Invalid open action: kind='{}' missing required fields",
                payload.kind
            ));
            return ErrorCode::InvalidArgument as c_int;
        }
    };

    // Also update the oxidize-pdf Document model (no-op for writes since the writer
    // doesn't emit it, but keeps the model consistent for any future core fix).
    let action = match payload.kind.as_str() {
        "goto" => {
            if let Some(dest) = payload.destination {
                oxidize_pdf::actions::Action::goto(dest.to_core())
            } else {
                set_last_error("goto open action requires 'destination'");
                return ErrorCode::InvalidArgument as c_int;
            }
        }
        "uri" => {
            if let Some(uri) = payload.uri {
                oxidize_pdf::actions::Action::uri(uri)
            } else {
                set_last_error("uri open action requires 'uri'");
                return ErrorCode::InvalidArgument as c_int;
            }
        }
        other => {
            set_last_error(&format!("Unknown open action kind: {other}"));
            return ErrorCode::InvalidArgument as c_int;
        }
    };
    (*handle).inner.set_open_action(action);

    // Store the formatted PDF action for injection at save time.
    (*handle).pending_open_action_pdf = Some(action_pdf);

    ErrorCode::Success as c_int
}
