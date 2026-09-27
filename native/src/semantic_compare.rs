//! Bounded comparison and physical revision attribution using the official core.
use crate::{clear_last_error, set_last_error, ErrorCode};
use oxidize_pdf::verification::semantic_comparison::{
    compare_pdfs_semantically, SemanticComparisonLimits, SemanticComparisonOptions,
};
use serde::Deserialize;
use serde_json::{json, Value};
use std::ffi::{CStr, CString};
use std::os::raw::{c_char, c_int};

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Limits {
    max_objects: usize,
    max_depth: usize,
    max_decoded_stream_bytes: usize,
    max_canonical_bytes: usize,
    max_extracted_text_bytes: usize,
    max_unreachable_objects: usize,
    max_revisions: usize,
}
fn identity((number, generation): (u32, u16)) -> Value {
    json!({"object_number":number,"generation":generation})
}
/// Compare PDFs with caller-selected resource bounds.
///
/// # Safety
/// Input buffers must be readable for their lengths, options must be UTF-8
/// NUL-terminated JSON, and output must be writable. Free JSON with oxidize_free_string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_compare_semantically(
    left: *const u8,
    left_len: usize,
    right: *const u8,
    right_len: usize,
    options: *const c_char,
    output: *mut *mut c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if left.is_null() || right.is_null() || options.is_null() || output.is_null() {
            set_last_error("Null semantic comparison pointer");
            return ErrorCode::NullPointer as c_int;
        }
        *output = std::ptr::null_mut();
        let result = (|| -> Result<Value, String> {
            let l: Limits = serde_json::from_str(
                CStr::from_ptr(options)
                    .to_str()
                    .map_err(|e| e.to_string())?,
            )
            .map_err(|e| e.to_string())?;
            let options = SemanticComparisonOptions {
                limits: SemanticComparisonLimits {
                    max_objects: l.max_objects,
                    max_depth: l.max_depth,
                    max_decoded_stream_bytes: l.max_decoded_stream_bytes,
                    max_canonical_bytes: l.max_canonical_bytes,
                    max_extracted_text_bytes: l.max_extracted_text_bytes,
                    max_unreachable_objects: l.max_unreachable_objects,
                    max_revisions: l.max_revisions,
                },
            };
            let r = compare_pdfs_semantically(
                std::slice::from_raw_parts(left, left_len),
                std::slice::from_raw_parts(right, right_len),
                &options,
            )
            .map_err(|e| e.to_string())?;
            let revisions = |items: Vec<
                oxidize_pdf::verification::semantic_comparison::PdfRevisionSummary,
            >| {
                items.into_iter().map(|r| json!({
                "index":r.index,"xref_offset":r.xref_offset,
                "semantic_fingerprint":r.semantic_fingerprint.iter().map(|b|format!("{b:02x}")).collect::<String>(),
                "object_changes":r.object_changes.into_iter().map(|o|json!({"id":identity((o.object_number,o.generation)),"kind":format!("{:?}",o.kind)})).collect::<Vec<_>>()
            })).collect::<Vec<_>>()
            };
            Ok(json!({"semantically_equal":r.semantically_equal,
                "differences":r.differences.into_iter().map(|d|json!({"path":d.path,"class":format!("{:?}",d.class),"description":d.description})).collect::<Vec<_>>(),
                "left_revisions":revisions(r.left_revisions),"right_revisions":revisions(r.right_revisions),
                "left_unreachable_objects":r.left_unreachable_objects.into_iter().map(identity).collect::<Vec<_>>(),
                "right_unreachable_objects":r.right_unreachable_objects.into_iter().map(identity).collect::<Vec<_>>(),
                "equivalent_objects":r.semantically_equivalent_objects.into_iter().map(|(l,r)|json!({"left":identity(l),"right":identity(r)})).collect::<Vec<_>>()
            }))
        })();
        match result {
            Ok(value) => match CString::new(value.to_string()) {
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
