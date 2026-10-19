//! Seekable managed-stream adapters to core lazy and streaming documents.
use crate::{clear_last_error, set_last_error};
use base64::{engine::general_purpose::STANDARD, Engine};
use oxidize_pdf::{
    memory::{LazyDocument, MemoryOptions, UnsupportedMemoryOption},
    objects::ObjectId,
    parser::{PdfObject, PdfReader},
    streaming::{StreamingDocument, StreamingOptions},
};
use serde::Deserialize;
use serde_json::{json, Value};
use std::{
    ffi::CString,
    io::{self, Read, Seek, SeekFrom},
    os::raw::{c_char, c_int},
};

type ReadCallback = extern "C" fn(usize, *mut u8, usize) -> isize;
type SeekCallback = extern "C" fn(usize, i64, u8) -> i64;
pub struct CallbackReader {
    context: usize,
    read: ReadCallback,
    seek: SeekCallback,
}
impl Read for CallbackReader {
    fn read(&mut self, buffer: &mut [u8]) -> io::Result<usize> {
        let count = (self.read)(self.context, buffer.as_mut_ptr(), buffer.len());
        if count < 0 || count as usize > buffer.len() {
            Err(io::Error::other("Managed stream read failed"))
        } else {
            Ok(count as usize)
        }
    }
}
impl Seek for CallbackReader {
    fn seek(&mut self, position: SeekFrom) -> io::Result<u64> {
        let (offset, origin) = match position {
            SeekFrom::Start(n) => (
                i64::try_from(n).map_err(|_| io::Error::other("Seek offset exceeds Int64"))?,
                0,
            ),
            SeekFrom::Current(n) => (n, 1),
            SeekFrom::End(n) => (n, 2),
        };
        let pos = (self.seek)(self.context, offset, origin);
        if pos < 0 {
            Err(io::Error::other("Managed stream seek failed"))
        } else {
            Ok(pos as u64)
        }
    }
}
pub enum ReaderHandle {
    Lazy(Box<LazyDocument<CallbackReader>>),
    Streaming(Box<StreamingDocument<CallbackReader>>),
}
#[derive(Deserialize)]
#[serde(rename_all = "PascalCase", deny_unknown_fields)]
struct LazyOptions {
    lazy_loading: bool,
    memory_mapping: bool,
    cache_size: usize,
    streaming: bool,
    buffer_size: usize,
    mmap_threshold: usize,
}
#[derive(Deserialize)]
#[serde(rename_all = "PascalCase", deny_unknown_fields)]
struct StreamOptions {
    buffer_size: usize,
    page_cache_size: usize,
    max_content_stream_size: usize,
    progressive_hints: bool,
    memory_limit: usize,
}
fn lazy_options(o: &MemoryOptions) -> Value {
    json!({"LazyLoading":o.lazy_loading,"MemoryMapping":o.memory_mapping,"CacheSize":o.cache_size,"Streaming":o.streaming,"BufferSize":o.buffer_size,"MmapThreshold":o.mmap_threshold})
}
fn stream_options(o: &StreamingOptions) -> Value {
    json!({"BufferSize":o.buffer_size,"PageCacheSize":o.page_cache_size,"MaxContentStreamSize":o.max_content_stream_size,"ProgressiveHints":o.progressive_hints,"MemoryLimit":o.memory_limit})
}
fn fail(e: impl ToString) -> c_int {
    set_last_error(e.to_string());
    3
}
/// Create a reader. The caller retains callback context until reader_free returns.
/// # Safety
/// Callback pointers/context must remain valid and callbacks must never unwind. Input is
/// length readable bytes, output is writable. All use/free operations require exclusive access.
#[no_mangle]
pub unsafe extern "C" fn oxidize_reader_open(
    context: usize,
    read: Option<ReadCallback>,
    seek: Option<SeekCallback>,
    streaming: bool,
    input: *const u8,
    length: usize,
    output: *mut *mut ReaderHandle,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if output.is_null() {
            return 1;
        }
        *output = std::ptr::null_mut();
        if input.is_null() {
            return 1;
        }
        let (Some(read), Some(seek)) = (read, seek) else {
            return 1;
        };
        if length == 0 || length > 4096 {
            set_last_error("Reader options must be 1..4096 bytes");
            return 9;
        }
        let input = std::slice::from_raw_parts(input, length);
        let mut reader = CallbackReader {
            context,
            read,
            seek,
        };
        if let Err(error) = reader.seek(SeekFrom::Start(0)) {
            return fail(error);
        }
        let result = if streaming {
            let o = match serde_json::from_slice::<StreamOptions>(input) {
                Ok(o) => o,
                Err(e) => return fail(e),
            };
            if o.buffer_size > 1_048_576
                || o.page_cache_size > 1024
                || o.max_content_stream_size > 16_777_216
                || o.memory_limit > 268_435_456
            {
                set_last_error("Streaming option exceeds binding limit");
                return 9;
            }
            StreamingDocument::new(
                reader,
                StreamingOptions {
                    buffer_size: o.buffer_size,
                    page_cache_size: o.page_cache_size,
                    max_content_stream_size: o.max_content_stream_size,
                    progressive_hints: o.progressive_hints,
                    memory_limit: o.memory_limit,
                },
            )
            .map(|d| ReaderHandle::Streaming(Box::new(d)))
        } else {
            let o = match serde_json::from_slice::<LazyOptions>(input) {
                Ok(o) => o,
                Err(e) => return fail(e),
            };
            if o.buffer_size > 1_048_576 || o.cache_size > 65536 {
                set_last_error("Lazy option exceeds binding limit");
                return 9;
            }
            let reader = match PdfReader::new(reader) {
                Ok(r) => r,
                Err(e) => return fail(e),
            };
            LazyDocument::new(
                reader,
                MemoryOptions {
                    lazy_loading: o.lazy_loading,
                    memory_mapping: o.memory_mapping,
                    cache_size: o.cache_size,
                    streaming: o.streaming,
                    buffer_size: o.buffer_size,
                    mmap_threshold: o.mmap_threshold,
                },
            )
            .map(|d| ReaderHandle::Lazy(Box::new(d)))
        };
        match result {
            Ok(r) => {
                *output = Box::into_raw(Box::new(r));
                0
            }
            Err(e) => fail(e),
        }
    })
}
struct Budget {
    nodes: usize,
    bytes: usize,
}
fn object(o: &PdfObject, depth: usize, b: &mut Budget) -> Result<Value, String> {
    b.nodes += 1;
    if depth > 64 || b.nodes > 65536 {
        return Err("Object exceeds depth/node limit".into());
    }
    let v = match o {
        PdfObject::Null => json!({"Kind":"Null"}),
        PdfObject::Boolean(v) => json!({"Kind":"Boolean","Boolean":v}),
        PdfObject::Integer(v) => json!({"Kind":"Integer","Integer":v}),
        PdfObject::Real(v) => {
            if !v.is_finite() {
                return Err("Non-finite PDF real".into());
            }
            json!({"Kind":"Real","Real":v})
        }
        PdfObject::Name(v) => {
            b.bytes += v.0.len();
            json!({"Kind":"Name","Name":v.0})
        }
        PdfObject::String(v) => {
            b.bytes += v.as_bytes().len();
            if b.bytes > 8_388_608 {
                return Err("Object exceeds 8 MiB payload limit".into());
            }
            json!({"Kind":"String","Data":STANDARD.encode(v.as_bytes())})
        }
        PdfObject::Reference(n, g) => json!({"Kind":"Reference","Number":n,"Generation":g}),
        PdfObject::Array(a) => {
            json!({"Kind":"Array","Items":a.0.iter().map(|v|object(v,depth+1,b)).collect::<Result<Vec<_>,_>>()?})
        }
        PdfObject::Dictionary(d) => {
            let mut entries = serde_json::Map::new();
            for (k, v) in &d.0 {
                b.bytes += k.0.len();
                entries.insert(k.0.clone(), object(v, depth + 1, b)?);
            }
            json!({"Kind":"Dictionary","Entries":entries})
        }
        PdfObject::Stream(s) => {
            b.bytes += s.data.len();
            if b.bytes > 8_388_608 {
                return Err("Object exceeds 8 MiB payload limit".into());
            }
            let dict = object(&PdfObject::Dictionary(s.dict.clone()), depth + 1, b)?;
            json!({"Kind":"Stream","Entries":dict["Entries"],"Data":STANDARD.encode(&s.data)})
        }
    };
    if b.bytes > 8_388_608 {
        return Err("Object exceeds 8 MiB payload limit".into());
    }
    Ok(v)
}
/// Query a reader: 0 info, 1 lazy page, 2 lazy object, 3 preload, 4 clear, 5 next streamed page.
/// # Safety
/// Handle is live and exclusively borrowed; output is writable. Free JSON with oxidize_free_string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_reader_query(
    handle: *mut ReaderHandle,
    operation: u8,
    index: u32,
    generation: u16,
    output: *mut *mut c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if output.is_null() {
            return 1;
        }
        *output = std::ptr::null_mut();
        if handle.is_null() {
            return 1;
        }
        let mut budget = Budget { nodes: 0, bytes: 0 };
        let result = (|| -> Result<Value, String> {
            Ok(match (&mut *handle, operation) {
                (ReaderHandle::Lazy(d), 0) => {
                    let c = d.configuration();
                    let s = d.memory_stats();
                    json!({"PageCount":d.page_count(),"Requested":lazy_options(&c.requested),"Effective":lazy_options(&c.effective),"Unsupported":c.unsupported.iter().map(|v|match v{UnsupportedMemoryOption::MemoryMapping=>"MemoryMapping",UnsupportedMemoryOption::Streaming=>"Streaming"}).collect::<Vec<_>>(),"Stats":{"AllocatedBytes":s.allocated_bytes,"CachedObjects":s.cached_objects,"CacheHits":s.cache_hits,"CacheMisses":s.cache_misses,"LazyLoads":s.lazy_loads,"MappedRegions":s.mapped_regions}})
                }
                (ReaderHandle::Lazy(d), 1) => {
                    let p = d.get_page(index).map_err(|e| e.to_string())?;
                    json!({"Number":p.obj_ref.0,"Generation":p.obj_ref.1,"MediaBox":p.media_box,"CropBox":p.crop_box,"Rotation":p.rotation,"Dictionary":object(&PdfObject::Dictionary(p.dict),0,&mut budget)?,"InheritedResources":p.inherited_resources.map(|r|object(&PdfObject::Dictionary(r),0,&mut budget)).transpose()?,"Annotations":p.annotations.map(|a|object(&PdfObject::Array(a),0,&mut budget)).transpose()?})
                }
                (ReaderHandle::Lazy(d), 2) => object(
                    &*d.get_object(&ObjectId::new(index, generation))
                        .map_err(|e| e.to_string())?,
                    0,
                    &mut budget,
                )?,
                (ReaderHandle::Lazy(d), 3) => {
                    d.preload_page(index).map_err(|e| e.to_string())?;
                    Value::Null
                }
                (ReaderHandle::Lazy(d), 4) => {
                    d.clear_cache();
                    Value::Null
                }
                (ReaderHandle::Streaming(d), 0) => {
                    let c = d.configuration();
                    json!({"Requested":stream_options(&c.requested),"Effective":stream_options(&c.effective),"ProgressiveHintsUnsupported":c.progressive_hints_unsupported,"CachedPages":d.cached_pages(),"MemoryUsage":d.memory_usage()})
                }
                (ReaderHandle::Streaming(d), 4) => {
                    d.clear_cache();
                    Value::Null
                }
                (ReaderHandle::Streaming(d), 5) => {
                    match d.next_page().map_err(|e| e.to_string())? {
                        None => Value::Null,
                        Some(p) => {
                            let mut content = Vec::new();
                            p.process_content(|v| {
                                content.extend_from_slice(v);
                                Ok(())
                            })
                            .map_err(|e| e.to_string())?;
                            json!({"PageIndex":p.number(),"Width":p.width(),"Height":p.height(),"MediaBox":p.media_box(),"Text":p.extract_text_streaming().map_err(|e|e.to_string())?,"Content":STANDARD.encode(&content)})
                        }
                    }
                }
                _ => return Err("Operation not supported by this reader".into()),
            })
        })();
        match result {
            Ok(v) => match CString::new(v.to_string()) {
                Ok(s) => {
                    *output = s.into_raw();
                    0
                }
                Err(e) => fail(e),
            },
            Err(e) => fail(e),
        }
    })
}
/// # Safety
/// Handle is null or a live unique reader handle. No query/callback may be active.
#[no_mangle]
pub unsafe extern "C" fn oxidize_reader_free(handle: *mut ReaderHandle) {
    crate::ffi_guard_unit(move || {
        if !handle.is_null() {
            drop(Box::from_raw(handle));
        }
    })
}
