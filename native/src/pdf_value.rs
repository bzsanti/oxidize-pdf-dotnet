use base64::Engine as _;
use oxidize_pdf::parser::objects::{PdfArray, PdfDictionary, PdfName, PdfObject, PdfString};
use serde::Deserialize;

#[derive(Deserialize)]
#[serde(tag = "type", content = "value")]
pub(crate) enum PdfValue {
    Null,
    Boolean(bool),
    Integer(i64),
    Real(f64),
    Text(String),
    Name(String),
    Bytes(String),
    Array(Vec<PdfValue>),
    Dictionary(std::collections::BTreeMap<String, PdfValue>),
    Reference((u32, u16)),
}
impl PdfValue {
    pub(crate) fn core(self) -> Result<PdfObject, String> {
        Ok(match self {
            Self::Null => PdfObject::Null,
            Self::Boolean(b) => PdfObject::Boolean(b),
            Self::Integer(n) => PdfObject::Integer(n),
            Self::Real(n) => {
                if !n.is_finite() {
                    return Err("PDF real must be finite".into());
                }
                PdfObject::Real(n)
            }
            Self::Text(text) => {
                let mut bytes = vec![0xfe, 0xff];
                bytes.extend(text.encode_utf16().flat_map(u16::to_be_bytes));
                PdfObject::String(PdfString::new(bytes))
            }
            Self::Name(name) => PdfObject::Name(PdfName(name)),
            Self::Bytes(bytes) => PdfObject::String(PdfString::new(
                base64::engine::general_purpose::STANDARD
                    .decode(bytes)
                    .map_err(|e| e.to_string())?,
            )),
            Self::Array(a) => PdfObject::Array(PdfArray(
                a.into_iter().map(Self::core).collect::<Result<_, _>>()?,
            )),
            Self::Dictionary(d) => {
                let mut result = PdfDictionary::new();
                for (k, v) in d {
                    result.insert(k, v.core()?);
                }
                PdfObject::Dictionary(result)
            }
            Self::Reference((n, g)) => PdfObject::Reference(n, g),
        })
    }
}

pub(crate) fn dictionary_json(d: &PdfDictionary) -> serde_json::Value {
    serde_json::Value::Object(
        d.0.iter()
            .map(|(k, v)| (k.0.clone(), object_json(v)))
            .collect(),
    )
}
pub(crate) fn object_json(o: &PdfObject) -> serde_json::Value {
    use serde_json::json;
    match o {
        PdfObject::Null => json!({"type":"Null"}),
        PdfObject::Boolean(v) => json!({"type":"Boolean","value":v}),
        PdfObject::Integer(v) => json!({"type":"Integer","value":v}),
        PdfObject::Real(v) => json!({"type":"Real","value":v}),
        PdfObject::Name(v) => json!({"type":"Name","value":v.0}),
        PdfObject::String(v) => {
            json!({"type":"Bytes","value":base64::engine::general_purpose::STANDARD.encode(v.as_bytes())})
        }
        PdfObject::Reference(n, g) => json!({"type":"Reference","value":[n, &u32::from(*g)]}),
        PdfObject::Array(v) => {
            json!({"type":"Array","value":v.0.iter().map(object_json).collect::<Vec<_>>()})
        }
        PdfObject::Dictionary(v) => json!({"type":"Dictionary","value":dictionary_json(v)}),
        PdfObject::Stream(v) => {
            json!({"type":"Stream","value":{"dictionary":dictionary_json(&v.dict),"data":base64::engine::general_purpose::STANDARD.encode(&v.data)}})
        }
    }
}
