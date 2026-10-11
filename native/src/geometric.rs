//! Incremental geometric editing over a JSON FFI boundary.
use crate::{clear_last_error, set_last_error, ErrorCode};
use base64::Engine as _;
use oxidize_pdf::writer::{
    GeometricAnnotation, GeometricColor, GeometricDashPattern, GeometricGeometry, GeometricId,
    GeometricMutation, GeometricOpacity, GeometricStyle, GeometricWidth,
    IncrementalGeometricEditor, LineEnding,
};
use serde::{Deserialize, Serialize};
use serde_json::{json, Value};
use std::ffi::{CStr, CString};
use std::os::raw::{c_char, c_int};

#[derive(Deserialize)]
#[serde(tag = "operation", rename_all = "snake_case", deny_unknown_fields)]
enum Mutation {
    Add {
        page_index: u32,
        geometry: Geometry,
        style: Style,
    },
    Update {
        object_number: u32,
        generation_number: u16,
        geometry: Geometry,
        style: Style,
    },
    Remove {
        object_number: u32,
        generation_number: u16,
    },
}
#[derive(Deserialize, Serialize)]
#[serde(tag = "kind", deny_unknown_fields)]
enum Geometry {
    Line {
        start: [f64; 2],
        end: [f64; 2],
        start_ending: Ending,
        end_ending: Ending,
    },
    Square {
        rect: [f64; 4],
    },
    Circle {
        rect: [f64; 4],
    },
    Polygon {
        vertices: Vec<[f64; 2]>,
    },
    PolyLine {
        vertices: Vec<[f64; 2]>,
        start_ending: Ending,
        end_ending: Ending,
    },
}
macro_rules! endings {
    ($($variant:ident),+ $(,)?) => {
        #[derive(Deserialize, Serialize)]
        enum Ending { $($variant),+ }
        impl From<Ending> for LineEnding { fn from(value: Ending) -> Self { match value { $(Ending::$variant => Self::$variant),+ } } }
        impl From<LineEnding> for Ending { fn from(value: LineEnding) -> Self { match value { $(LineEnding::$variant => Self::$variant),+ } } }
    }
}
endings!(
    None,
    Square,
    Circle,
    Diamond,
    OpenArrow,
    ClosedArrow,
    Butt,
    ROpenArrow,
    RClosedArrow,
    Slash
);
fn point(p: [f64; 2]) -> oxidize_pdf::Point {
    oxidize_pdf::Point::new(p[0], p[1])
}
fn pair(p: oxidize_pdf::Point) -> [f64; 2] {
    [p.x, p.y]
}
impl From<Geometry> for GeometricGeometry {
    fn from(g: Geometry) -> Self {
        match g {
            Geometry::Line {
                start,
                end,
                start_ending,
                end_ending,
            } => Self::Line {
                start: point(start),
                end: point(end),
                start_ending: start_ending.into(),
                end_ending: end_ending.into(),
            },
            Geometry::Square { rect } => Self::Square { rect },
            Geometry::Circle { rect } => Self::Circle { rect },
            Geometry::Polygon { vertices } => Self::Polygon {
                vertices: vertices.into_iter().map(point).collect(),
            },
            Geometry::PolyLine {
                vertices,
                start_ending,
                end_ending,
            } => Self::PolyLine {
                vertices: vertices.into_iter().map(point).collect(),
                start_ending: start_ending.into(),
                end_ending: end_ending.into(),
            },
        }
    }
}
impl From<GeometricGeometry> for Geometry {
    fn from(g: GeometricGeometry) -> Self {
        match g {
            GeometricGeometry::Line {
                start,
                end,
                start_ending,
                end_ending,
            } => Self::Line {
                start: pair(start),
                end: pair(end),
                start_ending: start_ending.into(),
                end_ending: end_ending.into(),
            },
            GeometricGeometry::Square { rect } => Self::Square { rect },
            GeometricGeometry::Circle { rect } => Self::Circle { rect },
            GeometricGeometry::Polygon { vertices } => Self::Polygon {
                vertices: vertices.into_iter().map(pair).collect(),
            },
            GeometricGeometry::PolyLine {
                vertices,
                start_ending,
                end_ending,
            } => Self::PolyLine {
                vertices: vertices.into_iter().map(pair).collect(),
                start_ending: start_ending.into(),
                end_ending: end_ending.into(),
            },
        }
    }
}
#[derive(Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
struct Style {
    stroke_color: Vec<f64>,
    fill_color: Option<Vec<f64>>,
    width: f64,
    dash_pattern: Option<Vec<f64>>,
    opacity: Option<f64>,
}
fn color(c: Vec<f64>) -> Result<GeometricColor, String> {
    match c.as_slice() {
        [g] => GeometricColor::gray(*g),
        [r, g, b] => GeometricColor::rgb([*r, *g, *b]),
        [c, m, y, k] => GeometricColor::cmyk([*c, *m, *y, *k]),
        _ => return Err("Color requires 1, 3 or 4 components".into()),
    }
    .map_err(|e| e.to_string())
}
fn components(c: GeometricColor) -> Vec<f64> {
    match c {
        GeometricColor::Gray(v) => v.to_vec(),
        GeometricColor::Rgb(v) => v.to_vec(),
        GeometricColor::Cmyk(v) => v.to_vec(),
    }
}
impl Style {
    fn into_core(self) -> Result<GeometricStyle, String> {
        Ok(GeometricStyle {
            stroke_color: color(self.stroke_color)?,
            fill_color: self.fill_color.map(color).transpose()?,
            width: GeometricWidth::new(self.width).map_err(|e| e.to_string())?,
            dash_pattern: self
                .dash_pattern
                .map(GeometricDashPattern::new)
                .transpose()
                .map_err(|e| e.to_string())?,
            opacity: self
                .opacity
                .map(GeometricOpacity::new)
                .transpose()
                .map_err(|e| e.to_string())?,
        })
    }
}
impl From<GeometricStyle> for Style {
    fn from(s: GeometricStyle) -> Self {
        Self {
            stroke_color: components(s.stroke_color),
            fill_color: s.fill_color.map(components),
            width: s.width.value(),
            dash_pattern: s.dash_pattern.map(|p| p.values().to_vec()),
            opacity: s.opacity.map(|o| o.value()),
        }
    }
}
impl Mutation {
    fn into_core(self) -> Result<GeometricMutation, String> {
        Ok(match self {
            Self::Add {
                page_index,
                geometry,
                style,
            } => GeometricMutation::Add {
                page_index,
                geometry: geometry.into(),
                style: style.into_core()?,
            },
            Self::Update {
                object_number,
                generation_number,
                geometry,
                style,
            } => GeometricMutation::Update {
                id: GeometricId::new(object_number, generation_number),
                geometry: geometry.into(),
                style: style.into_core()?,
            },
            Self::Remove {
                object_number,
                generation_number,
            } => GeometricMutation::Remove {
                id: GeometricId::new(object_number, generation_number),
            },
        })
    }
}
fn annotation_json(h: GeometricAnnotation) -> Value {
    json!({ "id": {"object_number": h.id.object_number, "generation_number": h.id.generation_number},
        "page_index": h.page_index, "geometry": Geometry::from(h.geometry), "style": Style::from(h.style) })
}

/// List annotations when `mutations_json` is null, or apply an atomic add/update/remove batch.
///
/// # Safety
/// `bytes` points to `len` readable bytes; non-null `mutations_json` points to a
/// NUL-terminated UTF-8 string. `out_json` is writable. Free returned JSON using
/// `oxidize_free_string`. The JSON contains base64 PDF bytes only for edits.
#[no_mangle]
pub unsafe extern "C" fn oxidize_geometric_json(
    bytes: *const u8,
    len: usize,
    mutations_json: *const c_char,
    out_json: *mut *mut c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if bytes.is_null() || out_json.is_null() {
            set_last_error("Null pointer in geometric operation");
            return ErrorCode::NullPointer as c_int;
        }
        *out_json = std::ptr::null_mut();
        if len == 0 {
            set_last_error("PDF data must not be empty");
            return ErrorCode::InvalidArgument as c_int;
        }
        let editor = IncrementalGeometricEditor::new(std::slice::from_raw_parts(bytes, len));
        let result = (|| -> Result<Value, (ErrorCode, String)> {
            if mutations_json.is_null() {
                let annotations = editor
                    .annotations()
                    .map_err(|e| (ErrorCode::PdfParseError, e.to_string()))?;
                return Ok(
                    json!({ "annotations": annotations.into_iter().map(annotation_json).collect::<Vec<_>>() }),
                );
            }
            let input = CStr::from_ptr(mutations_json)
                .to_str()
                .map_err(|e| (ErrorCode::InvalidUtf8, e.to_string()))?;
            let dtos: Vec<Mutation> = serde_json::from_str(input)
                .map_err(|e| (ErrorCode::SerializationError, e.to_string()))?;
            if dtos.is_empty() {
                return Err((
                    ErrorCode::InvalidArgument,
                    "At least one mutation is required".into(),
                ));
            }
            let mutations = dtos
                .into_iter()
                .map(Mutation::into_core)
                .collect::<Result<Vec<_>, _>>()
                .map_err(|e| (ErrorCode::InvalidArgument, e))?;
            let update = editor
                .apply(&mutations)
                .map_err(|e| (ErrorCode::InvalidArgument, e.to_string()))?;
            Ok(
                json!({ "pdf_bytes": base64::engine::general_purpose::STANDARD.encode(update.pdf_bytes),
                "annotations": update.annotations.into_iter().map(annotation_json).collect::<Vec<_>>() }),
            )
        })();
        match result {
            Ok(value) => match CString::new(value.to_string()) {
                Ok(json) => {
                    *out_json = json.into_raw();
                    ErrorCode::Success as c_int
                }
                Err(e) => {
                    set_last_error(e.to_string());
                    ErrorCode::SerializationError as c_int
                }
            },
            Err((code, message)) => {
                set_last_error(message);
                code as c_int
            }
        }
    })
}
