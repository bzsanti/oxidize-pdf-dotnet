//! Append provider-produced OCR fragments without rebuilding existing PDF objects.
use crate::{clear_last_error, set_last_error, ErrorCode};
use base64::Engine as _;
use oxidize_pdf::writer::{
    IncrementalOcrLayerEditor, OcrLayerFragment, OcrLayerPage, OcrLayerPlan,
};
use serde::Deserialize;
use serde_json::{json, Value};
use std::ffi::{CStr, CString};
use std::os::raw::{c_char, c_int};

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Fragment {
    text: String,
    region: [f64; 4],
    confidence: f64,
    reading_order: u32,
}
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Page {
    page_index: u32,
    language: String,
    fragments: Vec<Fragment>,
}
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Request {
    plan_only: bool,
    pages: Vec<Page>,
}
fn plan(p: OcrLayerPlan) -> Value {
    json!({"pages":p.pages,"streams_added":p.streams_added,"resources_added":p.resources_added,"pages_skipped_existing_ocr":p.pages_skipped_existing_ocr})
}
/// Plan or apply an incremental OCR layer from caller-supplied recognition data.
///
/// # Safety
/// PDF buffer and NUL-terminated UTF-8 request must be readable. Output must be
/// writable and freed with oxidize_free_string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_ocr_layer(
    bytes: *const u8,
    len: usize,
    request: *const c_char,
    output: *mut *mut c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if bytes.is_null() || request.is_null() || output.is_null() {
            set_last_error("Null OCR layer pointer");
            return ErrorCode::NullPointer as c_int;
        }
        *output = std::ptr::null_mut();
        let result = (|| -> Result<Value, String> {
            let r: Request = serde_json::from_str(
                CStr::from_ptr(request)
                    .to_str()
                    .map_err(|e| e.to_string())?,
            )
            .map_err(|e| e.to_string())?;
            let pages = r
                .pages
                .into_iter()
                .map(|p| OcrLayerPage {
                    page_index: p.page_index,
                    language: p.language,
                    fragments: p
                        .fragments
                        .into_iter()
                        .map(|f| OcrLayerFragment {
                            text: f.text,
                            region: f.region,
                            confidence: f.confidence,
                            reading_order: f.reading_order,
                        })
                        .collect(),
                })
                .collect::<Vec<_>>();
            let editor = IncrementalOcrLayerEditor::new(std::slice::from_raw_parts(bytes, len));
            if r.plan_only {
                editor.plan(&pages).map(plan).map_err(|e| e.to_string())
            } else {
                editor.apply(&pages).map(|r|json!({"pdf_bytes":base64::engine::general_purpose::STANDARD.encode(r.pdf_bytes),"plan":plan(r.plan)})).map_err(|e|e.to_string())
            }
        })();
        match result {
            Ok(v) => match CString::new(v.to_string()) {
                Ok(s) => {
                    *output = s.into_raw();
                    ErrorCode::Success as c_int
                }
                Err(e) => {
                    set_last_error(e.to_string());
                    ErrorCode::SerializationError as c_int
                }
            },
            Err(e) => {
                set_last_error(e);
                ErrorCode::InvalidArgument as c_int
            }
        }
    })
}
