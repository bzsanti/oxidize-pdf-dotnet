//! Typed bridge to core form validation; requests are independent and ordered.
use crate::{clear_last_error, set_last_error, ErrorCode};
use oxidize_pdf::forms::{calculations::FieldValue, validation::*};
use serde::Deserialize;
use serde_json::{json, Value};
use std::ffi::CString;
use std::os::raw::{c_char, c_int};

#[derive(Deserialize)]
#[serde(tag = "Kind")]
enum FormValue {
    Number {
        #[serde(rename = "Value")]
        value: f64,
    },
    Text {
        #[serde(rename = "Value")]
        value: String,
    },
    Boolean {
        #[serde(rename = "Value")]
        value: bool,
    },
    Empty,
}
impl FormValue {
    fn into_core(self) -> Result<FieldValue, String> {
        Ok(match self {
            Self::Number { value } => FieldValue::Number(value),
            Self::Text { value } => {
                bound(&value, 65_536)?;
                FieldValue::Text(value)
            }
            Self::Boolean { value } => FieldValue::Boolean(value),
            Self::Empty => FieldValue::Empty,
        })
    }
}

macro_rules! mapped_enum {
    ($name:ident, $core:ident, $($variant:ident),+) => {
        #[derive(Deserialize)]
        #[allow(clippy::upper_case_acronyms)] // Mirror core's serialized format names.
        enum $name { $($variant),+ }
        impl From<$name> for $core {
            fn from(value: $name) -> Self { match value { $($name::$variant => Self::$variant),+ } }
        }
    };
}
mapped_enum!(Country, PhoneCountry, US, UK, EU, Japan, Custom);
mapped_enum!(DateLayout, DateFormat, MDY, DMY, YMD, DotDMY, DashMDY);
mapped_enum!(TimeLayout, TimeFormat, HM, HMS, HMAM, HMSAM);

#[derive(Deserialize)]
#[serde(tag = "Kind", rename_all_fields = "PascalCase")]
enum Rule {
    Required,
    Range {
        min: Option<f64>,
        max: Option<f64>,
    },
    Length {
        min: Option<usize>,
        max: Option<usize>,
    },
    Pattern {
        expression: String,
    },
    Email,
    Url,
    Phone {
        country: Country,
    },
    Date {
        min: Option<String>,
        max: Option<String>,
    },
    Time {
        min: Option<String>,
        max: Option<String>,
    },
    CreditCard,
    Custom {
        name: String,
        passed: bool,
    },
}
fn parse_date(value: Option<String>) -> Result<Option<chrono::NaiveDate>, String> {
    value
        .map(|s| chrono::NaiveDate::parse_from_str(&s, "%Y-%m-%d").map_err(|e| e.to_string()))
        .transpose()
}
fn parse_time(value: Option<String>) -> Result<Option<chrono::NaiveTime>, String> {
    value
        .map(|s| chrono::NaiveTime::parse_from_str(&s, "%H:%M:%S%.f").map_err(|e| e.to_string()))
        .transpose()
}
impl Rule {
    fn into_core(self) -> Result<ValidationRule, String> {
        Ok(match self {
            Self::Required => ValidationRule::Required,
            Self::Range { min, max } => ValidationRule::Range { min, max },
            Self::Length { min, max } => ValidationRule::Length { min, max },
            Self::Pattern { expression } => {
                bound(&expression, 4096)?;
                ValidationRule::Pattern(expression)
            }
            Self::Email => ValidationRule::Email,
            Self::Url => ValidationRule::Url,
            Self::Phone { country } => ValidationRule::PhoneNumber {
                country: country.into(),
            },
            Self::Date { min, max } => ValidationRule::Date {
                min: parse_date(min)?,
                max: parse_date(max)?,
            },
            Self::Time { min, max } => ValidationRule::Time {
                min: parse_time(min)?,
                max: parse_time(max)?,
            },
            Self::CreditCard => ValidationRule::CreditCard,
            Self::Custom { name, passed } => {
                bound(&name, 4096)?;
                ValidationRule::Custom {
                    name,
                    validator: if passed { |_| true } else { |_| false },
                }
            }
        })
    }
}
#[derive(Deserialize)]
#[serde(tag = "Kind", rename_all_fields = "PascalCase")]
enum Mask {
    Number {
        decimals: usize,
        thousands_separator: bool,
        allow_negative: bool,
        prefix: Option<String>,
        suffix: Option<String>,
    },
    Date {
        format: DateLayout,
    },
    Time {
        format: TimeLayout,
        use24_hour: bool,
    },
    Phone {
        country: Country,
    },
    Ssn,
    ZipCode {
        plus_four: bool,
    },
    CreditCard,
    Custom {
        pattern: String,
        placeholder: char,
    },
}
impl Mask {
    fn into_core(self) -> Result<FormatMask, String> {
        Ok(match self {
            Self::Number {
                decimals,
                thousands_separator,
                allow_negative,
                prefix,
                suffix,
            } => {
                if decimals > 32 {
                    return Err("Decimals exceeds 32".into());
                }
                optional_bound(&prefix)?;
                optional_bound(&suffix)?;
                FormatMask::Number {
                    decimals,
                    thousands_separator,
                    allow_negative,
                    prefix,
                    suffix,
                }
            }
            Self::Date { format } => FormatMask::Date {
                format: format.into(),
            },
            Self::Time { format, use24_hour } => FormatMask::Time {
                format: format.into(),
                use_24_hour: use24_hour,
            },
            Self::Phone { country } => FormatMask::Phone {
                country: country.into(),
            },
            Self::Ssn => FormatMask::SSN,
            Self::ZipCode { plus_four } => FormatMask::ZipCode { plus_four },
            Self::CreditCard => FormatMask::CreditCard,
            Self::Custom {
                pattern,
                placeholder,
            } => {
                bound(&pattern, 4096)?;
                FormatMask::Custom {
                    pattern,
                    placeholder,
                }
            }
        })
    }
}
#[derive(Deserialize)]
#[serde(tag = "Kind", rename_all_fields = "PascalCase")]
enum Condition {
    Always,
    IfFieldEquals { field: String, value: FormValue },
    IfFieldNotEmpty { field: String },
    IfGroupHasValue { group: String },
}
impl Condition {
    fn into_core(self) -> Result<RequirementCondition, String> {
        Ok(match self {
            Self::Always => RequirementCondition::Always,
            Self::IfFieldEquals { field, value } => {
                bound(&field, 1024)?;
                RequirementCondition::IfFieldEquals {
                    field,
                    value: value.into_core()?,
                }
            }
            Self::IfFieldNotEmpty { field } => {
                bound(&field, 1024)?;
                RequirementCondition::IfFieldNotEmpty { field }
            }
            Self::IfGroupHasValue { group } => {
                bound(&group, 4096)?;
                RequirementCondition::IfGroupHasValue { group }
            }
        })
    }
}
#[derive(Deserialize)]
#[serde(rename_all = "PascalCase")]
struct Required {
    error_message: String,
    group: Option<String>,
    condition: Option<Condition>,
}
#[derive(Deserialize)]
#[serde(rename_all = "PascalCase")]
struct Validator {
    rules: Vec<Rule>,
    format_mask: Option<Mask>,
    error_message: Option<String>,
}
#[derive(Deserialize)]
#[serde(rename_all = "PascalCase")]
struct Field {
    field_name: String,
    value: FormValue,
    validator: Option<Validator>,
    required: Option<Required>,
}
#[derive(Deserialize)]
#[serde(rename_all = "PascalCase")]
struct Settings {
    validate_on_change: bool,
    show_format_hints: bool,
    auto_format: bool,
    allow_partial: bool,
    real_time_validation: bool,
    highlight_errors: bool,
    show_error_messages: bool,
}
impl From<Settings> for ValidationSettings {
    fn from(s: Settings) -> Self {
        Self {
            validate_on_change: s.validate_on_change,
            show_format_hints: s.show_format_hints,
            auto_format: s.auto_format,
            allow_partial: s.allow_partial,
            real_time_validation: s.real_time_validation,
            highlight_errors: s.highlight_errors,
            show_error_messages: s.show_error_messages,
        }
    }
}
#[derive(Deserialize)]
#[serde(rename_all = "PascalCase")]
struct Request {
    fields: Vec<Field>,
    settings: Settings,
}
fn bound(value: &str, limit: usize) -> Result<(), String> {
    if value.len() > limit {
        Err(format!("String exceeds {limit} UTF-8 bytes"))
    } else {
        Ok(())
    }
}
fn optional_bound(value: &Option<String>) -> Result<(), String> {
    value.as_deref().map(|s| bound(s, 4096)).unwrap_or(Ok(()))
}
fn run(request: Request) -> Result<Value, String> {
    if request.fields.len() > 256 {
        return Err("At most 256 fields supported".into());
    }
    let total_rules: usize = request
        .fields
        .iter()
        .filter_map(|f| f.validator.as_ref())
        .map(|v| v.rules.len())
        .sum();
    if total_rules > 1024 {
        return Err("At most 1024 rules per call supported".into());
    }
    let settings: ValidationSettings = request.settings.into();
    let mut results = Vec::new();
    for field in request.fields {
        bound(&field.field_name, 1024)?;
        let value = field.value.into_core()?;
        let mut system = FormValidationSystem::with_settings(settings.clone());
        if let Some(validator) = field.validator {
            if validator.rules.len() > 128 {
                return Err("At most 128 rules per field supported".into());
            }
            optional_bound(&validator.error_message)?;
            system.add_validator(FieldValidator {
                field_name: field.field_name.clone(),
                rules: validator
                    .rules
                    .into_iter()
                    .map(Rule::into_core)
                    .collect::<Result<_, _>>()?,
                format_mask: validator.format_mask.map(Mask::into_core).transpose()?,
                error_message: validator.error_message,
            });
        }
        if let Some(required) = field.required {
            bound(&required.error_message, 4096)?;
            optional_bound(&required.group)?;
            system.add_required_field(RequiredFieldInfo {
                field_name: field.field_name.clone(),
                error_message: required.error_message,
                group: required.group,
                condition: required.condition.map(Condition::into_core).transpose()?,
            });
        }
        let result = system.validate_field(&field.field_name, &value);
        let errors: Vec<_> = result.errors.into_iter().map(|e| {
            let kind = match e.error_type {
                ValidationErrorType::Required => "Required", ValidationErrorType::Format => "Format",
                ValidationErrorType::Range => "Range", ValidationErrorType::Length => "Length",
                ValidationErrorType::Pattern => "Pattern", ValidationErrorType::Custom => "Custom",
            };
            json!({"FieldName":e.field_name,"ErrorType":kind,"Message":e.message,"Details":e.details})
        }).collect();
        results.push(
            json!({"FieldName":result.field_name,"IsValid":result.is_valid,"Errors":errors,
            "Warnings":result.warnings,"FormattedValue":result.formatted_value}),
        );
    }
    Ok(json!(results))
}

/// Validate bounded UTF-8 JSON. Output is owned and freed with oxidize_free_string.
/// No output is returned if any request is invalid or a panic is caught.
/// # Safety
/// `input` points to `length` readable bytes; `output` is aligned and writable.
#[no_mangle]
pub unsafe extern "C" fn oxidize_form_validate(
    input: *const u8,
    length: usize,
    output: *mut *mut c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if output.is_null() {
            return ErrorCode::NullPointer as c_int;
        }
        *output = std::ptr::null_mut();
        if input.is_null() {
            return ErrorCode::NullPointer as c_int;
        }
        if length == 0 || length > 1_048_576 {
            set_last_error("Validation request must be 1..1048576 bytes");
            return ErrorCode::InvalidArgument as c_int;
        }
        let request =
            match serde_json::from_slice::<Request>(std::slice::from_raw_parts(input, length)) {
                Ok(r) => r,
                Err(e) => {
                    set_last_error(format!("Invalid validation request: {e}"));
                    return ErrorCode::InvalidArgument as c_int;
                }
            };
        let result = match run(request) {
            Ok(r) => r,
            Err(e) => {
                set_last_error(e);
                return ErrorCode::InvalidArgument as c_int;
            }
        };
        let json = result.to_string();
        if json.len() > 8_388_608 {
            set_last_error("Validation result exceeds 8 MiB");
            return ErrorCode::InvalidArgument as c_int;
        }
        match CString::new(json) {
            Ok(value) => {
                *output = value.into_raw();
                ErrorCode::Success as c_int
            }
            Err(e) => {
                set_last_error(e.to_string());
                ErrorCode::SerializationError as c_int
            }
        }
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn boundary_rejects_before_dereferencing_and_clears_output() {
        unsafe {
            let mut output = std::ptr::dangling_mut::<c_char>();
            assert_eq!(oxidize_form_validate(std::ptr::null(), 1, &mut output), 1);
            assert!(output.is_null());
            assert_eq!(
                oxidize_form_validate(b"x".as_ptr(), usize::MAX, &mut output),
                9
            );
            assert!(output.is_null());
            assert_eq!(oxidize_form_validate(b"x".as_ptr(), 0, &mut output), 9);
            assert_eq!(
                oxidize_form_validate(b"x".as_ptr(), 1, std::ptr::null_mut()),
                1
            );
            assert_eq!(oxidize_form_validate([255].as_ptr(), 1, &mut output), 9);
            assert!(output.is_null());
        }
    }
    #[test]
    fn invalid_late_field_returns_no_partial_response() {
        let input = json!({"Settings":{"ValidateOnChange":true,"ShowFormatHints":true,"AutoFormat":true,
            "AllowPartial":false,"RealTimeValidation":true,"HighlightErrors":true,"ShowErrorMessages":true},
            "Fields":[{"FieldName":"ok","Value":{"Kind":"Text","Value":"ok"}},
                {"FieldName":"bad","Value":{"Kind":"Text","Value":"x"},"Validator":{"Rules":[],
                    "FormatMask":{"Kind":"Number","Decimals":33,"ThousandsSeparator":false,"AllowNegative":true}}}] }).to_string();
        unsafe {
            let mut output = std::ptr::dangling_mut::<c_char>();
            assert_eq!(
                oxidize_form_validate(input.as_ptr(), input.len(), &mut output),
                9
            );
            assert!(output.is_null());
        }
    }
}
