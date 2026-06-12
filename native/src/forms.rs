//! FFI for the interactive-forms write-path (milestone M2): create AcroForm
//! fields (text, checkbox, radio, combobox, listbox, pushbutton) and attach
//! widget annotations to pages.
//!
//! These are write-path entry points: they mutate the `oxidize_pdf::Document`
//! behind a [`DocumentHandle`] and the `oxidize_pdf::Page` behind a
//! [`PageHandle`]. The result is observed by serializing the document to PDF
//! bytes (`oxidize_document_save_to_bytes_with_config`) and re-reading it with
//! the existing read-path (`oxidize_get_form_fields`).
//!
//! ## Call order (enforced by the API shape)
//! `oxidize_pdf::Document::add_page` **clones** the page into the document, so
//! a widget must be attached to a page *before* that page is added, and the
//! field's object reference must exist *before* the widget that links to it.
//! The natural sequence from a caller is therefore:
//!   1. `oxidize_document_add_form_field_json` → returns the field's object
//!      number (it calls `Document::enable_forms` internally, idempotently).
//!   2. `oxidize_page_add_form_widget_json(page, .., field_obj_num)` → places a
//!      widget annotation on the page, linked to the field via `/Parent`.
//!   3. `oxidize_document_add_page(doc, page)` → clones the page (with its
//!      widget) into the document.
//!
//! Complex inputs cross the boundary as JSON (serde), mirroring
//! `document_metadata.rs`.

use std::ffi::CStr;
use std::os::raw::{c_char, c_int};

use oxidize_pdf::forms::{
    CheckBox, ComboBox, FieldFlags, FieldOptions, ListBox, PushButton, RadioButton, TextField,
    Widget,
};
use oxidize_pdf::objects::ObjectReference;
use oxidize_pdf::writer::IncrementalFormFiller;
use oxidize_pdf::{Point, Rectangle};
use serde::Deserialize;

use crate::document::DocumentHandle;
use crate::page::PageHandle;
use crate::{clear_last_error, set_last_error, ErrorCode};

// ── JSON payloads ─────────────────────────────────────────────────────────────

/// A `[x1, y1, x2, y2]` widget rectangle in PDF points.
type RectArray = [f64; 4];

#[derive(Deserialize)]
struct OptionJson {
    /// Export value (the value stored in `/V` / the option's machine value).
    export: String,
    /// Human-visible label.
    label: String,
}

/// One tagged DTO for every field type. `kind` selects the branch; the
/// remaining fields are interpreted per branch (unused fields are ignored by
/// serde). The widget `rect` travels with the field so the
/// `FormManager::add_*(field, widget, options)` call is made atomically.
#[derive(Deserialize)]
struct CreateFieldJson {
    kind: String,
    name: String,
    rect: RectArray,

    // text / combobox
    #[serde(default)]
    value: Option<String>,
    #[serde(default)]
    default_value: Option<String>,
    #[serde(default)]
    max_length: Option<i32>,
    #[serde(default)]
    multiline: bool,
    #[serde(default)]
    password: bool,

    // checkbox
    #[serde(default)]
    checked: bool,
    #[serde(default)]
    export_value: Option<String>,

    // radio / combobox / listbox
    #[serde(default)]
    options: Vec<OptionJson>,
    #[serde(default)]
    selected: Option<usize>,
    #[serde(default)]
    selected_indices: Vec<usize>,
    #[serde(default)]
    multi_select: bool,
    #[serde(default)]
    editable: bool,

    // pushbutton
    #[serde(default)]
    caption: Option<String>,

    // shared field options
    #[serde(default)]
    read_only: bool,
    #[serde(default)]
    required: bool,
    #[serde(default)]
    no_export: bool,
    #[serde(default)]
    quadding: Option<i32>,
}

#[derive(Deserialize)]
struct WidgetJson {
    rect: RectArray,
}

// ── Helpers ───────────────────────────────────────────────────────────────────

fn rect_from_array(r: &RectArray) -> Rectangle {
    Rectangle::new(Point::new(r[0], r[1]), Point::new(r[2], r[3]))
}

fn field_options_from(dto: &CreateFieldJson) -> Option<FieldOptions> {
    let flags = FieldFlags {
        read_only: dto.read_only,
        required: dto.required,
        no_export: dto.no_export,
    };
    // Only build a FieldOptions when something is actually set, so the
    // upstream `to_flags() != 0` short-circuit keeps the field dict clean.
    if !dto.read_only && !dto.required && !dto.no_export && dto.quadding.is_none() {
        return None;
    }
    Some(FieldOptions {
        flags,
        default_appearance: None,
        quadding: dto.quadding,
    })
}

// ── Enable forms ──────────────────────────────────────────────────────────────

/// Enable interactive forms on the document (idempotent).
///
/// Creates the `FormManager` + `AcroForm` if not already present. Calling
/// `oxidize_document_add_form_field_json` also does this implicitly, so this
/// entry point is provided mainly for callers that want to make the intent
/// explicit.
///
/// # Safety
/// - `handle` must be a valid pointer from `oxidize_document_create`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_enable_forms(handle: *mut DocumentHandle) -> c_int {
    clear_last_error();
    if handle.is_null() {
        set_last_error("Null pointer to oxidize_document_enable_forms");
        return ErrorCode::NullPointer as c_int;
    }
    (*handle).inner.enable_forms();
    ErrorCode::Success as c_int
}

// ── Create field ──────────────────────────────────────────────────────────────

/// Create an AcroForm field from a tagged JSON DTO and return its object
/// number (generation is always 0) via `out_obj_num`.
///
/// JSON shape (fields not relevant to `kind` are ignored):
/// `{"kind":"text|checkbox|radio|combobox|listbox|pushbutton","name":str,
///   "rect":[x1,y1,x2,y2], ...type-specific..., "read_only":bool,
///   "required":bool,"no_export":bool,"quadding":int}`.
///
/// Calls `Document::enable_forms` internally (idempotent), so callers need not
/// enable forms first. The returned object number is the value to pass to
/// `oxidize_page_add_form_widget_json` to place additional linked widgets.
///
/// # Safety
/// - `handle` must be a valid pointer from `oxidize_document_create`.
/// - `json` must be a NUL-terminated UTF-8 string.
/// - `out_obj_num` must be a writable pointer.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_add_form_field_json(
    handle: *mut DocumentHandle,
    json: *const c_char,
    out_obj_num: *mut u32,
) -> c_int {
    clear_last_error();
    if handle.is_null() || json.is_null() || out_obj_num.is_null() {
        set_last_error("Null pointer to oxidize_document_add_form_field_json");
        return ErrorCode::NullPointer as c_int;
    }
    *out_obj_num = 0;

    let s = match CStr::from_ptr(json).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in form field JSON");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    let dto: CreateFieldJson = match serde_json::from_str(s) {
        Ok(v) => v,
        Err(e) => {
            set_last_error(format!("Invalid form field JSON: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };

    let rect = rect_from_array(&dto.rect);
    let widget = Widget::new(rect);
    let options = field_options_from(&dto);

    let fm = (*handle).inner.enable_forms();

    let result = match dto.kind.as_str() {
        "text" => {
            let mut field = TextField::new(&dto.name);
            if let Some(ref v) = dto.value {
                field = field.with_value(v);
            }
            if let Some(ref dv) = dto.default_value {
                field = field.with_default_value(dv);
            }
            if let Some(ml) = dto.max_length {
                field = field.with_max_length(ml);
            }
            if dto.multiline {
                field = field.multiline();
            }
            if dto.password {
                field = field.password();
            }
            fm.add_text_field(field, widget, options)
        }
        "checkbox" => {
            let mut field = CheckBox::new(&dto.name);
            if dto.checked {
                field = field.checked();
            }
            if let Some(ref ev) = dto.export_value {
                field = field.with_export_value(ev);
            }
            fm.add_checkbox(field, widget, options)
        }
        "radio" => {
            let mut field = RadioButton::new(&dto.name);
            for opt in &dto.options {
                field = field.add_option(&opt.export, &opt.label);
            }
            if let Some(sel) = dto.selected {
                field = field.with_selected(sel);
            }
            fm.add_radio_buttons(field, vec![widget], options)
        }
        "combobox" => {
            let mut field = ComboBox::new(&dto.name);
            for opt in &dto.options {
                field = field.add_option(&opt.export, &opt.label);
            }
            if dto.editable {
                field = field.editable();
            }
            if let Some(ref v) = dto.value {
                field = field.with_value(v);
            }
            if let Some(sel) = dto.selected {
                field = field.with_selected(sel);
            }
            fm.add_combo_box(field, widget, options)
        }
        "listbox" => {
            let mut field = ListBox::new(&dto.name);
            for opt in &dto.options {
                field = field.add_option(&opt.export, &opt.label);
            }
            if dto.multi_select {
                field = field.multi_select();
            }
            if !dto.selected_indices.is_empty() {
                field = field.with_selected(dto.selected_indices.clone());
            }
            fm.add_list_box(field, widget, options)
        }
        "pushbutton" => {
            let mut field = PushButton::new(&dto.name);
            if let Some(ref c) = dto.caption {
                field = field.with_caption(c);
            }
            fm.add_push_button(field, widget, options)
        }
        other => {
            set_last_error(format!("Unknown form field kind: {other}"));
            return ErrorCode::InvalidArgument as c_int;
        }
    };

    match result {
        Ok(obj_ref) => {
            *out_obj_num = obj_ref.number();
            ErrorCode::Success as c_int
        }
        Err(e) => {
            set_last_error(format!("Failed to add form field: {e}"));
            ErrorCode::PdfParseError as c_int
        }
    }
}

// ── Add widget to page ────────────────────────────────────────────────────────

/// Attach a widget annotation to a page, linked to an existing AcroForm field
/// by its object number (as returned by `oxidize_document_add_form_field_json`).
///
/// JSON shape: `{"rect":[x1,y1,x2,y2]}`. This is the recommended path for
/// placing a field's visual on a page; the resulting widget carries
/// `/Parent <field_obj_num> 0 R` per ISO 32000-1 §12.7.3.1.
///
/// # Safety
/// - `handle` must be a valid pointer from `oxidize_page_create` /
///   `oxidize_document_new_page_*`.
/// - `json` must be a NUL-terminated UTF-8 string.
#[no_mangle]
pub unsafe extern "C" fn oxidize_page_add_form_widget_json(
    handle: *mut PageHandle,
    json: *const c_char,
    field_obj_num: u32,
) -> c_int {
    clear_last_error();
    if handle.is_null() || json.is_null() {
        set_last_error("Null pointer to oxidize_page_add_form_widget_json");
        return ErrorCode::NullPointer as c_int;
    }
    let s = match CStr::from_ptr(json).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in widget JSON");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    let dto: WidgetJson = match serde_json::from_str(s) {
        Ok(v) => v,
        Err(e) => {
            set_last_error(format!("Invalid widget JSON: {e}"));
            return ErrorCode::SerializationError as c_int;
        }
    };

    let widget = Widget::new(rect_from_array(&dto.rect));
    let field_ref = ObjectReference::new(field_obj_num, 0);
    match (*handle).inner.add_form_widget_with_ref(widget, field_ref) {
        Ok(()) => ErrorCode::Success as c_int,
        Err(e) => {
            set_last_error(format!("Failed to add form widget: {e}"));
            ErrorCode::PdfParseError as c_int
        }
    }
}

// ── Fill field (in-process only) ──────────────────────────────────────────────

/// Set the value of a form field registered in this document's `FormManager`
/// (updates `/V` and regenerates the widget appearance streams).
///
/// This only works for fields created in the current process via
/// `oxidize_document_add_form_field_json` — there is no upstream path to fill
/// fields of a parsed/existing PDF in 2.13.0.
///
/// # Safety
/// - `handle` must be a valid pointer from `oxidize_document_create`.
/// - `name` and `value` must be NUL-terminated UTF-8 strings.
#[no_mangle]
pub unsafe extern "C" fn oxidize_document_fill_field(
    handle: *mut DocumentHandle,
    name: *const c_char,
    value: *const c_char,
) -> c_int {
    clear_last_error();
    if handle.is_null() || name.is_null() || value.is_null() {
        set_last_error("Null pointer to oxidize_document_fill_field");
        return ErrorCode::NullPointer as c_int;
    }
    let name_s = match CStr::from_ptr(name).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in field name");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };
    let value_s = match CStr::from_ptr(value).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in field value");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    match (*handle).inner.fill_field(name_s, value_s) {
        Ok(()) => ErrorCode::Success as c_int,
        Err(e) => {
            set_last_error(format!("Failed to fill field '{name_s}': {e}"));
            ErrorCode::InvalidArgument as c_int
        }
    }
}

// ── Fill an existing/parsed PDF (incremental update, upstream 2.15.0) ─────────

/// One `{ "name": .., "value": .. }` entry for `oxidize_fill_existing_form_json`.
#[derive(Deserialize)]
struct FieldFillJson {
    /// Fully-qualified AcroForm field name (e.g. `"address.street"`).
    name: String,
    /// New text value to set on the field's `/V`.
    value: String,
}

/// Fill AcroForm fields on an existing (already-serialized) PDF and return the
/// updated bytes, using upstream `IncrementalFormFiller` (ISO 32000-1 §7.5.6
/// incremental update). The base bytes are preserved verbatim as the output
/// prefix; only the touched field objects, a partial xref section and a new
/// trailer are appended.
///
/// Unlike [`oxidize_document_fill_field`] (in-process `FormManager` only), this
/// works on any parsed PDF (Acrobat, pdftk, ReportLab, …). It sets
/// `/AcroForm/NeedAppearances true`; compliant viewers regenerate the field
/// appearance on open. `/AP` appearance-stream generation is an upstream
/// follow-up.
///
/// # Arguments
/// * `pdf_bytes` / `pdf_len` — the base PDF.
/// * `fields_json` — a `FieldFillJson[]` JSON C string:
///   `[{"name":..,"value":..}, …]`.
/// * `out_bytes` / `out_len` — receive the heap-allocated result; free with
///   `oxidize_free_bytes`.
///
/// # Returns
/// `Success`; or `NullPointer`, `InvalidUtf8`, `InvalidArgument` (bad JSON or
/// empty field list), `PdfParseError` (`pdf_len == 0`, parse failure, or an
/// unknown field name). `*out_bytes` is null on any error.
///
/// # Safety
/// - `pdf_bytes` must point to `pdf_len` readable bytes.
/// - `fields_json` must be a NUL-terminated UTF-8 C string.
/// - `out_bytes` must be a writeable `*mut *mut u8`; `out_len` a writeable
///   `*mut usize`.
#[no_mangle]
pub unsafe extern "C" fn oxidize_fill_existing_form_json(
    pdf_bytes: *const u8,
    pdf_len: usize,
    fields_json: *const c_char,
    out_bytes: *mut *mut u8,
    out_len: *mut usize,
) -> c_int {
    clear_last_error();
    if pdf_bytes.is_null() || fields_json.is_null() || out_bytes.is_null() || out_len.is_null() {
        set_last_error("Null pointer provided to oxidize_fill_existing_form_json");
        return ErrorCode::NullPointer as c_int;
    }
    *out_bytes = std::ptr::null_mut();
    *out_len = 0;

    if pdf_len == 0 {
        set_last_error("PDF data is empty (0 bytes)");
        return ErrorCode::PdfParseError as c_int;
    }

    let fields_str = match CStr::from_ptr(fields_json).to_str() {
        Ok(v) => v,
        Err(_) => {
            set_last_error("Invalid UTF-8 in fields JSON");
            return ErrorCode::InvalidUtf8 as c_int;
        }
    };

    let fills: Vec<FieldFillJson> = match serde_json::from_str(fields_str) {
        Ok(v) => v,
        Err(e) => {
            set_last_error(format!("Invalid fields JSON: {e}"));
            return ErrorCode::InvalidArgument as c_int;
        }
    };
    if fills.is_empty() {
        set_last_error("Field list is empty");
        return ErrorCode::InvalidArgument as c_int;
    }

    let pairs: Vec<(&str, &str)> = fills
        .iter()
        .map(|f| (f.name.as_str(), f.value.as_str()))
        .collect();

    let base = std::slice::from_raw_parts(pdf_bytes, pdf_len);
    let filled = match IncrementalFormFiller::new(base).fill_many(&pairs) {
        Ok(b) => b,
        Err(e) => {
            set_last_error(format!("Failed to fill form fields: {e}"));
            return ErrorCode::PdfParseError as c_int;
        }
    };

    let len = filled.len();
    let mut boxed = filled.into_boxed_slice();
    *out_bytes = boxed.as_mut_ptr();
    *out_len = len;
    std::mem::forget(boxed);

    ErrorCode::Success as c_int
}

// ── Tests ─────────────────────────────────────────────────────────────────────

#[cfg(test)]
mod tests {
    use super::*;
    use crate::document::{
        oxidize_document_add_page, oxidize_document_create, oxidize_document_free,
    };
    use crate::page::{oxidize_page_create, oxidize_page_free};
    use std::ffi::CString;

    /// Add a field via the FFI from a JSON literal, asserting success and
    /// returning the field's object number.
    unsafe fn add_field(handle: *mut DocumentHandle, json: &str) -> u32 {
        let c = CString::new(json).unwrap();
        let mut obj_num: u32 = 0;
        let code = oxidize_document_add_form_field_json(handle, c.as_ptr(), &mut obj_num);
        assert_eq!(
            code,
            ErrorCode::Success as c_int,
            "add_form_field_json failed for {json}"
        );
        obj_num
    }

    /// Place a widget on a page via the FFI, asserting success.
    unsafe fn add_widget(page: *mut PageHandle, rect_json: &str, obj_num: u32) {
        let c = CString::new(rect_json).unwrap();
        let code = oxidize_page_add_form_widget_json(page, c.as_ptr(), obj_num);
        assert_eq!(
            code,
            ErrorCode::Success as c_int,
            "add_form_widget_json failed"
        );
    }

    /// Build a single-page doc with one field+widget, return serialized bytes
    /// as a UTF-8-lossy string for structure assertions.
    unsafe fn build_and_serialize(field_json: &str) -> (String, u32) {
        let handle = oxidize_document_create();
        let obj_num = add_field(handle, field_json);
        let page = oxidize_page_create(595.0, 842.0);
        add_widget(page, r#"{"rect":[50,700,300,720]}"#, obj_num);
        assert_eq!(
            oxidize_document_add_page(handle, page),
            ErrorCode::Success as c_int
        );
        oxidize_page_free(page);
        let bytes = (*handle).inner.to_bytes().unwrap();
        oxidize_document_free(handle);
        (String::from_utf8_lossy(&bytes).into_owned(), obj_num)
    }

    #[test]
    fn enable_forms_null_handle_returns_null_pointer() {
        let code = unsafe { oxidize_document_enable_forms(std::ptr::null_mut()) };
        assert_eq!(code, ErrorCode::NullPointer as c_int);
    }

    #[test]
    fn add_text_field_returns_nonzero_obj_num() {
        unsafe {
            let handle = oxidize_document_create();
            let obj_num = add_field(
                handle,
                r#"{"kind":"text","name":"username","rect":[50,700,300,720],"value":"Alice"}"#,
            );
            oxidize_document_free(handle);
            assert!(
                obj_num >= 1,
                "field object number must be >= 1, got {obj_num}"
            );
        }
    }

    #[test]
    fn text_field_round_trip_emits_acroform_and_field_dict() {
        unsafe {
            let (s, _) = build_and_serialize(
                r#"{"kind":"text","name":"username","rect":[50,700,300,720],"value":"Alice"}"#,
            );
            assert!(s.contains("/AcroForm"), "missing /AcroForm in output");
            assert!(s.contains("/FT /Tx"), "missing text field type /FT /Tx");
            assert!(
                s.contains("/T (username)"),
                "missing field name /T (username)"
            );
            assert!(s.contains("/V (Alice)"), "missing field value /V (Alice)");
        }
    }

    /// Serialize a single-field/single-widget doc and read its form fields
    /// back through the existing read-path (`oxidize_get_form_fields`). Returns
    /// the parsed JSON array string. This is the authoritative behavioral
    /// round-trip: it proves a viewer/reader can recover the field the
    /// write-path created.
    unsafe fn build_and_reread(field_json: &str) -> String {
        let handle = oxidize_document_create();
        let obj_num = add_field(handle, field_json);
        let page = oxidize_page_create(595.0, 842.0);
        add_widget(page, r#"{"rect":[50,700,300,720]}"#, obj_num);
        assert_eq!(
            oxidize_document_add_page(handle, page),
            ErrorCode::Success as c_int
        );
        oxidize_page_free(page);
        let bytes = (*handle).inner.to_bytes().unwrap();
        oxidize_document_free(handle);

        let mut out_json: *mut c_char = std::ptr::null_mut();
        let code =
            crate::parser::oxidize_get_form_fields(bytes.as_ptr(), bytes.len(), &mut out_json);
        assert_eq!(
            code,
            ErrorCode::Success as c_int,
            "oxidize_get_form_fields failed"
        );
        let json = CStr::from_ptr(out_json).to_string_lossy().into_owned();
        crate::oxidize_free_string(out_json);
        json
    }

    #[test]
    fn text_field_is_recoverable_via_read_path() {
        unsafe {
            let json = build_and_reread(
                r#"{"kind":"text","name":"username","rect":[50,700,300,720],"value":"Alice"}"#,
            );
            assert!(
                json.contains("\"field_name\":\"username\""),
                "read-path did not recover field name; got: {json}"
            );
            assert!(
                json.contains("\"field_type\":\"text\""),
                "read-path did not recover text type; got: {json}"
            );
            assert!(
                json.contains("\"value\":\"Alice\""),
                "read-path did not recover value; got: {json}"
            );
        }
    }

    #[test]
    fn checkbox_round_trips_checked_state() {
        unsafe {
            let json = build_and_reread(
                r#"{"kind":"checkbox","name":"agree","rect":[50,700,70,720],"checked":true,"export_value":"Yes"}"#,
            );
            assert!(
                json.contains("\"field_name\":\"agree\""),
                "name; got {json}"
            );
            assert!(
                json.contains("\"field_type\":\"checkbox\""),
                "type; got {json}"
            );
            assert!(
                json.contains("\"value\":\"Yes\""),
                "checked value; got {json}"
            );
        }
    }

    #[test]
    fn radio_round_trips_selected_value_and_type() {
        unsafe {
            let json = build_and_reread(
                r#"{"kind":"radio","name":"color","rect":[50,700,70,720],
                    "options":[{"export":"R","label":"Red"},{"export":"G","label":"Green"},{"export":"B","label":"Blue"}],
                    "selected":1}"#,
            );
            assert!(
                json.contains("\"field_name\":\"color\""),
                "name; got {json}"
            );
            assert!(
                json.contains("\"field_type\":\"radio\""),
                "type; got {json}"
            );
            assert!(
                json.contains("\"value\":\"G\""),
                "selected export value; got {json}"
            );
        }
    }

    #[test]
    fn combobox_round_trips_options_and_value() {
        unsafe {
            let json = build_and_reread(
                r#"{"kind":"combobox","name":"country","rect":[50,700,200,720],
                    "options":[{"export":"US","label":"United States"},{"export":"CA","label":"Canada"}],
                    "value":"US"}"#,
            );
            assert!(
                json.contains("\"field_name\":\"country\""),
                "name; got {json}"
            );
            // Combo boxes are reported as "dropdown" by the read-path classifier.
            assert!(
                json.contains("\"field_type\":\"dropdown\""),
                "type; got {json}"
            );
            assert!(json.contains("\"value\":\"US\""), "value; got {json}");
            assert!(
                json.contains("\"export_value\":\"US\"")
                    && json.contains("\"export_value\":\"CA\""),
                "both options must be recovered; got {json}"
            );
            assert!(
                json.contains("\"display_text\":\"United States\""),
                "display text; got {json}"
            );
        }
    }

    #[test]
    fn listbox_round_trips_options_and_type() {
        unsafe {
            let json = build_and_reread(
                r#"{"kind":"listbox","name":"sizes","rect":[50,650,200,720],
                    "options":[{"export":"S","label":"Small"},{"export":"M","label":"Medium"},{"export":"L","label":"Large"}],
                    "selected_indices":[1]}"#,
            );
            assert!(
                json.contains("\"field_name\":\"sizes\""),
                "name; got {json}"
            );
            assert!(
                json.contains("\"field_type\":\"listbox\""),
                "type; got {json}"
            );
            assert!(
                json.contains("\"export_value\":\"S\"")
                    && json.contains("\"export_value\":\"M\"")
                    && json.contains("\"export_value\":\"L\""),
                "all three options must be recovered; got {json}"
            );
        }
    }

    #[test]
    fn pushbutton_round_trips_type() {
        unsafe {
            let json = build_and_reread(
                r#"{"kind":"pushbutton","name":"submit","rect":[50,700,150,720],"caption":"Submit"}"#,
            );
            assert!(
                json.contains("\"field_name\":\"submit\""),
                "name; got {json}"
            );
            assert!(
                json.contains("\"field_type\":\"pushbutton\""),
                "type; got {json}"
            );
        }
    }

    #[test]
    fn read_only_and_required_flags_round_trip() {
        unsafe {
            let json = build_and_reread(
                r#"{"kind":"text","name":"locked","rect":[50,700,300,720],"value":"x","read_only":true,"required":true}"#,
            );
            assert!(
                json.contains("\"is_read_only\":true"),
                "read_only; got {json}"
            );
            assert!(
                json.contains("\"is_required\":true"),
                "required; got {json}"
            );
        }
    }

    // ── fill_field (in-process) ──────────────────────────────────────────────

    #[test]
    fn fill_field_updates_value_observable_via_read_path() {
        unsafe {
            let handle = oxidize_document_create();
            // Text field created with no initial value.
            let obj_num = add_field(
                handle,
                r#"{"kind":"text","name":"email","rect":[50,700,300,720]}"#,
            );
            let page = oxidize_page_create(595.0, 842.0);
            add_widget(page, r#"{"rect":[50,700,300,720]}"#, obj_num);
            assert_eq!(
                oxidize_document_add_page(handle, page),
                ErrorCode::Success as c_int
            );
            oxidize_page_free(page);

            let name = CString::new("email").unwrap();
            let value = CString::new("user@example.com").unwrap();
            let code = oxidize_document_fill_field(handle, name.as_ptr(), value.as_ptr());
            assert_eq!(code, ErrorCode::Success as c_int, "fill_field failed");

            let bytes = (*handle).inner.to_bytes().unwrap();
            oxidize_document_free(handle);

            let mut out_json: *mut c_char = std::ptr::null_mut();
            assert_eq!(
                crate::parser::oxidize_get_form_fields(bytes.as_ptr(), bytes.len(), &mut out_json),
                ErrorCode::Success as c_int
            );
            let json = CStr::from_ptr(out_json).to_string_lossy().into_owned();
            crate::oxidize_free_string(out_json);
            assert!(
                json.contains("\"field_name\":\"email\""),
                "field name; got {json}"
            );
            assert!(
                json.contains("\"value\":\"user@example.com\""),
                "fill_field value not observable via read-path; got {json}"
            );
        }
    }

    #[test]
    fn fill_field_unknown_name_returns_invalid_argument() {
        unsafe {
            let handle = oxidize_document_create();
            add_field(
                handle,
                r#"{"kind":"text","name":"present","rect":[50,700,300,720]}"#,
            );
            let name = CString::new("absent").unwrap();
            let value = CString::new("x").unwrap();
            let code = oxidize_document_fill_field(handle, name.as_ptr(), value.as_ptr());
            oxidize_document_free(handle);
            assert_eq!(code, ErrorCode::InvalidArgument as c_int);
        }
    }

    // ── fill existing/parsed PDF (incremental update, FORM-008) ───────────────

    /// Build a single-text-field PDF (empty value) and return its serialized
    /// bytes — a stand-in for an arbitrary "form template" PDF.
    unsafe fn build_form_template(field_name: &str) -> Vec<u8> {
        let handle = oxidize_document_create();
        let field_json =
            format!(r#"{{"kind":"text","name":"{field_name}","rect":[50,700,300,720]}}"#);
        let obj_num = add_field(handle, &field_json);
        let page = oxidize_page_create(595.0, 842.0);
        add_widget(page, r#"{"rect":[50,700,300,720]}"#, obj_num);
        assert_eq!(
            oxidize_document_add_page(handle, page),
            ErrorCode::Success as c_int
        );
        oxidize_page_free(page);
        let bytes = (*handle).inner.to_bytes().unwrap();
        oxidize_document_free(handle);
        bytes
    }

    #[test]
    fn fill_existing_form_sets_value_recoverable_via_read_path() {
        unsafe {
            let base = build_form_template("full_name");

            let fields = CString::new(r#"[{"name":"full_name","value":"Ada Lovelace"}]"#).unwrap();
            let mut out: *mut u8 = std::ptr::null_mut();
            let mut out_len: usize = 0;
            let code = oxidize_fill_existing_form_json(
                base.as_ptr(),
                base.len(),
                fields.as_ptr(),
                &mut out,
                &mut out_len,
            );
            assert_eq!(
                code,
                ErrorCode::Success as c_int,
                "fill on existing PDF failed"
            );
            assert!(
                !out.is_null() && out_len > base.len(),
                "expected an appended incremental update (out_len {out_len} > base {})",
                base.len()
            );

            let filled = std::slice::from_raw_parts(out, out_len).to_vec();
            crate::oxidize_free_bytes(out, out_len);

            // ISO 32000-1 §7.5.6: the base bytes must be a verbatim prefix.
            assert_eq!(
                &filled[..base.len()],
                &base[..],
                "base bytes must be preserved verbatim as the prefix"
            );

            // The read-path (which follows the most-recent startxref + /Prev
            // chain) must recover the newly-set value.
            let mut out_json: *mut c_char = std::ptr::null_mut();
            assert_eq!(
                crate::parser::oxidize_get_form_fields(
                    filled.as_ptr(),
                    filled.len(),
                    &mut out_json
                ),
                ErrorCode::Success as c_int,
                "read-path failed on the filled PDF"
            );
            let json = CStr::from_ptr(out_json).to_string_lossy().into_owned();
            crate::oxidize_free_string(out_json);
            assert!(
                json.contains("\"field_name\":\"full_name\""),
                "field name not recovered; got {json}"
            );
            assert!(
                json.contains("\"value\":\"Ada Lovelace\""),
                "filled value not recovered via read-path; got {json}"
            );
        }
    }

    #[test]
    fn fill_existing_form_unknown_field_errors_and_nulls_out() {
        unsafe {
            let base = build_form_template("full_name");
            let fields = CString::new(r#"[{"name":"does_not_exist","value":"x"}]"#).unwrap();
            let mut out: *mut u8 = std::ptr::null_mut();
            let mut out_len: usize = 0;
            let code = oxidize_fill_existing_form_json(
                base.as_ptr(),
                base.len(),
                fields.as_ptr(),
                &mut out,
                &mut out_len,
            );
            assert_ne!(
                code,
                ErrorCode::Success as c_int,
                "filling an unknown field must error"
            );
            assert!(out.is_null(), "out_bytes must be null on error");
            assert_eq!(out_len, 0, "out_len must be 0 on error");
        }
    }

    #[test]
    fn fill_existing_form_empty_field_list_is_invalid_argument() {
        unsafe {
            let base = build_form_template("full_name");
            let fields = CString::new("[]").unwrap();
            let mut out: *mut u8 = std::ptr::null_mut();
            let mut out_len: usize = 0;
            let code = oxidize_fill_existing_form_json(
                base.as_ptr(),
                base.len(),
                fields.as_ptr(),
                &mut out,
                &mut out_len,
            );
            assert_eq!(code, ErrorCode::InvalidArgument as c_int);
            assert!(out.is_null());
        }
    }

    #[test]
    fn fill_existing_form_empty_pdf_is_parse_error() {
        unsafe {
            let fields = CString::new(r#"[{"name":"x","value":"y"}]"#).unwrap();
            let mut out: *mut u8 = std::ptr::null_mut();
            let mut out_len: usize = 0;
            let code = oxidize_fill_existing_form_json(
                std::ptr::null(),
                0,
                fields.as_ptr(),
                &mut out,
                &mut out_len,
            );
            // Null pdf pointer is caught before the length check.
            assert_eq!(code, ErrorCode::NullPointer as c_int);
        }
    }

    // ── Error paths ──────────────────────────────────────────────────────────

    #[test]
    fn add_field_null_handle_returns_null_pointer() {
        unsafe {
            let c = CString::new("{}").unwrap();
            let mut n: u32 = 0;
            let code =
                oxidize_document_add_form_field_json(std::ptr::null_mut(), c.as_ptr(), &mut n);
            assert_eq!(code, ErrorCode::NullPointer as c_int);
        }
    }

    #[test]
    fn add_field_bad_json_returns_serialization_error() {
        unsafe {
            let handle = oxidize_document_create();
            let c = CString::new("{ not json").unwrap();
            let mut n: u32 = 0;
            let code = oxidize_document_add_form_field_json(handle, c.as_ptr(), &mut n);
            oxidize_document_free(handle);
            assert_eq!(code, ErrorCode::SerializationError as c_int);
        }
    }

    #[test]
    fn add_field_unknown_kind_returns_invalid_argument() {
        unsafe {
            let handle = oxidize_document_create();
            let c = CString::new(r#"{"kind":"slider","name":"x","rect":[0,0,1,1]}"#).unwrap();
            let mut n: u32 = 0;
            let code = oxidize_document_add_form_field_json(handle, c.as_ptr(), &mut n);
            oxidize_document_free(handle);
            assert_eq!(code, ErrorCode::InvalidArgument as c_int);
        }
    }

    #[test]
    fn page_add_widget_null_page_returns_null_pointer() {
        unsafe {
            let c = CString::new(r#"{"rect":[0,0,1,1]}"#).unwrap();
            let code = oxidize_page_add_form_widget_json(std::ptr::null_mut(), c.as_ptr(), 4);
            assert_eq!(code, ErrorCode::NullPointer as c_int);
        }
    }

    #[test]
    fn page_add_widget_bad_json_returns_serialization_error() {
        unsafe {
            let page = oxidize_page_create(595.0, 842.0);
            let c = CString::new("nope").unwrap();
            let code = oxidize_page_add_form_widget_json(page, c.as_ptr(), 4);
            oxidize_page_free(page);
            assert_eq!(code, ErrorCode::SerializationError as c_int);
        }
    }

    #[test]
    fn multiple_fields_all_recovered_via_read_path() {
        unsafe {
            let handle = oxidize_document_create();
            let t = add_field(
                handle,
                r#"{"kind":"text","name":"fullname","rect":[50,700,300,720],"value":"Bob"}"#,
            );
            let c = add_field(
                handle,
                r#"{"kind":"checkbox","name":"subscribe","rect":[50,660,70,680],"checked":true,"export_value":"On"}"#,
            );
            let p = add_field(
                handle,
                r#"{"kind":"pushbutton","name":"go","rect":[50,620,150,640]}"#,
            );
            let page = oxidize_page_create(595.0, 842.0);
            add_widget(page, r#"{"rect":[50,700,300,720]}"#, t);
            add_widget(page, r#"{"rect":[50,660,70,680]}"#, c);
            add_widget(page, r#"{"rect":[50,620,150,640]}"#, p);
            assert_eq!(
                oxidize_document_add_page(handle, page),
                ErrorCode::Success as c_int
            );
            oxidize_page_free(page);
            let bytes = (*handle).inner.to_bytes().unwrap();
            oxidize_document_free(handle);

            let mut out_json: *mut c_char = std::ptr::null_mut();
            assert_eq!(
                crate::parser::oxidize_get_form_fields(bytes.as_ptr(), bytes.len(), &mut out_json),
                ErrorCode::Success as c_int
            );
            let json = CStr::from_ptr(out_json).to_string_lossy().into_owned();
            crate::oxidize_free_string(out_json);
            assert!(
                json.contains("\"field_name\":\"fullname\""),
                "text; got {json}"
            );
            assert!(
                json.contains("\"field_name\":\"subscribe\""),
                "checkbox; got {json}"
            );
            assert!(
                json.contains("\"field_name\":\"go\""),
                "pushbutton; got {json}"
            );
        }
    }
}
