//! Base-document incremental writing; the full-save ABI remains unchanged.
use crate::document::DocumentHandle;
use crate::{clear_last_error, set_last_error, ErrorCode};
use oxidize_pdf::parser::PdfReader;
use oxidize_pdf::writer::{IncrementalInfoPolicy, PdfWriter, WriterConfig};
use serde::Deserialize;
use std::ffi::CStr;
use std::io::Cursor;
use std::os::raw::{c_char, c_int};
use std::ptr;

type Failure = (ErrorCode, String);

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Request {
    mode: Mode,
    info_policy: InfoPolicy,
    use_xref_streams: bool,
    use_object_streams: bool,
    pdf_version: String,
    compress_streams: bool,
    incremental_update: bool,
}
#[derive(Deserialize)]
enum Mode {
    AppendPages,
    ReplaceLeadingPages,
}
#[derive(Deserialize)]
enum InfoPolicy {
    Preserve,
    Replace,
}

fn save(
    base: &[u8],
    document: &mut oxidize_pdf::Document,
    request: Request,
) -> Result<Vec<u8>, Failure> {
    if !request.incremental_update
        || request.use_xref_streams
        || request.use_object_streams
        || !matches!(
            request.pdf_version.as_str(),
            "1.0" | "1.1" | "1.2" | "1.3" | "1.4" | "1.5" | "1.6" | "1.7" | "2.0"
        )
    {
        return Err((ErrorCode::InvalidArgument, "Incremental writing requires incremental_update=true, classic output xrefs and a valid PDF version".into()));
    }
    let reader =
        PdfReader::new(Cursor::new(base)).map_err(|e| (ErrorCode::PdfParseError, e.to_string()))?;
    if reader.trailer().is_encrypted() {
        return Err((
            ErrorCode::EncryptionError,
            "Encrypted bases are unsupported for incremental writing".into(),
        ));
    }
    // Retain both original file identifiers. Only byte strings are valid here.
    let ids = match reader.trailer().id() {
        None => None,
        Some(value) => {
            let values = value.as_array().filter(|a| a.0.len() == 2).ok_or_else(|| {
                (
                    ErrorCode::PdfParseError,
                    "Base trailer ID must contain two strings".into(),
                )
            })?;
            let mut pair = Vec::new();
            for value in &values.0 {
                pair.push(
                    value
                        .as_string()
                        .ok_or_else(|| {
                            (
                                ErrorCode::PdfParseError,
                                "Base trailer ID must contain two strings".into(),
                            )
                        })?
                        .as_bytes()
                        .to_vec(),
                );
            }
            Some(pair)
        }
    };
    drop(reader);
    let temp = tempfile::tempdir().map_err(|e| (ErrorCode::IoError, e.to_string()))?;
    let path = temp.path().join("base.pdf");
    std::fs::write(&path, base).map_err(|e| (ErrorCode::IoError, e.to_string()))?;
    let replace_info = matches!(request.info_policy, InfoPolicy::Replace);
    let mut bytes = Vec::new();
    let mut writer = PdfWriter::with_config(
        &mut bytes,
        WriterConfig {
            use_xref_streams: request.use_xref_streams,
            use_object_streams: request.use_object_streams,
            pdf_version: request.pdf_version,
            compress_streams: request.compress_streams,
            incremental_update: request.incremental_update,
        },
    );
    writer.set_incremental_info_policy(if replace_info {
        IncrementalInfoPolicy::Replace
    } else {
        IncrementalInfoPolicy::Preserve
    });
    match request.mode {
        Mode::AppendPages => writer.write_incremental_update(&path, document),
        Mode::ReplaceLeadingPages => {
            writer.write_incremental_with_page_replacement(&path, document)
        }
    }
    .map_err(|e| (ErrorCode::SerializationError, e.to_string()))?;
    drop(writer);
    if replace_info {
        bytes = crate::merge_metadata::encode_generated_info_text(
            bytes,
            &[
                "Title", "Author", "Subject", "Keywords", "Creator", "Producer",
            ],
        )
        .map_err(|e| (ErrorCode::SerializationError, e))?;
    }
    if let Some(ids) = ids {
        restore_ids(&mut bytes, &ids).map_err(|e| (ErrorCode::SerializationError, e))?;
    }
    Ok(bytes)
}

// Only edit the last trailer emitted by our core/Info repair. No preceding
// object/xref offset changes, and the original prefix remains byte-for-byte intact.
fn restore_ids(bytes: &mut Vec<u8>, ids: &[Vec<u8>]) -> Result<(), String> {
    use std::fmt::Write;
    let reader = PdfReader::new(Cursor::new(bytes.as_slice())).map_err(|e| e.to_string())?;
    let xref = usize::try_from(reader.trailer().xref_offset).map_err(|e| e.to_string())?;
    drop(reader);
    let tail = bytes.get(xref..).ok_or("Generated xref outside output")?;
    if !tail.starts_with(b"xref") {
        return Err("Unexpected generated xref".into());
    }
    let trailer = tail
        .windows(8)
        .position(|w| w == b"trailer\n")
        .ok_or("Missing generated trailer")?
        + 8;
    let closing = tail[trailer..]
        .windows(2)
        .position(|w| w == b">>")
        .ok_or("Missing generated trailer end")?;
    let mut id = String::from(" /ID [");
    for value in ids {
        id.push('<');
        for byte in value {
            write!(id, "{byte:02X}").map_err(|e| e.to_string())?;
        }
        id.push_str("> ");
    }
    id.push_str("] ");
    bytes.splice(
        xref + trailer + closing..xref + trailer + closing,
        id.bytes(),
    );
    Ok(())
}

/// Append a revision using a live document and immutable base bytes.
/// Successful output is owned by the caller and freed with oxidize_free_bytes.
/// All writable outputs are cleared before validation.
///
/// # Safety
/// The handle must be live and exclusively borrowed; base must be readable for
/// base_len bytes, request must be NUL-terminated UTF-8, and outputs must be valid,
/// aligned, writable and non-overlapping. All memory must survive the call.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_save_incremental(
    handle: *mut DocumentHandle,
    base: *const u8,
    base_len: usize,
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
            if handle.is_null()
                || base.is_null()
                || request_json.is_null()
                || out_bytes.is_null()
                || out_len.is_null()
            {
                return Err((
                    ErrorCode::NullPointer,
                    "Null incremental writer pointer".into(),
                ));
            }
            if base_len == 0 || base_len > isize::MAX as usize {
                return Err((ErrorCode::InvalidArgument, "Invalid base length".into()));
            }
            let json = CStr::from_ptr(request_json)
                .to_str()
                .map_err(|e| (ErrorCode::InvalidUtf8, e.to_string()))?;
            let request = serde_json::from_str(json).map_err(|e| {
                (
                    ErrorCode::InvalidArgument,
                    format!("Invalid incremental request: {e}"),
                )
            })?;
            save(
                std::slice::from_raw_parts(base, base_len),
                &mut (*handle).inner,
                request,
            )
        })();
        match result {
            Ok(bytes) => {
                crate::operations::set_out_bytes(bytes, out_bytes, out_len);
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

    fn request() -> serde_json::Value {
        serde_json::json!({"mode":"AppendPages", "info_policy":"Preserve",
            "use_xref_streams":false,"use_object_streams":false,
            "pdf_version":"1.4","compress_streams":true,"incremental_update":true})
    }

    #[test]
    fn invalid_configurations_and_json_reset_outputs() {
        let mut doc = DocumentHandle {
            inner: oxidize_pdf::Document::new(),
        };
        let mut bad = vec!["{}".to_owned(), "[".to_owned()];
        for (key, value) in [
            ("mode", serde_json::json!("Unknown")),
            ("info_policy", serde_json::json!("Unknown")),
            ("use_xref_streams", serde_json::json!(true)),
            ("use_object_streams", serde_json::json!(true)),
            ("incremental_update", serde_json::json!(false)),
            ("pdf_version", serde_json::json!("1.7\nInjected")),
            ("unexpected", serde_json::json!(true)),
        ] {
            let mut json = request();
            json[key] = value;
            bad.push(json.to_string());
        }
        for json in bad {
            let json = CString::new(json).unwrap();
            let mut output = ptr::dangling_mut();
            let mut len = usize::MAX;
            let code = unsafe {
                oxidize_document_save_incremental(
                    &mut doc,
                    b"bad".as_ptr(),
                    3,
                    json.as_ptr(),
                    &mut output,
                    &mut len,
                )
            };
            assert_eq!(code, ErrorCode::InvalidArgument as c_int, "{json:?}");
            assert!(output.is_null());
            assert_eq!(len, 0);
        }
    }

    #[test]
    fn null_length_and_utf8_boundaries_reset_each_writable_output() {
        let mut doc = DocumentHandle {
            inner: oxidize_pdf::Document::new(),
        };
        let json = CString::new(request().to_string()).unwrap();
        let mut output = ptr::dangling_mut();
        let mut len = usize::MAX;
        unsafe {
            assert_eq!(
                oxidize_document_save_incremental(
                    ptr::null_mut(),
                    ptr::null(),
                    0,
                    ptr::null(),
                    &mut output,
                    ptr::null_mut()
                ),
                ErrorCode::NullPointer as c_int
            );
            assert!(output.is_null());
            assert_eq!(
                oxidize_document_save_incremental(
                    ptr::null_mut(),
                    ptr::null(),
                    0,
                    ptr::null(),
                    ptr::null_mut(),
                    &mut len
                ),
                ErrorCode::NullPointer as c_int
            );
            assert_eq!(len, 0);
            for size in [0, usize::MAX] {
                output = ptr::dangling_mut();
                len = usize::MAX;
                assert_eq!(
                    oxidize_document_save_incremental(
                        &mut doc,
                        b"x".as_ptr(),
                        size,
                        json.as_ptr(),
                        &mut output,
                        &mut len
                    ),
                    ErrorCode::InvalidArgument as c_int
                );
                assert!(output.is_null());
                assert_eq!(len, 0);
            }
            let invalid = [255u8, 0];
            assert_eq!(
                oxidize_document_save_incremental(
                    &mut doc,
                    b"x".as_ptr(),
                    1,
                    invalid.as_ptr().cast(),
                    &mut output,
                    &mut len
                ),
                ErrorCode::InvalidUtf8 as c_int
            );
            assert!(output.is_null());
            assert_eq!(len, 0);
        }
    }

    #[test]
    fn malformed_pdf_failure_does_not_consume_document_or_allocate_output() {
        let mut doc = DocumentHandle {
            inner: oxidize_pdf::Document::new(),
        };
        doc.inner.add_page(oxidize_pdf::Page::a4());
        let json = CString::new(request().to_string()).unwrap();
        let mut output = ptr::dangling_mut();
        let mut len = usize::MAX;
        let code = unsafe {
            oxidize_document_save_incremental(
                &mut doc,
                b"bad".as_ptr(),
                3,
                json.as_ptr(),
                &mut output,
                &mut len,
            )
        };
        assert_eq!(code, ErrorCode::PdfParseError as c_int);
        assert!(output.is_null());
        assert_eq!(len, 0);
        assert!(doc.inner.to_bytes().unwrap().starts_with(b"%PDF-"));
    }
}
