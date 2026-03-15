use std::ffi::CStr;
use std::os::raw::{c_char, c_int};

use crate::document::DocumentHandle;
use crate::{clear_last_error, set_last_error, ErrorCode};

/// Encrypt a document with user and owner passwords using default permissions.
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `user_pw` and `owner_pw` must be valid null-terminated UTF-8 strings.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_encrypt(
    handle: *mut DocumentHandle,
    user_pw: *const c_char,
    owner_pw: *const c_char,
) -> c_int {
    clear_last_error();
    if handle.is_null() || user_pw.is_null() || owner_pw.is_null() {
        set_last_error("Null pointer provided to oxidize_document_encrypt");
        return ErrorCode::NullPointer as c_int;
    }

    let user = match CStr::from_ptr(user_pw).to_str() {
        Ok(s) => s,
        Err(_) => {
            set_last_error("Invalid UTF-8 in user_pw");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    let owner = match CStr::from_ptr(owner_pw).to_str() {
        Ok(s) => s,
        Err(_) => {
            set_last_error("Invalid UTF-8 in owner_pw");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    (*handle).inner.encrypt_with_passwords(user, owner);
    ErrorCode::Success as c_int
}

/// Encrypt a document with user and owner passwords and explicit permission flags.
///
/// Permission bit flags:
/// - Bit 0 (0x01): print
/// - Bit 1 (0x02): copy
/// - Bit 2 (0x04): modify_contents
/// - Bit 3 (0x08): modify_annotations
/// - Bit 4 (0x10): fill_forms
/// - Bit 5 (0x20): accessibility
/// - Bit 6 (0x40): assemble
/// - Bit 7 (0x80): print_high_quality
///
/// # Safety
/// - `handle` must be a valid pointer returned by `oxidize_document_create`.
/// - `user_pw` and `owner_pw` must be valid null-terminated UTF-8 strings.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_encrypt_with_permissions(
    handle: *mut DocumentHandle,
    user_pw: *const c_char,
    owner_pw: *const c_char,
    permissions_flags: u32,
) -> c_int {
    clear_last_error();
    if handle.is_null() || user_pw.is_null() || owner_pw.is_null() {
        set_last_error("Null pointer provided to oxidize_document_encrypt_with_permissions");
        return ErrorCode::NullPointer as c_int;
    }

    let user = match CStr::from_ptr(user_pw).to_str() {
        Ok(s) => s,
        Err(_) => {
            set_last_error("Invalid UTF-8 in user_pw");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    let owner = match CStr::from_ptr(owner_pw).to_str() {
        Ok(s) => s,
        Err(_) => {
            set_last_error("Invalid UTF-8 in owner_pw");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    let mut perms = oxidize_pdf::encryption::Permissions::new();
    perms.set_print((permissions_flags & 0x01) != 0);
    perms.set_copy((permissions_flags & 0x02) != 0);
    perms.set_modify_contents((permissions_flags & 0x04) != 0);
    perms.set_modify_annotations((permissions_flags & 0x08) != 0);
    perms.set_fill_forms((permissions_flags & 0x10) != 0);
    perms.set_accessibility((permissions_flags & 0x20) != 0);
    perms.set_assemble((permissions_flags & 0x40) != 0);
    perms.set_print_high_quality((permissions_flags & 0x80) != 0);

    let enc = oxidize_pdf::document::DocumentEncryption::new(
        user,
        owner,
        perms,
        oxidize_pdf::document::EncryptionStrength::Rc4_128bit,
    );
    (*handle).inner.set_encryption(enc);

    ErrorCode::Success as c_int
}
