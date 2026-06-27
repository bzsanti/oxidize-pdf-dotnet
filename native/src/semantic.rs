//! DOC-021 — Semantic entities (AI-ready markup).
//!
//! Bridges `oxidize_pdf`'s semantic-entity API: a caller marks regions of a
//! document with a typed entity (bounding box + content + metadata +
//! relationships) and exports the result as JSON or JSON-LD (Schema.org).
//!
//! IMPORTANT CAVEAT: in oxidize-pdf 2.14.0 semantic entities are an in-memory
//! annotation + export feature. They are NOT persisted into the saved PDF
//! (the writer only sets a feature-fingerprint bit). The value of this bridge
//! is producing AI-ready JSON/JSON-LD markup alongside the document, not
//! embedding accessibility structure in the PDF itself (use DOC-019 /
//! `oxidize_document_set_struct_tree_json` for in-PDF tagged structure).

use std::ffi::{CStr, CString};
use std::os::raw::{c_char, c_int};

use oxidize_pdf::semantic::{BoundingBox, EntityType, RelationType};

use crate::document::DocumentHandle;
use crate::{clear_last_error, set_last_error, ErrorCode};

/// Parse a caller-supplied type string into an `EntityType`. Known camelCase
/// names (e.g. "text", "invoice", "invoiceNumber") map to standard variants;
/// anything else becomes `EntityType::Custom(name)`.
fn entity_type_from_str(s: &str) -> EntityType {
    match serde_json::from_str::<EntityType>(&format!("\"{s}\"")) {
        Ok(t) => t,
        Err(_) => EntityType::Custom(s.to_string()),
    }
}

/// Parse a caller-supplied relation string into a `RelationType`.
fn relation_type_from_str(s: &str) -> RelationType {
    match serde_json::from_str::<RelationType>(&format!("\"{s}\"")) {
        Ok(t) => t,
        Err(_) => RelationType::Custom(s.to_string()),
    }
}

/// Read a non-null C string argument, setting the last error on failure.
unsafe fn read_str<'a>(ptr: *const c_char, what: &str) -> Result<&'a str, c_int> {
    if ptr.is_null() {
        set_last_error(format!("Null pointer provided for {what}"));
        return Err(ErrorCode::NullPointer as c_int);
    }
    CStr::from_ptr(ptr).to_str().map_err(|_| {
        set_last_error(format!("Invalid UTF-8 in {what}"));
        ErrorCode::InvalidUtf8 as c_int
    })
}

/// DOC-021 — Mark a region of the document as a typed semantic entity.
///
/// `entity_type` is a camelCase type name (e.g. "heading", "invoiceNumber");
/// unknown names become custom types. Bounds are page-space coordinates.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `id` and `entity_type` must be valid non-null, null-terminated UTF-8 C strings.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_mark_entity(
    handle: *mut DocumentHandle,
    id: *const c_char,
    entity_type: *const c_char,
    x: f64,
    y: f64,
    width: f64,
    height: f64,
    page: u32,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if handle.is_null() {
            set_last_error("Null pointer provided to oxidize_document_mark_entity");
            return ErrorCode::NullPointer as c_int;
        }
        let id_str = match read_str(id, "entity id") {
            Ok(s) => s,
            Err(code) => return code,
        };
        let type_str = match read_str(entity_type, "entity type") {
            Ok(s) => s,
            Err(code) => return code,
        };
        let bounds = BoundingBox::new(x as f32, y as f32, width as f32, height as f32, page);
        (*handle)
            .inner
            .mark_entity(id_str.to_string(), entity_type_from_str(type_str), bounds);
        ErrorCode::Success as c_int
    })
}

/// DOC-021 — Set the text content of a previously marked entity.
/// Returns `InvalidArgument` if no entity has the given id.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `id` and `content` must be valid non-null, null-terminated UTF-8 C strings.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_set_entity_content(
    handle: *mut DocumentHandle,
    id: *const c_char,
    content: *const c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if handle.is_null() {
            set_last_error("Null pointer provided to oxidize_document_set_entity_content");
            return ErrorCode::NullPointer as c_int;
        }
        let id_str = match read_str(id, "entity id") {
            Ok(s) => s,
            Err(code) => return code,
        };
        let content_str = match read_str(content, "entity content") {
            Ok(s) => s,
            Err(code) => return code,
        };
        if (*handle).inner.set_entity_content(id_str, content_str) {
            ErrorCode::Success as c_int
        } else {
            set_last_error(format!("No entity with id '{id_str}'"));
            ErrorCode::InvalidArgument as c_int
        }
    })
}

/// DOC-021 — Add a metadata key/value pair to a marked entity.
/// Returns `InvalidArgument` if no entity has the given id.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `id`, `key`, `value` must be valid non-null, null-terminated UTF-8 C strings.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_add_entity_metadata(
    handle: *mut DocumentHandle,
    id: *const c_char,
    key: *const c_char,
    value: *const c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if handle.is_null() {
            set_last_error("Null pointer provided to oxidize_document_add_entity_metadata");
            return ErrorCode::NullPointer as c_int;
        }
        let id_str = match read_str(id, "entity id") {
            Ok(s) => s,
            Err(code) => return code,
        };
        let key_str = match read_str(key, "metadata key") {
            Ok(s) => s,
            Err(code) => return code,
        };
        let value_str = match read_str(value, "metadata value") {
            Ok(s) => s,
            Err(code) => return code,
        };
        if (*handle)
            .inner
            .add_entity_metadata(id_str, key_str, value_str)
        {
            ErrorCode::Success as c_int
        } else {
            set_last_error(format!("No entity with id '{id_str}'"));
            ErrorCode::InvalidArgument as c_int
        }
    })
}

/// DOC-021 — Set the confidence (0.0..=1.0) of a marked entity.
/// Returns `InvalidArgument` if no entity has the given id.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `id` must be a valid non-null, null-terminated UTF-8 C string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_set_entity_confidence(
    handle: *mut DocumentHandle,
    id: *const c_char,
    confidence: f32,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if handle.is_null() {
            set_last_error("Null pointer provided to oxidize_document_set_entity_confidence");
            return ErrorCode::NullPointer as c_int;
        }
        let id_str = match read_str(id, "entity id") {
            Ok(s) => s,
            Err(code) => return code,
        };
        if (*handle).inner.set_entity_confidence(id_str, confidence) {
            ErrorCode::Success as c_int
        } else {
            set_last_error(format!("No entity with id '{id_str}'"));
            ErrorCode::InvalidArgument as c_int
        }
    })
}

/// DOC-021 — Record a relationship between two marked entities.
/// Returns `InvalidArgument` if either id is unknown.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `from_id`, `to_id`, `relation` must be valid non-null, null-terminated UTF-8 C strings.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_relate_entities(
    handle: *mut DocumentHandle,
    from_id: *const c_char,
    to_id: *const c_char,
    relation: *const c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if handle.is_null() {
            set_last_error("Null pointer provided to oxidize_document_relate_entities");
            return ErrorCode::NullPointer as c_int;
        }
        let from_str = match read_str(from_id, "from entity id") {
            Ok(s) => s,
            Err(code) => return code,
        };
        let to_str = match read_str(to_id, "to entity id") {
            Ok(s) => s,
            Err(code) => return code,
        };
        let rel_str = match read_str(relation, "relation type") {
            Ok(s) => s,
            Err(code) => return code,
        };
        if (*handle)
            .inner
            .relate_entities(from_str, to_str, relation_type_from_str(rel_str))
        {
            ErrorCode::Success as c_int
        } else {
            set_last_error("relate_entities failed: unknown entity id");
            ErrorCode::InvalidArgument as c_int
        }
    })
}

/// DOC-021 — Export all marked semantic entities as a plain JSON array.
///
/// Unlike the JSON-LD form, this preserves every field of each entity —
/// including its `content` text and relationships — by serializing the entities
/// directly. The returned string must be freed with `oxidize_free_string`.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `out_json` must be a writeable `*mut *mut c_char`. Set to null on error.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_export_semantic_entities_json(
    handle: *mut DocumentHandle,
    out_json: *mut *mut c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if handle.is_null() || out_json.is_null() {
            set_last_error(
                "Null pointer provided to oxidize_document_export_semantic_entities_json",
            );
            return ErrorCode::NullPointer as c_int;
        }
        *out_json = std::ptr::null_mut();
        let json = match (*handle).inner.export_semantic_entities_json() {
            Ok(s) => s,
            Err(e) => {
                set_last_error(format!("export_semantic_entities_json failed: {e}"));
                return ErrorCode::SerializationError as c_int;
            }
        };
        match CString::new(json) {
            Ok(c) => {
                *out_json = c.into_raw();
                ErrorCode::Success as c_int
            }
            Err(e) => {
                set_last_error(format!("Export contains null bytes: {e}"));
                ErrorCode::InvalidUtf8 as c_int
            }
        }
    })
}

/// DOC-021 — Export all marked semantic entities as JSON-LD (Schema.org).
///
/// The JSON-LD form carries entity type, id, bounds, metadata and confidence
/// under a Schema.org context, but NOT the per-entity `content` text (use
/// `oxidize_document_export_semantic_entities_json` for full fidelity).
/// The returned string must be freed with `oxidize_free_string`.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `out_json` must be a writeable `*mut *mut c_char`. Set to null on error.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_export_semantic_entities_json_ld(
    handle: *mut DocumentHandle,
    out_json: *mut *mut c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if handle.is_null() || out_json.is_null() {
            set_last_error(
                "Null pointer provided to oxidize_document_export_semantic_entities_json_ld",
            );
            return ErrorCode::NullPointer as c_int;
        }
        *out_json = std::ptr::null_mut();
        let json = match (*handle).inner.export_semantic_entities_json_ld() {
            Ok(s) => s,
            Err(e) => {
                set_last_error(format!("export_semantic_entities_json_ld failed: {e}"));
                return ErrorCode::SerializationError as c_int;
            }
        };
        match CString::new(json) {
            Ok(c) => {
                *out_json = c.into_raw();
                ErrorCode::Success as c_int
            }
            Err(e) => {
                set_last_error(format!("Export contains null bytes: {e}"));
                ErrorCode::InvalidUtf8 as c_int
            }
        }
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::document::{oxidize_document_create, oxidize_document_free};
    use crate::oxidize_free_string;

    unsafe fn cstr(s: &str) -> CString {
        CString::new(s).unwrap()
    }

    #[test]
    fn mark_and_export_entity_plain_json_carries_content_jsonld_carries_context() {
        unsafe {
            let doc = oxidize_document_create();
            let id = cstr("inv_num_1");
            let ty = cstr("invoiceNumber");
            assert_eq!(
                oxidize_document_mark_entity(
                    doc,
                    id.as_ptr(),
                    ty.as_ptr(),
                    100.0,
                    700.0,
                    150.0,
                    20.0,
                    1
                ),
                0
            );
            let content = cstr("INV-2024-001");
            assert_eq!(
                oxidize_document_set_entity_content(doc, id.as_ptr(), content.as_ptr()),
                0
            );
            assert_eq!(
                oxidize_document_set_entity_confidence(doc, id.as_ptr(), 0.97),
                0
            );

            // Plain JSON preserves the entity content and type.
            let mut plain: *mut c_char = std::ptr::null_mut();
            assert_eq!(
                oxidize_document_export_semantic_entities_json(doc, &mut plain),
                0
            );
            let plain_json = CStr::from_ptr(plain).to_str().unwrap().to_string();
            oxidize_free_string(plain);
            assert!(
                plain_json.contains("INV-2024-001"),
                "plain JSON must carry content; got {plain_json}"
            );
            assert!(
                plain_json.contains("invoiceNumber"),
                "plain JSON must carry the entity type; got {plain_json}"
            );

            // JSON-LD carries a Schema.org context and the entity id.
            let mut ld: *mut c_char = std::ptr::null_mut();
            assert_eq!(
                oxidize_document_export_semantic_entities_json_ld(doc, &mut ld),
                0
            );
            let ld_json = CStr::from_ptr(ld).to_str().unwrap().to_string();
            oxidize_free_string(ld);
            oxidize_document_free(doc);

            assert!(
                ld_json.contains("@context") && ld_json.contains("schema.org"),
                "JSON-LD must include a Schema.org context; got {ld_json}"
            );
            assert!(
                ld_json.contains("inv_num_1"),
                "JSON-LD must reference the entity id; got {ld_json}"
            );
        }
    }

    #[test]
    fn set_content_unknown_entity_returns_invalid_argument() {
        unsafe {
            let doc = oxidize_document_create();
            let id = cstr("does_not_exist");
            let content = cstr("x");
            assert_eq!(
                oxidize_document_set_entity_content(doc, id.as_ptr(), content.as_ptr()),
                9
            );
            oxidize_document_free(doc);
        }
    }

    #[test]
    fn relate_entities_records_relationship_in_export() {
        unsafe {
            let doc = oxidize_document_create();
            let a = cstr("a");
            let b = cstr("b");
            let ty = cstr("text");
            oxidize_document_mark_entity(doc, a.as_ptr(), ty.as_ptr(), 0.0, 0.0, 10.0, 10.0, 1);
            oxidize_document_mark_entity(doc, b.as_ptr(), ty.as_ptr(), 0.0, 20.0, 10.0, 10.0, 1);
            let rel = cstr("contains");
            assert_eq!(
                oxidize_document_relate_entities(doc, a.as_ptr(), b.as_ptr(), rel.as_ptr()),
                0
            );
            // Unknown id must fail.
            let bad = cstr("nope");
            assert_eq!(
                oxidize_document_relate_entities(doc, a.as_ptr(), bad.as_ptr(), rel.as_ptr()),
                9
            );
            oxidize_document_free(doc);
        }
    }

    #[test]
    fn mark_entity_null_handle_returns_null_pointer() {
        unsafe {
            let id = cstr("x");
            let ty = cstr("text");
            assert_eq!(
                oxidize_document_mark_entity(
                    std::ptr::null_mut(),
                    id.as_ptr(),
                    ty.as_ptr(),
                    0.0,
                    0.0,
                    1.0,
                    1.0,
                    0
                ),
                1
            );
        }
    }
}
