//! Inspection, dry-run and incremental editing of existing tagged PDFs.
use crate::pdf_value::{dictionary_json, object_json, PdfValue};
use crate::{clear_last_error, set_last_error, ErrorCode};
use base64::Engine as _;
use oxidize_pdf::parser::objects::PdfDictionary;
use oxidize_pdf::verification::tagged_pdf::*;
use oxidize_pdf::writer::{IncrementalTaggedPdfEditor, TaggedPdfEditPlan, TaggedPdfMutation};
use serde::Deserialize;
use serde_json::{json, Value};
use std::collections::BTreeMap;
use std::ffi::{CStr, CString};
use std::os::raw::{c_char, c_int};

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Limits {
    max_objects: usize,
    max_depth: usize,
    max_entries: usize,
    max_decoded_content_bytes: usize,
}
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Request {
    action: String,
    limits: Limits,
    #[serde(default)]
    mutations: Vec<Mutation>,
    #[serde(default)]
    preparation: Option<Preparation>,
}
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Preparation {
    recover_indexes: bool,
    document_language: Option<String>,
}
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Id {
    object_number: u32,
    generation: u16,
}
impl Id {
    fn core(self) -> TaggedPdfObjectRef {
        TaggedPdfObjectRef {
            object_number: self.object_number,
            generation: self.generation,
        }
    }
}
#[derive(Deserialize)]
#[serde(tag = "operation", deny_unknown_fields)]
enum Mutation {
    CreateElement {
        parent: Id,
        structure_type: String,
        attributes: BTreeMap<String, PdfValue>,
        index: Option<usize>,
    },
    SetElementAttribute {
        element: Id,
        key: String,
        value: Option<PdfValue>,
    },
    ReparentElement {
        element: Id,
        new_parent: Id,
        index: Option<usize>,
    },
    AssociateMcid {
        element: Id,
        page: Id,
        mcid: u32,
    },
    SetParentTreeEntry {
        key: i64,
        value: Option<PdfValue>,
    },
}
impl Mutation {
    fn core(self) -> Result<TaggedPdfMutation, String> {
        Ok(match self {
            Self::CreateElement {
                parent,
                structure_type,
                attributes,
                index,
            } => {
                let mut dict = PdfDictionary::new();
                for (k, v) in attributes {
                    dict.insert(k, v.core()?);
                }
                TaggedPdfMutation::CreateElement {
                    parent: parent.core(),
                    structure_type,
                    attributes: dict,
                    index,
                }
            }
            Self::SetElementAttribute {
                element,
                key,
                value,
            } => TaggedPdfMutation::SetElementAttribute {
                element: element.core(),
                key,
                value: value.map(PdfValue::core).transpose()?,
            },
            Self::ReparentElement {
                element,
                new_parent,
                index,
            } => TaggedPdfMutation::ReparentElement {
                element: element.core(),
                new_parent: new_parent.core(),
                index,
            },
            Self::AssociateMcid {
                element,
                page,
                mcid,
            } => TaggedPdfMutation::AssociateMcid {
                element: element.core(),
                page: page.core(),
                mcid,
            },
            Self::SetParentTreeEntry { key, value } => TaggedPdfMutation::SetParentTreeEntry {
                key,
                value: value.map(PdfValue::core).transpose()?,
            },
        })
    }
}
fn id(v: TaggedPdfObjectRef) -> Value {
    json!({"object_number":v.object_number,"generation":v.generation})
}
fn report(r: TaggedPdfValidationReport) -> Value {
    json!({"tagged":r.tagged,"valid":r.valid,"structure_tree_root":r.structure_tree_root.map(id),
        "role_map":r.role_map,"class_names":r.class_names,
        "class_map":r.class_map.iter().map(|(k,v)|(k.clone(),object_json(v))).collect::<BTreeMap<_,_>>(),
        "artifacts":r.artifacts.into_iter().map(|a|json!({"context":id(a.context),"operation_index":a.operation_index,"subtype":a.subtype})).collect::<Vec<_>>(),
        "elements":r.elements.into_iter().map(|e|json!({"object":id(e.object),"structure_type":e.structure_type,"parent":e.parent.map(id),"child_count":e.child_count,"marked_content_count":e.marked_content_count,"language":e.language,"alternate_text":e.alternate_text,"actual_text":e.actual_text,"title":e.title,"dictionary_keys":e.dictionary_keys,"dictionary":dictionary_json(&e.dictionary)})).collect::<Vec<_>>(),
        "parent_tree_entries":r.parent_tree_entries,
        "parent_tree":r.parent_tree.iter().map(|(k,v)|json!({"key":k,"value":object_json(v)})).collect::<Vec<_>>(),
        "content_mcids":r.content_mcids.into_iter().map(|(k,v)|json!({"context":id(k),"mcids":v})).collect::<Vec<_>>(),
        "findings":r.findings.into_iter().map(|f|json!({"severity":format!("{:?}",f.severity),"code":format!("{:?}",f.code),"path":f.path,"message":f.message,"object":f.object.map(id)})).collect::<Vec<_>>()})
}
fn plan(p: TaggedPdfEditPlan) -> Value {
    json!({"changed_objects":p.changed_objects.into_iter().map(|o|json!({"object":id(o.object),"kind":format!("{:?}",o.kind)})).collect::<Vec<_>>(),
        "mutations":p.mutations.into_iter().map(|m|match m {
            TaggedPdfMutation::CreateElement{parent,structure_type,attributes,index}=>json!({"operation":"CreateElement","parent":id(parent),"structure_type":structure_type,"attributes":dictionary_json(&attributes),"index":index}),
            TaggedPdfMutation::SetElementAttribute{element,key,value}=>json!({"operation":"SetElementAttribute","element":id(element),"key":key,"value":value.as_ref().map(object_json)}),
            TaggedPdfMutation::ReparentElement{element,new_parent,index}=>json!({"operation":"ReparentElement","element":id(element),"new_parent":id(new_parent),"index":index}),
            TaggedPdfMutation::AssociateMcid{element,page,mcid}=>json!({"operation":"AssociateMcid","element":id(element),"page":id(page),"mcid":mcid}),
            TaggedPdfMutation::SetParentTreeEntry{key,value}=>json!({"operation":"SetParentTreeEntry","key":key,"value":value.as_ref().map(object_json)}),
        }).collect::<Vec<_>>()})
}
type Failure = (ErrorCode, String);
fn invalid(message: &str) -> Failure {
    (ErrorCode::InvalidArgument, message.into())
}
fn core_error(error: oxidize_pdf::PdfError) -> Failure {
    use oxidize_pdf::PdfError;
    let code = match &error {
        PdfError::PermissionDenied(_) | PdfError::EncryptionError(_) => ErrorCode::PermissionError,
        PdfError::Io(_) => ErrorCode::IoError,
        PdfError::ParseError(_) | PdfError::InvalidHeader | PdfError::InvalidFormat(_) => {
            ErrorCode::PdfParseError
        }
        PdfError::SerializationError(_) | PdfError::EncodingError(_) => {
            ErrorCode::SerializationError
        }
        _ => ErrorCode::InvalidArgument,
    };
    (code, error.to_string())
}
fn edit_result(r: oxidize_pdf::writer::TaggedPdfMutationReport) -> Value {
    json!({"pdf_bytes":base64::engine::general_purpose::STANDARD.encode(r.pdf_bytes),
        "plan":plan(r.plan),"validation_before":report(r.validation_before),"validation_after":report(r.validation_after)})
}
fn execute(pdf: &[u8], r: Request) -> Result<Value, Failure> {
    let options = TaggedPdfValidationOptions {
        limits: TaggedPdfLimits {
            max_objects: r.limits.max_objects,
            max_depth: r.limits.max_depth,
            max_entries: r.limits.max_entries,
            max_decoded_content_bytes: r.limits.max_decoded_content_bytes,
        },
    };
    let prepared = matches!(r.action.as_str(), "plan_prepared" | "apply_prepared");
    if !prepared && r.preparation.is_some() {
        return Err(invalid(
            "Preparation is only accepted by prepared operations",
        ));
    }
    if matches!(r.action.as_str(), "inspect" | "preflight") && !r.mutations.is_empty() {
        return Err(invalid("Inspection/preflight does not accept mutations"));
    }
    if r.action == "inspect" {
        return validate_tagged_pdf(pdf, &options)
            .map(report)
            .map_err(core_error);
    }
    let mut editor = IncrementalTaggedPdfEditor::new(pdf).with_validation_options(options);
    if r.action == "preflight" {
        return editor.preflight().map(report).map_err(core_error);
    }
    if prepared {
        let preparation = r
            .preparation
            .ok_or_else(|| invalid("Explicit preparation is required"))?;
        if !preparation.recover_indexes && preparation.document_language.is_none() {
            return Err(invalid(
                "Select index recovery or document language preparation",
            ));
        }
        if preparation.recover_indexes {
            editor = editor.with_recovered_indexes().map_err(core_error)?;
        }
        if let Some(language) = preparation.document_language {
            editor = editor
                .with_document_language(&language)
                .map_err(core_error)?;
        }
    }
    let mutations = r
        .mutations
        .into_iter()
        .map(Mutation::core)
        .collect::<Result<Vec<_>, _>>()
        .map_err(|e| invalid(&e))?;
    let preparation_objects = editor
        .preparation_objects()
        .iter()
        .copied()
        .map(id)
        .collect::<Vec<_>>();
    match r.action.as_str() {
        "plan" => editor.plan(&mutations).map(plan).map_err(core_error),
        "apply" => editor
            .apply(&mutations)
            .map(edit_result)
            .map_err(core_error),
        "plan_prepared" => editor
            .plan(&mutations)
            .map(|p| json!({"preparation_objects":preparation_objects,"mutation_plan":plan(p)}))
            .map_err(core_error),
        "apply_prepared" => editor
            .apply(&mutations)
            .map(|r| json!({"preparation_objects":preparation_objects,"edit":edit_result(r)}))
            .map_err(core_error),
        _ => Err(invalid("Unknown tagged-PDF action")),
    }
}

/// Inspect, preflight or incrementally edit tagged structure. Prepared operations
/// return preparation identities separately from the subsequent mutation plan.
///
/// # Safety
/// PDF buffer of `len` bytes and NUL-terminated UTF-8 request must be readable.
/// Output must be writable and released with oxidize_free_string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_tagged_existing(
    bytes: *const u8,
    len: usize,
    request: *const c_char,
    output: *mut *mut c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if !output.is_null() {
            *output = std::ptr::null_mut();
        }
        if bytes.is_null() || request.is_null() || output.is_null() {
            set_last_error("Null tagged-PDF pointer");
            return ErrorCode::NullPointer as c_int;
        }
        if len == 0 || len > isize::MAX as usize {
            set_last_error("Invalid tagged-PDF buffer length");
            return ErrorCode::InvalidArgument as c_int;
        }
        let mut extended = false;
        let result = (|| -> Result<Value, Failure> {
            let r: Request = serde_json::from_str(
                CStr::from_ptr(request)
                    .to_str()
                    .map_err(|e| invalid(&e.to_string()))?,
            )
            .map_err(|e| invalid(&e.to_string()))?;
            extended = matches!(
                r.action.as_str(),
                "preflight" | "plan_prepared" | "apply_prepared"
            );
            execute(std::slice::from_raw_parts(bytes, len), r)
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
            Err((code, message)) => {
                set_last_error(message);
                // Preserve the legacy ABI's error classification for old actions.
                if extended {
                    code as c_int
                } else {
                    ErrorCode::InvalidArgument as c_int
                }
            }
        }
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::ptr;

    fn request(action: &str) -> Value {
        json!({"action":action,"limits":{"max_objects":100000,"max_depth":256,
            "max_entries":1000000,"max_decoded_content_bytes":268435456},"mutations":[]})
    }
    fn call(json: &str) -> (c_int, bool) {
        let mut document = oxidize_pdf::Document::new();
        document.add_page(oxidize_pdf::Page::a4());
        let bytes = document.to_bytes().unwrap();
        let json = CString::new(json).unwrap();
        let mut output = ptr::dangling_mut();
        unsafe {
            let code =
                oxidize_tagged_existing(bytes.as_ptr(), bytes.len(), json.as_ptr(), &mut output);
            let absent = output.is_null();
            if !absent {
                crate::oxidize_free_string(output);
            }
            (code, absent)
        }
    }
    #[test]
    fn preparation_is_explicit_and_unknown_or_conflicting_fields_fail_closed() {
        let mut cases = vec![request("apply_prepared"), request("plan_prepared")];
        for action in ["inspect", "plan", "apply", "preflight", "apply_prepared"] {
            let mut value = request(action);
            value["preparation"] = json!({"recover_indexes":false,"document_language":null});
            cases.push(value);
        }
        let mut unknown = request("apply_prepared");
        unknown["preparation"] = json!({"recover_indexes":true,"ignore_permissions":true});
        cases.push(unknown);
        let mut mutation = request("preflight");
        mutation["mutations"] = json!([{"operation":"SetParentTreeEntry","key":0,"value":null}]);
        cases.push(mutation);
        for value in cases {
            let (code, absent) = call(&value.to_string());
            assert_eq!(code, ErrorCode::InvalidArgument as c_int, "{value}");
            assert!(absent, "No output may escape a failed operation: {value}");
        }
        assert_eq!(call("{"), (ErrorCode::InvalidArgument as c_int, true));
        // Rejection never poisons subsequent legacy calls.
        assert_eq!(call(&request("inspect").to_string()), (0, false));
    }
    #[test]
    fn language_value_and_unrelated_catalog_data_survive_real_ffi() {
        let objects = [
            "<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 3 0 R /Private (keep) >>",
            "<< /Type /Pages /Kids [] /Count 0 >>",
            "<< /Type /StructTreeRoot /K 4 0 R /ParentTree 5 0 R >>",
            "<< /Type /StructElem /S /Document /P 3 0 R /K [] >>",
            "<< /Nums [] >>",
        ];
        let mut bytes = b"%PDF-1.7\n".to_vec();
        let mut offsets = Vec::new();
        for (index, object) in objects.iter().enumerate() {
            offsets.push(bytes.len());
            bytes.extend_from_slice(format!("{} 0 obj\n{object}\nendobj\n", index + 1).as_bytes());
        }
        let xref = bytes.len();
        bytes.extend_from_slice(b"xref\n0 6\n0000000000 65535 f \n");
        for offset in offsets {
            bytes.extend_from_slice(format!("{offset:010} 00000 n \n").as_bytes());
        }
        bytes.extend_from_slice(
            format!("trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n").as_bytes(),
        );
        let mut value = request("apply_prepared");
        value["preparation"] = json!({"recover_indexes":false,"document_language":"es-ES"});
        let json = CString::new(value.to_string()).unwrap();
        let mut output = ptr::null_mut();
        unsafe {
            let code =
                oxidize_tagged_existing(bytes.as_ptr(), bytes.len(), json.as_ptr(), &mut output);
            assert_eq!(code, 0);
            let response: Value =
                serde_json::from_slice(CStr::from_ptr(output).to_bytes()).unwrap();
            crate::oxidize_free_string(output);
            assert_eq!(
                response["preparation_objects"],
                json!([{"object_number":1,"generation":0}])
            );
            let result = base64::engine::general_purpose::STANDARD
                .decode(response["edit"]["pdf_bytes"].as_str().unwrap())
                .unwrap();
            assert!(result.starts_with(&bytes));
            let mut reader =
                oxidize_pdf::parser::PdfReader::new(std::io::Cursor::new(result)).unwrap();
            let catalog = reader.catalog().unwrap();
            assert_eq!(
                catalog.get("Lang").unwrap().as_string().unwrap().to_text(),
                "es-ES"
            );
            assert_eq!(
                catalog
                    .get("Private")
                    .unwrap()
                    .as_string()
                    .unwrap()
                    .to_text(),
                "keep"
            );
        }
    }

    #[test]
    fn ffi_null_utf8_and_length_failures_clear_writable_output() {
        unsafe {
            let mut output = ptr::dangling_mut();
            let bytes = [1u8];
            assert_eq!(
                oxidize_tagged_existing(ptr::null(), 1, c"{}".as_ptr(), &mut output),
                ErrorCode::NullPointer as c_int
            );
            assert!(output.is_null());
            for length in [0, usize::MAX] {
                output = ptr::dangling_mut();
                assert_eq!(
                    oxidize_tagged_existing(bytes.as_ptr(), length, c"{}".as_ptr(), &mut output),
                    ErrorCode::InvalidArgument as c_int
                );
                assert!(output.is_null());
            }
            let invalid = [255u8, 0];
            output = ptr::dangling_mut();
            assert_eq!(
                oxidize_tagged_existing(bytes.as_ptr(), 1, invalid.as_ptr().cast(), &mut output),
                ErrorCode::InvalidArgument as c_int
            );
            assert!(output.is_null());
        }
    }
}
