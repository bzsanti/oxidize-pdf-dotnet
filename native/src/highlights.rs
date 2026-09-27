//! Incremental highlight editing over a JSON FFI boundary.
use crate::{clear_last_error, set_last_error, ErrorCode};
use base64::Engine as _;
use oxidize_pdf::writer::{
    Highlight, HighlightColor, HighlightId, HighlightMutation, HighlightOpacity, HighlightQuad,
    IncrementalHighlightEditor,
};
use serde::Deserialize;
use serde_json::{json, Value};
use std::ffi::{CStr, CString};
use std::os::raw::{c_char, c_int};

#[derive(Deserialize)]
#[serde(tag = "operation", rename_all = "snake_case", deny_unknown_fields)]
enum Mutation {
    Add {
        page_index: u32,
        quadrilaterals: Vec<[f64; 8]>,
        color: [f64; 3],
        opacity: Option<f64>,
        contents: Option<String>,
    },
    Remove {
        object_number: u32,
        generation_number: u16,
    },
}

impl Mutation {
    fn into_core(self) -> Result<HighlightMutation, String> {
        Ok(match self {
            Self::Add {
                page_index,
                quadrilaterals,
                color,
                opacity,
                contents,
            } => {
                if quadrilaterals.is_empty() {
                    return Err("Highlight geometry must not be empty".into());
                }
                let quads = quadrilaterals
                    .into_iter()
                    .map(|q| {
                        HighlightQuad::new(std::array::from_fn(|i| {
                            oxidize_pdf::Point::new(q[i * 2], q[i * 2 + 1])
                        }))
                        .map_err(|e| e.to_string())
                    })
                    .collect::<Result<Vec<_>, _>>()?;
                HighlightMutation::Add {
                    page_index,
                    quadrilaterals: quads,
                    color: HighlightColor::new(color).map_err(|e| e.to_string())?,
                    opacity: opacity
                        .map(HighlightOpacity::new)
                        .transpose()
                        .map_err(|e| e.to_string())?,
                    contents,
                }
            }
            Self::Remove {
                object_number,
                generation_number,
            } => HighlightMutation::Remove {
                id: HighlightId::new(object_number, generation_number),
            },
        })
    }
}

fn highlight_json(h: Highlight) -> Value {
    let quads: Vec<[f64; 8]> = h
        .quadrilaterals
        .iter()
        .map(|q| {
            std::array::from_fn(|i| {
                if i % 2 == 0 {
                    q.points()[i / 2].x
                } else {
                    q.points()[i / 2].y
                }
            })
        })
        .collect();
    json!({ "id": {"object_number": h.id.object_number, "generation_number": h.id.generation_number},
        "page_index": h.page_index, "rect": h.rect, "quadrilaterals": quads,
        "color": h.color.components(), "opacity": h.opacity.map(|v| v.value()), "contents": h.contents })
}

/// List highlights when `mutations_json` is null, or apply an atomic add/remove batch.
///
/// # Safety
/// `bytes` points to `len` readable bytes; non-null `mutations_json` points to a
/// NUL-terminated UTF-8 string. `out_json` is writable. Free returned JSON using
/// `oxidize_free_string`. The JSON contains base64 PDF bytes only for edits.
#[no_mangle]
pub unsafe extern "C" fn oxidize_highlights_json(
    bytes: *const u8,
    len: usize,
    mutations_json: *const c_char,
    out_json: *mut *mut c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if bytes.is_null() || out_json.is_null() {
            set_last_error("Null pointer in highlight operation");
            return ErrorCode::NullPointer as c_int;
        }
        *out_json = std::ptr::null_mut();
        if len == 0 {
            set_last_error("PDF data must not be empty");
            return ErrorCode::InvalidArgument as c_int;
        }
        let editor = IncrementalHighlightEditor::new(std::slice::from_raw_parts(bytes, len));
        let result = (|| -> Result<Value, (ErrorCode, String)> {
            if mutations_json.is_null() {
                let highlights = editor
                    .highlights()
                    .map_err(|e| (ErrorCode::PdfParseError, e.to_string()))?;
                return Ok(
                    json!({ "highlights": highlights.into_iter().map(highlight_json).collect::<Vec<_>>() }),
                );
            }
            let input = CStr::from_ptr(mutations_json)
                .to_str()
                .map_err(|e| (ErrorCode::InvalidUtf8, e.to_string()))?;
            let dtos: Vec<Mutation> = serde_json::from_str(input)
                .map_err(|e| (ErrorCode::SerializationError, e.to_string()))?;
            if dtos.is_empty() {
                return Err((
                    ErrorCode::InvalidArgument,
                    "At least one mutation is required".into(),
                ));
            }
            let mutations = dtos
                .into_iter()
                .map(Mutation::into_core)
                .collect::<Result<Vec<_>, _>>()
                .map_err(|e| (ErrorCode::InvalidArgument, e))?;
            let update = editor
                .apply(&mutations)
                .map_err(|e| (ErrorCode::InvalidArgument, e.to_string()))?;
            Ok(
                json!({ "pdf_bytes": base64::engine::general_purpose::STANDARD.encode(update.pdf_bytes),
                "highlights": update.highlights.into_iter().map(highlight_json).collect::<Vec<_>>() }),
            )
        })();
        match result {
            Ok(value) => match CString::new(value.to_string()) {
                Ok(json) => {
                    *out_json = json.into_raw();
                    ErrorCode::Success as c_int
                }
                Err(e) => {
                    set_last_error(e.to_string());
                    ErrorCode::SerializationError as c_int
                }
            },
            Err((code, message)) => {
                set_last_error(message);
                code as c_int
            }
        }
    })
}
