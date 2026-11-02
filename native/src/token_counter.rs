//! Token-counter selection shared by standalone counting and both chunkers.
use crate::{clear_last_error, set_last_error, ErrorCode};
use oxidize_pdf::pipeline::{TokenCounter, WordProxyCounter};
use serde::Deserialize;
use std::sync::Arc;

#[derive(Clone, Copy, Debug, Default, Deserialize)]
pub enum CounterKind {
    #[default]
    WordProxy,
    Cl100kBase,
}

impl CounterKind {
    pub fn resolve(self) -> Result<Arc<dyn TokenCounter>, i32> {
        match self {
            Self::WordProxy => Ok(Arc::new(WordProxyCounter)),
            Self::Cl100kBase => {
                #[cfg(feature = "tiktoken")]
                {
                    use oxidize_pdf::pipeline::TiktokenCounter;
                    use std::sync::OnceLock;
                    static COUNTER: OnceLock<Arc<TiktokenCounter>> = OnceLock::new();
                    Ok(COUNTER
                        .get_or_init(|| Arc::new(TiktokenCounter::cl100k_base()))
                        .clone())
                }
                #[cfg(not(feature = "tiktoken"))]
                {
                    set_last_error("cl100k_base requires the native tiktoken feature");
                    Err(ErrorCode::UnsupportedFeature as i32)
                }
            }
        }
    }
}

/// Returns 1 if the counter (0=WordProxy, 1=Cl100kBase) is compiled in; 0 otherwise.
#[no_mangle]
pub extern "C" fn oxidize_token_counter_available(kind: u8) -> u8 {
    u8::from(kind == 0 || (kind == 1 && cfg!(feature = "tiktoken")))
}

/// Count UTF-8 bytes with the selected counter. Input is borrowed only for this
/// call; no native resource is retained. Empty input may have a null pointer.
/// Requests are limited to 16 MiB. Output is zero on error.
///
/// # Safety
/// Nonempty `text` must reference `length` readable bytes; `output` must reference
/// a writable usize and must not alias the input.
#[no_mangle]
pub unsafe extern "C" fn oxidize_count_tokens(
    text: *const u8,
    length: usize,
    kind: u8,
    output: *mut usize,
) -> i32 {
    crate::ffi_guard(|| {
        clear_last_error();
        if output.is_null() {
            set_last_error("Null token-count output");
            return ErrorCode::NullPointer as i32;
        }
        *output = 0;
        if length > 16 * 1024 * 1024 {
            set_last_error("Token-count input exceeds 16 MiB");
            return ErrorCode::InvalidArgument as i32;
        }
        if text.is_null() && length != 0 {
            set_last_error("Null token-count input");
            return ErrorCode::NullPointer as i32;
        }
        let kind = match kind {
            0 => CounterKind::WordProxy,
            1 => CounterKind::Cl100kBase,
            _ => {
                set_last_error("Unknown token counter");
                return ErrorCode::InvalidArgument as i32;
            }
        };
        let bytes = if length == 0 {
            &[]
        } else {
            std::slice::from_raw_parts(text, length)
        };
        let text = match std::str::from_utf8(bytes) {
            Ok(text) => text,
            Err(_) => {
                set_last_error("Invalid UTF-8 token-count input");
                return ErrorCode::InvalidUtf8 as i32;
            }
        };
        let counter = match kind.resolve() {
            Ok(c) => c,
            Err(code) => return code,
        };
        *output = counter.count(text);
        ErrorCode::Success as i32
    })
}
