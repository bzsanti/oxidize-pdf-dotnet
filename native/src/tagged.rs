//! DOC-019 — Tagged PDF logical structure tree.
//!
//! Bridges `oxidize_pdf`'s structure-tree API (`StructTree` /
//! `StructureElement` / `StandardStructureType`) so a .NET caller can attach a
//! logical structure tree to a document via a single JSON description. When the
//! document is serialized the writer emits `/StructTreeRoot`, `/MarkInfo
//! <</Marked true>>`, and the `/StructElem` dictionaries (ISO 32000-1 §14.7-14.8),
//! producing a Tagged PDF — the basis for PDF/UA accessibility.

use std::ffi::CStr;
use std::os::raw::{c_char, c_int};

use serde::Deserialize;

use oxidize_pdf::structure::{StandardStructureType, StructTree, StructureElement};

use crate::document::DocumentHandle;
use crate::{clear_last_error, set_last_error, ErrorCode};

/// A marked-content reference linking a structure element to tagged content on
/// a page (the MCID returned by `oxidize_page_begin_marked_content`).
#[derive(Debug, Deserialize)]
struct McidDto {
    page: usize,
    mcid: u32,
}

/// One structure element. `parent` is the zero-based index of the parent in the
/// `elements` array; the single element with `parent == null` is the root.
/// Parents must appear before their children in the array.
#[derive(Debug, Deserialize)]
struct StructElementDto {
    /// PDF structure type name, e.g. "Document", "H1", "P", "Figure". Unknown
    /// names become custom structure types (and should be role-mapped).
    #[serde(rename = "type")]
    type_name: String,
    parent: Option<usize>,
    id: Option<String>,
    lang: Option<String>,
    alt_text: Option<String>,
    actual_text: Option<String>,
    title: Option<String>,
    #[serde(default)]
    mcids: Vec<McidDto>,
}

/// Top-level structure-tree description. `role_map` maps custom structure type
/// names to standard PDF structure type names (e.g. {"Sidebar": "Aside"}).
#[derive(Debug, Deserialize)]
struct StructTreeDto {
    elements: Vec<StructElementDto>,
    #[serde(default)]
    role_map: std::collections::HashMap<String, String>,
}

fn build_element(dto: &StructElementDto) -> StructureElement {
    let mut elem = match StandardStructureType::from_pdf_name(&dto.type_name) {
        Some(std_type) => StructureElement::new(std_type),
        None => StructureElement::new_custom(dto.type_name.clone()),
    };
    if let Some(id) = &dto.id {
        elem = elem.with_id(id.clone());
    }
    if let Some(lang) = &dto.lang {
        elem = elem.with_language(lang.clone());
    }
    if let Some(alt) = &dto.alt_text {
        elem = elem.with_alt_text(alt.clone());
    }
    if let Some(actual) = &dto.actual_text {
        elem = elem.with_actual_text(actual.clone());
    }
    if let Some(title) = &dto.title {
        elem = elem.with_title(title.clone());
    }
    for m in &dto.mcids {
        elem.add_mcid(m.page, m.mcid);
    }
    elem
}

fn build_struct_tree(dto: &StructTreeDto) -> Result<StructTree, String> {
    if dto.elements.is_empty() {
        return Err("structure tree must have at least one element".to_string());
    }

    let mut tree = StructTree::new();
    // Maps a DTO element index to the index assigned inside the StructTree.
    let mut tree_index = vec![usize::MAX; dto.elements.len()];

    for (i, el) in dto.elements.iter().enumerate() {
        let element = build_element(el);
        match el.parent {
            None => {
                if i != 0 {
                    return Err(format!(
                        "root element (parent=null) must be first; found at index {i}"
                    ));
                }
                tree_index[i] = tree.set_root(element);
            }
            Some(parent_dto_idx) => {
                if parent_dto_idx >= i {
                    return Err(format!(
                        "element {i} references parent {parent_dto_idx} that does not precede it"
                    ));
                }
                let parent_tree_idx = tree_index[parent_dto_idx];
                let child_idx = tree
                    .add_child(parent_tree_idx, element)
                    .map_err(|e| format!("add_child failed for element {i}: {e}"))?;
                tree_index[i] = child_idx;
            }
        }
    }

    for (custom, standard) in &dto.role_map {
        match StandardStructureType::from_pdf_name(standard) {
            Some(std_type) => tree.role_map.add_mapping(custom.clone(), std_type),
            None => {
                return Err(format!(
                    "role_map maps '{custom}' to unknown standard type '{standard}'"
                ))
            }
        }
    }

    Ok(tree)
}

/// DOC-019 — Attach a logical structure tree (Tagged PDF) to a document from a
/// JSON description. On the next serialization the writer emits
/// `/StructTreeRoot`, `/MarkInfo <</Marked true>>` and the structure-element
/// dictionaries.
///
/// JSON shape:
/// ```json
/// {
///   "elements": [
///     { "type": "Document", "parent": null },
///     { "type": "P", "parent": 0, "lang": "en-US", "alt_text": "...",
///       "mcids": [ { "page": 0, "mcid": 0 } ] }
///   ],
///   "role_map": { "Sidebar": "Aside" }
/// }
/// ```
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `json` must be a valid non-null, null-terminated UTF-8 C string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_set_struct_tree_json(
    handle: *mut DocumentHandle,
    json: *const c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if handle.is_null() || json.is_null() {
            set_last_error("Null pointer provided to oxidize_document_set_struct_tree_json");
            return ErrorCode::NullPointer as c_int;
        }
        let json_str = match CStr::from_ptr(json).to_str() {
            Ok(s) => s,
            Err(_) => {
                set_last_error("Invalid UTF-8 in structure-tree JSON");
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };
        let dto: StructTreeDto = match serde_json::from_str(json_str) {
            Ok(d) => d,
            Err(e) => {
                set_last_error(format!("Failed to parse structure-tree JSON: {e}"));
                return ErrorCode::SerializationError as c_int;
            }
        };
        let tree = match build_struct_tree(&dto) {
            Ok(t) => t,
            Err(e) => {
                set_last_error(format!("Invalid structure tree: {e}"));
                return ErrorCode::InvalidArgument as c_int;
            }
        };
        (*handle).inner.set_struct_tree(tree);
        ErrorCode::Success as c_int
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::document::{oxidize_document_create, oxidize_document_free};
    use std::ffi::CString;

    #[test]
    fn set_struct_tree_emits_struct_tree_root_and_elements() {
        unsafe {
            let doc = oxidize_document_create();

            // A page tagged with one marked-content sequence (MCID 0).
            let mut page = oxidize_pdf::Page::a4();
            let mcid = page.begin_marked_content("P").unwrap();
            page.text()
                .set_font(oxidize_pdf::text::Font::Helvetica, 12.0)
                .at(72.0, 700.0)
                .write("TAGGED")
                .unwrap();
            page.end_marked_content().unwrap();
            assert_eq!(mcid, 0);
            (*doc).inner.add_page(page);

            let json = CString::new(
                r#"{
                    "elements": [
                        { "type": "Document", "parent": null },
                        { "type": "P", "parent": 0, "lang": "en-US",
                          "actual_text": "TAGGED",
                          "mcids": [ { "page": 0, "mcid": 0 } ] }
                    ]
                }"#,
            )
            .unwrap();
            assert_eq!(oxidize_document_set_struct_tree_json(doc, json.as_ptr()), 0);

            (*doc).inner.set_compress(false);
            let bytes = (*doc).inner.to_bytes().unwrap();
            oxidize_document_free(doc);

            let content = String::from_utf8_lossy(&bytes);
            assert!(
                content.contains("/StructTreeRoot"),
                "catalog must reference /StructTreeRoot; content:\n{content}"
            );
            assert!(
                content.contains("/MarkInfo"),
                "must mark document as tagged"
            );
            assert!(
                content.contains("/Type /StructTreeRoot")
                    || content.contains("/Type/StructTreeRoot"),
                "must emit StructTreeRoot object"
            );
            assert!(
                content.contains("/StructElem"),
                "must emit structure elements"
            );
            assert!(
                content.contains("/S /P") || content.contains("/S/P"),
                "must emit the /P element"
            );
            assert!(
                content.contains("/MCID 0"),
                "must reference marked content MCID 0"
            );
        }
    }

    #[test]
    fn set_struct_tree_null_returns_error() {
        unsafe {
            let json = CString::new("{\"elements\":[]}").unwrap();
            assert_eq!(
                oxidize_document_set_struct_tree_json(std::ptr::null_mut(), json.as_ptr()),
                1
            );
        }
    }

    #[test]
    fn set_struct_tree_empty_elements_returns_invalid_argument() {
        unsafe {
            let doc = oxidize_document_create();
            let json = CString::new("{\"elements\":[]}").unwrap();
            assert_eq!(oxidize_document_set_struct_tree_json(doc, json.as_ptr()), 9);
            oxidize_document_free(doc);
        }
    }

    #[test]
    fn set_struct_tree_child_before_parent_returns_invalid_argument() {
        unsafe {
            let doc = oxidize_document_create();
            // Element 0 is the root, element 1 references parent 2 (after it).
            let json = CString::new(
                r#"{"elements":[
                    {"type":"Document","parent":null},
                    {"type":"P","parent":2},
                    {"type":"P","parent":0}
                ]}"#,
            )
            .unwrap();
            assert_eq!(oxidize_document_set_struct_tree_json(doc, json.as_ptr()), 9);
            oxidize_document_free(doc);
        }
    }
}
