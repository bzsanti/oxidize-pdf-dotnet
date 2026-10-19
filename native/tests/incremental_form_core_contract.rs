//! Direct pinned-core probes for #92. No binding replacement or acceptance claim.
use oxidize_pdf::parser::PdfReader;
use oxidize_pdf::writer::IncrementalFormFiller;
use std::io::Cursor;

// Independently authored pre-existing PDF, with accurate byte offsets.
fn template(read_only: bool) -> Vec<u8> {
    template_with_policy(read_only, false)
}
fn template_with_policy(read_only: bool, certified: bool) -> Vec<u8> {
    let mut objects = vec![
        "<< /Type /Catalog /Pages 2 0 R /AcroForm 4 0 R >>".to_string(),
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>".to_string(),
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Annots [5 0 R] >>".to_string(),
        "<< /Fields [5 0 R] /DA (/Helv 12 Tf 0 g) >>".to_string(),
        format!("<< /Type /Annot /Subtype /Widget /FT /Tx /T (name) /V (original) /Ff {} /Rect [10 10 200 40] /P 3 0 R >>", u8::from(read_only)),
    ];
    if certified {
        objects[0] =
            "<< /Type /Catalog /Pages 2 0 R /AcroForm 4 0 R /Perms << /DocMDP 6 0 R >> >>".into();
        objects.push("<< /Type /Sig /ByteRange [0 999999 1000001 1] /Contents <0000> /Reference [<< /TransformMethod /DocMDP /TransformParams << /P 1 /V /1.2 >> >>] >>".into());
    }
    let size = objects.len() + 1;
    let mut pdf = b"%PDF-1.7\n".to_vec();
    let mut offsets = vec![0];
    for (i, object) in objects.iter().enumerate() {
        offsets.push(pdf.len());
        pdf.extend_from_slice(format!("{} 0 obj\n{object}\nendobj\n", i + 1).as_bytes());
    }
    let xref = pdf.len();
    pdf.extend_from_slice(format!("xref\n0 {size}\n0000000000 65535 f \n").as_bytes());
    for offset in offsets.iter().skip(1) {
        pdf.extend_from_slice(format!("{offset:010} 00000 n \n").as_bytes());
    }
    pdf.extend_from_slice(
        format!("trailer\n<< /Size {size} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n").as_bytes(),
    );
    pdf
}

#[test]
fn writable_control_preserves_prefix_and_updates_value() {
    let base = template(false);
    let output = IncrementalFormFiller::new(&base)
        .fill("name", "changed")
        .unwrap();
    assert!(output.starts_with(&base));
    let mut reader = PdfReader::new(Cursor::new(output.as_slice())).unwrap();
    let field = reader.get_object(5, 0).unwrap().as_dict().unwrap();
    assert_eq!(
        field.get("V").unwrap().as_string().unwrap().as_bytes(),
        b"changed"
    );
}

#[test]
fn missing_field_batch_returns_no_output() {
    let base = template(false);
    assert!(IncrementalFormFiller::new(&base)
        .fill_many(&[("name", "changed"), ("missing", "bad")])
        .is_err());
}

#[test]
fn read_only_field_rejects_mutation() {
    let base = template(true);
    let result = IncrementalFormFiller::new(&base).fill("name", "changed");
    assert!(
        result.is_err(),
        "ReadOnly /Ff 1 must reject filling, but core returned an updated PDF"
    );
}

#[test]
#[ignore = "#92 core 5.5.0 filler does not invoke certification policy checks"]
fn certified_no_changes_policy_rejects_form_fill() {
    let base = template_with_policy(false, true);
    // Same policy is understood and rejected by the public core object editor.
    assert!(matches!(
        oxidize_pdf::writer::IncrementalObjectEditor::new(&base),
        Err(oxidize_pdf::PdfError::PermissionDenied(_))
    ));
    assert!(
        matches!(
            IncrementalFormFiller::new(&base).fill("name", "changed"),
            Err(oxidize_pdf::PdfError::PermissionDenied(_))
        ),
        "DocMDP P=1 must not allow a form-field update"
    );
}
