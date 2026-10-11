//! Application signature slots. Handwriting completion does not produce CMS.
use crate::{clear_last_error, set_last_error, ErrorCode};
use base64::Engine as _;
use oxidize_pdf::signatures as sig;
use serde::Deserialize;
use serde_json::{json, Value};
use std::ffi::{CStr, CString};
use std::os::raw::{c_char, c_int};

#[derive(Deserialize)]
#[serde(tag = "action", deny_unknown_fields)]
enum Request {
    Create {
        name: String,
        page: usize,
        rect: [f64; 4],
        metadata: String,
    },
    List {},
    Read {
        name: String,
    },
    Remove {
        name: String,
    },
    Draw {
        name: String,
        strokes: Vec<Vec<[f64; 2]>>,
        complete: bool,
        label: Option<String>,
    },
    Complete {
        name: String,
        strokes: Vec<Vec<[f64; 2]>>,
        complete: bool,
    },
}
fn slot(s: sig::SignatureSlot) -> Value {
    json!({"name":s.name,"metadata":s.metadata,"page_index":s.page_index,
        "rotation":s.rotation,"rect":[s.rect.left,s.rect.bottom,s.rect.right,s.rect.top],
        "completed":s.completed,"digitally_signed":s.digitally_signed})
}
fn dispatch(base: &[u8], request: Request) -> Result<Value, String> {
    let result = match request {
        Request::List {} => {
            return sig::list_signature_slots(base)
                .map(|s| Value::Array(s.into_iter().map(slot).collect()))
                .map_err(|e| e.to_string())
        }
        Request::Read { name } => {
            return sig::read_signature_slot(base, &name)
                .map(slot)
                .map_err(|e| e.to_string())
        }
        Request::Create {
            name,
            page,
            rect,
            metadata,
        } => sig::create_signature_slot(
            base,
            &name,
            page,
            sig::SignatureRect {
                left: rect[0],
                bottom: rect[1],
                right: rect[2],
                top: rect[3],
            },
            &metadata,
        ),
        Request::Remove { name } => sig::remove_signature_slot(base, &name),
        Request::Draw {
            name,
            strokes,
            complete,
            label,
        } => sig::draw_signature_slot(base, &name, &strokes, complete, label.as_deref()),
        Request::Complete {
            name,
            strokes,
            complete,
        } => sig::complete_signature_slot(base, &name, &strokes, complete),
    };
    result
        .map(|bytes| json!(base64::engine::general_purpose::STANDARD.encode(bytes)))
        .map_err(|e| e.to_string())
}
/// Execute a typed slot operation; output is JSON owned by the caller.
///
/// # Safety
/// Input bytes and NUL-terminated UTF-8 request must be readable for their lengths.
/// Output must be writable and freed using oxidize_free_string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_signature_slots(
    bytes: *const u8,
    len: usize,
    request: *const c_char,
    output: *mut *mut c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if output.is_null() {
            set_last_error("Null slot output");
            return ErrorCode::NullPointer as c_int;
        }
        *output = std::ptr::null_mut();
        if bytes.is_null() || request.is_null() {
            set_last_error("Null slot input");
            return ErrorCode::NullPointer as c_int;
        }
        let result = (|| -> Result<Value, String> {
            let request = serde_json::from_str(
                CStr::from_ptr(request)
                    .to_str()
                    .map_err(|e| e.to_string())?,
            )
            .map_err(|e| e.to_string())?;
            dispatch(std::slice::from_raw_parts(bytes, len), request)
        })();
        match result {
            Ok(value) => match CString::new(value.to_string()) {
                Ok(value) => {
                    *output = value.into_raw();
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
    fn null_input_clears_output_before_returning_error() {
        let request = CString::new(r#"{"action":"List"}"#).unwrap();
        let mut output = std::ptr::dangling_mut::<c_char>();
        let status =
            unsafe { oxidize_signature_slots(std::ptr::null(), 0, request.as_ptr(), &mut output) };
        assert_eq!(status, ErrorCode::NullPointer as c_int);
        assert!(output.is_null());
    }

    #[test]
    fn malformed_and_unknown_fields_fail_closed() {
        let bytes = b"%PDF-1.7";
        for request in [
            r#"{"action":"List","ignored":true}"#,
            r#"{"action":"Unknown"}"#,
            "{",
        ] {
            assert!(serde_json::from_str::<Request>(request).is_err());
            let request = CString::new(request).unwrap();
            let mut output = std::ptr::null_mut();
            let status = unsafe {
                oxidize_signature_slots(bytes.as_ptr(), bytes.len(), request.as_ptr(), &mut output)
            };
            assert_eq!(status, ErrorCode::InvalidArgument as c_int);
            assert!(output.is_null());
        }
    }
}
