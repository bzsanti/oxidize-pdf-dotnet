//! Ordered, bounded access to both core calculation engines; no PDF/script runtime.
use crate::{clear_last_error, set_last_error, ErrorCode};
use oxidize_pdf::forms::{calculation_system::*, calculations::*};
use oxidize_pdf::PdfError;
use serde::Deserialize;
use serde_json::{json, Value};
use std::ffi::CString;
use std::os::raw::{c_char, c_int};

macro_rules! mapped {
    ($name:ident, $core:ident, $($v:ident),+) => {
        #[derive(Deserialize)]
        #[allow(clippy::upper_case_acronyms)]
        enum $name { $($v),+ }
        impl From<$name> for $core {
            fn from(v: $name) -> Self { match v { $($name::$v => Self::$v),+ } }
        }
    };
}
mapped!(
    Simple,
    SimpleOperation,
    Sum,
    Product,
    Average,
    Minimum,
    Maximum
);
mapped!(
    PercentKind,
    PercentMode,
    PercentOf,
    PercentageOf,
    AddPercent,
    SubtractPercent
);
mapped!(
    Separator,
    SeparatorStyle,
    CommaPeriod,
    PeriodComma,
    SpacePeriod,
    ApostrophePeriod,
    None
);
mapped!(
    Negative,
    NegativeStyle,
    MinusBlack,
    RedParentheses,
    BlackParentheses,
    MinusRed
);
mapped!(
    Special,
    SpecialFormat,
    ZipCode,
    ZipCodePlus4,
    PhoneNumber,
    SSN
);

#[derive(Deserialize)]
#[serde(tag = "Kind", rename_all_fields = "PascalCase", deny_unknown_fields)]
enum InputValue {
    Number { value: f64 },
    Text { value: String },
    Boolean { value: bool },
    Empty,
}
impl InputValue {
    fn core(self) -> FieldValue {
        match self {
            Self::Number { value } => FieldValue::Number(value),
            Self::Text { value } => FieldValue::Text(value),
            Self::Boolean { value } => FieldValue::Boolean(value),
            Self::Empty => FieldValue::Empty,
        }
    }
}
fn value(v: &FieldValue) -> Value {
    match v {
        FieldValue::Number(n) => {
            json!({"Kind":"Number", "Value": if n.is_finite() {json!(n)} else {json!(if n.is_nan() {"NaN"} else if n.is_sign_positive() {"Infinity"} else {"-Infinity"})}})
        }
        FieldValue::Text(s) => json!({"Kind":"Text","Value":s}),
        FieldValue::Boolean(b) => json!({"Kind":"Boolean","Value":b}),
        FieldValue::Empty => json!({"Kind":"Empty"}),
    }
}

#[derive(Deserialize)]
#[serde(tag = "Kind", rename_all_fields = "PascalCase", deny_unknown_fields)]
enum Definition {
    Arithmetic {
        expression: String,
    },
    JavaScript {
        script: String,
    },
    Constant {
        value: InputValue,
    },
    Sum {
        fields: Vec<String>,
    },
    Average {
        fields: Vec<String>,
    },
    Min {
        fields: Vec<String>,
    },
    Max {
        fields: Vec<String>,
    },
    Product {
        fields: Vec<String>,
    },
    Count {
        fields: Vec<String>,
    },
    If {
        condition_field: String,
        true_value: Box<Definition>,
        false_value: Box<Definition>,
    },
}
impl Definition {
    fn core(self, depth: usize) -> Result<Calculation, PdfError> {
        if depth > 16 {
            return Err(PdfError::InvalidOperation(
                "Calculation nesting exceeds 16".into(),
            ));
        }
        Ok(match self {
            Self::Arithmetic { expression } => {
                if expression.len() > 4096 {
                    return Err(PdfError::InvalidOperation(
                        "Expression exceeds 4096 bytes".into(),
                    ));
                }
                Calculation::Arithmetic(ArithmeticExpression::from_string(&expression)?)
            }
            Self::JavaScript { script } => Calculation::JavaScript(script),
            Self::Constant { value } => Calculation::Constant(value.core()),
            Self::Sum { fields } => Calculation::Function(CalculationFunction::Sum(fields)),
            Self::Average { fields } => Calculation::Function(CalculationFunction::Average(fields)),
            Self::Min { fields } => Calculation::Function(CalculationFunction::Min(fields)),
            Self::Max { fields } => Calculation::Function(CalculationFunction::Max(fields)),
            Self::Product { fields } => Calculation::Function(CalculationFunction::Product(fields)),
            Self::Count { fields } => Calculation::Function(CalculationFunction::Count(fields)),
            Self::If {
                condition_field,
                true_value,
                false_value,
            } => Calculation::Function(CalculationFunction::If {
                condition_field,
                true_value: Box::new(true_value.core(depth + 1)?),
                false_value: Box::new(false_value.core(depth + 1)?),
            }),
        })
    }
}
#[derive(Deserialize)]
#[serde(tag = "Kind", rename_all_fields = "PascalCase", deny_unknown_fields)]
enum SystemDefinition {
    Simple {
        operation: Simple,
        fields: Vec<String>,
    },
    Percent {
        base_field: String,
        percent_field: String,
        mode: PercentKind,
    },
    Date {
        start_date_field: String,
        days_field: Option<String>,
        format: String,
    },
    Range {
        field: String,
        min: Option<f64>,
        max: Option<f64>,
    },
    Number {
        field: String,
        decimals: usize,
        separator: Separator,
        currency: Option<String>,
    },
    Custom {
        script: String,
        dependencies: Vec<String>,
    },
}
impl SystemDefinition {
    fn core(self) -> JavaScriptCalculation {
        match self {
            Self::Simple { operation, fields } => JavaScriptCalculation::SimpleCalculate {
                operation: operation.into(),
                fields,
            },
            Self::Percent {
                base_field,
                percent_field,
                mode,
            } => JavaScriptCalculation::PercentCalculate {
                base_field,
                percent_field,
                mode: mode.into(),
            },
            Self::Date {
                start_date_field,
                days_field,
                format,
            } => JavaScriptCalculation::DateCalculate {
                start_date_field,
                days_field,
                format,
            },
            Self::Range { field, min, max } => {
                JavaScriptCalculation::RangeCalculate { field, min, max }
            }
            Self::Number {
                field,
                decimals,
                separator,
                currency,
            } => JavaScriptCalculation::NumberCalculate {
                field,
                decimals,
                sep_style: separator.into(),
                currency,
            },
            Self::Custom {
                script,
                dependencies,
            } => JavaScriptCalculation::Custom {
                script,
                dependencies,
            },
        }
    }
}
#[derive(Deserialize)]
#[serde(tag = "Kind", rename_all_fields = "PascalCase", deny_unknown_fields)]
enum Format {
    Number {
        decimals: usize,
        separator: Separator,
        negative_style: Negative,
        currency: Option<String>,
    },
    Percent {
        decimals: usize,
    },
    Date {
        format: String,
    },
    Time {
        format: String,
    },
    Special {
        format_type: Special,
    },
    Custom {
        format_string: String,
    },
}
impl Format {
    fn core(self) -> FieldFormat {
        match self {
            Self::Number {
                decimals,
                separator,
                negative_style,
                currency,
            } => FieldFormat::Number {
                decimals,
                separator: separator.into(),
                negative_style: negative_style.into(),
                currency,
            },
            Self::Percent { decimals } => FieldFormat::Percent { decimals },
            Self::Date { format } => FieldFormat::Date { format },
            Self::Time { format } => FieldFormat::Time { format },
            Self::Special { format_type } => FieldFormat::Special {
                format_type: format_type.into(),
            },
            Self::Custom { format_string } => FieldFormat::Custom { format_string },
        }
    }
}
#[derive(Deserialize)]
#[serde(tag = "Kind", rename_all_fields = "PascalCase", deny_unknown_fields)]
enum Step {
    Set {
        field: String,
        value: InputValue,
    },
    Get {
        field: String,
    },
    Define {
        field: String,
        definition: Definition,
    },
    DefineSystem {
        field: String,
        definition: SystemDefinition,
    },
    SetFormat {
        field: String,
        format: Format,
    },
    Remove {
        field: String,
    },
    Calculate {
        field: String,
    },
    Recalculate,
    Summary,
    Events {
        count: usize,
    },
    ClearEvents,
    ExportOrder,
    Settings,
}
#[derive(Deserialize)]
#[serde(rename_all = "PascalCase", deny_unknown_fields)]
struct Settings {
    auto_recalculate: bool,
    max_depth: usize,
    log_events: bool,
    decimal_precision: usize,
}
#[derive(Deserialize)]
#[serde(rename_all = "PascalCase", deny_unknown_fields)]
struct Request {
    system: bool,
    settings: Option<Settings>,
    steps: Vec<Step>,
}
fn error(e: PdfError, index: usize) -> Value {
    let kind = match &e {
        PdfError::InvalidStructure(_) => "InvalidStructure",
        PdfError::InvalidOperation(_) => "InvalidOperation",
        PdfError::InvalidFormat(_) => "InvalidFormat",
        _ => "CoreFailure",
    };
    json!({"Error":{"Kind":kind,"Message":e.to_string(),"StepIndex":index}})
}
// JSON-level bounds also cover nested definitions and avoid huge formatting allocations.
fn bounded(v: &Value, depth: usize) -> bool {
    if depth > 48 {
        return false;
    }
    match v {
        Value::String(s) => s.len() <= 65_536,
        Value::Array(a) => a.len() <= 256 && a.iter().all(|v| bounded(v, depth + 1)),
        Value::Object(o) => o.iter().all(|(k, v)| {
            (!(k == "Decimals" || k == "DecimalPrecision") || v.as_u64().is_some_and(|v| v <= 32))
                && (k != "MaxDepth" || v.as_u64().is_some_and(|v| v <= 256))
                && bounded(v, depth + 1)
        }),
        _ => true,
    }
}
fn run(request: Request) -> Value {
    let settings = request
        .settings
        .map(|s| CalculationSettings {
            auto_recalculate: s.auto_recalculate,
            max_depth: s.max_depth,
            log_events: s.log_events,
            decimal_precision: s.decimal_precision,
        })
        .unwrap_or_default();
    let mut system = FormCalculationSystem::with_settings(settings);
    let mut engine = CalculationEngine::new();
    let mut results = Vec::new();
    let mut response_bytes = 0usize;
    for (index, step) in request.steps.into_iter().enumerate() {
        let result = (|| -> Result<Value, PdfError> {
            let mut result = json!({});
            match (request.system, step) {
                (mode, Step::Set { field, value }) => {
                    if mode {
                        system.set_field_value(field, value.core())?
                    } else {
                        engine.set_field_value(field, value.core())
                    }
                }
                (mode, Step::Get { field }) => {
                    result["Value"] = (if mode {
                        system.get_field_value(&field)
                    } else {
                        engine.get_field_value(&field)
                    })
                    .map(value)
                    .unwrap_or(Value::Null);
                }
                (false, Step::Define { field, definition }) => {
                    engine.add_calculation(field, definition.core(0)?)?
                }
                (false, Step::Remove { field }) => engine.remove_calculation(&field),
                (false, Step::Calculate { field }) => engine.calculate_field(&field)?,
                (false, Step::Recalculate) => engine.recalculate_all()?,
                (true, Step::DefineSystem { field, definition }) => {
                    system.add_js_calculation(field, definition.core())?
                }
                (true, Step::SetFormat { field, format }) => {
                    system.set_field_format(field, format.core())
                }
                (true, Step::ClearEvents) => system.clear_events(),
                (false, Step::Summary) => {
                    let s = engine.get_summary();
                    result["EngineSummary"] = json!({"TotalFields":s.total_fields,"CalculatedFields":s.calculated_fields,"Dependencies":s.dependencies,"CalculationOrder":s.calculation_order});
                }
                (true, Step::Summary) => {
                    let s = system.get_summary();
                    result["SystemSummary"] = json!({"TotalFields":s.total_fields,"Calculations":s.js_calculations,"FormattedFields":s.formatted_fields,"EventsLogged":s.events_logged});
                }
                (true, Step::Settings) => {
                    let s = system.settings();
                    result["Settings"] = json!({"AutoRecalculate":s.auto_recalculate,"MaxDepth":s.max_depth,"LogEvents":s.log_events,"DecimalPrecision":s.decimal_precision});
                }
                (true, Step::Events { count }) => {
                    result["Events"]=json!(system.get_recent_events(count.min(65_536)).iter().map(|e|json!({"Timestamp":e.timestamp().to_rfc3339(),"Field":e.field(),"Kind":match e.event_type(){EventType::ValueChanged=>"ValueChanged",EventType::CalculationTriggered=>"CalculationTriggered",EventType::ValidationFailed=>"ValidationFailed",EventType::FormatApplied=>"FormatApplied",EventType::DependencyUpdated=>"DependencyUpdated"},"OldValue":e.old_value().map(value),"NewValue":e.new_value().map(value)})).collect::<Vec<_>>());
                }
                (true, Step::ExportOrder) => {
                    let d = system.to_pdf_dict();
                    result["ExportedOrder"] = match d.get("CO") {
                        Some(oxidize_pdf::objects::Object::Array(a)) => json!(a
                            .iter()
                            .filter_map(|v| if let oxidize_pdf::objects::Object::String(s) = v {
                                Some(s)
                            } else {
                                None
                            })
                            .collect::<Vec<_>>()),
                        _ => json!([]),
                    };
                }
                _ => {
                    return Err(PdfError::InvalidOperation(
                        "Operation is not supported by the selected engine".into(),
                    ))
                }
            }
            Ok(result)
        })();
        match result {
            Ok(r) => results.push(r),
            Err(e) => return error(e, index),
        };
        // Each result is counted once, bounding retained output across the sequence.
        let size = results.last().map_or(0, |v| v.to_string().len());
        response_bytes += size;
        if size > 4_194_304 || response_bytes > 8_388_608 {
            return error(
                PdfError::InvalidOperation("Response exceeds calculation limits".into()),
                index,
            );
        }
    }
    json!({"Results":results})
}
/// Executes one isolated sequence. Free output with oxidize_free_string.
/// # Safety
/// Input must contain length readable bytes, output must be writable. Neither is retained.
#[no_mangle]
pub unsafe extern "C" fn oxidize_calculations(
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
            set_last_error("Calculation request must be 1..1048576 bytes");
            return ErrorCode::InvalidArgument as c_int;
        }
        let parsed = serde_json::from_slice::<Value>(std::slice::from_raw_parts(input, length));
        let request = match parsed {
            Ok(v) if bounded(&v, 0) => serde_json::from_value::<Request>(v),
            _ => {
                set_last_error("Invalid or excessive calculation request");
                return ErrorCode::InvalidArgument as c_int;
            }
        };
        let request = match request {
            Ok(r) => r,
            Err(e) => {
                set_last_error(e.to_string());
                return ErrorCode::InvalidArgument as c_int;
            }
        };
        match CString::new(run(request).to_string()) {
            Ok(s) => {
                *output = s.into_raw();
                0
            }
            Err(e) => {
                set_last_error(e.to_string());
                ErrorCode::SerializationError as c_int
            }
        }
    })
}
