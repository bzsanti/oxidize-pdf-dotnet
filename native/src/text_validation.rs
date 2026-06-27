//! TXT-016 — Text validation / key-info extraction.
//!
//! Bridges `oxidize_pdf::text::TextValidator`. NOTE on scope: the upstream
//! `text/validation.rs` is a *text-content* validator — it finds dates,
//! monetary amounts, contract numbers and party names in already-extracted
//! text — NOT a PDF-structure integrity checker. The .NET caller passes a text
//! string (e.g. the output of `ExtractTextAsync`) and receives structured
//! matches as JSON.

use std::ffi::{CStr, CString};
use std::os::raw::{c_char, c_int};

use serde::Serialize;

use oxidize_pdf::text::{MatchType, TextValidationResult, TextValidator};

use crate::{clear_last_error, set_last_error, ErrorCode};

#[derive(Serialize)]
struct TextMatchDto {
    text: String,
    position: usize,
    length: usize,
    confidence: f64,
    match_type: String,
}

#[derive(Serialize)]
struct TextValidationResultDto {
    found: bool,
    confidence: f64,
    matches: Vec<TextMatchDto>,
    metadata: std::collections::HashMap<String, String>,
}

fn match_type_name(mt: &MatchType) -> String {
    match mt {
        MatchType::Date => "date".to_string(),
        MatchType::ContractNumber => "contractNumber".to_string(),
        MatchType::PartyName => "partyName".to_string(),
        MatchType::MonetaryAmount => "monetaryAmount".to_string(),
        MatchType::Location => "location".to_string(),
        MatchType::Custom(s) => format!("custom:{s}"),
    }
}

fn to_dto(result: TextValidationResult) -> TextValidationResultDto {
    TextValidationResultDto {
        found: result.found,
        confidence: result.confidence,
        matches: result
            .matches
            .into_iter()
            .map(|m| TextMatchDto {
                text: m.text,
                position: m.position,
                length: m.length,
                confidence: m.confidence,
                match_type: match_type_name(&m.match_type),
            })
            .collect(),
        metadata: result.metadata,
    }
}

unsafe fn emit_json<T: Serialize>(value: &T, out_json: *mut *mut c_char, what: &str) -> c_int {
    let json = match serde_json::to_string(value) {
        Ok(s) => s,
        Err(e) => {
            set_last_error(format!("Failed to serialize {what}: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };
    match CString::new(json) {
        Ok(c) => {
            *out_json = c.into_raw();
            ErrorCode::Success as c_int
        }
        Err(e) => {
            set_last_error(format!("{what} contains null bytes: {e}"));
            ErrorCode::InvalidUtf8 as c_int
        }
    }
}

/// TXT-016 — Validate contract-style text, returning matched dates, amounts,
/// contract numbers and party names as a JSON `TextValidationResult`.
///
/// The returned string must be freed with `oxidize_free_string`.
///
/// # Safety
/// - `text` must be a valid non-null, null-terminated UTF-8 C string.
/// - `out_json` must be a writeable `*mut *mut c_char`. Set to null on error.
#[no_mangle]
pub unsafe extern "C" fn oxidize_text_validate_contract(
    text: *const c_char,
    out_json: *mut *mut c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if text.is_null() || out_json.is_null() {
            set_last_error("Null pointer provided to oxidize_text_validate_contract");
            return ErrorCode::NullPointer as c_int;
        }
        *out_json = std::ptr::null_mut();
        let text_str = match CStr::from_ptr(text).to_str() {
            Ok(s) => s,
            Err(_) => {
                set_last_error("Invalid UTF-8 in validation text");
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };
        let validator = TextValidator::new();
        let result = validator.validate_contract_text(text_str);
        emit_json(&to_dto(result), out_json, "validation result")
    })
}

/// TXT-016 — Search `text` for a target string, returning matches as a JSON
/// `TextValidationResult`.
///
/// The returned string must be freed with `oxidize_free_string`.
///
/// # Safety
/// - `text` and `target` must be valid non-null, null-terminated UTF-8 C strings.
/// - `out_json` must be a writeable `*mut *mut c_char`. Set to null on error.
#[no_mangle]
pub unsafe extern "C" fn oxidize_text_search_target(
    text: *const c_char,
    target: *const c_char,
    out_json: *mut *mut c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if text.is_null() || target.is_null() || out_json.is_null() {
            set_last_error("Null pointer provided to oxidize_text_search_target");
            return ErrorCode::NullPointer as c_int;
        }
        *out_json = std::ptr::null_mut();
        let text_str = match CStr::from_ptr(text).to_str() {
            Ok(s) => s,
            Err(_) => {
                set_last_error("Invalid UTF-8 in search text");
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };
        let target_str = match CStr::from_ptr(target).to_str() {
            Ok(s) => s,
            Err(_) => {
                set_last_error("Invalid UTF-8 in search target");
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };
        let validator = TextValidator::new();
        let result = validator.search_for_target(text_str, target_str);
        emit_json(&to_dto(result), out_json, "search result")
    })
}

/// TXT-016 — Extract key information (dates, monetary amounts, organizations,
/// …) from `text` as a JSON object mapping category → list of strings.
///
/// The returned string must be freed with `oxidize_free_string`.
///
/// # Safety
/// - `text` must be a valid non-null, null-terminated UTF-8 C string.
/// - `out_json` must be a writeable `*mut *mut c_char`. Set to null on error.
#[no_mangle]
pub unsafe extern "C" fn oxidize_text_extract_key_info(
    text: *const c_char,
    out_json: *mut *mut c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if text.is_null() || out_json.is_null() {
            set_last_error("Null pointer provided to oxidize_text_extract_key_info");
            return ErrorCode::NullPointer as c_int;
        }
        *out_json = std::ptr::null_mut();
        let text_str = match CStr::from_ptr(text).to_str() {
            Ok(s) => s,
            Err(_) => {
                set_last_error("Invalid UTF-8 in extraction text");
                return ErrorCode::InvalidUtf8 as c_int;
            }
        };
        let validator = TextValidator::new();
        let info = validator.extract_key_info(text_str);
        emit_json(&info, out_json, "key info")
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::oxidize_free_string;

    unsafe fn run(text: &str) -> String {
        let ctext = CString::new(text).unwrap();
        let mut out: *mut c_char = std::ptr::null_mut();
        let rc = oxidize_text_validate_contract(ctext.as_ptr(), &mut out);
        assert_eq!(rc, 0);
        assert!(!out.is_null());
        let json = CStr::from_ptr(out).to_str().unwrap().to_string();
        oxidize_free_string(out);
        json
    }

    #[test]
    fn validate_contract_finds_date_and_amount() {
        unsafe {
            let json = run(
                "This agreement was signed on 30 September 2016 for $1,000,000 between ABC Corp and XYZ LLC.",
            );
            // Behavioral: real matches with their type and the matched substring.
            assert!(
                json.contains("\"found\":true"),
                "must report found; got {json}"
            );
            assert!(json.contains("date"), "must classify the date; got {json}");
            assert!(
                json.contains("monetaryAmount"),
                "must classify the amount; got {json}"
            );
        }
    }

    #[test]
    fn extract_key_info_groups_dates_and_amounts() {
        unsafe {
            let ctext = CString::new(
                "Agreement between ABC Corp for $1,000,000 signed on 30 September 2016.",
            )
            .unwrap();
            let mut out: *mut c_char = std::ptr::null_mut();
            assert_eq!(oxidize_text_extract_key_info(ctext.as_ptr(), &mut out), 0);
            let json = CStr::from_ptr(out).to_str().unwrap().to_string();
            oxidize_free_string(out);
            assert!(json.contains("dates"), "must group dates; got {json}");
            assert!(
                json.contains("monetary_amounts"),
                "must group monetary amounts; got {json}"
            );
        }
    }

    #[test]
    fn validate_contract_null_returns_null_pointer() {
        unsafe {
            let mut out: *mut c_char = std::ptr::null_mut();
            assert_eq!(
                oxidize_text_validate_contract(std::ptr::null(), &mut out),
                1
            );
        }
    }
}
