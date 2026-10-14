//! Incremental Ink editing over a JSON FFI boundary.
use crate::{clear_last_error, set_last_error, ErrorCode};
use base64::Engine as _;
use oxidize_pdf::writer::{
    IncrementalInkEditor, Ink, InkColor, InkId, InkMutation, InkOpacity, InkStroke, InkWidth,
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
        strokes: Vec<Vec<f64>>,
        color: Vec<f64>,
        width: f64,
        opacity: Option<f64>,
    },
    Update {
        object_number: u32,
        generation_number: u16,
        strokes: Vec<Vec<f64>>,
        color: Vec<f64>,
        width: f64,
        opacity: Option<f64>,
    },
    Remove {
        object_number: u32,
        generation_number: u16,
    },
}
fn color(values: Vec<f64>) -> Result<InkColor, String> {
    match values.as_slice() {
        [g] => InkColor::gray(*g),
        [r, g, b] => InkColor::new([*r, *g, *b]),
        [c, m, y, k] => InkColor::cmyk([*c, *m, *y, *k]),
        _ => return Err("Ink color requires 1, 3 or 4 components".into()),
    }
    .map_err(|e| e.to_string())
}
fn strokes(values: Vec<Vec<f64>>) -> Result<Vec<InkStroke>, String> {
    if values.is_empty() || values.len() > 4096 {
        return Err("Ink requires 1..4096 strokes".into());
    }
    let mut points = 0usize;
    values
        .into_iter()
        .map(|stroke| {
            points = points.saturating_add(stroke.len() / 2);
            if stroke.is_empty() || stroke.len() % 2 != 0 || points > 1_000_000 {
                return Err(
                    "Ink strokes require coordinate pairs and at most 1000000 points".into(),
                );
            }
            InkStroke::new(
                stroke
                    .chunks_exact(2)
                    .map(|p| oxidize_pdf::Point::new(p[0], p[1]))
                    .collect(),
            )
            .map_err(|e| e.to_string())
        })
        .collect()
}
impl Mutation {
    fn into_core(self) -> Result<InkMutation, String> {
        Ok(match self {
            Self::Add {
                page_index,
                strokes: s,
                color: c,
                width,
                opacity,
            } => InkMutation::Add {
                page_index,
                strokes: strokes(s)?,
                color: color(c)?,
                width: InkWidth::new(width).map_err(|e| e.to_string())?,
                opacity: opacity
                    .map(InkOpacity::new)
                    .transpose()
                    .map_err(|e| e.to_string())?,
            },
            Self::Update {
                object_number,
                generation_number,
                strokes: s,
                color: c,
                width,
                opacity,
            } => InkMutation::Update {
                id: InkId::new(object_number, generation_number),
                strokes: strokes(s)?,
                color: color(c)?,
                width: InkWidth::new(width).map_err(|e| e.to_string())?,
                opacity: opacity
                    .map(InkOpacity::new)
                    .transpose()
                    .map_err(|e| e.to_string())?,
            },
            Self::Remove {
                object_number,
                generation_number,
            } => InkMutation::Remove {
                id: InkId::new(object_number, generation_number),
            },
        })
    }
}
fn annotation_json(h: Ink) -> Value {
    let color = match h.color {
        InkColor::Gray(c) => c.to_vec(),
        InkColor::Rgb(c) => c.to_vec(),
        InkColor::Cmyk(c) => c.to_vec(),
    };
    let strokes: Vec<Vec<f64>> = h
        .strokes
        .iter()
        .map(|s| s.points().iter().flat_map(|p| [p.x, p.y]).collect())
        .collect();
    json!({ "id": {"object_number": h.id.object_number, "generation_number": h.id.generation_number},
        "page_index": h.page_index, "rect": h.rect, "strokes": strokes,
        "color": color, "width": h.width.value(), "opacity": h.opacity.map(|o| o.value()) })
}

/// List annotations when `mutations_json` is null, or apply an atomic add/update/remove batch.
///
/// # Safety
/// `bytes` points to `len` readable bytes; non-null `mutations_json` points to a
/// NUL-terminated UTF-8 string. `out_json` is writable. Free returned JSON using
/// `oxidize_free_string`. The JSON contains base64 PDF bytes only for edits.
#[no_mangle]
pub unsafe extern "C" fn oxidize_ink_json(
    bytes: *const u8,
    len: usize,
    mutations_json: *const c_char,
    out_json: *mut *mut c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if bytes.is_null() || out_json.is_null() {
            set_last_error("Null pointer in Ink operation");
            return ErrorCode::NullPointer as c_int;
        }
        *out_json = std::ptr::null_mut();
        if len == 0 {
            set_last_error("PDF data must not be empty");
            return ErrorCode::InvalidArgument as c_int;
        }
        let editor = IncrementalInkEditor::new(std::slice::from_raw_parts(bytes, len));
        let result = (|| -> Result<Value, (ErrorCode, String)> {
            if mutations_json.is_null() {
                let annotations = editor
                    .annotations()
                    .map_err(|e| (ErrorCode::PdfParseError, e.to_string()))?;
                return Ok(
                    json!({ "annotations": annotations.into_iter().map(annotation_json).collect::<Vec<_>>() }),
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
                "annotations": update.annotations.into_iter().map(annotation_json).collect::<Vec<_>>() }),
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
