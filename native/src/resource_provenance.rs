//! Bounded resource ownership inspection; resource contents are not decoded.
use crate::{clear_last_error, set_last_error, ErrorCode};
use oxidize_pdf::parser::objects::PdfDictionary;
use oxidize_pdf::parser::{ParsedPage, PdfDocument};
use serde::Serialize;
use std::collections::HashSet;
use std::ffi::CString;
use std::io::{Read, Seek};
use std::os::raw::{c_char, c_int};
use std::ptr;

#[derive(Clone, Copy, Serialize)]
pub(crate) struct ObjectId {
    object_number: u32,
    generation_number: u16,
}
impl From<(u32, u16)> for ObjectId {
    fn from((object_number, generation_number): (u32, u16)) -> Self {
        Self {
            object_number,
            generation_number,
        }
    }
}

/// The first declaring node owns the entire resource dictionary, even when empty.
pub(crate) struct ResourceSource {
    owner: Option<ObjectId>,
    dictionary_reference: Option<ObjectId>,
    depth: Option<usize>,
    pub(crate) dictionary: Option<PdfDictionary>,
}

pub(crate) fn find_resources<R: Read + Seek>(
    document: &PdfDocument<R>,
    page: &ParsedPage,
    max_depth: usize,
) -> Result<ResourceSource, String> {
    let mut node = page.dict.clone();
    let mut owner = page.obj_ref;
    let mut visited = HashSet::from([owner]);
    let mut depth = 0;
    loop {
        if let Some(resources) = node.get("Resources") {
            let reference = resources.as_reference().map(ObjectId::from);
            let resolved = document
                .resolve(resources)
                .map_err(|e| format!("Invalid Resources reference: {e}"))?;
            let dictionary = resolved
                .as_dict()
                .ok_or("Resources must resolve to a dictionary")?
                .clone();
            return Ok(ResourceSource {
                owner: Some(owner.into()),
                dictionary_reference: reference,
                depth: Some(depth),
                dictionary: Some(dictionary),
            });
        }
        let Some(parent) = node.get("Parent") else {
            return Ok(ResourceSource {
                owner: None,
                dictionary_reference: None,
                depth: None,
                dictionary: None,
            });
        };
        let reference = parent
            .as_reference()
            .ok_or("Parent must be an indirect reference")?;
        if !visited.insert(reference) {
            return Err("Cycle in resource ancestry".into());
        }
        if depth >= max_depth {
            return Err("Resource ancestor depth limit exceeded".into());
        }
        depth += 1;
        let resolved = document
            .get_object(reference.0, reference.1)
            .map_err(|e| format!("Invalid Parent reference: {e}"))?;
        node = resolved
            .as_dict()
            .ok_or("Parent must resolve to a dictionary")?
            .clone();
        owner = reference;
    }
}

#[derive(Serialize)]
struct ResourceEntry {
    category: String,
    name: String,
    reference: Option<ObjectId>,
}
#[derive(Default, Serialize)]
struct Summary {
    font_names: Vec<String>,
    has_xobjects: bool,
    resource_keys: Vec<String>,
}
#[derive(Serialize)]
struct Inspection {
    page_number: usize,
    page_reference: ObjectId,
    origin: &'static str,
    source_reference: Option<ObjectId>,
    resource_dictionary_reference: Option<ObjectId>,
    ancestor_depth: Option<usize>,
    resolved_resources: Summary,
    entries: Vec<ResourceEntry>,
}
struct Budget {
    entries: usize,
    name_bytes: usize,
}
impl Budget {
    fn name(&mut self, name: &str) -> Result<(), String> {
        self.name_bytes = self
            .name_bytes
            .checked_sub(name.len())
            .ok_or("Resource name byte limit exceeded")?;
        Ok(())
    }
    fn entry(&mut self) -> Result<(), String> {
        self.entries = self
            .entries
            .checked_sub(1)
            .ok_or("Resource entry limit exceeded")?;
        Ok(())
    }
}

fn inspect<R: Read + Seek>(
    document: &PdfDocument<R>,
    page_number: usize,
    max_depth: usize,
    max_entries: usize,
    max_name_bytes: usize,
) -> Result<Inspection, String> {
    let page = document
        .get_page((page_number - 1) as u32)
        .map_err(|e| e.to_string())?;
    let source = find_resources(document, &page, max_depth)?;
    let mut result = Inspection {
        page_number,
        page_reference: page.obj_ref.into(),
        origin: match source.depth {
            None => "None",
            Some(0) => "Direct",
            Some(_) => "Inherited",
        },
        source_reference: source.owner,
        resource_dictionary_reference: source.dictionary_reference,
        ancestor_depth: source.depth,
        resolved_resources: Summary::default(),
        entries: Vec::new(),
    };
    let Some(resources) = source.dictionary else {
        return Ok(result);
    };
    let mut budget = Budget {
        entries: max_entries,
        name_bytes: max_name_bytes,
    };
    // Check counts before allocating a sortable list. Names are charged before cloning.
    if resources.0.len() > budget.entries {
        return Err("Resource entry limit exceeded".into());
    }
    let mut categories: Vec<_> = resources.0.iter().collect();
    categories.sort_by_key(|(name, _)| name.as_str());
    for (category, value) in categories {
        let category = category.as_str();
        budget.entry()?;
        budget.name(category)?;
        result
            .resolved_resources
            .resource_keys
            .push(category.to_string());
        // ProcSet is an array, not a named-resource dictionary. Unknown keys are reported
        // but not interpreted, so inspection does not dereference arbitrary extensions.
        if !matches!(
            category,
            "Font" | "XObject" | "ExtGState" | "ColorSpace" | "Pattern" | "Shading" | "Properties"
        ) {
            continue;
        }
        let resolved = document
            .resolve(value)
            .map_err(|e| format!("Invalid {category} reference: {e}"))?;
        let dictionary = resolved
            .as_dict()
            .ok_or_else(|| format!("{category} must resolve to a dictionary"))?;
        if dictionary.0.len() > budget.entries {
            return Err("Resource entry limit exceeded".into());
        }
        let mut entries: Vec<_> = dictionary.0.iter().collect();
        entries.sort_by_key(|(name, _)| name.as_str());
        for (name, value) in entries {
            let name = name.as_str();
            budget.entry()?;
            budget.name(category)?;
            budget.name(name)?;
            if category == "Font" {
                budget.name(name)?;
                result.resolved_resources.font_names.push(name.to_string());
            }
            if category == "XObject" {
                result.resolved_resources.has_xobjects = true;
            }
            result.entries.push(ResourceEntry {
                category: category.into(),
                name: name.into(),
                reference: value.as_reference().map(ObjectId::from),
            });
        }
    }
    Ok(result)
}

/// Inspect resource provenance and a resolved inventory with one-based page indexing.
///
/// # Safety
/// `bytes` must point to `len` readable bytes and not alias writable `out_json`.
/// Output ownership transfers only on success; release it with `oxidize_free_string`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_get_page_resource_provenance(
    bytes: *const u8,
    len: usize,
    page_number: usize,
    max_depth: usize,
    max_entries: usize,
    max_name_bytes: usize,
    out_json: *mut *mut c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if out_json.is_null() {
            set_last_error("Null resource output pointer");
            return ErrorCode::NullPointer as c_int;
        }
        *out_json = ptr::null_mut();
        if bytes.is_null() {
            set_last_error("Null PDF pointer");
            return ErrorCode::NullPointer as c_int;
        }
        if len == 0
            || len > isize::MAX as usize
            || page_number == 0
            || u32::try_from(page_number - 1).is_err()
            || max_depth > 256
        {
            set_last_error("Invalid PDF length, page number or ancestor depth limit");
            return ErrorCode::InvalidArgument as c_int;
        }
        let result = (|| -> Result<CString, String> {
            let bytes = std::slice::from_raw_parts(bytes, len);
            let document = PdfDocument::new(crate::parser::open_lenient(bytes)?);
            let result = inspect(
                &document,
                page_number,
                max_depth,
                max_entries,
                max_name_bytes,
            )?;
            let json = serde_json::to_string(&result).map_err(|e| e.to_string())?;
            CString::new(json).map_err(|e| e.to_string())
        })();
        match result {
            Ok(json) => {
                *out_json = json.into_raw();
                ErrorCode::Success as c_int
            }
            Err(error) => {
                set_last_error(error);
                ErrorCode::PdfParseError as c_int
            }
        }
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn invalid_inputs_clear_output_before_returning_errors() {
        let bytes = b"invalid PDF";
        let mut output = ptr::dangling_mut::<c_char>();
        unsafe {
            assert_eq!(
                oxidize_get_page_resource_provenance(
                    bytes.as_ptr(),
                    bytes.len(),
                    1,
                    256,
                    10,
                    100,
                    ptr::null_mut()
                ),
                ErrorCode::NullPointer as c_int
            );
            assert_eq!(
                oxidize_get_page_resource_provenance(ptr::null(), 1, 1, 256, 10, 100, &mut output),
                ErrorCode::NullPointer as c_int
            );
            assert!(output.is_null());
            for (len, page, depth) in [
                (0, 1, 256),
                (usize::MAX, 1, 256),
                (bytes.len(), 0, 256),
                (bytes.len(), usize::MAX, 256),
                (bytes.len(), 1, 257),
            ] {
                output = ptr::dangling_mut::<c_char>();
                assert_eq!(
                    oxidize_get_page_resource_provenance(
                        bytes.as_ptr(),
                        len,
                        page,
                        depth,
                        10,
                        100,
                        &mut output
                    ),
                    ErrorCode::InvalidArgument as c_int
                );
                assert!(output.is_null());
            }
            assert_eq!(
                oxidize_get_page_resource_provenance(
                    bytes.as_ptr(),
                    bytes.len(),
                    1,
                    256,
                    10,
                    100,
                    &mut output
                ),
                ErrorCode::PdfParseError as c_int
            );
            assert!(output.is_null());
        }
    }
}
