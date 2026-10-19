//! Complete typed extraction/recovery settings transported privately as JSON.
use crate::{clear_last_error, set_last_error, ErrorCode};
use oxidize_pdf::parser::{
    encoding::EncodingType, filters::FlateRecoveryKind, ParseOptions, PdfDocument, PdfReader,
};
use oxidize_pdf::text::{
    CarriageReturnHandling, ExtractionOptions, RecoveredText, RecoveryLocation, TextExtractor,
    TextRecoveryAction,
};
use serde::Deserialize;
use serde_json::{json, Value};
use std::ffi::{CStr, CString};
use std::io::Cursor;
use std::os::raw::{c_char, c_int};

#[derive(Deserialize)]
#[serde(rename_all = "PascalCase", deny_unknown_fields)]
struct Settings {
    preserve_layout: bool,
    space_threshold: f64,
    newline_threshold: f64,
    sort_by_position: bool,
    detect_columns: bool,
    column_threshold: f64,
    merge_hyphenated: bool,
    tj_space_threshold: f64,
    reconstruct_paragraphs: bool,
    include_artifacts: bool,
    reorder_columns: bool,
    reading_order: bool,
    carriage_return_handling: u8,
    max_extracted_bytes: Option<usize>,
    include_link_annotations: bool,
    include_unreliable_figure_text: bool,
    normalize_non_breaking_spaces: bool,
    track_space_decisions: bool,
}
impl Settings {
    fn extractor(self) -> Result<TextExtractor, String> {
        let carriage = match self.carriage_return_handling {
            0 => CarriageReturnHandling::Remove,
            1 => CarriageReturnHandling::ReplaceWithSpace,
            2 => CarriageReturnHandling::NormalizeLineEnding,
            _ => return Err("Invalid carriage-return policy".into()),
        };
        Ok(TextExtractor::with_options(ExtractionOptions {
            preserve_layout: self.preserve_layout,
            space_threshold: self.space_threshold,
            newline_threshold: self.newline_threshold,
            sort_by_position: self.sort_by_position,
            detect_columns: self.detect_columns,
            column_threshold: self.column_threshold,
            merge_hyphenated: self.merge_hyphenated,
            tj_space_threshold: self.tj_space_threshold,
            reconstruct_paragraphs: self.reconstruct_paragraphs,
            include_artifacts: self.include_artifacts,
            reorder_columns: self.reorder_columns,
            track_space_decisions: self.track_space_decisions,
            max_extracted_bytes: self.max_extracted_bytes,
        })
        .with_reading_order(self.reading_order)
        .with_carriage_return_handling(carriage)
        .with_link_annotation_extraction(self.include_link_annotations)
        .with_unreliable_figure_text(self.include_unreliable_figure_text)
        .with_non_breaking_space_normalization(self.normalize_non_breaking_spaces))
    }
}
#[derive(Deserialize)]
#[serde(rename_all = "PascalCase", deny_unknown_fields)]
pub(crate) struct ParserSettings {
    strict_mode: bool,
    recover_from_stream_errors: bool,
    ignore_corrupt_streams: bool,
    partial_content_allowed: bool,
    max_recovery_attempts: usize,
    log_recovery_details: bool,
    lenient_streams: bool,
    max_recovery_bytes: usize,
    collect_warnings: bool,
    lenient_encoding: bool,
    preferred_encoding: Option<u8>,
    lenient_syntax: bool,
}
impl ParserSettings {
    pub(crate) fn core(&self) -> Result<ParseOptions, String> {
        let preferred_encoding = self
            .preferred_encoding
            .map(|v| match v {
                0 => Ok(EncodingType::Utf8),
                1 => Ok(EncodingType::Latin1),
                2 => Ok(EncodingType::Windows1252),
                3 => Ok(EncodingType::MacRoman),
                4 => Ok(EncodingType::PdfDocEncoding),
                5 => Ok(EncodingType::Mixed),
                _ => Err("Invalid preferred encoding".to_string()),
            })
            .transpose()?;
        Ok(ParseOptions {
            strict_mode: self.strict_mode,
            recover_from_stream_errors: self.recover_from_stream_errors,
            ignore_corrupt_streams: self.ignore_corrupt_streams,
            partial_content_allowed: self.partial_content_allowed,
            max_recovery_attempts: self.max_recovery_attempts,
            log_recovery_details: self.log_recovery_details,
            lenient_streams: self.lenient_streams,
            max_recovery_bytes: self.max_recovery_bytes,
            collect_warnings: self.collect_warnings,
            lenient_encoding: self.lenient_encoding,
            preferred_encoding,
            lenient_syntax: self.lenient_syntax,
        })
    }
}
#[derive(Deserialize)]
enum Action {
    Extract,
    Version,
}
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Request {
    action: Action,
    options: Settings,
    parser_options: ParserSettings,
    page_index: Option<u32>,
    max_stream_bytes: Option<usize>,
}

pub(crate) fn extracted(text: oxidize_pdf::text::ExtractedText) -> Value {
    let fragments:Vec<_>=text.fragments.into_iter().map(|f| {
 let color=f.color.map(|c|match c {
 oxidize_pdf::graphics::Color::Rgb(r,g,b)=>json!({"space":0,"components":[r,g,b]}),
 oxidize_pdf::graphics::Color::Gray(g)=>json!({"space":1,"components":[g]}),
 oxidize_pdf::graphics::Color::Cmyk(c,m,y,k)=>json!({"space":2,"components":[c,m,y,k]}),
 });
 let decisions:Vec<_>=f.space_decisions.into_iter().map(|d|json!({"offset":d.offset,"dx":d.dx,"threshold":d.threshold,"confidence":d.confidence,"inserted":d.inserted})).collect();
 json!({"text":f.text,"x":f.x,"y":f.y,"width":f.width,"height":f.height,"font_size":f.font_size,"font_name":f.font_name,"is_bold":f.is_bold,"is_italic":f.is_italic,"render_mode":f.render_mode as u8,"mcid":f.mcid,"struct_tag":f.struct_tag,"color":color,"space_decisions":decisions})
 }).collect();
    json!({"text":text.text,"truncated":text.truncated,"fragments":fragments})
}
fn recovered(r: RecoveredText) -> Result<Value, String> {
    let diagnostics=r.diagnostics.into_iter().map(|d| {
 let (location,contents_index,form_name,object_id)=match d.location {
 RecoveryLocation::PageContents(i)=>(0,Some(i),None,None),
 RecoveryLocation::FormXObject{name,object}=>(1,None,Some(name),Some(object)),
 _=>return Err("Unsupported recovery location".to_string()),
 };
 let (action,filter_index,kind,error)=match d.action {
 TextRecoveryAction::Recovered(f)=>{
 let kind=match f.kind {FlateRecoveryKind::Unverified=>0,FlateRecoveryKind::Incomplete=>1,_=>return Err("Unsupported recovery kind".into())};
 (0,Some(f.filter_index),Some(kind),f.error)},
 TextRecoveryAction::Omitted{error}=>(1,None,None,error),
 _=>return Err("Unsupported recovery action".into()),
 };
 Ok(json!({"location":location,"contents_index":contents_index,"form_name":form_name,"object_id":object_id,"action":action,"filter_index":filter_index,"kind":kind,"error":error}))
 }).collect::<Result<Vec<_>,String>>()?;
    Ok(json!({"page_index":r.page_index,"text":extracted(r.text),"diagnostics":diagnostics}))
}
fn dispatch(bytes: &[u8], request: Request) -> Result<Value, String> {
    let reader = PdfReader::new_with_options(Cursor::new(bytes), request.parser_options.core()?)
        .map_err(|e| e.to_string())?;
    let doc = PdfDocument::new(reader);
    if matches!(request.action, Action::Version) {
        return doc
            .effective_version()
            .map(|v| json!(v.to_string()))
            .map_err(|e| e.to_string());
    }
    let pages = match request.page_index {
        Some(i) => vec![i],
        None => (0..doc.page_count().map_err(|e| e.to_string())?).collect(),
    };
    let mut extractor = request.options.extractor()?;
    let mut result = Vec::with_capacity(pages.len());
    for page in pages {
        result.push(match request.max_stream_bytes {
 Some(limit)=>recovered(extractor.extract_from_page_with_recovery(&doc,page,limit).map_err(|e|e.to_string())?)?,
 None=>json!({"page_index":page,"text":extracted(extractor.extract_from_page(&doc,page).map_err(|e|e.to_string())?),"diagnostics":[]}),
 });
    }
    Ok(Value::Array(result))
}
/// Extract text with every option, including explicit bounded Flate recovery.
///
/// # Safety
/// Bytes and UTF-8 NUL-terminated request must be readable; output writable.
/// Caller frees output with oxidize_free_string. Buffer length must fit isize.
#[no_mangle]
pub unsafe extern "C" fn oxidize_text_extraction(
    bytes: *const u8,
    len: usize,
    request: *const c_char,
    output: *mut *mut c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if output.is_null() {
            set_last_error("Null extraction output");
            return ErrorCode::NullPointer as c_int;
        }
        *output = std::ptr::null_mut();
        if bytes.is_null() || request.is_null() {
            set_last_error("Null extraction input");
            return ErrorCode::NullPointer as c_int;
        }
        let result = (|| -> Result<Value, String> {
            let r = serde_json::from_str(
                CStr::from_ptr(request)
                    .to_str()
                    .map_err(|e| e.to_string())?,
            )
            .map_err(|e| e.to_string())?;
            dispatch(std::slice::from_raw_parts(bytes, len), r)
        })();
        match result {
            Ok(v) => match CString::new(v.to_string()) {
                Ok(v) => {
                    *output = v.into_raw();
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
    fn fragment_mapping_preserves_original_color_and_space_decisions() {
        use oxidize_pdf::graphics::Color;
        use oxidize_pdf::text::extraction::SpaceDecision;
        for (color, expected_space, expected_components) in [
            (Color::Rgb(0.2, 0.3, 0.4), 0, vec![0.2, 0.3, 0.4]),
            (Color::Gray(0.5), 1, vec![0.5]),
            (Color::Cmyk(0.1, 0.2, 0.3, 0.4), 2, vec![0.1, 0.2, 0.3, 0.4]),
        ] {
            let mut fragment =
                oxidize_pdf::text::TextFragment::new("A B", 10.0, 20.0, 30.0, 12.0, 12.0);
            fragment.color = Some(color);
            fragment.space_decisions.push(SpaceDecision {
                offset: 1,
                dx: 6.0,
                threshold: 3.0,
                confidence: 1.0,
                inserted: true,
            });
            let value = extracted(oxidize_pdf::text::ExtractedText::new(
                "A B".into(),
                vec![fragment],
            ));
            let result = &value["fragments"][0];
            assert_eq!(
                result["color"],
                json!({"space":expected_space,"components":expected_components})
            );
            assert_eq!(
                result["space_decisions"][0],
                json!({"offset":1,"dx":6.0,"threshold":3.0,"confidence":1.0,"inserted":true})
            );
        }
    }

    #[test]
    fn null_input_clears_output_and_does_not_dereference_bytes() {
        let request = CString::new("{}").unwrap();
        let mut output = std::ptr::dangling_mut::<c_char>();
        let status =
            unsafe { oxidize_text_extraction(std::ptr::null(), 0, request.as_ptr(), &mut output) };
        assert_eq!(status, ErrorCode::NullPointer as c_int);
        assert!(output.is_null());
    }
}
