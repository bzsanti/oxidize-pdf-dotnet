//! Optional core page executor and atomic writer, separate from batch scheduling.
use crate::{clear_last_error, set_last_error};
use std::os::raw::{c_char, c_int};

/// Capability bits of the loaded library: performance=1, Rayon=2.
#[no_mangle]
pub extern "C" fn oxidize_generation_capabilities() -> u32 {
    u32::from(cfg!(feature = "performance")) | (u32::from(cfg!(feature = "rayon")) << 1)
}

/// Clone a page for ownership transfer from a callback.
/// # Safety
/// The page must remain alive and unmodified during this call. The returned
/// pointer is owned and must be consumed by generation or oxidize_page_free.
#[no_mangle]
pub unsafe extern "C" fn oxidize_generation_clone_page(
    page: *const crate::page::PageHandle,
) -> *mut crate::page::PageHandle {
    crate::ffi_guard_ptr(|| {
        if page.is_null() {
            set_last_error("Null generation page");
            return std::ptr::null_mut();
        }
        Box::into_raw(Box::new(crate::page::PageHandle {
            inner: (*page).inner.clone(),
        }))
    })
}

type Render = extern "C" fn(u32) -> *mut crate::page::PageHandle;

/// Opaque owner of generated pages and reusable output buffers.
pub struct GenerationHandle {
    #[cfg(feature = "performance")]
    inner: enabled::Generated,
}

/// Generate owned pages without writing output.
/// # Safety
/// Input/output pointers must be valid. Callback must be thread-safe, never
/// unwind, and return a unique owned cloned PageHandle or null on failure.
/// All callbacks join before return. Free the returned owner exactly once.
#[no_mangle]
pub unsafe extern "C" fn oxidize_generation_create(
    input: *const u8,
    length: usize,
    render: Option<Render>,
    output: *mut *mut GenerationHandle,
) -> c_int {
    crate::ffi_guard(|| {
        clear_last_error();
        if output.is_null() {
            return 1;
        }
        *output = std::ptr::null_mut();
        if input.is_null() || render.is_none() {
            return 1;
        }
        if length == 0 || length > 64 * 1024 * 1024 {
            set_last_error("Generation request must be 1..67108864 bytes");
            return 9;
        }
        #[cfg(not(feature = "performance"))]
        {
            set_last_error("Performance generation is unavailable in this native library");
            11
        }
        #[cfg(feature = "performance")]
        {
            match enabled::create(std::slice::from_raw_parts(input, length), render.unwrap()) {
                Ok(inner) => {
                    *output = Box::into_raw(Box::new(GenerationHandle { inner }));
                    0
                }
                Err(e) => e.report(),
            }
        }
    })
}

/// Release a generation owner.
/// # Safety
/// Handle must be null or a live owner returned by create, not used concurrently.
#[no_mangle]
pub unsafe extern "C" fn oxidize_generation_free(handle: *mut GenerationHandle) {
    crate::ffi_guard_unit(|| {
        if !handle.is_null() {
            drop(Box::from_raw(handle));
        }
    });
}

/// Save generated pages, retaining buffers for subsequent saves.
/// # Safety
/// Handle must be live and exclusively borrowed for this call. Path/output
/// buffers must be valid; path is length-delimited UTF-8, output is freed with
/// oxidize_free_string. Do not free the handle concurrently or share output paths.
#[no_mangle]
pub unsafe extern "C" fn oxidize_generation_save(
    handle: *mut GenerationHandle,
    path: *const u8,
    length: usize,
    output: *mut *mut c_char,
) -> c_int {
    crate::ffi_guard(|| {
        clear_last_error();
        if output.is_null() {
            return 1;
        }
        *output = std::ptr::null_mut();
        if handle.is_null() || path.is_null() {
            return 1;
        }
        if length == 0 || length > 1_048_576 {
            set_last_error("Invalid output path length");
            return 9;
        }
        let path = match std::str::from_utf8(std::slice::from_raw_parts(path, length)) {
            Ok(p) => p,
            Err(e) => {
                set_last_error(e.to_string());
                return 2;
            }
        };
        #[cfg(not(feature = "performance"))]
        {
            let _ = path;
            set_last_error("Performance generation unavailable");
            11
        }
        #[cfg(feature = "performance")]
        {
            match (*handle).inner.save(path) {
                Ok(value) => match std::ffi::CString::new(value) {
                    Ok(value) => {
                        *output = value.into_raw();
                        0
                    }
                    Err(e) => {
                        set_last_error(e.to_string());
                        5
                    }
                },
                Err(e) => e.report(),
            }
        }
    })
}

#[cfg(feature = "performance")]
mod enabled {
    use super::Render;
    use crate::ErrorCode;
    use base64::Engine;
    use oxidize_pdf::performance::parallel_generation::{
        PageSpec, ParallelGenerationOptions, ParallelPageGenerator,
    };
    use oxidize_pdf::performance::{HighPerformanceDocument, PerformanceOptions};
    use serde::Deserialize;
    use std::collections::BTreeMap;

    pub(super) struct Failure {
        code: ErrorCode,
        message: String,
    }
    impl Failure {
        fn invalid(message: impl ToString) -> Self {
            Self {
                code: ErrorCode::InvalidArgument,
                message: message.to_string(),
            }
        }
        pub(super) fn report(self) -> i32 {
            crate::set_last_error(self.message);
            self.code as i32
        }
    }
    impl From<oxidize_pdf::PdfError> for Failure {
        fn from(error: oxidize_pdf::PdfError) -> Self {
            let code = match &error {
                oxidize_pdf::PdfError::Io(_) => ErrorCode::IoError,
                oxidize_pdf::PdfError::PermissionDenied(_) => ErrorCode::PermissionError,
                oxidize_pdf::PdfError::SerializationError(_) => ErrorCode::SerializationError,
                _ => ErrorCode::InvalidArgument,
            };
            Self {
                code,
                message: error.to_string(),
            }
        }
    }
    pub(super) struct Generated {
        doc: HighPerformanceDocument,
        count: usize,
        parallel: bool,
        recorded_pages: usize,
        thread_usage: std::collections::HashMap<usize, usize>,
    }
    impl Generated {
        pub(super) fn save(&self, path: &str) -> Result<String, Failure> {
            self.doc.save(path)?;
            let pool = self.doc.performance_stats().memory_pool_stats;
            Ok(
                serde_json::json!({"Pages":self.count,"Mode":if self.parallel {2} else {1},
                "RecordedPages":self.recorded_pages,"ThreadUsage":self.thread_usage,
                "PoolRequests":pool.total_requests,"PoolHits":pool.pool_hits})
                .to_string(),
            )
        }
    }

    #[derive(Deserialize)]
    #[serde(rename_all = "PascalCase", deny_unknown_fields)]
    struct Page {
        index: u32,
        width: f64,
        height: f64,
    }
    #[derive(Deserialize)]
    #[serde(rename_all = "PascalCase", deny_unknown_fields)]
    struct Options {
        mode: u8,
        max_threads: usize,
        chunk_size: usize,
        load_balancing: bool,
        memory_limit_per_callback: usize,
        stack_size: usize,
        thread_name_prefix: String,
        thread_priority: u8,
        streaming_writer: bool,
        stream_buffer_size: usize,
        compression: bool,
        memory_pooling: bool,
        memory_pool_size: usize,
        resource_deduplication: bool,
        collect_metrics: bool,
    }
    #[derive(Deserialize)]
    #[serde(rename_all = "PascalCase", deny_unknown_fields)]
    struct Request {
        pages: Vec<Page>,
        options: Options,
        fonts: BTreeMap<String, String>,
    }

    pub(super) fn create(input: &[u8], render: Render) -> Result<Generated, Failure> {
        let r: Request = serde_json::from_slice(input).map_err(Failure::invalid)?;
        if r.pages.len() > u32::MAX as usize || r.options.mode > 2 || r.options.thread_priority > 2
        {
            return Err(Failure::invalid("Invalid generation count or mode"));
        }
        let parallel = match r.options.mode {
            0 => cfg!(feature = "rayon"),
            1 => false,
            _ if cfg!(feature = "rayon") => true,
            _ => {
                return Err(Failure {
                    code: ErrorCode::UnsupportedFeature,
                    message: "Rayon generation is unavailable in this native library".into(),
                })
            }
        };
        let o = r.options;
        if o.stack_size < 2 * 1024 * 1024 {
            return Err(Failure::invalid(
                "StackSize must be at least 2097152 bytes for managed callbacks",
            ));
        }
        let mut doc = HighPerformanceDocument::new(PerformanceOptions {
            parallel_generation: parallel,
            max_threads: o.max_threads,
            resource_deduplication: o.resource_deduplication,
            streaming_writer: o.streaming_writer,
            stream_buffer_size: o.stream_buffer_size,
            intelligent_compression: o.compression,
            memory_pooling: o.memory_pooling,
            memory_pool_size: o.memory_pool_size,
            collect_metrics: o.collect_metrics,
        })
        .map_err(Failure::from)?;
        for (name, bytes) in r.fonts {
            let bytes = base64::engine::general_purpose::STANDARD
                .decode(bytes)
                .map_err(Failure::invalid)?;
            doc.add_font_from_bytes(name, bytes)
                .map_err(Failure::from)?;
        }
        let options = ParallelGenerationOptions {
            max_threads: o.max_threads,
            chunk_size: o.chunk_size,
            load_balancing: o.load_balancing,
            max_memory_per_thread: o.memory_limit_per_callback,
            progress_reporting: o.collect_metrics,
            thread_pool_config: oxidize_pdf::performance::parallel_generation::ThreadPoolConfig {
                stack_size: o.stack_size,
                thread_name_prefix: o.thread_name_prefix,
                thread_priority: match o.thread_priority {
                    0 => oxidize_pdf::performance::parallel_generation::ThreadPriority::Low,
                    1 => oxidize_pdf::performance::parallel_generation::ThreadPriority::Normal,
                    _ => oxidize_pdf::performance::parallel_generation::ThreadPriority::High,
                },
            },
        };
        let generator = if parallel {
            ParallelPageGenerator::new(options)
        } else {
            ParallelPageGenerator::new_sequential(options)
        }
        .map_err(Failure::from)?;
        // Ordinal callback identity allows duplicate caller page indices.
        let specs = r
            .pages
            .iter()
            .enumerate()
            .map(|(i, p)| {
                let _ = p.index;
                PageSpec::new(i as u32, p.width, p.height)
            })
            .collect();
        let pages = generator
            .generate_pdf_pages(specs, |spec| {
                let pointer = render(spec.index);
                if pointer.is_null() {
                    return Err(oxidize_pdf::PdfError::InvalidOperation(
                        "Managed page callback failed".into(),
                    ));
                }
                // Callback transfers a unique clone allocated by the matching native library.
                Ok(unsafe { Box::from_raw(pointer) }.inner)
            })
            .map_err(Failure::from)?;
        let count = pages.len();
        for page in pages {
            doc.add_pdf_page(page).map_err(Failure::from)?;
        }
        let stats = generator.stats();
        Ok(Generated {
            doc,
            count,
            parallel,
            recorded_pages: stats.pages_completed,
            thread_usage: stats.thread_usage,
        })
    }
}
