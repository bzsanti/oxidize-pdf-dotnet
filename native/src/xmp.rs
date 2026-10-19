//! PDF/A XMP model plus bounded catalog metadata integration via core object editor.
use crate::{clear_last_error, set_last_error, ErrorCode};
use oxidize_pdf::parser::{
    objects::PdfStream, ParseOptions, PdfDictionary, PdfName, PdfObject, PdfReader,
};
use oxidize_pdf::pdfa::{PdfAConformance, XmpMetadata, XmpPdfAIdentifier};
use oxidize_pdf::writer::IncrementalObjectEditor;
use serde::{Deserialize, Serialize};
use serde_json::{json, Value};
use std::ffi::CString;
use std::io::Cursor;
use std::os::raw::{c_char, c_int};

const LIMIT: usize = 1_048_576;
#[derive(Deserialize, Serialize)]
enum Conformance {
    A,
    B,
    U,
}
#[derive(Deserialize, Serialize)]
#[serde(rename_all = "PascalCase", deny_unknown_fields)]
struct Identifier {
    part: u8,
    conformance: Conformance,
    amendment: Option<String>,
    corrigenda: Option<String>,
}
#[derive(Deserialize, Serialize)]
#[serde(rename_all = "PascalCase", deny_unknown_fields)]
struct Metadata {
    title: Option<String>,
    creator: Option<Vec<String>>,
    description: Option<String>,
    keywords: Option<Vec<String>>,
    create_date: Option<String>,
    modify_date: Option<String>,
    creator_tool: Option<String>,
    #[serde(rename = "PdfAIdentifier")]
    identifier: Option<Identifier>,
    document_id: Option<String>,
    instance_id: Option<String>,
}
impl Metadata {
    fn core(self) -> XmpMetadata {
        XmpMetadata {
            title: self.title,
            creator: self.creator,
            description: self.description,
            keywords: self.keywords,
            create_date: self.create_date,
            modify_date: self.modify_date,
            creator_tool: self.creator_tool,
            document_id: self.document_id,
            instance_id: self.instance_id,
            pdfa_id: self.identifier.map(|i| XmpPdfAIdentifier {
                part: i.part,
                conformance: match i.conformance {
                    Conformance::A => PdfAConformance::A,
                    Conformance::B => PdfAConformance::B,
                    Conformance::U => PdfAConformance::U,
                },
                amd: i.amendment,
                corr: i.corrigenda,
            }),
        }
    }
    fn from_core(m: XmpMetadata) -> Self {
        Self {
            title: m.title,
            creator: m.creator,
            description: m.description,
            keywords: m.keywords,
            create_date: m.create_date,
            modify_date: m.modify_date,
            creator_tool: m.creator_tool,
            document_id: m.document_id,
            instance_id: m.instance_id,
            identifier: m.pdfa_id.map(|i| Identifier {
                part: i.part,
                conformance: match i.conformance {
                    PdfAConformance::A => Conformance::A,
                    PdfAConformance::B => Conformance::B,
                    PdfAConformance::U => Conformance::U,
                },
                amendment: i.amd,
                corrigenda: i.corr,
            }),
        }
    }
}
#[derive(Deserialize)]
#[serde(
    tag = "Operation",
    rename_all_fields = "PascalCase",
    deny_unknown_fields
)]
enum Request {
    Parse { xml: String },
    Serialize { metadata: Metadata },
    Validate { metadata: Metadata },
    IdentifierRdf { metadata: Metadata },
}
fn failure(code: ErrorCode, e: impl ToString) -> c_int {
    set_last_error(e.to_string());
    code as c_int
}
unsafe fn output_json(v: Value, output: *mut *mut c_char) -> c_int {
    match CString::new(v.to_string()) {
        Ok(s) => {
            *output = s.into_raw();
            0
        }
        Err(e) => failure(ErrorCode::SerializationError, e),
    }
}
/// Model operation; output is owned JSON, released with oxidize_free_string.
/// # Safety
/// Input contains length bytes; output is writable. Neither pointer is retained.
#[no_mangle]
pub unsafe extern "C" fn oxidize_xmp_model(
    input: *const u8,
    length: usize,
    output: *mut *mut c_char,
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
        if length == 0 || length > LIMIT {
            return failure(
                ErrorCode::InvalidArgument,
                "XMP request exceeds 1 MiB or is empty",
            );
        }
        let request =
            match serde_json::from_slice::<Request>(std::slice::from_raw_parts(input, length)) {
                Ok(v) => v,
                Err(e) => return failure(ErrorCode::InvalidArgument, e),
            };
        let result = match request {
            Request::Parse { xml } => match XmpMetadata::parse(&xml) {
                Ok(m) => json!(Metadata::from_core(m)),
                Err(e) => return failure(ErrorCode::InvalidArgument, e),
            },
            Request::Serialize { metadata } => json!(metadata.core().to_xml()),
            Request::Validate { metadata } => match metadata.core().validate_for_pdfa() {
                Ok(()) => json!({"IsValid":true,"Message":null}),
                Err(e) => json!({"IsValid":false,"Message":e.to_string()}),
            },
            Request::IdentifierRdf { metadata } => match metadata.core().pdfa_id {
                Some(i) => json!(i.to_rdf()),
                None => return failure(ErrorCode::InvalidArgument, "Missing PDF/A identifier"),
            },
        };
        if result.to_string().len() > 8 * LIMIT {
            return failure(ErrorCode::InvalidArgument, "XMP output exceeds 8 MiB");
        }
        output_json(result, output)
    })
}
/// Read catalog XMP with bounded decoding. Missing metadata yields JSON null.
/// # Safety
/// Pdf contains length bytes; output is writable. Neither pointer is retained.
#[no_mangle]
pub unsafe extern "C" fn oxidize_xmp_read(
    pdf: *const u8,
    length: usize,
    output: *mut *mut c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if output.is_null() {
            return 1;
        }
        *output = std::ptr::null_mut();
        if pdf.is_null() {
            return 1;
        }
        if length == 0 || length > 256 * LIMIT {
            return failure(ErrorCode::InvalidArgument, "PDF must be 1 byte..256 MiB");
        }
        let options = ParseOptions::strict();
        let mut reader = match PdfReader::new_with_options(
            Cursor::new(std::slice::from_raw_parts(pdf, length)),
            options.clone(),
        ) {
            Ok(r) => r,
            Err(e) => return failure(ErrorCode::PdfParseError, e),
        };
        if reader.is_encrypted() {
            return failure(
                ErrorCode::PermissionError,
                "XMP reading does not support encrypted PDFs",
            );
        }
        let entry = match reader.catalog() {
            Ok(c) => c.get("Metadata").cloned(),
            Err(e) => return failure(ErrorCode::PdfParseError, e),
        };
        let object = match entry {
            None | Some(PdfObject::Null) => return output_json(Value::Null, output),
            Some(PdfObject::Reference(n, g)) => match reader.get_object(n, g) {
                Ok(o) => o.clone(),
                Err(e) => return failure(ErrorCode::PdfParseError, e),
            },
            Some(o) => o,
        };
        let stream = match object.as_stream() {
            Some(s) => s,
            None => return failure(ErrorCode::PdfParseError, "Catalog Metadata is not a stream"),
        };
        let bytes = match stream.decode_with_limit(&options, LIMIT) {
            Ok(b) => b,
            Err(e) => return failure(ErrorCode::PdfParseError, e),
        };
        let xml = match String::from_utf8(bytes) {
            Ok(s) => s,
            Err(e) => return failure(ErrorCode::InvalidUtf8, e),
        };
        let metadata = match XmpMetadata::parse(&xml) {
            Ok(m) => m,
            Err(e) => return failure(ErrorCode::InvalidArgument, e),
        };
        output_json(
            json!({"Xml":xml,"Metadata":Metadata::from_core(metadata)}),
            output,
        )
    })
}
/// Replace catalog metadata through the core editor; Info is preserved, never synchronized.
/// # Safety
/// Pdf/input contain their stated lengths; both output slots are writable.
/// Free successful bytes with oxidize_free_bytes. No pointer is retained.
#[no_mangle]
pub unsafe extern "C" fn oxidize_xmp_write(
    pdf: *const u8,
    length: usize,
    input: *const u8,
    input_length: usize,
    output: *mut *mut u8,
    output_length: *mut usize,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if output.is_null() || output_length.is_null() {
            return 1;
        }
        *output = std::ptr::null_mut();
        *output_length = 0;
        if pdf.is_null() || input.is_null() {
            return 1;
        }
        if length == 0 || length > 256 * LIMIT || input_length == 0 || input_length > LIMIT {
            return failure(
                ErrorCode::InvalidArgument,
                "PDF/input exceeds XMP limits or is empty",
            );
        }
        let metadata = match serde_json::from_slice::<Metadata>(std::slice::from_raw_parts(
            input,
            input_length,
        )) {
            Ok(m) => m.core(),
            Err(e) => return failure(ErrorCode::InvalidArgument, e),
        };
        let xml = metadata.to_xml();
        if xml.len() > LIMIT {
            return failure(ErrorCode::InvalidArgument, "Serialized XMP exceeds 1 MiB");
        }
        if let Err(e) = XmpMetadata::parse(&xml) {
            return failure(ErrorCode::InvalidArgument, e);
        }
        let result = (|| -> Result<Vec<u8>, oxidize_pdf::PdfError> {
            let mut editor = IncrementalObjectEditor::new(std::slice::from_raw_parts(pdf, length))?;
            let id = editor.allocate_id()?;
            let mut dict = PdfDictionary::new();
            dict.insert("Type".into(), PdfObject::Name(PdfName("Metadata".into())));
            dict.insert("Subtype".into(), PdfObject::Name(PdfName("XML".into())));
            editor.replace(
                id,
                PdfObject::Stream(PdfStream {
                    dict,
                    data: xml.into_bytes(),
                }),
            )?;
            editor.set_catalog_entry("Metadata", PdfObject::Reference(id.0, id.1))?;
            editor.finish()
        })();
        match result {
            Ok(bytes) => {
                let mut bytes = bytes.into_boxed_slice();
                *output_length = bytes.len();
                *output = bytes.as_mut_ptr();
                std::mem::forget(bytes);
                0
            }
            Err(e) => failure(
                if matches!(e, oxidize_pdf::PdfError::PermissionDenied(_)) {
                    ErrorCode::PermissionError
                } else {
                    ErrorCode::PdfParseError
                },
                e,
            ),
        }
    })
}
