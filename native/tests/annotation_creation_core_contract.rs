//! #98 final-PDF core probes; no FFI acceptance claim.
use oxidize_pdf::annotations::{
    Annotation, FileAttachmentAnnotation, FreeTextAnnotation, InkAnnotation,
};
use oxidize_pdf::geometry::{Point, Rectangle};
use oxidize_pdf::parser::{objects::PdfDictionary, PdfReader};
use oxidize_pdf::{Document, Page};
use std::io::Cursor;

fn rect() -> Rectangle {
    Rectangle::new(Point::new(10.0, 10.0), Point::new(100.0, 60.0))
}

fn serialized_annotation(annotation: Annotation) -> PdfDictionary {
    let mut page = Page::new(300.0, 300.0);
    page.add_annotation(annotation);
    let mut document = Document::new();
    document.add_page(page);
    let bytes = document.to_bytes().unwrap();
    let mut reader = PdfReader::new(Cursor::new(bytes.as_slice())).unwrap();
    let pages = reader.pages().unwrap();
    let (page_number, page_generation) = pages.get("Kids").unwrap().as_array().unwrap().0[0]
        .as_reference()
        .unwrap();
    let page = reader
        .get_object(page_number, page_generation)
        .unwrap()
        .as_dict()
        .unwrap();
    let annots = page.get("Annots").unwrap().as_array().unwrap();
    assert_eq!(annots.0.len(), 1);
    let (number, generation) = annots.0[0].as_reference().unwrap();
    reader
        .get_object(number, generation)
        .unwrap()
        .as_dict()
        .unwrap()
        .clone()
}

#[test]
fn free_text_serialized_contents_and_alignment_control() {
    let dict = serialized_annotation(
        FreeTextAnnotation::new(rect(), "control text")
            .with_justification(2)
            .to_annotation(),
    );
    assert_eq!(
        dict.get("Subtype").unwrap().as_name().unwrap().0,
        "FreeText"
    );
    assert_eq!(
        dict.get("Contents")
            .unwrap()
            .as_string()
            .unwrap()
            .as_bytes(),
        b"control text"
    );
    assert_eq!(dict.get("Q").unwrap().as_integer(), Some(2));
}

#[test]
fn ink_serialized_stroke_coordinates_control() {
    let dict = serialized_annotation(
        InkAnnotation::new()
            .add_stroke(vec![Point::new(10.0, 20.0), Point::new(30.0, 40.0)])
            .to_annotation(),
    );
    assert_eq!(dict.get("Subtype").unwrap().as_name().unwrap().0, "Ink");
    let strokes = dict.get("InkList").unwrap().as_array().unwrap();
    assert_eq!(strokes.0.len(), 1);
    let points = strokes.0[0].as_array().unwrap();
    let values: Vec<f64> = points.0.iter().map(|v| v.as_real().unwrap()).collect();
    assert_eq!(values, vec![10.0, 20.0, 30.0, 40.0]);
}

#[test]
fn file_attachment_serializes_an_indirect_embedded_file() {
    let dict = serialized_annotation(
        FileAttachmentAnnotation::new(
            rect(),
            "payload.txt".into(),
            b"unique attachment payload".to_vec(),
        )
        .to_annotation(),
    );
    assert_eq!(
        dict.get("Subtype").unwrap().as_name().unwrap().0,
        "FileAttachment"
    );
    let spec = dict.get("FS").unwrap().as_dict().unwrap();
    assert_eq!(
        spec.get("F").unwrap().as_string().unwrap().as_bytes(),
        b"payload.txt"
    );
    let embedded = spec.get("EF").unwrap().as_dict().unwrap().get("F").unwrap();
    assert!(
        embedded.as_reference().is_some(),
        "Expected indirect EmbeddedFile stream; got {embedded:?}"
    );
}

#[test]
fn attachment_with_mime_type_remains_readable() {
    let dict = serialized_annotation(
        FileAttachmentAnnotation::new(rect(), "payload.txt".into(), b"payload".to_vec())
            .with_mime_type("text/plain".into())
            .to_annotation(),
    );
    let subtype = dict.get("Subtype").and_then(|value| value.as_name());
    assert_eq!(subtype.map(|name| name.0.as_str()), Some("FileAttachment"));
}
