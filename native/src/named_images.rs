//! Select one named image without visiting or decoding unrelated page resources.
use crate::{
    clear_last_error, set_last_error, streams, text_extraction::ParserSettings, ErrorCode,
};
use base64::{engine::general_purpose::STANDARD, Engine as _};
use oxidize_pdf::parser::{
    objects::{PdfDictionary, PdfObject, PdfStream},
    PdfDocument, PdfReader,
};
use serde::{Deserialize, Serialize};
use serde_json::{json, Value};
use std::{
    collections::HashSet,
    ffi::{CStr, CString},
    io::Cursor,
    os::raw::{c_char, c_int},
};

// Wire discriminants match PdfImageErrorKind; decoding retains its core classification.
const MISSING: u8 = 0;
const NOT_IMAGE: u8 = 1;
const NOT_FORM: u8 = 2;
const UNSUPPORTED: u8 = 3;
const LIMIT: u8 = 4;
const INVALID: u8 = 5;
const DECODE: u8 = 6;
#[derive(Serialize)]
struct Failure {
    kind: u8,
    message: String,
    stream_error_kind: Option<u8>,
}
type Result<T> = std::result::Result<T, Failure>;
fn failure(kind: u8, message: impl std::fmt::Display) -> Failure {
    Failure {
        kind,
        message: message.to_string(),
        stream_error_kind: None,
    }
}
fn inspection_error((kind, message): streams::Failure) -> Failure {
    failure(if kind == 1 { LIMIT } else { INVALID }, message)
}
fn from_stream((kind, message): streams::Failure) -> Failure {
    Failure {
        kind: if kind == 1 { LIMIT } else { DECODE },
        message,
        stream_error_kind: Some(kind),
    }
}
#[derive(Deserialize)]
#[serde(rename_all = "PascalCase", deny_unknown_fields)]
struct Limits {
    max_images: usize,
    max_encoded_bytes_per_image: usize,
    max_total_encoded_bytes: usize,
    max_decoded_pixels_per_image: u64,
}
#[derive(Deserialize)]
#[serde(rename_all = "PascalCase", deny_unknown_fields)]
struct Options {
    limits: Limits,
    max_decoded_bytes: usize,
    allow_recovery: bool,
    max_ancestor_depth: usize,
    max_form_depth: usize,
    max_reference_depth: usize,
    max_name_bytes: usize,
    max_metadata_depth: usize,
    max_metadata_nodes: usize,
    max_metadata_bytes: usize,
}
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Request {
    page_index: u32,
    resource_name: String,
    form_path: Vec<String>,
    options: Options,
    parser_options: ParserSettings,
}
fn resolved(
    doc: &PdfDocument<Cursor<&[u8]>>,
    value: PdfObject,
    depth: usize,
) -> Result<(PdfObject, Option<(u32, u16)>)> {
    streams::resolve(doc, value, depth)
        .map_err(|(kind, message)| failure(if kind == 1 { LIMIT } else { INVALID }, message))
}
fn dictionary(
    doc: &PdfDocument<Cursor<&[u8]>>,
    value: &PdfObject,
    depth: usize,
) -> Result<(PdfDictionary, Option<(u32, u16)>)> {
    let (value, id) = resolved(doc, value.clone(), depth)?;
    match value {
        PdfObject::Dictionary(d) => Ok((d, id)),
        _ => Err(failure(INVALID, "Resource dictionary has invalid type")),
    }
}
fn named(
    doc: &PdfDocument<Cursor<&[u8]>>,
    resources: &PdfDictionary,
    name: &str,
    depth: usize,
    not_stream: u8,
) -> Result<(PdfStream, Option<(u32, u16)>)> {
    let objects = resources
        .get("XObject")
        .ok_or_else(|| failure(MISSING, "No XObject resource dictionary"))?;
    let (objects, _) = dictionary(doc, objects, depth)?;
    let value = objects
        .get(name)
        .ok_or_else(|| failure(MISSING, format!("XObject resource not found: {name}")))?;
    let (value, id) = resolved(doc, value.clone(), depth)?;
    match value {
        PdfObject::Stream(stream) => Ok((stream, id)),
        _ => Err(failure(not_stream, "Named XObject is not a stream")),
    }
}
fn positive_integer(
    doc: &PdfDocument<Cursor<&[u8]>>,
    dict: &PdfDictionary,
    key: &str,
    depth: usize,
) -> Result<u32> {
    let value = dict
        .get(key)
        .ok_or_else(|| failure(INVALID, format!("Missing image {key}")))?;
    let (value, _) = resolved(doc, value.clone(), depth)?;
    match value {
        PdfObject::Integer(n) if n > 0 => {
            u32::try_from(n).map_err(|_| failure(INVALID, format!("Invalid image {key}")))
        }
        _ => Err(failure(INVALID, format!("Invalid image {key}"))),
    }
}
fn dispatch(bytes: &[u8], r: Request) -> Result<Value> {
    let o = &r.options;
    if o.max_ancestor_depth > 256
        || o.max_form_depth > 64
        || o.max_reference_depth > 128
        || o.max_metadata_depth > 16
    {
        return Err(failure(
            INVALID,
            "Invalid ancestor/Form/reference/metadata depth limit",
        ));
    }
    if r.form_path.len() > o.max_form_depth {
        return Err(failure(LIMIT, "Form path depth exceeded"));
    }
    let mut names = o.max_name_bytes;
    for name in r.form_path.iter().chain(std::iter::once(&r.resource_name)) {
        names = names
            .checked_sub(name.len())
            .ok_or_else(|| failure(LIMIT, "Resource name byte budget exceeded"))?;
    }
    let l = &o.limits;
    if l.max_images < 1 {
        return Err(failure(LIMIT, "Image count limit exceeded"));
    }
    let reader = PdfReader::new_with_options(
        Cursor::new(bytes),
        r.parser_options.core().map_err(|e| failure(INVALID, e))?,
    )
    .map_err(|e| failure(INVALID, e))?;
    let doc = PdfDocument::new(reader);
    let page = doc
        .get_page(r.page_index)
        .map_err(|e| failure(INVALID, e))?;
    let source = crate::resource_provenance::find_resources(&doc, &page, o.max_ancestor_depth)
        .map_err(|e| {
            let kind = if matches!(
                e,
                crate::resource_provenance::ResourceLookupError::DepthLimit
            ) {
                LIMIT
            } else {
                INVALID
            };
            failure(kind, e)
        })?;
    let mut owner = source.owner;
    let mut dictionary_id = source.dictionary_reference;
    let mut resources = source
        .dictionary
        .ok_or_else(|| failure(MISSING, "Page has no resources"))?;
    let mut form_ids = Vec::new();
    let mut visited = HashSet::new();
    for name in &r.form_path {
        let (form, id) = named(&doc, &resources, name, o.max_reference_depth, NOT_FORM)?;
        if !matches!(form.dict.get("Subtype"),Some(PdfObject::Name(n)) if n.as_str()=="Form") {
            return Err(failure(NOT_FORM, "Path component is not a Form XObject"));
        }
        if id.is_some_and(|id| !visited.insert(id)) {
            return Err(failure(INVALID, "Cycle in Form resource path"));
        }
        form_ids.push(streams::identity(id));
        // Only absence inherits. Empty/invalid Resources must never fall back.
        if let Some(value) = form.dict.get("Resources") {
            let (next, next_id) = dictionary(&doc, value, o.max_reference_depth)?;
            resources = next;
            owner = id.map(Into::into);
            dictionary_id = next_id.map(Into::into);
        }
    }
    let (image, id) = named(
        &doc,
        &resources,
        &r.resource_name,
        o.max_reference_depth,
        NOT_IMAGE,
    )?;
    if !matches!(image.dict.get("Subtype"),Some(PdfObject::Name(n)) if n.as_str()=="Image") {
        return Err(failure(NOT_IMAGE, "Named XObject is not an image"));
    }
    if image.data.len() > l.max_encoded_bytes_per_image
        || image.data.len() > l.max_total_encoded_bytes
    {
        return Err(failure(LIMIT, "Encoded image byte limit exceeded"));
    }
    let width = positive_integer(&doc, &image.dict, "Width", o.max_reference_depth)?;
    let height = positive_integer(&doc, &image.dict, "Height", o.max_reference_depth)?;
    if u64::from(width) * u64::from(height) > l.max_decoded_pixels_per_image {
        return Err(failure(LIMIT, "Decoded image pixel limit exceeded"));
    }
    let image_mask = match image.dict.get("ImageMask") {
        None | Some(PdfObject::Boolean(false)) => false,
        Some(PdfObject::Boolean(true)) => true,
        _ => return Err(failure(INVALID, "Invalid ImageMask")),
    };
    let bits = if image_mask && image.dict.get("BitsPerComponent").is_none() {
        1
    } else {
        positive_integer(&doc, &image.dict, "BitsPerComponent", o.max_reference_depth)?
    };
    if !matches!(bits, 1 | 2 | 4 | 8 | 16) || (image_mask && bits != 1) {
        return Err(failure(INVALID, "Invalid BitsPerComponent"));
    }
    if !image_mask && image.dict.get("ColorSpace").is_none() {
        return Err(failure(INVALID, "Missing image ColorSpace"));
    }

    // Bound declared metadata before constructing JSON or collecting filter names.
    let mut budget = streams::MetadataBudget {
        nodes: o.max_metadata_nodes,
        bytes: o.max_metadata_bytes,
    };
    for (key, value) in &image.dict.0 {
        budget.bytes(key.0.len()).map_err(inspection_error)?;
        budget
            .check(value, 0, o.max_metadata_depth)
            .map_err(inspection_error)?;
    }
    let filters = match image.dict.get("Filter") {
        None => vec![],
        Some(PdfObject::Name(name)) => vec![name.as_str()],
        Some(PdfObject::Array(a)) => {
            a.0.iter()
                .map(|v| {
                    v.as_name()
                        .map(|n| n.as_str())
                        .ok_or_else(|| failure(INVALID, "Invalid image Filter entry"))
                })
                .collect::<Result<Vec<_>>>()?
        }
        _ => return Err(failure(INVALID, "Invalid image Filter")),
    };
    let jpeg = filters.len() == 1 && matches!(filters[0], "DCTDecode" | "DCT");
    if !jpeg
        && filters.iter().any(|f| {
            !matches!(
                *f,
                "FlateDecode"
                    | "Fl"
                    | "LZWDecode"
                    | "LZW"
                    | "ASCIIHexDecode"
                    | "AHx"
                    | "ASCII85Decode"
                    | "A85"
                    | "RunLengthDecode"
                    | "RL"
            )
        })
    {
        return Err(failure(UNSUPPORTED,"Image filter chain has no bounded sample decoder; only standalone DCT is passed through"));
    }
    let (data, diagnostics) = if jpeg {
        if image.data.len() > o.max_decoded_bytes {
            return Err(failure(LIMIT, "Image output byte budget exceeded"));
        }
        (image.data.clone(), vec![])
    } else {
        streams::decode(&doc, &image, o.max_decoded_bytes, o.allow_recovery).map_err(from_stream)?
    };
    Ok(json!({
        "page_index":r.page_index,"page_object_id":streams::identity(Some(page.obj_ref)),
        "resource_name":r.resource_name,"form_path":r.form_path,"form_object_ids":form_ids,
        "object_id":streams::identity(id),"resource_owner_id":owner,"resource_dictionary_id":dictionary_id,
        "width":width,"height":height,"bits_per_component":bits,"image_mask":image_mask,
        "encoded_format":if jpeg {2} else if filters.is_empty() {1} else {0},
        "output_format":if jpeg {2} else {1},"encoded_data":STANDARD.encode(&image.data),"data":STANDARD.encode(data),
        "dictionary":crate::pdf_value::dictionary_json(&image.dict),"diagnostics":diagnostics
    }))
}

/// Select a named image. Domain errors use an owned JSON envelope.
/// # Safety
/// PDF bytes must be readable for len, request must be NUL-terminated UTF-8,
/// and output must be writable and not alias inputs. Free output with oxidize_free_string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_named_image(
    bytes: *const u8,
    len: usize,
    request: *const c_char,
    output: *mut *mut c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if output.is_null() {
            set_last_error("Null named image output");
            return ErrorCode::NullPointer as c_int;
        }
        *output = std::ptr::null_mut();
        if bytes.is_null() || request.is_null() {
            set_last_error("Null named image input");
            return ErrorCode::NullPointer as c_int;
        }
        if len == 0 || len > isize::MAX as usize {
            set_last_error("Invalid PDF length");
            return ErrorCode::InvalidArgument as c_int;
        }
        let result = (|| {
            let request = CStr::from_ptr(request)
                .to_str()
                .map_err(|e| failure(INVALID, e))?;
            let request = serde_json::from_str(request).map_err(|e| failure(INVALID, e))?;
            dispatch(std::slice::from_raw_parts(bytes, len), request)
        })();
        let envelope = match result {
            Ok(value) => json!({"result":value}),
            Err(error) => json!({"error":error}),
        };
        match CString::new(envelope.to_string()) {
            Ok(value) => {
                *output = value.into_raw();
                ErrorCode::Success as c_int
            }
            Err(error) => {
                set_last_error(error.to_string());
                ErrorCode::SerializationError as c_int
            }
        }
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn ffi_pointer_and_length_errors_clear_output() {
        let bytes = [1u8];
        let request = CString::new("{}").unwrap();
        for (input, len, expected) in [
            (std::ptr::null(), 1, ErrorCode::NullPointer),
            (bytes.as_ptr(), 0, ErrorCode::InvalidArgument),
            (
                bytes.as_ptr(),
                isize::MAX as usize + 1,
                ErrorCode::InvalidArgument,
            ),
        ] {
            let mut output = std::ptr::dangling_mut::<c_char>();
            let code = unsafe { oxidize_named_image(input, len, request.as_ptr(), &mut output) };
            assert_eq!(code, expected as c_int);
            assert!(output.is_null());
        }
        let mut output = std::ptr::dangling_mut::<c_char>();
        assert_eq!(
            unsafe { oxidize_named_image(bytes.as_ptr(), 1, std::ptr::null(), &mut output) },
            ErrorCode::NullPointer as c_int
        );
        assert!(output.is_null());
        assert_eq!(
            unsafe {
                oxidize_named_image(bytes.as_ptr(), 1, request.as_ptr(), std::ptr::null_mut())
            },
            ErrorCode::NullPointer as c_int
        );
    }
    #[test]
    fn malformed_request_returns_owned_error_without_result() {
        let bytes = [1u8];
        let request = CString::new("{}").unwrap();
        let mut output = std::ptr::null_mut();
        assert_eq!(
            unsafe { oxidize_named_image(bytes.as_ptr(), 1, request.as_ptr(), &mut output) },
            ErrorCode::Success as c_int
        );
        let owned = unsafe { CString::from_raw(output) };
        let value: Value = serde_json::from_str(owned.to_str().unwrap()).unwrap();
        assert_eq!(value["error"]["kind"], INVALID);
        assert!(value["error"]["message"]
            .as_str()
            .unwrap()
            .contains("missing field"));
        assert!(value.get("result").is_none());
        assert!(value["error"]["stream_error_kind"].is_null());
    }
}
