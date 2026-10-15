//! Options for legacy reconstructing split. Caller names never become paths.
use crate::ErrorCode;
use base64::Engine;
use oxidize_pdf::operations::{PageRange, SplitMode, SplitOptions};
use serde::Deserialize;

type Failure = (ErrorCode, String);
fn invalid(message: &str) -> Failure {
    (ErrorCode::InvalidArgument, message.into())
}
fn io(error: std::io::Error) -> Failure {
    (ErrorCode::IoError, error.to_string())
}

#[derive(Deserialize)]
#[serde(tag = "mode")]
enum Mode {
    SinglePages,
    ChunkSize { chunk_size: usize },
    Ranges { ranges: Vec<[usize; 2]> },
    SplitAt { split_at: Vec<usize> },
}

#[derive(Deserialize)]
struct Request {
    #[serde(flatten)]
    mode: Mode,
    #[serde(default = "yes")]
    preserve_metadata: bool,
    #[serde(default)]
    optimize: bool,
    #[serde(default = "pattern")]
    output_pattern: String,
    #[serde(default)]
    named: bool,
}
fn yes() -> bool {
    true
}
fn pattern() -> String {
    "page_{}.pdf".into()
}

fn portable(name: &str) -> bool {
    let stem = name
        .split('.')
        .next()
        .unwrap_or("")
        .trim_end()
        .to_ascii_uppercase();
    !name.is_empty()
        && name.len() <= 200
        && !name.ends_with(['.', ' '])
        && !name
            .chars()
            .any(|c| c.is_control() || "<>:\"/\\|?*".contains(c))
        && !matches!(
            stem.as_str(),
            "CON" | "PRN" | "AUX" | "NUL" | "CONIN$" | "CONOUT$"
        )
        && !["COM¹", "COM²", "COM³", "LPT¹", "LPT²", "LPT³"].contains(&stem.as_str())
        && !(stem.len() == 4
            && (stem.starts_with("COM") || stem.starts_with("LPT"))
            && matches!(stem.as_bytes()[3], b'1'..=b'9'))
}

pub(crate) fn split(bytes: &[u8], json: &str) -> Result<String, Failure> {
    let request: Request =
        serde_json::from_str(json).map_err(|e| invalid(&format!("Invalid split options: {e}")))?;
    let single = matches!(request.mode, Mode::SinglePages);
    let mut tokens = request
        .output_pattern
        .replace("{}", "1")
        .replace("{n}", "1");
    tokens = if single {
        tokens.replace("{page}", "1")
    } else {
        tokens.replace("{start}", "1").replace("{end}", "1")
    };
    if !portable(&tokens) || tokens.contains(['{', '}']) {
        return Err(invalid(
            "Invalid output pattern: use a portable filename and supported placeholders",
        ));
    }
    let mode = match request.mode {
        Mode::SinglePages => SplitMode::SinglePages,
        Mode::ChunkSize { chunk_size } => {
            // Core loops forever for zero and can overflow usize for huge chunks.
            if chunk_size == 0 || chunk_size > i32::MAX as usize {
                return Err(invalid("Chunk size must be between 1 and Int32.MaxValue"));
            }
            SplitMode::ChunkSize(chunk_size)
        }
        Mode::Ranges { ranges } => {
            if ranges.is_empty() || ranges.iter().any(|[a, b]| a > b || *b > i32::MAX as usize) {
                return Err(invalid("Nonempty ascending ranges are required"));
            }
            SplitMode::Ranges(
                ranges
                    .into_iter()
                    .map(|[a, b]| PageRange::Range(a, b))
                    .collect(),
            )
        }
        Mode::SplitAt { split_at } => {
            // Preserve core semantics: out-of-bounds/zero ignored; unsorted points
            // reach core range validation. No sorting or deduplication is implied.
            SplitMode::SplitAt(split_at)
        }
    };
    let temp = tempfile::tempdir().map_err(io)?;
    let input = temp.path().join("input.pdf");
    std::fs::write(&input, bytes).map_err(io)?;
    // Only trusted tokens enter filesystem paths. Ordinal prevents overwrite even
    // for repeated ranges. All partial outputs are removed by the directory guard.
    let output_pattern = temp
        .path()
        .join("{}_{n}.pdf")
        .to_str()
        .ok_or_else(|| invalid("Temporary path is not UTF-8"))?
        .to_owned();
    let paths = oxidize_pdf::operations::split_pdf(
        input,
        SplitOptions {
            mode,
            output_pattern,
            preserve_metadata: request.preserve_metadata,
            optimize: request.optimize,
        },
    )
    .map_err(|e| {
        use oxidize_pdf::operations::OperationError;
        let code = match &e {
            OperationError::PageIndexOutOfBounds(..)
            | OperationError::InvalidPageRange(_)
            | OperationError::NoPagesToProcess => ErrorCode::InvalidArgument,
            OperationError::Io(_) => ErrorCode::IoError,
            _ => ErrorCode::PdfParseError,
        };
        (code, e.to_string())
    })?;
    let mut names = std::collections::HashSet::new();
    let mut results = Vec::new();
    let mut legacy = Vec::new();
    for (index, path) in paths.iter().enumerate() {
        let file = path
            .file_name()
            .and_then(|s| s.to_str())
            .ok_or_else(|| invalid("Invalid internal filename"))?;
        let range = file
            .split('_')
            .next()
            .ok_or_else(|| invalid("Missing internal range"))?;
        let (start, end) = range.split_once('-').unwrap_or((range, range));
        let name = request
            .output_pattern
            .replace("{}", range)
            .replace("{n}", &(index + 1).to_string())
            .replace("{page}", start)
            .replace("{start}", start)
            .replace("{end}", end);
        if !portable(&name) || (request.named && !names.insert(name.to_lowercase())) {
            return Err(invalid("Output names must be portable and unique ignoring case; use {n} for repeated ranges"));
        }
        let output = std::fs::read(path).map_err(io)?;
        let output = crate::merge_metadata::encode_info_text(output)
            .map_err(|e| (ErrorCode::SerializationError, e))?;
        let encoded = base64::engine::general_purpose::STANDARD.encode(output);
        if request.named {
            results.push(serde_json::json!({"FileName": name, "Bytes": encoded}));
        } else {
            legacy.push(encoded);
        }
    }
    if request.named {
        serde_json::to_string(&results)
    } else {
        serde_json::to_string(&legacy)
    }
    .map_err(|e| (ErrorCode::SerializationError, e.to_string()))
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::ffi::{CStr, CString};
    use std::ptr;

    fn pdf() -> Vec<u8> {
        let mut doc = oxidize_pdf::Document::new();
        doc.add_page(oxidize_pdf::Page::a4());
        doc.add_page(oxidize_pdf::Page::a4());
        doc.to_bytes().unwrap()
    }

    fn call(bytes: &[u8], json: &str) -> (i32, Option<String>) {
        let json = CString::new(json).unwrap();
        let mut output = ptr::dangling_mut();
        unsafe {
            let code = crate::operations::oxidize_split_pdf_bytes_with_options(
                bytes.as_ptr(),
                bytes.len(),
                json.as_ptr(),
                &mut output,
            );
            let text = if output.is_null() {
                None
            } else {
                let result = CStr::from_ptr(output).to_str().unwrap().to_owned();
                crate::oxidize_free_string(output);
                Some(result)
            };
            (code, text)
        }
    }

    #[test]
    fn ffi_validates_options_without_entering_core_zero_chunk_loop() {
        let bytes = pdf();
        for json in [
            r#"{"mode":"ChunkSize","chunk_size":0}"#,
            r#"{"mode":"ChunkSize","chunk_size":18446744073709551615}"#,
            r#"{"mode":"Ranges","ranges":[[1,0]]}"#,
            r#"{"mode":"Ranges","ranges":[]}"#,
            r#"{"mode":"Ranges","ranges":[[0,18446744073709551615]]}"#,
            r#"{"mode":"SinglePages","preserve_metadata":"true"}"#,
            r#"{"mode":"SinglePages","output_pattern":null}"#,
            r#"{"mode":"SinglePages","output_pattern":"../escape-{n}.pdf"}"#,
            r#"{"mode":"Unknown"}"#,
            "{",
        ] {
            let (code, output) = call(&bytes, json);
            assert_eq!(code, ErrorCode::InvalidArgument as i32, "{json}");
            assert!(output.is_none());
        }
    }

    #[test]
    fn ffi_partial_failure_then_success_and_legacy_json_defaults() {
        let bytes = pdf();
        let (code, output) = call(&bytes, r#"{"mode":"Ranges","ranges":[[0,0],[99,99]]}"#);
        assert_eq!(code, ErrorCode::InvalidArgument as i32);
        assert!(output.is_none());
        assert_eq!(
            call(b"not pdf", r#"{"mode":"SinglePages"}"#).0,
            ErrorCode::PdfParseError as i32
        );
        let (code, output) = call(&bytes, r#"{"mode":"SinglePages"}"#);
        assert_eq!(code, 0);
        let values: Vec<String> = serde_json::from_str(&output.unwrap()).unwrap();
        assert_eq!(values.len(), 2);
        for value in values {
            let decoded = base64::engine::general_purpose::STANDARD
                .decode(value)
                .unwrap();
            assert!(decoded.starts_with(b"%PDF-"));
        }
    }

    #[test]
    fn ffi_suggested_names_do_not_create_caller_files() {
        let marker = tempfile::tempdir().unwrap();
        let prefix = marker.path().file_name().unwrap().to_str().unwrap();
        let json = serde_json::json!({"mode": "SinglePages", "named": true,
            "output_pattern": format!("split-{prefix}-{{n}}.pdf")})
        .to_string();
        let (code, output) = call(&pdf(), &json);
        assert_eq!(code, 0);
        let results: Vec<serde_json::Value> = serde_json::from_str(&output.unwrap()).unwrap();
        assert_eq!(results.len(), 2);
        for (index, result) in results.iter().enumerate() {
            let name = format!("split-{prefix}-{}.pdf", index + 1);
            assert_eq!(result["FileName"], name);
            assert!(
                !std::path::Path::new(&name).exists(),
                "Caller filename was written: {name}"
            );
        }
    }

    #[test]
    fn ffi_clears_output_on_null_invalid_utf8_and_impossible_length() {
        unsafe {
            let mut output = ptr::dangling_mut();
            let json = c"{\"mode\":\"SinglePages\"}";
            let code = crate::operations::oxidize_split_pdf_bytes_with_options(
                ptr::null(),
                1,
                json.as_ptr(),
                &mut output,
            );
            assert_eq!(code, ErrorCode::NullPointer as i32);
            assert!(output.is_null());
            let bytes = [1u8];
            let invalid = [255u8, 0];
            output = ptr::dangling_mut();
            let code = crate::operations::oxidize_split_pdf_bytes_with_options(
                bytes.as_ptr(),
                1,
                invalid.as_ptr().cast(),
                &mut output,
            );
            assert_eq!(code, ErrorCode::InvalidUtf8 as i32);
            assert!(output.is_null());
            output = ptr::dangling_mut();
            let code = crate::operations::oxidize_split_pdf_bytes_with_options(
                bytes.as_ptr(),
                usize::MAX,
                json.as_ptr(),
                &mut output,
            );
            assert_ne!(code, 0);
            assert!(output.is_null());
        }
    }
}
