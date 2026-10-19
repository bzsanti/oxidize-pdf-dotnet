//! Reconstructing merge with explicit core Info metadata selection.
use crate::operations::{set_out_bytes, MergeInputJson, PageRangeJson};
use crate::{clear_last_error, set_last_error, ErrorCode};
use base64::Engine;
use oxidize_pdf::operations::merge::MetadataMode;
use oxidize_pdf::operations::{MergeInput, MergeOptions, OperationError, PdfMerger};
use serde::Deserialize;
use std::ffi::CStr;
use std::os::raw::{c_char, c_int};
use std::ptr;

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Request {
    inputs: Vec<MergeInputJson>,
    metadata: Metadata,
}

#[derive(Deserialize)]
#[serde(tag = "mode", deny_unknown_fields)]
enum Metadata {
    FromFirst {},
    FromDocument {
        index: usize,
    },
    Custom {
        title: Option<String>,
        author: Option<String>,
        subject: Option<String>,
        keywords: Option<String>,
    },
    None {},
}

type Failure = (ErrorCode, String);

fn merge(request: Request) -> Result<Vec<u8>, Failure> {
    let invalid = |message: &str| (ErrorCode::InvalidArgument, message.to_owned());
    if request.inputs.is_empty() {
        return Err(invalid("At least one PDF is required"));
    }
    let metadata_mode = match request.metadata {
        Metadata::FromFirst {} => MetadataMode::FromFirst,
        Metadata::FromDocument { index } => {
            if index >= request.inputs.len() {
                return Err(invalid("Metadata source index is outside the input list"));
            }
            MetadataMode::FromDocument(index)
        }
        Metadata::Custom {
            title,
            author,
            subject,
            keywords,
        } => MetadataMode::Custom {
            title,
            author,
            subject,
            keywords,
        },
        Metadata::None {} => MetadataMode::None,
    };
    // Core takes paths. A private RAII directory prevents shared-name collisions
    // and removes inputs on success, errors and unwinding through ffi_guard.
    let temp = tempfile::tempdir().map_err(|e| (ErrorCode::IoError, e.to_string()))?;
    let mut merger = PdfMerger::new(MergeOptions {
        metadata_mode,
        ..Default::default()
    });
    for (index, input) in request.inputs.into_iter().enumerate() {
        match &input.pages {
            Some(PageRangeJson::List { indices }) if indices.is_empty() => {
                return Err(invalid("Page index list cannot be empty"))
            }
            Some(PageRangeJson::Range { from, to }) if from > to => {
                return Err(invalid("Page range must be ascending"))
            }
            _ => {}
        }
        let bytes = base64::engine::general_purpose::STANDARD
            .decode(input.pdf)
            .map_err(|_| invalid("Invalid base64 PDF input"))?;
        if bytes.is_empty() {
            return Err(invalid("PDF input cannot be empty"));
        }
        let path = temp.path().join(format!("input-{index}.pdf"));
        std::fs::write(&path, bytes).map_err(|e| (ErrorCode::IoError, e.to_string()))?;
        merger.add_input(match input.pages {
            Some(pages) => MergeInput::with_pages(path, pages.into_core()),
            None => MergeInput::new(path),
        });
    }
    let mut document = merger.merge().map_err(|e| {
        let code = match &e {
            OperationError::PageIndexOutOfBounds(..)
            | OperationError::InvalidPageRange(_)
            | OperationError::NoPagesToProcess => ErrorCode::InvalidArgument,
            OperationError::Io(_) => ErrorCode::IoError,
            _ => ErrorCode::PdfParseError,
        };
        (code, e.to_string())
    })?;
    let bytes = document
        .to_bytes()
        .map_err(|e| (ErrorCode::SerializationError, e.to_string()))?;
    encode_info_text(bytes).map_err(|e| (ErrorCode::SerializationError, e))
}

// Core 5.4.2 writes Document metadata strings as raw UTF-8 in a PDF 1.x
// literal. Repair only our newly generated output, never arbitrary input PDFs.
// A single Info revision uses UTF-16BE for non-ASCII selected fields. The core's
// other Info values and original objects remain intact. Remove on core fix.
pub(crate) fn encode_info_text(bytes: Vec<u8>) -> Result<Vec<u8>, String> {
    encode_generated_info_text(bytes, &["Title", "Author", "Subject", "Keywords"])
}

// Only for Info created by the core writer, never preserved source dictionaries.
pub(crate) fn encode_generated_info_text(
    mut bytes: Vec<u8>,
    fields: &[&str],
) -> Result<Vec<u8>, String> {
    use oxidize_pdf::parser::PdfReader;
    use std::fmt::Write;
    use std::io::Cursor;

    let mut reader = PdfReader::new(Cursor::new(bytes.as_slice())).map_err(|e| e.to_string())?;
    let trailer = reader.trailer().clone();
    let (info_number, info_generation) = trailer.info().ok_or("Generated PDF has no Info")?;
    let info = reader
        .info()
        .map_err(|e| e.to_string())?
        .ok_or("Generated PDF has no Info dictionary")?;
    let mut changed = false;
    let mut dictionary = String::from("<<");
    let mut entries = info.0.iter().collect::<Vec<_>>();
    entries.sort_by(|a, b| a.0 .0.cmp(&b.0 .0));
    for (name, value) in entries {
        let raw = value
            .as_string()
            .ok_or("Unexpected generated Info value")?
            .as_bytes();
        let corrected;
        let value = if fields.contains(&name.0.as_str()) && !raw.is_ascii() {
            let text = std::str::from_utf8(raw).map_err(|e| e.to_string())?;
            corrected = [0xfe, 0xff]
                .into_iter()
                .chain(text.encode_utf16().flat_map(u16::to_be_bytes))
                .collect::<Vec<_>>();
            changed = true;
            corrected.as_slice()
        } else {
            raw
        };
        dictionary.push_str(" /");
        // Encode every name byte, preventing delimiters from entering PDF syntax.
        for byte in name.0.as_bytes() {
            write!(dictionary, "#{byte:02X}").map_err(|e| e.to_string())?;
        }
        dictionary.push_str(" <");
        for byte in value {
            write!(dictionary, "{byte:02X}").map_err(|e| e.to_string())?;
        }
        dictionary.push('>');
    }
    if !changed {
        return Ok(bytes);
    }
    // This helper is intentionally bounded to the unencrypted, classic-xref
    // output of Document::to_bytes in the pinned core. Fail if that changes.
    if trailer.is_encrypted()
        || trailer.id().is_some()
        || !bytes
            .get(trailer.xref_offset as usize..)
            .is_some_and(|tail| tail.starts_with(b"xref"))
    {
        return Err("Unexpected generated trailer for Info encoding repair".into());
    }
    let size = trailer.size().map_err(|e| e.to_string())?;
    let (root, root_generation) = trailer.root().map_err(|e| e.to_string())?;
    let previous = trailer.xref_offset;
    drop(reader);
    bytes.push(b'\n');
    let offset = bytes.len();
    if offset as u64 > 9_999_999_999 {
        return Err("Generated PDF exceeds classic xref offset limit".into());
    }
    bytes.extend_from_slice(
        format!("{info_number} {info_generation} obj\n{dictionary} >>\nendobj\n").as_bytes(),
    );
    let xref = bytes.len();
    bytes.extend_from_slice(format!("xref\n{info_number} 1\n{offset:010} {info_generation:05} n \ntrailer\n<< /Size {size} /Root {root} {root_generation} R /Info {info_number} {info_generation} R /Prev {previous} >>\nstartxref\n{xref}\n%%EOF\n").as_bytes());
    Ok(bytes)
}

/// Merge page selections and copy only the four core-supported Info fields.
/// The JSON request owns no native handles. Successful output must be freed
/// exactly once with `oxidize_free_bytes`; writable outputs are reset on failure.
///
/// # Safety
/// `request_json` must point to a readable NUL-terminated UTF-8 string; output
/// pointers must be writable, properly aligned and non-overlapping. Null pointers
/// are rejected. All memory must remain valid for the duration of the call.
#[no_mangle]
pub unsafe extern "C" fn oxidize_merge_pdfs_with_metadata(
    request_json: *const c_char,
    out_bytes: *mut *mut u8,
    out_len: *mut usize,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if !out_bytes.is_null() {
            *out_bytes = ptr::null_mut();
        }
        if !out_len.is_null() {
            *out_len = 0;
        }
        let result = (|| {
            if request_json.is_null() || out_bytes.is_null() || out_len.is_null() {
                return Err((ErrorCode::NullPointer, "Null merge pointer".into()));
            }
            let json = CStr::from_ptr(request_json)
                .to_str()
                .map_err(|e| (ErrorCode::InvalidUtf8, e.to_string()))?;
            let request = serde_json::from_str(json).map_err(|e| {
                (
                    ErrorCode::InvalidArgument,
                    format!("Invalid merge request: {e}"),
                )
            })?;
            merge(request)
        })();
        match result {
            Ok(bytes) => {
                set_out_bytes(bytes, out_bytes, out_len);
                ErrorCode::Success as c_int
            }
            Err((code, message)) => {
                set_last_error(message);
                code as c_int
            }
        }
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::ffi::CString;

    #[test]
    fn malformed_requests_reset_outputs_and_return_typed_errors() {
        let invalid = [
            "{}",
            r#"{"inputs":[],"metadata":{"mode":"FromFirst"}}"#,
            r#"{"inputs":[{"pdf":"JVBERg=="}],"metadata":{"mode":"FromDocument","index":1}}"#,
            r#"{"inputs":[{"pdf":"JVBERg=="}],"metadata":{"mode":"FromDocument","index":-1}}"#,
            r#"{"inputs":[{"pdf":"JVBERg=="}],"metadata":{"mode":"Unknown"}}"#,
            r#"{"inputs":[{"pdf":"JVBERg=="}],"metadata":{"mode":"None","index":0}}"#,
            r#"{"inputs":[{"pdf":""}],"metadata":{"mode":"None"}}"#,
            r#"{"inputs":[{"pdf":"bad base64"}],"metadata":{"mode":"None"}}"#,
            r#"{"inputs":[{"pdf":"JVBERg==","pages":{"kind":"List","indices":[]}}],"metadata":{"mode":"None"}}"#,
            r#"{"inputs":[{"pdf":"JVBERg==","pages":{"kind":"Range","from":2,"to":1}}],"metadata":{"mode":"None"}}"#,
        ];
        for request in invalid {
            let request = CString::new(request).unwrap();
            let mut bytes = ptr::dangling_mut::<u8>();
            let mut len = usize::MAX;
            let code =
                unsafe { oxidize_merge_pdfs_with_metadata(request.as_ptr(), &mut bytes, &mut len) };
            assert_eq!(
                code,
                ErrorCode::InvalidArgument as c_int,
                "request: {request:?}"
            );
            assert!(bytes.is_null());
            assert_eq!(len, 0);
        }
    }

    #[test]
    fn null_and_invalid_utf8_reset_each_writable_output() {
        unsafe {
            let mut bytes = ptr::dangling_mut::<u8>();
            let mut len = usize::MAX;
            assert_eq!(
                oxidize_merge_pdfs_with_metadata(ptr::null(), &mut bytes, ptr::null_mut()),
                ErrorCode::NullPointer as c_int
            );
            assert!(bytes.is_null());
            assert_eq!(
                oxidize_merge_pdfs_with_metadata(ptr::null(), ptr::null_mut(), &mut len),
                ErrorCode::NullPointer as c_int
            );
            assert_eq!(len, 0);
            let utf8 = [255u8, 0];
            assert_eq!(
                oxidize_merge_pdfs_with_metadata(utf8.as_ptr().cast(), &mut bytes, &mut len),
                ErrorCode::InvalidUtf8 as c_int
            );
            assert!(bytes.is_null());
            assert_eq!(len, 0);
        }
    }
}
