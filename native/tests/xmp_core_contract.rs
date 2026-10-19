//! Acceptance probes for #91 against the pinned upstream core.
//! These assert the required behavior, not the known defects. Run explicitly:
//! cargo test --offline --manifest-path native/Cargo.toml --test xmp_core_contract -- --include-ignored
use oxidize_pdf::pdfa::PdfAConformance;
use oxidize_pdf::pdfa::{XmpMetadata, XmpPdfAIdentifier};

#[test]
fn keywords_survive_serialization() {
    let original = XmpMetadata {
        keywords: Some(vec!["café".into(), "日本語".into()]),
        ..Default::default()
    };
    let parsed = XmpMetadata::parse(&original.to_xml()).unwrap();
    assert_eq!(parsed.keywords, original.keywords);
}

#[test]
fn unicode_and_entities_round_trip() {
    let original = XmpMetadata {
        title: Some("café & 日本語 <title>".into()),
        creator: Some(vec!["A & B".into()]),
        ..Default::default()
    };
    let parsed = XmpMetadata::parse(&original.to_xml()).unwrap();
    assert_eq!(parsed.title, original.title);
    assert_eq!(parsed.creator, original.creator);
}

#[test]
fn malformed_xml_is_rejected() {
    assert!(XmpMetadata::parse("<x:xmpmeta><broken>").is_err());
}

#[test]
fn equivalent_namespace_prefix_is_recognized() {
    let original = XmpMetadata {
        title: Some("Namespace title".into()),
        ..Default::default()
    };
    let xml = original
        .to_xml()
        .replace("xmlns:dc=", "xmlns:d=")
        .replace("dc:title", "d:title");
    assert_eq!(XmpMetadata::parse(&xml).unwrap().title, original.title);
}

#[test]
fn identifier_text_cannot_inject_properties() {
    let mut id = XmpPdfAIdentifier::new(2, PdfAConformance::B);
    id.amd = Some("</pdfaid:amd><dc:title>injected</dc:title><pdfaid:amd>".into());
    let metadata = XmpMetadata {
        pdfa_id: Some(id),
        ..Default::default()
    };
    let xml = metadata.to_xml();
    assert!(xml.contains("&lt;/pdfaid:amd&gt;"));
    assert_eq!(XmpMetadata::parse(&xml).unwrap().title, None);
}

#[test]
fn ordinary_unicode_and_identifier_control() {
    let original = XmpMetadata {
        title: Some("café 日本語".into()),
        pdfa_id: Some(XmpPdfAIdentifier::new(2, PdfAConformance::B)),
        ..Default::default()
    };
    let parsed = XmpMetadata::parse(&original.to_xml()).unwrap();
    assert_eq!(parsed.title, original.title);
    assert_eq!(parsed.pdfa_id, original.pdfa_id);
    assert!(parsed.validate_for_pdfa().is_ok());
}
