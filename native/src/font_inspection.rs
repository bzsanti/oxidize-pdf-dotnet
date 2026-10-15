//! Bounded CID lookup and font inspection; decoding remains in the core.
use crate::{clear_last_error, set_last_error, ErrorCode};
use base64::{engine::general_purpose::STANDARD, Engine};
use oxidize_pdf::fonts::{FontSubtype, ResolvedFontResource, WritingMode};
use oxidize_pdf::parser::objects::{PdfDictionary, PdfObject};
use oxidize_pdf::parser::{PdfDocument, PdfReader};
use oxidize_pdf::text::cid_to_unicode::AdobeCidCollection;
use serde::{Deserialize, Serialize};
use serde_json::{json, Value};
use std::collections::BTreeMap;
use std::ffi::{CStr, CString};
use std::io::Cursor;
use std::os::raw::{c_char, c_int};
use std::ptr;

type Failure = (u8, String); // InvalidInput, InvalidPdf, MissingFont, LimitExceeded
fn invalid(message: impl ToString) -> Failure {
    (0, message.to_string())
}
fn pdf_error(message: impl ToString) -> Failure {
    (1, message.to_string())
}
fn limit(message: &str) -> Failure {
    (3, message.into())
}

#[derive(Deserialize)]
#[serde(tag = "action", deny_unknown_fields)]
enum Request {
    Collection {
        ordering: String,
        cids: Vec<u16>,
    },
    Inspect {
        page_index: u32,
        resource_name: String,
        character_codes: String,
        cids: Vec<u32>,
        options: Options,
        parser_options: crate::text_extraction::ParserSettings,
    },
}
#[derive(Deserialize)]
#[serde(rename_all = "PascalCase", deny_unknown_fields)]
struct Options {
    max_code_bytes: usize,
    max_cids: usize,
    max_metric_entries: usize,
    max_lookup_work: usize,
    max_unicode_bytes: usize,
}
impl Options {
    fn validate(&self) -> Result<(), Failure> {
        if self.max_code_bytes > 65536
            || self.max_cids > 4096
            || self.max_metric_entries > 65536
            || self.max_lookup_work > 16_777_216
            || self.max_unicode_bytes > 16_777_216
        {
            return Err(invalid("Font limits exceed supported maxima"));
        }
        Ok(())
    }
}

#[derive(Serialize)]
struct AdvanceRange {
    first: u32,
    last: u32,
    advance: f64,
}
#[derive(Serialize)]
struct VerticalMetrics {
    default_advance: f64,
    overrides: BTreeMap<u32, f64>,
    ranges: Vec<AdvanceRange>,
}
impl VerticalMetrics {
    fn advance(&self, cid: u32) -> f64 {
        self.overrides
            .get(&cid)
            .copied()
            .or_else(|| {
                self.ranges
                    .iter()
                    .rev()
                    .find(|r| cid >= r.first && cid <= r.last)
                    .map(|r| r.advance)
            })
            .unwrap_or(self.default_advance)
    }
}
type Document<'a> = PdfDocument<Cursor<&'a [u8]>>;
fn resolve(document: &Document<'_>, value: &PdfObject) -> Result<PdfObject, Failure> {
    document.resolve(value).map_err(pdf_error)
}
fn number(document: &Document<'_>, value: &PdfObject) -> Result<f64, Failure> {
    resolve(document, value)?
        .as_real()
        .filter(|v| v.is_finite())
        .ok_or_else(|| pdf_error("Vertical metric must be finite numeric data"))
}
fn cid(document: &Document<'_>, value: &PdfObject) -> Result<u32, Failure> {
    resolve(document, value)?
        .as_integer()
        .and_then(|n| u16::try_from(n).ok())
        .map(u32::from)
        .ok_or_else(|| pdf_error("Vertical metric CID must be in 0..65535"))
}

// Public core does not expose CidVerticalWidths. Read its supported pen advances
// only, with the same explicit-overrides / reverse-range / default precedence.
// Drawing-origin coordinates are validated but are not pen advances.
fn vertical(
    document: &Document<'_>,
    font: &PdfDictionary,
    max: usize,
) -> Result<VerticalMetrics, Failure> {
    let mut metrics = VerticalMetrics {
        default_advance: -1000.0,
        overrides: BTreeMap::new(),
        ranges: Vec::new(),
    };
    if let Some(value) = font.get("DW2") {
        let value = resolve(document, value)?;
        if !matches!(value, PdfObject::Null) {
            let array = value
                .as_array()
                .filter(|a| a.0.len() == 2)
                .ok_or_else(|| pdf_error("DW2 must have two numbers"))?;
            number(document, &array.0[0])?;
            metrics.default_advance = number(document, &array.0[1])?;
        }
    }
    let Some(value) = font.get("W2") else {
        return Ok(metrics);
    };
    let value = resolve(document, value)?;
    if matches!(value, PdfObject::Null) {
        return Ok(metrics);
    }
    let array = value
        .as_array()
        .ok_or_else(|| pdf_error("W2 must be an array"))?;
    // Every entry consumes at least two top-level elements. Bound input traversal
    // as well as retained entries (duplicates cannot evade the budget).
    if array.0.len() > max.saturating_mul(5) {
        return Err(limit("Vertical metric table exceeds entry budget"));
    }
    let mut position = 0;
    let mut entries = 0usize;
    while position < array.0.len() {
        let first = cid(document, &array.0[position])?;
        let next = resolve(
            document,
            array
                .0
                .get(position + 1)
                .ok_or_else(|| pdf_error("Truncated W2 entry"))?,
        )?;
        if let Some(values) = next.as_array() {
            if values.0.len() % 3 != 0 {
                return Err(pdf_error("W2 override list must contain triples"));
            }
            entries = entries
                .checked_add(values.0.len() / 3)
                .ok_or_else(|| limit("Metric count overflow"))?;
            if entries > max {
                return Err(limit("Vertical metric table exceeds entry budget"));
            }
            for (offset, triple) in values.0.chunks_exact(3).enumerate() {
                let key = first as usize + offset;
                if key > u16::MAX as usize {
                    return Err(pdf_error("W2 override exceeds CID 65535"));
                }
                let advance = number(document, &triple[0])?;
                number(document, &triple[1])?;
                number(document, &triple[2])?;
                metrics.overrides.insert(key as u32, advance);
            }
            position += 2;
        } else {
            let last = cid(document, &next)?;
            if last < first {
                return Err(pdf_error("Descending W2 range"));
            }
            let triple = array
                .0
                .get(position + 2..position + 5)
                .ok_or_else(|| pdf_error("Truncated W2 range"))?;
            entries += 1;
            if entries > max {
                return Err(limit("Vertical metric table exceeds entry budget"));
            }
            let advance = number(document, &triple[0])?;
            number(document, &triple[1])?;
            number(document, &triple[2])?;
            metrics.ranges.push(AdvanceRange {
                first,
                last,
                advance,
            });
            position += 5;
        }
    }
    Ok(metrics)
}

// Declared metadata only: the core independently decides whether a collection
// is compatible with the encoding and may supply Unicode fallback.
fn system_info(document: &Document<'_>, font: &PdfDictionary) -> Result<Option<Value>, Failure> {
    let Some(info) = font.get("CIDSystemInfo") else {
        return Ok(None);
    };
    let info = resolve(document, info)?;
    let dictionary = info
        .as_dict()
        .ok_or_else(|| pdf_error("CIDSystemInfo must be a dictionary"))?;
    let text = |key| -> Result<Option<String>, Failure> {
        dictionary
            .get(key)
            .map(|v| resolve(document, v).map(|v| v.as_string().map(|s| s.to_text())))
            .transpose()
            .map(Option::flatten)
    };
    let supplement = dictionary
        .get("Supplement")
        .map(|v| resolve(document, v).map(|v| v.as_integer()))
        .transpose()?
        .flatten();
    Ok(Some(
        json!({"registry":text("Registry")?,"ordering":text("Ordering")?,"supplement":supplement}),
    ))
}

fn inspect(bytes: &[u8], request: Request) -> Result<Value, Failure> {
    match request {
        Request::Collection { ordering, cids } => {
            if ordering.len() > 64 || cids.len() > 4096 {
                return Err(limit("Collection lookup exceeds bounds"));
            }
            let collection = AdobeCidCollection::from_ordering(&ordering);
            let mappings: Vec<_> = cids.into_iter().map(|cid| json!({
                "cid":cid, "unicode":collection.and_then(|c| c.cid_to_unicode_sequence(cid)),
                "unicode_scalar":collection.and_then(|c| c.cid_to_unicode(cid)).map(u32::from)
            })).collect();
            Ok(json!({"ordering":ordering,"supported":collection.is_some(),"mappings":mappings}))
        }
        Request::Inspect {
            page_index,
            resource_name,
            character_codes,
            cids,
            options,
            parser_options,
        } => {
            options.validate()?;
            if bytes.is_empty() || bytes.len() > 100 * 1024 * 1024 {
                return Err(invalid("PDF must contain 1 byte to 100 MiB"));
            }
            if resource_name.len() > 1024 {
                return Err(limit("Font resource name exceeds 1024 UTF-8 bytes"));
            }
            if character_codes.len() > options.max_code_bytes.div_ceil(3) * 4
                || cids.len() > options.max_cids
            {
                return Err(limit("Font query exceeds code/CID budget"));
            }
            let codes = STANDARD.decode(character_codes).map_err(invalid)?;
            if codes.len() > options.max_code_bytes {
                return Err(limit("Character codes exceed byte budget"));
            }
            let reader = PdfReader::new_with_options(
                Cursor::new(bytes),
                parser_options.core().map_err(invalid)?,
            )
            .map_err(pdf_error)?;
            let document = PdfDocument::new(reader);
            let page = document.get_page(page_index).map_err(pdf_error)?;
            let resources = document
                .get_page_resources(&page)
                .map_err(pdf_error)?
                .ok_or_else(|| (2, "Page has no font resources".into()))?;
            let fonts = resolve(
                &document,
                resources
                    .get("Font")
                    .ok_or_else(|| (2, "Page has no fonts".into()))?,
            )?;
            let font_object = fonts
                .as_dict()
                .ok_or_else(|| pdf_error("Font resources must be a dictionary"))?
                .get(&resource_name)
                .ok_or_else(|| (2, format!("Missing font /{resource_name}")))?;
            let dictionary = resolve(&document, font_object)?;
            let dictionary = dictionary
                .as_dict()
                .ok_or_else(|| pdf_error("Font must be a dictionary"))?;
            let is_composite = dictionary
                .get("Subtype")
                .and_then(PdfObject::as_name)
                .is_some_and(|n| n.0 == "Type0");
            let concrete;
            let mut cid_system_info = None;
            let metrics = if is_composite {
                let descendants = resolve(
                    &document,
                    dictionary
                        .get("DescendantFonts")
                        .ok_or_else(|| pdf_error("Missing DescendantFonts"))?,
                )?;
                let descendant = descendants
                    .as_array()
                    .and_then(|a| a.0.first())
                    .ok_or_else(|| pdf_error("Missing descendant font"))?;
                concrete = resolve(&document, descendant)?;
                cid_system_info = system_info(
                    &document,
                    concrete
                        .as_dict()
                        .ok_or_else(|| pdf_error("Descendant must be a dictionary"))?,
                )?;
                Some(vertical(
                    &document,
                    concrete
                        .as_dict()
                        .ok_or_else(|| pdf_error("Descendant must be a dictionary"))?,
                    options.max_metric_entries,
                )?)
            } else {
                None
            };
            let work = (codes.len() + cids.len())
                .checked_mul(metrics.as_ref().map_or(1, |m| m.ranges.len() + 1))
                .ok_or_else(|| limit("Lookup work overflow"))?;
            if work > options.max_lookup_work {
                return Err(limit("Vertical range lookup work exceeds budget"));
            }
            // Core validates descendant subtype/encoding and resolves all mapping authorities.
            let font = ResolvedFontResource::resolve(&document, &resource_name, font_object)
                .map_err(pdf_error)?;
            let glyphs = font.decode_glyphs(&codes).map_err(pdf_error)?;
            let unicode_bytes = glyphs
                .iter()
                .try_fold(0usize, |n, g| {
                    n.checked_add(g.unicode.as_ref().map_or(0, String::len))
                })
                .ok_or_else(|| limit("Unicode output overflow"))?;
            if unicode_bytes > options.max_unicode_bytes {
                return Err(limit("Decoded Unicode exceeds output budget"));
            }
            let glyphs: Vec<_> = glyphs.into_iter().map(|g| json!({"source_code":STANDARD.encode(g.source_code),"cid":g.cid,"glyph_id":g.gid,"unicode":g.unicode,"advance":g.advance})).collect();
            let advances: Vec<_> = cids
                .into_iter()
                .map(|cid| json!({"cid":cid,"advance":metrics.as_ref().map(|m| m.advance(cid))}))
                .collect();
            let subtype = match font.subtype {
                FontSubtype::Type1 => "type1",
                FontSubtype::TrueType => "true_type",
                FontSubtype::CidFontType0 => "cid_font_type0",
                FontSubtype::CidFontType2 => "cid_font_type2",
                FontSubtype::Type3 => "type3",
            };
            Ok(
                json!({"resource_name":resource_name,"subtype":subtype,"writing_mode":if font.writing_mode==WritingMode::Vertical {1}else{0},"glyphs":glyphs,"cid_system_info":cid_system_info,"vertical_metrics":metrics,"vertical_advances":advances}),
            )
        }
    }
}

/// Inspect one page font or query a known Adobe CID collection. Output JSON is
/// caller-owned and released with oxidize_free_string. Semantic errors are typed
/// JSON envelopes; invalid FFI pointers/UTF-8 use ordinary ABI error codes.
///
/// # Safety
/// Non-null PDF pointers must cover pdf_len readable bytes; a null PDF is allowed
/// only with zero length (collection lookup). Request is NUL-terminated UTF-8.
/// Output is writable, aligned, non-overlapping and valid for the full call.
#[no_mangle]
pub unsafe extern "C" fn oxidize_font_inspect(
    pdf: *const u8,
    pdf_len: usize,
    request: *const c_char,
    output: *mut *mut c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if !output.is_null() {
            *output = ptr::null_mut();
        }
        if output.is_null() || request.is_null() || (pdf.is_null() && pdf_len != 0) {
            return ErrorCode::NullPointer as c_int;
        }
        if pdf_len > isize::MAX as usize {
            return ErrorCode::InvalidArgument as c_int;
        }
        let request = match CStr::from_ptr(request).to_str() {
            Ok(s) => s,
            Err(e) => {
                set_last_error(e.to_string());
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };
        let bytes = if pdf_len == 0 {
            &[]
        } else {
            std::slice::from_raw_parts(pdf, pdf_len)
        };
        let result = serde_json::from_str(request)
            .map_err(invalid)
            .and_then(|request| inspect(bytes, request));
        let envelope = match result {
            Ok(result) => json!({"result":result}),
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
    use oxidize_pdf::parser::objects::PdfArray;

    fn call(request: &str) -> Value {
        let request = CString::new(request).unwrap();
        let mut output = ptr::dangling_mut();
        let status = unsafe { oxidize_font_inspect(ptr::null(), 0, request.as_ptr(), &mut output) };
        assert_eq!(status, 0);
        assert!(!output.is_null());
        let value = unsafe { serde_json::from_slice(CStr::from_ptr(output).to_bytes()).unwrap() };
        unsafe { crate::oxidize_free_string(output) };
        value
    }

    #[test]
    fn collection_lookup_preserves_sequences_missing_maps_and_typed_failures() {
        let result = call(r#"{"action":"Collection","ordering":"Japan1","cids":[1133,0,65535]}"#);
        assert_eq!(
            result["result"]["mappings"][0]["unicode"],
            "\u{9022}\u{e0100}"
        );
        assert!(result["result"]["mappings"][0]["unicode_scalar"].is_null());
        assert!(result["result"]["mappings"][1]["unicode"].is_null());
        assert!(result["result"]["mappings"][2]["unicode"].is_null());
        for request in [
            "{}",
            "[",
            r#"{"action":"Collection","ordering":"Japan1","cids":[65536]}"#,
            r#"{"action":"Collection","ordering":"Japan1","cids":[],"unexpected":true}"#,
        ] {
            assert_eq!(call(request)["error"]["kind"], 0);
        }
        let request = json!({"action":"Collection","ordering":"Japan1","cids":vec![1;4097]});
        assert_eq!(call(&request.to_string())["error"]["kind"], 3);
    }

    #[test]
    fn ffi_pointer_length_and_utf8_failures_clear_writable_output() {
        let request = CString::new("{}").unwrap();
        let mut output = ptr::dangling_mut();
        unsafe {
            assert_eq!(
                oxidize_font_inspect(ptr::null(), 0, ptr::null(), &mut output),
                ErrorCode::NullPointer as c_int
            );
            assert!(output.is_null());
            output = ptr::dangling_mut();
            assert_eq!(
                oxidize_font_inspect(b"x".as_ptr(), usize::MAX, request.as_ptr(), &mut output),
                ErrorCode::InvalidArgument as c_int
            );
            assert!(output.is_null());
            assert_eq!(
                oxidize_font_inspect(ptr::null(), 1, request.as_ptr(), &mut output),
                ErrorCode::NullPointer as c_int
            );
            assert!(output.is_null());
            let invalid = [255u8, 0];
            assert_eq!(
                oxidize_font_inspect(ptr::null(), 0, invalid.as_ptr().cast(), &mut output),
                ErrorCode::InvalidUtf8 as c_int
            );
            assert!(output.is_null());
            assert_eq!(
                oxidize_font_inspect(ptr::null(), 0, request.as_ptr(), ptr::null_mut()),
                ErrorCode::NullPointer as c_int
            );
        }
    }

    #[test]
    fn vertical_parser_bounds_duplicates_and_rejects_nonfinite_values() {
        let mut source = oxidize_pdf::Document::new();
        source.add_page(oxidize_pdf::Page::a4());
        let bytes = source.to_bytes().unwrap();
        let doc = PdfDocument::new(PdfReader::new(Cursor::new(bytes.as_slice())).unwrap());
        let ints = |values: &[i64]| {
            PdfObject::Array(PdfArray(
                values.iter().copied().map(PdfObject::Integer).collect(),
            ))
        };
        let mut dict = PdfDictionary::new();
        dict.insert("W2".into(), ints(&[0, 65535, -777, 0, 0]));
        let table = vertical(&doc, &dict, 1).unwrap();
        assert_eq!(table.ranges.len(), 1);
        assert!(table.overrides.is_empty());
        assert_eq!(table.advance(65535), -777.0);
        assert_eq!(table.advance(65536), -1000.0);
        dict.insert("W2".into(), ints(&[1, 1, -700, 0, 0, 1, 1, -800, 0, 0]));
        assert_eq!(vertical(&doc, &dict, 1).err().unwrap().0, 3);
        dict.insert(
            "DW2".into(),
            PdfObject::Array(PdfArray(vec![
                PdfObject::Integer(880),
                PdfObject::Real(f64::NAN),
            ])),
        );
        assert_eq!(vertical(&doc, &dict, 10).err().unwrap().0, 1);
    }
}
