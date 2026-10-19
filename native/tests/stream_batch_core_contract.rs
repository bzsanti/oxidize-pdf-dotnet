//! #100/#101 regression probes against pinned core 5.5.0, not FFI acceptance.
use oxidize_pdf::batch::{BatchJob, BatchOptions, BatchProcessor};
use oxidize_pdf::memory::{LazyDocument, MemoryOptions};
use oxidize_pdf::parser::PdfReader;
use oxidize_pdf::streaming::PageStreamer;
use oxidize_pdf::{Document, Page, PdfError};
use std::io::Cursor;
use std::sync::{
    atomic::{AtomicBool, AtomicUsize, Ordering},
    Arc,
};
use std::time::Duration;

fn one_page() -> Vec<u8> {
    let mut doc = Document::new();
    doc.add_page(Page::new(321.0, 456.0));
    doc.to_bytes().unwrap()
}

#[test]
fn ordinary_reader_one_page_control() {
    let mut reader = PdfReader::new(Cursor::new(one_page())).unwrap();
    assert_eq!(reader.page_count().unwrap(), 1);
}

#[test]
fn lazy_document_exposes_real_page_count() {
    let reader = PdfReader::new(Cursor::new(one_page())).unwrap();
    let lazy = LazyDocument::new(reader, MemoryOptions::default()).unwrap();
    assert_eq!(lazy.page_count(), 1);
    assert!(lazy.get_page(0).is_ok());
}

#[test]
fn streamer_returns_real_geometry_and_count() {
    let mut streamer = PageStreamer::new(Cursor::new(one_page()));
    let mut dimensions = Vec::new();
    while let Some(page) = streamer.next().unwrap() {
        dimensions.push((page.width(), page.height()));
        assert!(dimensions.len() < 10, "bounded diagnostic iteration");
    }
    assert_eq!(dimensions, vec![(321.0, 456.0)]);
}

#[test]
fn batch_mixed_results_keep_order_and_worker_bound_control() {
    let active = Arc::new(AtomicUsize::new(0));
    let peak = Arc::new(AtomicUsize::new(0));
    let mut processor = BatchProcessor::new(BatchOptions::default().with_parallelism(2));
    for index in 0..8 {
        let active = active.clone();
        let peak = peak.clone();
        processor.add_job(BatchJob::Custom {
            name: format!("job-{index}"),
            operation: Box::new(move || {
                let count = active.fetch_add(1, Ordering::SeqCst) + 1;
                peak.fetch_max(count, Ordering::SeqCst);
                std::thread::sleep(Duration::from_millis(5));
                active.fetch_sub(1, Ordering::SeqCst);
                if index % 2 == 0 {
                    Ok(())
                } else {
                    Err(PdfError::InvalidStructure(format!("failure-{index}")))
                }
            }),
        });
    }
    let summary = processor.execute().unwrap();
    assert_eq!(
        (summary.total_jobs, summary.successful, summary.failed),
        (8, 4, 4)
    );
    assert_eq!(summary.results.len(), 8);
    for (index, result) in summary.results.iter().enumerate() {
        assert_eq!(result.job_name(), format!("job-{index}"));
        assert_eq!(result.is_success(), index % 2 == 0);
        if index % 2 != 0 {
            assert!(result
                .error()
                .unwrap()
                .contains(&format!("failure-{index}")));
        }
    }
    assert!((1..=2).contains(&peak.load(Ordering::SeqCst)));
    assert_eq!(active.load(Ordering::SeqCst), 0);
}

#[test]
fn job_exceeding_timeout_is_not_reported_successful() {
    let mut processor = BatchProcessor::new(
        BatchOptions::default()
            .with_parallelism(1)
            .with_job_timeout(Duration::from_millis(1)),
    );
    processor.add_job(BatchJob::Custom {
        name: "exceeds-timeout".into(),
        operation: Box::new(|| {
            std::thread::sleep(Duration::from_millis(30));
            Ok(())
        }),
    });
    let summary = processor.execute().unwrap();
    assert_eq!(summary.results.len(), 1);
    assert!(
        !summary.results[0].is_success(),
        "30ms job reported successful despite 1ms timeout"
    );
}

#[test]
fn serial_stop_on_error_prevents_later_operation() {
    let ran = Arc::new(AtomicBool::new(false));
    let mut processor = BatchProcessor::new(
        BatchOptions::default()
            .with_parallelism(1)
            .stop_on_error(true),
    );
    processor.add_job(BatchJob::Custom {
        name: "fails-first".into(),
        operation: Box::new(|| Err(PdfError::InvalidStructure("expected".into()))),
    });
    let observed = ran.clone();
    processor.add_job(BatchJob::Custom {
        name: "must-not-run".into(),
        operation: Box::new(move || {
            observed.store(true, Ordering::SeqCst);
            Ok(())
        }),
    });
    let summary = processor.execute().unwrap();
    assert_eq!(summary.failed, 1);
    assert!(
        !ran.load(Ordering::SeqCst),
        "second operation executed after serial failure"
    );
}
