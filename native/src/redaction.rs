//! Explicit visual masking versus upstream's supported irreversible text removal.
use crate::{clear_last_error, set_last_error, ErrorCode};
use base64::Engine as _;
use oxidize_pdf::operations::semantic_redactor::{
    RedactionConfig, RedactionStyle, SemanticRedactor,
};
use oxidize_pdf::semantic::{BoundingBox, EntityType, SemanticEntity};
use serde::Deserialize;
use serde_json::{json, Value};
use std::ffi::{CStr, CString};
use std::os::raw::{c_char, c_int};
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Entity {
    id: String,
    #[serde(rename = "type")]
    entity_type: EntityType,
    bounds: BoundingBox,
    content: String,
}
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Request {
    irreversible: bool,
    entities: Vec<Entity>,
    entity_types: Vec<EntityType>,
    placeholder: Option<String>,
}
/// Remove supported text irreversibly, or perform explicit visual masking.
///
/// # Safety
/// PDF buffer and NUL-terminated UTF-8 JSON must be readable. Output must be
/// writable and released with oxidize_free_string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_redaction(
    bytes: *const u8,
    len: usize,
    request: *const c_char,
    output: *mut *mut c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if bytes.is_null() || request.is_null() || output.is_null() {
            set_last_error("Null redaction pointer");
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
            if r.irreversible && r.placeholder.is_some() {
                return Err("Placeholder is only supported for visual masking".into());
            }
            let entities = r
                .entities
                .into_iter()
                .map(|e| SemanticEntity::new(e.id, e.entity_type, e.bounds).with_content(e.content))
                .collect::<Vec<_>>();
            let config = RedactionConfig {
                entity_types: r.entity_types,
                style: r
                    .placeholder
                    .map(RedactionStyle::Placeholder)
                    .unwrap_or_default(),
            };
            let pdf = std::slice::from_raw_parts(bytes, len);
            let (pdf, report) = if r.irreversible {
                SemanticRedactor::redact_irreversible(pdf, &entities, config)
            } else {
                SemanticRedactor::redact(pdf, &entities, config)
            }
            .map_err(|e| e.to_string())?;
            Ok(
                json!({"pdf_bytes":base64::engine::general_purpose::STANDARD.encode(pdf),
                "report":{"mode":format!("{:?}",report.mode()),"is_irreversible":report.is_irreversible(),"residual_risks":report.residual_risks(),"redacted_count":report.redacted_count(),"pages_affected":report.pages_affected(),
                "entries":report.entries().iter().map(|e|json!({"entity_id":e.entity_id,"entity_type":e.entity_type,"page":e.page})).collect::<Vec<_>>()}}),
            )
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
