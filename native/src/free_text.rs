//! Incremental free-text editing over a JSON FFI boundary.
use crate::{clear_last_error, set_last_error, ErrorCode};
use base64::Engine as _;
use oxidize_pdf::writer::{
    FreeText, FreeTextAlignment, FreeTextId, FreeTextMutation, IncrementalFreeTextEditor,
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
        rect: [f64; 4],
        contents: String,
        default_appearance: String,
        alignment: u8,
    },
    Update {
        object_number: u32,
        generation_number: u16,
        rect: [f64; 4],
        contents: String,
        default_appearance: String,
        alignment: u8,
    },
    Remove {
        object_number: u32,
        generation_number: u16,
    },
}
fn alignment(value: u8) -> Result<FreeTextAlignment, String> {
    match value {
        0 => Ok(FreeTextAlignment::Left),
        1 => Ok(FreeTextAlignment::Center),
        2 => Ok(FreeTextAlignment::Right),
        _ => Err("Invalid FreeText alignment".into()),
    }
}
impl Mutation {
    fn into_core(self) -> Result<FreeTextMutation, String> {
        Ok(match self {
            Self::Add {
                page_index,
                rect,
                contents,
                default_appearance,
                alignment: a,
            } => FreeTextMutation::Add {
                page_index,
                rect,
                contents,
                default_appearance,
                alignment: alignment(a)?,
            },
            Self::Update {
                object_number,
                generation_number,
                rect,
                contents,
                default_appearance,
                alignment: a,
            } => FreeTextMutation::Update {
                id: FreeTextId::new(object_number, generation_number),
                rect,
                contents,
                default_appearance,
                alignment: alignment(a)?,
            },
            Self::Remove {
                object_number,
                generation_number,
            } => FreeTextMutation::Remove {
                id: FreeTextId::new(object_number, generation_number),
            },
        })
    }
}
fn annotation_json(h: FreeText) -> Value {
    let alignment = match h.alignment {
        FreeTextAlignment::Left => 0,
        FreeTextAlignment::Center => 1,
        FreeTextAlignment::Right => 2,
    };
    json!({ "id": {"object_number": h.id.object_number, "generation_number": h.id.generation_number},
        "page_index": h.page_index, "rect": h.rect, "contents": h.contents,
        "default_appearance": h.default_appearance, "alignment": alignment })
}

/// List annotations when `mutations_json` is null, or apply an atomic add/update/remove batch.
///
/// # Safety
/// `bytes` points to `len` readable bytes; non-null `mutations_json` points to a
/// NUL-terminated UTF-8 string. `out_json` is writable. Free returned JSON using
/// `oxidize_free_string`. The JSON contains base64 PDF bytes only for edits.
#[no_mangle]
pub unsafe extern "C" fn oxidize_free_text_json(
    bytes: *const u8,
    len: usize,
    mutations_json: *const c_char,
    out_json: *mut *mut c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if bytes.is_null() || out_json.is_null() {
            set_last_error("Null pointer in free-text operation");
            return ErrorCode::NullPointer as c_int;
        }
        *out_json = std::ptr::null_mut();
        if len == 0 {
            set_last_error("PDF data must not be empty");
            return ErrorCode::InvalidArgument as c_int;
        }
        let editor = IncrementalFreeTextEditor::new(std::slice::from_raw_parts(bytes, len));
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
