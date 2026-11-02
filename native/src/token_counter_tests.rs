use std::ffi::CString;
use std::ptr;

#[test]
fn count_checks_utf8_counter_and_null_boundaries() {
    use crate::token_counter::{oxidize_count_tokens, oxidize_token_counter_available};
    let mut count = 999;
    assert_eq!(
        unsafe { oxidize_count_tokens(ptr::null(), 0, 0, &mut count) },
        0
    );
    assert_eq!(count, 0);
    assert_eq!(
        unsafe { oxidize_count_tokens(ptr::null(), 1, 0, &mut count) },
        1
    );
    assert_eq!(
        unsafe { oxidize_count_tokens([255].as_ptr(), 1, 0, &mut count) },
        2
    );
    assert_eq!(
        unsafe { oxidize_count_tokens(b"x".as_ptr(), 1, 99, &mut count) },
        9
    );
    assert_eq!(count, 0);
    assert_eq!(
        unsafe { oxidize_count_tokens(b"x".as_ptr(), 1, 0, ptr::null_mut()) },
        1
    );
    assert_eq!(oxidize_token_counter_available(0), 1);
    assert_eq!(
        oxidize_token_counter_available(1),
        u8::from(cfg!(feature = "tiktoken"))
    );
    assert_eq!(oxidize_token_counter_available(99), 0);
    let rc = unsafe { oxidize_count_tokens(b"a\nb".as_ptr(), 3, 1, &mut count) };
    if cfg!(feature = "tiktoken") {
        assert_eq!(rc, 0);
        assert_eq!(count, 3);
    } else {
        assert_eq!(rc, 11);
        assert_eq!(count, 0);
    }
}

#[cfg(not(feature = "tiktoken"))]
#[test]
fn both_chunkers_refuse_exact_counter_without_feature() {
    let pdf = pdf();
    for semantic in [true, false] {
        let json = if semantic {
            r#"{"max_tokens":6,"overlap_tokens":0,"respect_element_boundaries":true,"token_counter":"Cl100kBase"}"#
        } else {
            r#"{"max_tokens":6,"overlap_tokens":0,"merge_adjacent":true,"propagate_headings":false,"merge_policy":"AnyInlineContent","token_counter":"Cl100kBase"}"#
        };
        let json = CString::new(json).unwrap();
        let mut out = ptr::null_mut();
        let rc = unsafe {
            if semantic {
                crate::parser::oxidize_semantic_chunks(
                    pdf.as_ptr(),
                    pdf.len(),
                    ptr::null(),
                    json.as_ptr(),
                    &mut out,
                )
            } else {
                crate::parser::oxidize_rag_chunks_with_config(
                    pdf.as_ptr(),
                    pdf.len(),
                    ptr::null(),
                    json.as_ptr(),
                    &mut out,
                )
            }
        };
        assert_eq!(rc, 11);
        assert!(out.is_null());
    }
}

fn pdf() -> Vec<u8> {
    let mut doc = oxidize_pdf::Document::new();
    let mut page = oxidize_pdf::Page::a4();
    page.text()
        .set_font(oxidize_pdf::Font::Helvetica, 12.0)
        .at(50.0, 700.0)
        .write("Hello, world! One more sentence.")
        .unwrap();
    doc.add_page(page);
    doc.to_bytes().unwrap()
}

#[test]
fn semantic_rejects_unknown_counter_instead_of_silently_using_words() {
    let pdf = pdf();
    let config = CString::new(r#"{"max_tokens":8,"overlap_tokens":0,"respect_element_boundaries":true,"token_counter":"Unknown"}"#).unwrap();
    let mut out = ptr::null_mut();
    let rc = unsafe {
        crate::parser::oxidize_semantic_chunks(
            pdf.as_ptr(),
            pdf.len(),
            ptr::null(),
            config.as_ptr(),
            &mut out,
        )
    };
    if !out.is_null() {
        unsafe { crate::oxidize_free_string(out) };
    }
    assert_eq!(rc, 9);
}

#[test]
fn hybrid_rejects_unknown_counter_instead_of_silently_using_words() {
    let pdf = pdf();
    let config = CString::new(r#"{"max_tokens":8,"overlap_tokens":0,"merge_adjacent":true,"propagate_headings":false,"merge_policy":"AnyInlineContent","token_counter":"Unknown"}"#).unwrap();
    let mut out = ptr::null_mut();
    let rc = unsafe {
        crate::parser::oxidize_rag_chunks_with_config(
            pdf.as_ptr(),
            pdf.len(),
            ptr::null(),
            config.as_ptr(),
            &mut out,
        )
    };
    if !out.is_null() {
        unsafe { crate::oxidize_free_string(out) };
    }
    assert_eq!(rc, 9);
}
