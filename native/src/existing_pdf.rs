//! Policy-driven existing-document operations using private temporary files.
use crate::{clear_last_error, set_last_error, ErrorCode};
use base64::Engine as _;
use oxidize_pdf::operations::{self as ops, existing_document as existing, *};
use serde::Deserialize;
use serde_json::{json, Value};
use std::ffi::{CStr, CString};
use std::os::raw::{c_char, c_int};
use std::path::PathBuf;

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Input {
    pdf: String,
    pages: Option<Vec<usize>>,
}
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Request {
    inputs: Vec<Input>,
    plan_only: bool,
    action: Action,
}
#[derive(Deserialize)]
enum Policy {
    PreserveBase,
    PreserveBaseWithFirstInputPageLabels,
    Reconstruct,
    ReconstructWithFirstInputMetadata,
}
impl Policy {
    fn core(self) -> ExistingDocumentPolicy {
        match self {
            Self::PreserveBase => ExistingDocumentPolicy::preserve_base(),
            Self::PreserveBaseWithFirstInputPageLabels => ExistingDocumentPolicy::preserve_base()
                .with_page_labels(DocumentStructurePolicy::FirstInputWins),
            Self::Reconstruct => ExistingDocumentPolicy::reconstruct(),
            Self::ReconstructWithFirstInputMetadata => {
                ExistingDocumentPolicy::reconstruct_with_metadata_from_first()
            }
        }
    }
}
#[derive(Deserialize)]
#[serde(tag = "operation", rename_all = "snake_case", deny_unknown_fields)]
enum Action {
    Merge {
        policy: Policy,
    },
    Extract {
        pages: Vec<usize>,
        policy: Policy,
    },
    Split {
        groups: Vec<Vec<usize>>,
        policy: Policy,
    },
    Mutate {
        mutations: Vec<Mutation>,
    },
    Reorder {
        order: Vec<usize>,
    },
}
#[derive(Deserialize)]
#[serde(tag = "operation", rename_all = "snake_case", deny_unknown_fields)]
enum Mutation {
    Move {
        from: usize,
        to: usize,
    },
    Rotate {
        page: usize,
        degrees: i32,
    },
    Delete {
        page: usize,
    },
    Duplicate {
        page: usize,
        at: usize,
    },
    Insert {
        source_input: usize,
        page: usize,
        at: usize,
    },
}
fn mutation_report(r: &PageMutationReport) -> Value {
    let refs = |v: &[(u32, u16)]| {
        v.iter()
            .map(|(n, g)| json!({"object_number": n, "generation_number": g}))
            .collect::<Vec<_>>()
    };
    json!({"page_count": r.page_count, "replaced_objects": refs(&r.replaced_objects),
        "added_objects": refs(&r.added_objects), "unreachable_objects": refs(&r.unreachable_objects)})
}
fn report(r: SemanticPreservationReport, paths: &[PathBuf]) -> Result<Value, String> {
    let mutation = match &r.plan {
        ExistingDocumentExecutionPlan::Incremental(p) => Some(mutation_report(p)),
        _ => None,
    };
    let inputs = r.inputs.into_iter().map(|input| {
        let index = paths.iter().position(|path| *path==input.path).ok_or("Unmapped semantic report input")?;
        Ok(json!({"input_index":index, "role":format!("{:?}",input.role), "selected_pages":input.selected_pages,
            "structures": input.structures.iter().map(|s| json!({"structure":format!("{:?}",s.structure), "disposition":format!("{:?}",s.disposition)})).collect::<Vec<_>>() }))
    }).collect::<Result<Vec<_>,String>>()?;
    Ok(
        json!({"engine":format!("{:?}",r.engine), "inputs":inputs, "page_count":r.plan.page_count(), "mutation":mutation}),
    )
}
fn run(request: Request) -> Result<Value, String> {
    if request.inputs.is_empty() {
        return Err("At least one input PDF is required".into());
    }
    let temp = tempfile::tempdir().map_err(|e| e.to_string())?;
    let mut paths = Vec::new();
    for (index, input) in request.inputs.iter().enumerate() {
        let bytes = base64::engine::general_purpose::STANDARD
            .decode(&input.pdf)
            .map_err(|e| e.to_string())?;
        if bytes.is_empty() {
            return Err("PDF data must not be empty".into());
        }
        let path = temp.path().join(format!("input-{index}.pdf"));
        std::fs::write(&path, bytes).map_err(|e| e.to_string())?;
        paths.push(path);
    }
    let output = temp.path().join("output.pdf");
    let mut outputs = Vec::new();
    let mut reports = Vec::new();
    let mut mutation = None;
    match request.action {
        Action::Merge { policy } => {
            let inputs = paths
                .iter()
                .zip(&request.inputs)
                .map(|(path, input)| ExistingDocumentMergeInput {
                    path: path.clone(),
                    pages: input.pages.clone().map(PageRange::List),
                })
                .collect::<Vec<_>>();
            let r = if request.plan_only {
                existing::plan_merge_pdfs(&inputs, policy.core())
            } else {
                existing::merge_pdfs(&inputs, &output, policy.core())
            }
            .map_err(|e| e.to_string())?;
            reports.push(report(r, &paths)?);
            if !request.plan_only {
                outputs.push(output);
            }
        }
        Action::Extract { pages, policy } => {
            if paths.len() != 1 {
                return Err("Extraction takes exactly one input".into());
            }
            let r = if request.plan_only {
                existing::plan_extract_pdf_pages(&paths[0], &pages, policy.core())
            } else {
                existing::extract_pdf_pages(&paths[0], &output, &pages, policy.core())
            }
            .map_err(|e| e.to_string())?;
            reports.push(report(r, &paths)?);
            if !request.plan_only {
                outputs.push(output);
            }
        }
        Action::Split { groups, policy } => {
            if paths.len() != 1 {
                return Err("Splitting takes exactly one input".into());
            }
            let ranges = groups.into_iter().map(PageRange::List).collect::<Vec<_>>();
            let targets = (0..ranges.len())
                .map(|i| temp.path().join(format!("split-{i}.pdf")))
                .collect::<Vec<_>>();
            let r = if request.plan_only {
                existing::plan_split_pdf(&paths[0], &ranges, policy.core())
            } else {
                existing::split_pdf(&paths[0], &ranges, &targets, policy.core())
            }
            .map_err(|e| e.to_string())?;
            reports = r
                .into_iter()
                .map(|r| report(r, &paths))
                .collect::<Result<_, _>>()?;
            if !request.plan_only {
                outputs = targets;
            }
        }
        action => {
            let operations = match action {
                Action::Mutate { mutations } => {
                    if mutations.is_empty() {
                        return Err("At least one page mutation is required".into());
                    }
                    mutations
                        .into_iter()
                        .map(|m| {
                            Ok(match m {
                                Mutation::Move { from, to } => PageMutation::Move { from, to },
                                Mutation::Rotate { page, degrees } => {
                                    PageMutation::Rotate { page, degrees }
                                }
                                Mutation::Delete { page } => PageMutation::Delete { page },
                                Mutation::Duplicate { page, at } => {
                                    PageMutation::Duplicate { page, at }
                                }
                                Mutation::Insert {
                                    source_input,
                                    page,
                                    at,
                                } => PageMutation::Insert {
                                    source: paths
                                        .get(source_input)
                                        .ok_or("Invalid inserted-page source index")?
                                        .clone(),
                                    page,
                                    at,
                                },
                            })
                        })
                        .collect::<Result<Vec<_>, String>>()?
                }
                Action::Reorder { order } => {
                    if paths.len() != 1 {
                        return Err("Reordering takes exactly one input".into());
                    }
                    let reader = oxidize_pdf::parser::PdfReader::open(&paths[0])
                        .map_err(|e| e.to_string())?;
                    let count = oxidize_pdf::parser::PdfDocument::new(reader)
                        .page_count()
                        .map_err(|e| e.to_string())? as usize;
                    let mut sorted = order.clone();
                    sorted.sort_unstable();
                    if sorted != (0..count).collect::<Vec<_>>() {
                        return Err("Order must be an exact page permutation".into());
                    }
                    let mut current = (0..count).collect::<Vec<_>>();
                    let mut operations = Vec::new();
                    for (to, page) in order.into_iter().enumerate() {
                        let from = current
                            .iter()
                            .position(|p| *p == page)
                            .ok_or("Invalid permutation")?;
                        if from != to {
                            operations.push(PageMutation::Move { from, to });
                            current.remove(from);
                            current.insert(to, page);
                        }
                    }
                    if operations.is_empty() {
                        // Keep identity permutations valid while still running all
                        // preservation and permission checks in the core planner.
                        operations.push(PageMutation::Move { from: 0, to: 0 });
                    }
                    operations
                }
                _ => unreachable!(),
            };
            let batch = PageMutationBatch { operations };
            let r = if request.plan_only {
                ops::plan_pdf_page_mutations(&paths[0], &batch)
            } else {
                ops::mutate_pdf_pages_lossless(&paths[0], &output, &batch)
            }
            .map_err(|e| e.to_string())?;
            mutation = Some(mutation_report(&r));
            if !request.plan_only {
                outputs.push(output);
            }
        }
    }
    let outputs = outputs
        .into_iter()
        .map(|path| {
            std::fs::read(path)
                .map(|b| base64::engine::general_purpose::STANDARD.encode(b))
                .map_err(|e| e.to_string())
        })
        .collect::<Result<Vec<_>, _>>()?;
    Ok(json!({"outputs":outputs,"reports":reports,"mutation":mutation}))
}
/// Plan or execute structural operations on in-memory PDFs.
///
/// # Safety
/// `request_json` must be a NUL-terminated UTF-8 string; `out_json` must be
/// writable. Free returned JSON using `oxidize_free_string`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_existing_pdf_json(
    request_json: *const c_char,
    out_json: *mut *mut c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if request_json.is_null() || out_json.is_null() {
            set_last_error("Null pointer in existing PDF operation");
            return ErrorCode::NullPointer as c_int;
        }
        *out_json = std::ptr::null_mut();
        let result = (|| -> Result<Value, String> {
            let input = CStr::from_ptr(request_json)
                .to_str()
                .map_err(|e| e.to_string())?;
            run(serde_json::from_str(input).map_err(|e| e.to_string())?)
        })();
        match result {
            Ok(value) => match CString::new(value.to_string()) {
                Ok(value) => {
                    *out_json = value.into_raw();
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

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn rotation_updates_only_the_requested_page() {
        let mut doc = oxidize_pdf::Document::new();
        doc.add_page(oxidize_pdf::Page::new(200.0, 300.0));
        doc.add_page(oxidize_pdf::Page::new(200.0, 300.0));
        let base = doc.to_bytes().unwrap();
        let request = Request {
            inputs: vec![Input {
                pdf: base64::engine::general_purpose::STANDARD.encode(&base),
                pages: None,
            }],
            plan_only: false,
            action: Action::Mutate {
                mutations: vec![Mutation::Rotate {
                    page: 1,
                    degrees: 90,
                }],
            },
        };
        let result = run(request).unwrap();
        let bytes = base64::engine::general_purpose::STANDARD
            .decode(result["outputs"][0].as_str().unwrap())
            .unwrap();
        assert!(bytes.starts_with(&base));
        let reader = oxidize_pdf::parser::PdfReader::new(std::io::Cursor::new(bytes)).unwrap();
        let document = oxidize_pdf::parser::PdfDocument::new(reader);
        assert_eq!(document.get_page(0).unwrap().rotation, 0);
        assert_eq!(document.get_page(1).unwrap().rotation, 90);
    }
}
