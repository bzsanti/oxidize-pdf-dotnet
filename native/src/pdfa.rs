//! Expose the pinned core validator without implying independent certification.
use crate::{clear_last_error, set_last_error, ErrorCode};
use oxidize_pdf::parser::PdfReader;
use oxidize_pdf::pdfa::{PdfAError, PdfALevel, PdfAValidator, ValidationError, ValidationWarning};
use serde::Deserialize;
use serde_json::{json, Value};
use std::ffi::{CStr, CString};
use std::io::Cursor;
use std::os::raw::{c_char, c_int};
use std::ptr;

#[derive(Deserialize)]
#[serde(rename_all = "PascalCase", deny_unknown_fields)]
struct Options {
    level: u8,
    collect_all_errors: bool,
    password: Option<String>,
}
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Request {
    options: Options,
    parser_options: crate::text_extraction::ParserSettings,
}

fn finding(error: &ValidationError) -> Value {
    use ValidationError::*;
    let (kind, mut fields) = match error {
        EncryptionForbidden => ("EncryptionForbidden", json!({})),
        FontNotEmbedded { font_name } => ("FontNotEmbedded", json!({"FontName":font_name})),
        FontMissingToUnicode { font_name } => {
            ("FontMissingToUnicode", json!({"FontName":font_name}))
        }
        JavaScriptForbidden { location } => ("JavaScriptForbidden", json!({"Location":location})),
        XmpMetadataMissing => ("XmpMetadataMissing", json!({})),
        XmpMissingPdfAIdentifier => ("XmpMissingPdfAIdentifier", json!({})),
        XmpInvalidPdfAIdentifier { details } => {
            ("XmpInvalidPdfAIdentifier", json!({"Details":details}))
        }
        InvalidColorSpace {
            color_space,
            location,
        } => (
            "InvalidColorSpace",
            json!({"ColorSpace":color_space,"Location":location}),
        ),
        MissingOutputIntent => ("MissingOutputIntent", json!({})),
        TransparencyForbidden { location } => {
            ("TransparencyForbidden", json!({"Location":location}))
        }
        ExternalReferenceForbidden { reference_type } => (
            "ExternalReferenceForbidden",
            json!({"ReferenceType":reference_type}),
        ),
        LzwCompressionForbidden { object_id } => {
            ("LzwCompressionForbidden", json!({"ObjectId":object_id}))
        }
        IncompatiblePdfVersion { actual, required } => (
            "IncompatiblePdfVersion",
            json!({"Actual":actual,"Required":required}),
        ),
        EmbeddedFileForbidden => ("EmbeddedFileForbidden", json!({})),
        EmbeddedFileMissingMetadata {
            file_name,
            missing_field,
        } => (
            "EmbeddedFileMissingMetadata",
            json!({"FileName":file_name,"MissingField":missing_field}),
        ),
        ActionForbidden { action_type } => ("ActionForbidden", json!({"ActionType":action_type})),
    };
    fields["Kind"] = json!(kind);
    fields["Message"] = json!(error.to_string());
    fields
}

fn warning(warning: &ValidationWarning) -> Value {
    use ValidationWarning::*;
    let (kind, mut fields) = match warning {
        FontSubsetWarning { font_name, details } => (
            "FontSubsetWarning",
            json!({"FontName":font_name,"Details":details}),
        ),
        OptionalMetadataMissing { field } => ("OptionalMetadataMissing", json!({"Field":field})),
        ColorProfileWarning { details } => ("ColorProfileWarning", json!({"Details":details})),
        LargeFileWarning { size_bytes } => ("LargeFileWarning", json!({"SizeBytes":size_bytes})),
    };
    fields["Kind"] = json!(kind);
    fields["Message"] = json!(warning.to_string());
    fields
}

fn failure(kind: &str, message: impl ToString) -> Value {
    json!({"kind":kind,"message":message.to_string()})
}
fn operation_error(error: PdfAError) -> Value {
    match error {
        PdfAError::Validation(e) => {
            json!({"kind":"Validation","message":e.to_string(),"finding":finding(&e)})
        }
        PdfAError::XmpParseError(e) => failure("XmpParseError", e),
        PdfAError::InvalidLevel(e) => failure("InvalidLevel", e),
        PdfAError::ParseError(e) => failure("ParseError", e),
        PdfAError::IoError(e) => failure("IoError", e),
    }
}
const LEVELS: [PdfALevel; 8] = [
    PdfALevel::A1a,
    PdfALevel::A1b,
    PdfALevel::A2a,
    PdfALevel::A2b,
    PdfALevel::A2u,
    PdfALevel::A3a,
    PdfALevel::A3b,
    PdfALevel::A3u,
];

fn validate(bytes: &[u8], request: Request) -> Result<Value, Value> {
    let level = LEVELS
        .get(usize::from(request.options.level))
        .copied()
        .ok_or_else(|| failure("InvalidLevel", "Unsupported PDF/A level"))?;
    let parser = request
        .parser_options
        .core()
        .map_err(|e| failure("InvalidInput", e))?;
    let mut reader = PdfReader::new_with_options(Cursor::new(bytes), parser)
        .map_err(|e| failure("ParseError", e))?;
    if let Some(password) = &request.options.password {
        reader
            .unlock(password)
            .map_err(|e| failure("ParseError", e))?;
    }
    // Forward this setting faithfully. 5.4.2 stores it but does not consult it.
    let result = PdfAValidator::new(level)
        .collect_all_errors(request.options.collect_all_errors)
        .validate(&mut reader)
        .map_err(operation_error)?;
    Ok(
        json!({"Level": LEVELS.iter().position(|v| *v == result.level()),
        "IsValid":result.is_valid(), "Errors":result.errors().iter().map(finding).collect::<Vec<_>>(),
        "Warnings":result.warnings().iter().map(warning).collect::<Vec<_>>() }),
    )
}

/// Validate bytes against one core PDF/A level. Conformance findings are results;
/// parser/operation errors are typed error envelopes. Output is owned by caller.
///
/// # Safety
/// PDF must cover len readable bytes (null permitted only for zero length).
/// Request is NUL-terminated UTF-8. Output is writable, aligned and non-overlapping.
/// Free every non-null output once with oxidize_free_string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_pdfa_validate(
    pdf: *const u8,
    len: usize,
    request: *const c_char,
    output: *mut *mut c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if !output.is_null() {
            *output = ptr::null_mut();
        }
        if output.is_null() || request.is_null() || (pdf.is_null() && len != 0) {
            return ErrorCode::NullPointer as c_int;
        }
        if len > isize::MAX as usize {
            return ErrorCode::InvalidArgument as c_int;
        }
        let request = match CStr::from_ptr(request).to_str() {
            Ok(s) => s,
            Err(e) => {
                set_last_error(e.to_string());
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };
        let result = if len == 0 || len > 100 * 1024 * 1024 {
            Err(failure(
                "InvalidInput",
                "PDF must contain 1 byte to 100 MiB",
            ))
        } else {
            serde_json::from_str(request)
                .map_err(|e| failure("InvalidInput", e))
                .and_then(|request| validate(std::slice::from_raw_parts(pdf, len), request))
        };
        let envelope = match result {
            Ok(result) => json!({"result":result}),
            Err(error) => json!({"error":error}),
        };
        match CString::new(envelope.to_string()) {
            Ok(s) => {
                *output = s.into_raw();
                ErrorCode::Success as c_int
            }
            Err(e) => {
                set_last_error(e.to_string());
                ErrorCode::SerializationError as c_int
            }
        }
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn warning_payloads_preserve_unicode_and_u64_without_affecting_validity() {
        let warnings = [
            ValidationWarning::FontSubsetWarning {
                font_name: "日本語".into(),
                details: "subset".into(),
            },
            ValidationWarning::OptionalMetadataMissing {
                field: "Title".into(),
            },
            ValidationWarning::ColorProfileWarning {
                details: "profile".into(),
            },
            ValidationWarning::LargeFileWarning {
                size_bytes: u64::MAX,
            },
        ];
        let result = oxidize_pdf::pdfa::ValidationResult::with_errors_and_warnings(
            PdfALevel::A1b,
            vec![],
            warnings.to_vec(),
        );
        assert!(result.is_valid());
        let values: Vec<_> = result.warnings().iter().map(warning).collect();
        assert_eq!(values[0]["Kind"], "FontSubsetWarning");
        assert_eq!(values[0]["FontName"], "日本語");
        assert_eq!(values[0]["Details"], "subset");
        assert_eq!(values[1]["Field"], "Title");
        assert_eq!(values[2]["Details"], "profile");
        assert_eq!(values[3]["SizeBytes"].as_u64(), Some(u64::MAX));
        assert!(values
            .iter()
            .all(|v| !v["Message"].as_str().unwrap().is_empty()));
    }

    #[test]
    fn declared_but_unemitted_variants_keep_operation_and_finding_payloads() {
        let missing = finding(&ValidationError::EmbeddedFileMissingMetadata {
            file_name: "á.pdf".into(),
            missing_field: "AFRelationship".into(),
        });
        assert_eq!(missing["Kind"], "EmbeddedFileMissingMetadata");
        assert_eq!(missing["FileName"], "á.pdf");
        assert_eq!(missing["MissingField"], "AFRelationship");
        assert_eq!(
            finding(&ValidationError::ActionForbidden {
                action_type: "Movie".into()
            })["ActionType"],
            "Movie"
        );
        assert_eq!(
            finding(&ValidationError::MissingOutputIntent)["Kind"],
            "MissingOutputIntent"
        );
        let failed = operation_error(PdfAError::Validation(ValidationError::EncryptionForbidden));
        assert_eq!(failed["kind"], "Validation");
        assert_eq!(failed["finding"]["Kind"], "EncryptionForbidden");
        for (error, kind) in [
            (PdfAError::XmpParseError("xmp".into()), "XmpParseError"),
            (PdfAError::IoError("io".into()), "IoError"),
            (PdfAError::InvalidLevel("4".into()), "InvalidLevel"),
            (PdfAError::ParseError("parse".into()), "ParseError"),
        ] {
            let failure = operation_error(error);
            assert_eq!(failure["kind"], kind);
            assert!(!failure["message"].as_str().unwrap().is_empty());
        }
    }

    #[test]
    fn boundary_clears_outputs_and_rejects_null_utf8_and_impossible_lengths() {
        unsafe {
            let request = CString::new("{}").unwrap();
            let mut out = ptr::dangling_mut();
            assert_eq!(
                oxidize_pdfa_validate(ptr::null(), 1, request.as_ptr(), &mut out),
                ErrorCode::NullPointer as i32
            );
            assert!(out.is_null());
            assert_eq!(
                oxidize_pdfa_validate(ptr::dangling(), usize::MAX, request.as_ptr(), &mut out),
                ErrorCode::InvalidArgument as i32
            );
            assert!(out.is_null());
            let invalid = [255u8, 0];
            assert_eq!(
                oxidize_pdfa_validate(ptr::null(), 0, invalid.as_ptr().cast(), &mut out),
                ErrorCode::InvalidUtf8 as i32
            );
            assert!(out.is_null());
            for len in [0, 100 * 1024 * 1024 + 1] {
                assert_eq!(
                    oxidize_pdfa_validate(ptr::dangling(), len, request.as_ptr(), &mut out),
                    0
                );
                let result: Value =
                    serde_json::from_str(CStr::from_ptr(out).to_str().unwrap()).unwrap();
                assert_eq!(result["error"]["kind"], "InvalidInput");
                crate::oxidize_free_string(out);
            }
        }
    }
}
