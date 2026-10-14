//! Provider-neutral signing with immutable prepared state owned by a native handle.
use crate::pdf_value::PdfValue;
use crate::{clear_last_error, set_last_error, ErrorCode};
use base64::Engine as _;
use oxidize_pdf::signatures::*;
use serde::Deserialize;
use serde_json::{json, Value};
use std::ffi::{CStr, CString};
use std::os::raw::{c_char, c_int};

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Options {
    field_name: String,
    existing: bool,
    page_index: usize,
    widget_index: Option<usize>,
    rect: Option<[f64; 4]>,
    placeholder_bytes: usize,
    filter: String,
    sub_filter: String,
    reason: Option<String>,
    location: Option<String>,
    contact_info: Option<String>,
    signing_time: Option<String>,
    certification: Option<u8>,
    field_lock: Option<Lock>,
    additional_entries: std::collections::BTreeMap<String, PdfValue>,
    appearance: Option<Appearance>,
}
#[derive(Deserialize)]
#[serde(tag = "mode", content = "fields")]
enum Lock {
    All,
    Include(Vec<String>),
    Exclude(Vec<String>),
}
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Appearance {
    signer_name: Option<String>,
    signing_date: Option<String>,
    text: Vec<String>,
    watermark: Option<Watermark>,
}
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Watermark {
    width: u32,
    height: u32,
    rgb: String,
    opacity: f32,
}
impl Appearance {
    fn core(self) -> Result<SignatureAppearance, String> {
        Ok(SignatureAppearance {
            signer_name: self.signer_name,
            signing_date: self.signing_date,
            text: self.text,
            watermark: self
                .watermark
                .map(|w| {
                    Ok::<_, String>(SignatureWatermark {
                        width: w.width,
                        height: w.height,
                        opacity: w.opacity,
                        rgb: base64::engine::general_purpose::STANDARD
                            .decode(w.rgb)
                            .map_err(|e| e.to_string())?,
                    })
                })
                .transpose()?,
        })
    }
}
fn rect(r: [f64; 4]) -> SignatureRect {
    SignatureRect {
        left: r[0],
        bottom: r[1],
        right: r[2],
        top: r[3],
    }
}
fn layout_json(l: SignatureAppearanceLayout) -> Value {
    json!({"lines":l.lines,"font_size":l.font_size,"margin":l.margin,"first_baseline":l.first_baseline,"line_height":l.line_height})
}
fn prepare(bytes: &[u8], options: Options) -> Result<(PreparedSignature, Value), String> {
    if !options.existing && options.widget_index.is_some() {
        return Err("Widget index is only valid for existing fields".into());
    }
    if options.existing && options.page_index != 0 {
        return Err("Page index is only valid for new fields".into());
    }
    let mut core = SignaturePreparationOptions::invisible(&options.field_name);
    core.target = if options.existing {
        SignatureTarget::Existing {
            field_name: options.field_name,
            widget_index: options.widget_index,
            rect: options.rect.map(rect),
        }
    } else {
        SignatureTarget::New {
            field_name: options.field_name,
            page_index: options.page_index,
            rect: options.rect.map(rect),
        }
    };
    core.placeholder_bytes = options.placeholder_bytes;
    core.filter = options.filter;
    core.sub_filter = options.sub_filter;
    core.reason = options.reason;
    core.location = options.location;
    core.contact_info = options.contact_info;
    core.signing_time = options.signing_time;
    core.certification = options
        .certification
        .map(|p| match p {
            1 => Ok(CertificationPermission::NoChanges),
            2 => Ok(CertificationPermission::FormFillAndSign),
            3 => Ok(CertificationPermission::FormFillSignAndAnnotate),
            _ => Err("Invalid certification level"),
        })
        .transpose()?;
    core.field_lock = options.field_lock.map(|l| match l {
        Lock::All => FieldLock::All,
        Lock::Include(f) => FieldLock::Include(f),
        Lock::Exclude(f) => FieldLock::Exclude(f),
    });
    for (k, v) in options.additional_entries {
        core.additional_signature_entries.insert(k, v.core()?);
    }
    let appearance = options.appearance.map(Appearance::core).transpose()?;
    let prepared = match &appearance {
        Some(a) => prepare_incremental_signature_with_appearance(bytes, &core, a),
        None => prepare_incremental_signature(bytes, &core),
    }
    .map_err(|e| e.to_string())?;
    let layout = match (appearance, options.rect) {
        (Some(a), Some(r)) => Some(layout_json(a.layout(rect(r)).map_err(|e| e.to_string())?)),
        _ => None,
    };
    let result = json!({"prepared_pdf":base64::engine::general_purpose::STANDARD.encode(prepared.prepared_pdf()),
        "bytes_to_digest":base64::engine::general_purpose::STANDARD.encode(prepared.bytes_to_digest()),
        "byte_range":prepared.byte_range().ranges(),"layout":layout});
    Ok((prepared, result))
}
/// Prepare an immutable signing session; caller never supplies private keys.
///
/// # Safety
/// PDF and JSON pointers must be readable for their documented lengths; output
/// pointers must be writable. Free the handle and JSON with their respective exports.
#[no_mangle]
pub unsafe extern "C" fn oxidize_signature_prepare(
    bytes: *const u8,
    len: usize,
    options: *const c_char,
    out_handle: *mut *mut PreparedSignature,
    out_json: *mut *mut c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if bytes.is_null() || options.is_null() || out_handle.is_null() || out_json.is_null() {
            set_last_error("Null signing pointer");
            return ErrorCode::NullPointer as c_int;
        }
        *out_handle = std::ptr::null_mut();
        *out_json = std::ptr::null_mut();
        let result = (|| -> Result<_, String> {
            let input = CStr::from_ptr(options)
                .to_str()
                .map_err(|e| e.to_string())?;
            prepare(
                std::slice::from_raw_parts(bytes, len),
                serde_json::from_str(input).map_err(|e| e.to_string())?,
            )
        })();
        match result {
            Ok((handle, json)) => match CString::new(json.to_string()) {
                Ok(json) => {
                    *out_json = json.into_raw();
                    *out_handle = Box::into_raw(Box::new(handle));
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
/// Finalize a copy of prepared state, allowing retries after provider failures.
///
/// # Safety
/// Handle must be live; CMS readable for `len` bytes; outputs writable. Free bytes
/// with `oxidize_free_bytes`. The handle must not be freed concurrently.
#[no_mangle]
pub unsafe extern "C" fn oxidize_signature_finalize(
    handle: *const PreparedSignature,
    cms: *const u8,
    len: usize,
    out_bytes: *mut *mut u8,
    out_len: *mut usize,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if handle.is_null() || cms.is_null() || out_bytes.is_null() || out_len.is_null() {
            set_last_error("Null signing pointer");
            return ErrorCode::NullPointer as c_int;
        }
        *out_bytes = std::ptr::null_mut();
        *out_len = 0;
        match (*handle)
            .clone()
            .finalize(std::slice::from_raw_parts(cms, len))
        {
            Ok(bytes) => {
                let mut bytes = bytes.into_boxed_slice();
                *out_len = bytes.len();
                *out_bytes = bytes.as_mut_ptr();
                std::mem::forget(bytes);
                ErrorCode::Success as c_int
            }
            Err(e) => {
                set_last_error(e.to_string());
                ErrorCode::InvalidArgument as c_int
            }
        }
    })
}
/// Free prepared signature state.
///
/// # Safety
/// Handle must be null or a live handle returned by prepare, freed exactly once.
#[no_mangle]
pub unsafe extern "C" fn oxidize_signature_free(handle: *mut PreparedSignature) {
    crate::ffi_guard_unit(move || {
        if !handle.is_null() {
            drop(Box::from_raw(handle));
        }
    });
}
/// Preflight appearance layout independently of a PDF.
///
/// # Safety
/// Input is NUL-terminated UTF-8; output is writable and freed by oxidize_free_string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_signature_layout(
    input: *const c_char,
    out_json: *mut *mut c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if input.is_null() || out_json.is_null() {
            set_last_error("Null layout pointer");
            return ErrorCode::NullPointer as c_int;
        }
        *out_json = std::ptr::null_mut();
        #[derive(Deserialize)]
        struct Request {
            rect: [f64; 4],
            appearance: Appearance,
        }
        let result = (|| -> Result<_, String> {
            let r: Request =
                serde_json::from_str(CStr::from_ptr(input).to_str().map_err(|e| e.to_string())?)
                    .map_err(|e| e.to_string())?;
            r.appearance
                .core()?
                .layout(rect(r.rect))
                .map(layout_json)
                .map_err(|e| e.to_string())
        })();
        match result {
            Ok(v) => match CString::new(v.to_string()) {
                Ok(s) => {
                    *out_json = s.into_raw();
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
