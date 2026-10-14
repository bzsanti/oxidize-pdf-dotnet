//! Raw stream inspection and document-aware bounded decoding.
use crate::{clear_last_error, set_last_error, text_extraction::ParserSettings, ErrorCode};
use base64::{engine::general_purpose::STANDARD, Engine as _};
use oxidize_pdf::parser::{
    filters::{FlateRecoveryKind, StreamRecoveryErrorKind},
    objects::{PdfObject, PdfStream},
    PdfDocument, PdfReader,
};
use serde::Deserialize;
use serde_json::{json, Value};
use std::{
    collections::HashSet,
    ffi::{CStr, CString},
    io::Cursor,
    os::raw::{c_char, c_int},
};

type Failure = (u8, String);
type Result<T> = std::result::Result<T, Failure>;
fn other(e: impl std::fmt::Display) -> Failure {
    (2, e.to_string())
}
fn limit(message: &str) -> Failure {
    (1, message.into())
}

#[derive(Deserialize)]
#[serde(rename_all = "PascalCase", deny_unknown_fields)]
struct Options {
    max_encoded_bytes: usize,
    max_decoded_bytes: usize,
    allow_recovery: bool,
    max_streams: usize,
    max_reference_depth: usize,
    max_metadata_depth: usize,
    max_metadata_nodes: usize,
    max_metadata_bytes: usize,
}
#[derive(Deserialize)]
enum Action {
    Read,
    ReadPage,
    Decode,
}
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Request {
    action: Action,
    object_id: Option<(u32, u16)>,
    page_index: Option<u32>,
    contents_index: Option<usize>,
    options: Options,
    parser_options: ParserSettings,
}
struct Stream {
    value: PdfStream,
    object_id: Option<(u32, u16)>,
    contents_index: Option<usize>,
}
fn identity(id: Option<(u32, u16)>) -> Value {
    id.map(|(n, g)| json!({"object_number":n,"generation_number":g}))
        .unwrap_or(Value::Null)
}
fn resolve(
    doc: &PdfDocument<Cursor<&[u8]>>,
    mut value: PdfObject,
    max_depth: usize,
) -> Result<(PdfObject, Option<(u32, u16)>)> {
    let mut visited = HashSet::new();
    let mut identity = None;
    while let PdfObject::Reference(n, g) = value {
        if visited.len() >= max_depth {
            return Err(limit("Stream reference depth exceeded"));
        }
        if !visited.insert((n, g)) {
            return Err(other("Cyclic stream reference"));
        }
        identity = Some((n, g));
        value = doc.get_object(n, g).map_err(other)?;
    }
    Ok((value, identity))
}
fn as_stream(
    value: PdfObject,
    id: Option<(u32, u16)>,
    index: Option<usize>,
    remaining: &mut usize,
) -> Result<Stream> {
    match value {
        PdfObject::Stream(value) => {
            *remaining = remaining
                .checked_sub(value.data.len())
                .ok_or_else(|| limit("Encoded byte budget exceeded"))?;
            Ok(Stream {
                value,
                object_id: id,
                contents_index: index,
            })
        }
        _ => Err(other("Selected object is not a stream")),
    }
}
fn select(doc: &PdfDocument<Cursor<&[u8]>>, r: &Request) -> Result<Vec<Stream>> {
    let mut remaining = r.options.max_encoded_bytes;
    if let Some((n, g)) = r.object_id {
        if r.page_index.is_some()
            || r.contents_index.is_some()
            || matches!(r.action, Action::ReadPage)
        {
            return Err(other("Conflicting stream selectors"));
        }
        let (value, id) = resolve(
            doc,
            PdfObject::Reference(n, g),
            r.options.max_reference_depth,
        )?;
        return Ok(vec![as_stream(value, id, None, &mut remaining)?]);
    }
    let page = doc
        .get_page(r.page_index.ok_or_else(|| other("Missing page selector"))?)
        .map_err(other)?;
    let Some(contents) = page.dict.get("Contents") else {
        return if matches!(r.action, Action::ReadPage) {
            Ok(vec![])
        } else {
            Err(other("Page has no content stream"))
        };
    };
    let (contents, id) = resolve(doc, contents.clone(), r.options.max_reference_depth)?;
    let values = match contents {
        PdfObject::Array(a) => a.0.into_iter().map(|v| (v, None)).collect::<Vec<_>>(),
        PdfObject::Null if matches!(r.action, Action::ReadPage) => return Ok(vec![]),
        v => vec![(v, id)],
    };
    if matches!(r.action, Action::ReadPage) && r.contents_index.is_some() {
        return Err(other("Unexpected content selector"));
    }
    if matches!(r.action, Action::ReadPage) && values.len() > r.options.max_streams {
        return Err(limit("Stream count exceeded"));
    }
    let selected = if matches!(r.action, Action::ReadPage) {
        None
    } else {
        Some(
            r.contents_index
                .ok_or_else(|| other("Missing content selector"))?,
        )
    };
    if selected.is_some_and(|i| i >= values.len()) {
        return Err(other("Content index out of range"));
    }
    values
        .into_iter()
        .enumerate()
        .filter(|(i, _)| selected.is_none_or(|s| s == *i))
        .map(|(i, (value, id))| {
            let (value, resolved_id) = resolve(doc, value, r.options.max_reference_depth)?;
            as_stream(value, resolved_id.or(id), Some(i), &mut remaining)
        })
        .collect()
}
struct MetadataBudget {
    nodes: usize,
    bytes: usize,
}
impl MetadataBudget {
    fn bytes(&mut self, n: usize) -> Result<()> {
        self.bytes = self
            .bytes
            .checked_sub(n)
            .ok_or_else(|| limit("Metadata byte budget exceeded"))?;
        Ok(())
    }
    fn check(&mut self, value: &PdfObject, depth: usize, max_depth: usize) -> Result<()> {
        if depth > max_depth {
            return Err(limit("Metadata depth exceeded"));
        }
        self.nodes = self
            .nodes
            .checked_sub(1)
            .ok_or_else(|| limit("Metadata node budget exceeded"))?;
        match value {
            PdfObject::Name(n) => self.bytes(n.0.len())?,
            PdfObject::String(s) => self.bytes(s.as_bytes().len())?,
            PdfObject::Array(a) => {
                for v in &a.0 {
                    self.check(v, depth + 1, max_depth)?;
                }
            }
            PdfObject::Dictionary(d) => {
                for (k, v) in &d.0 {
                    self.bytes(k.0.len())?;
                    self.check(v, depth + 1, max_depth)?;
                }
            }
            PdfObject::Stream(_) => return Err(other("Stream object in filter metadata")),
            _ => {}
        }
        Ok(())
    }
}
fn dispatch(bytes: &[u8], r: Request) -> Result<Value> {
    if r.options.max_metadata_depth > 16 || r.options.max_reference_depth > 128 {
        return Err(other(
            "Metadata depth must be <= 16 and reference depth <= 128",
        ));
    }
    let reader =
        PdfReader::new_with_options(Cursor::new(bytes), r.parser_options.core().map_err(other)?)
            .map_err(other)?;
    let doc = PdfDocument::new(reader);
    let streams = select(&doc, &r)?;
    let options = &r.options;
    let action = &r.action;
    if matches!(action, Action::Decode) {
        let stream = streams
            .into_iter()
            .next()
            .ok_or_else(|| other("Missing stream"))?;
        let (data, diagnostics) = if options.allow_recovery {
            let recovered = doc
                .decode_stream_with_recovery(&stream.value, options.max_decoded_bytes)
                .map_err(|e| {
                    let kind = match e.kind {
                        StreamRecoveryErrorKind::InvalidFlate => 0,
                        StreamRecoveryErrorKind::ResourceLimit => 1,
                        _ => 2,
                    };
                    (kind, e.to_string())
                })?;
            let diagnostics = recovered
                .diagnostics
                .into_iter()
                .map(|d| {
                    let kind = match d.kind {
                        FlateRecoveryKind::Unverified => 0,
                        FlateRecoveryKind::Incomplete => 1,
                        _ => return Err(other("Unknown recovery kind")),
                    };
                    Ok(json!({"filter_index":d.filter_index,"kind":kind,"error":d.error}))
                })
                .collect::<Result<Vec<_>>>()?;
            (recovered.data, diagnostics)
        } else {
            // Core 5.4.2 returns an empty Vec for /Filter [] even with nonempty input.
            // Do not publish that silent data loss as successful complete decoding.
            if matches!(stream.value.dict.get("Filter"), Some(PdfObject::Array(a)) if a.0.is_empty())
                && !stream.value.data.is_empty()
            {
                return Err((3,"Core 5.4.2 strict decoding of an empty Filter array is unsupported; raw bytes remain available".into()));
            }
            (
                doc.decode_stream_with_limit(&stream.value, options.max_decoded_bytes)
                    .map_err(|e| (3, e.to_string()))?,
                vec![],
            )
        };
        return Ok(
            json!({"data":STANDARD.encode(data),"diagnostics":diagnostics,"object_id":identity(stream.object_id),"contents_index":stream.contents_index}),
        );
    }
    let mut budget = MetadataBudget {
        nodes: options.max_metadata_nodes,
        bytes: options.max_metadata_bytes,
    };
    let mut result = Vec::new();
    for stream in streams {
        let mut metadata = |key| -> Result<Value> {
            match stream.value.dict.get(key) {
                Some(value) => {
                    budget.check(value, 0, options.max_metadata_depth)?;
                    Ok(crate::pdf_value::object_json(value))
                }
                None => Ok(Value::Null),
            }
        };
        let filter = metadata("Filter")?;
        let parms = metadata("DecodeParms")?;
        result.push(json!({"data":STANDARD.encode(&stream.value.data),"filter":filter,"decode_parameters":parms,"object_id":identity(stream.object_id),"contents_index":stream.contents_index}));
    }
    Ok(Value::Array(result))
}

/// Read or decode a stream. Domain errors are returned in the JSON envelope.
/// # Safety
/// Input buffers must be readable for their lengths (request NUL-terminated UTF-8),
/// output writable. Caller owns successful output and frees with oxidize_free_string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_streams(
    bytes: *const u8,
    len: usize,
    request: *const c_char,
    output: *mut *mut c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if output.is_null() {
            set_last_error("Null stream output");
            return ErrorCode::NullPointer as c_int;
        }
        *output = std::ptr::null_mut();
        if bytes.is_null() || request.is_null() {
            set_last_error("Null stream input");
            return ErrorCode::NullPointer as c_int;
        }
        if len == 0 || len > isize::MAX as usize {
            set_last_error("Invalid PDF length");
            return ErrorCode::InvalidArgument as c_int;
        }
        let result = (|| {
            let request = CStr::from_ptr(request).to_str().map_err(other)?;
            let request: Request = serde_json::from_str(request).map_err(other)?;
            dispatch(std::slice::from_raw_parts(bytes, len), request)
        })();
        let envelope = match result {
            Ok(value) => json!({"result":value}),
            Err((kind, message)) => json!({"error":{"kind":kind,"message":message}}),
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
    fn ffi_rejects_invalid_lengths_and_clears_output() {
        let input = [1u8];
        let request = CString::new("{}").unwrap();
        for (bytes, len, expected) in [
            (std::ptr::null(), 1, ErrorCode::NullPointer),
            (input.as_ptr(), 0, ErrorCode::InvalidArgument),
            (
                input.as_ptr(),
                isize::MAX as usize + 1,
                ErrorCode::InvalidArgument,
            ),
        ] {
            let mut output = std::ptr::dangling_mut::<c_char>();
            let code = unsafe { oxidize_streams(bytes, len, request.as_ptr(), &mut output) };
            assert_eq!(code, expected as c_int);
            assert!(output.is_null());
        }
        let mut output = std::ptr::dangling_mut::<c_char>();
        assert_eq!(
            unsafe { oxidize_streams(input.as_ptr(), 1, std::ptr::null(), &mut output) },
            ErrorCode::NullPointer as c_int
        );
        assert!(output.is_null());
        assert_eq!(
            unsafe { oxidize_streams(input.as_ptr(), 1, request.as_ptr(), std::ptr::null_mut()) },
            ErrorCode::NullPointer as c_int
        );
    }
    #[test]
    fn malformed_request_returns_owned_typed_error() {
        let input = [1u8];
        let request = CString::new("{}").unwrap();
        let mut output = std::ptr::null_mut();
        assert_eq!(
            unsafe { oxidize_streams(input.as_ptr(), 1, request.as_ptr(), &mut output) },
            ErrorCode::Success as c_int
        );
        let owned = unsafe { CString::from_raw(output) };
        let envelope: Value = serde_json::from_str(owned.to_str().unwrap()).unwrap();
        assert_eq!(envelope["error"]["kind"], 2);
        assert!(envelope["error"]["message"]
            .as_str()
            .unwrap()
            .contains("missing field"));
        assert!(envelope.get("result").is_none());
    }
}
