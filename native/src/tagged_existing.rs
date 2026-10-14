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
/// Inspect or edit tagged structure using bounded official upstream APIs.
///
/// # Safety
/// PDF buffer and NUL-terminated UTF-8 request must be readable. Output must be
/// writable and released with oxidize_free_string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_tagged_existing(
    bytes: *const u8,
    len: usize,
    request: *const c_char,
    output: *mut *mut c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if bytes.is_null() || request.is_null() || output.is_null() {
            set_last_error("Null tagged-PDF pointer");
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
            let options = TaggedPdfValidationOptions {
                limits: TaggedPdfLimits {
                    max_objects: r.limits.max_objects,
                    max_depth: r.limits.max_depth,
                    max_entries: r.limits.max_entries,
                    max_decoded_content_bytes: r.limits.max_decoded_content_bytes,
                },
            };
            let pdf = std::slice::from_raw_parts(bytes, len);
            if r.action == "inspect" {
                if !r.mutations.is_empty() {
                    return Err("Inspection does not accept mutations".into());
                }
                return validate_tagged_pdf(pdf, &options)
                    .map(report)
                    .map_err(|e| e.to_string());
            }
            let mutations = r
                .mutations
                .into_iter()
                .map(Mutation::core)
                .collect::<Result<Vec<_>, _>>()?;
            let editor = IncrementalTaggedPdfEditor::new(pdf).with_validation_options(options);
            match r.action.as_str() {
                "plan"=>editor.plan(&mutations).map(plan).map_err(|e|e.to_string()),
                "apply"=>editor.apply(&mutations).map(|r|json!({"pdf_bytes":base64::engine::general_purpose::STANDARD.encode(r.pdf_bytes),"plan":plan(r.plan),"validation_before":report(r.validation_before),"validation_after":report(r.validation_after)})).map_err(|e|e.to_string()),
                _=>Err("Unknown tagged-PDF action".into()),
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
