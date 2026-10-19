//! Core batch scheduling; callbacks are synchronous and joined before returning.
use crate::{clear_last_error, set_last_error, ErrorCode};
use oxidize_pdf::batch::{BatchJob, BatchOptions, BatchProcessor, JobResult};
use serde::Deserialize;
use serde_json::json;
use std::{
    ffi::CString,
    os::raw::{c_char, c_int},
    path::PathBuf,
    time::Duration,
};

#[derive(Deserialize)]
#[serde(tag = "Kind", rename_all_fields = "PascalCase", deny_unknown_fields)]
enum Job {
    Split {
        input: PathBuf,
        output_pattern: String,
        pages_per_file: usize,
    },
    Merge {
        inputs: Vec<PathBuf>,
        output: PathBuf,
    },
    Rotate {
        input: PathBuf,
        output: PathBuf,
        rotation: i32,
        pages: Option<Vec<usize>>,
    },
    Extract {
        input: PathBuf,
        output: PathBuf,
        pages: Vec<usize>,
    },
    Compress {
        input: PathBuf,
        output: PathBuf,
        quality: u8,
    },
    Custom {
        name: String,
    },
}
#[derive(Deserialize)]
#[serde(rename_all = "PascalCase", deny_unknown_fields)]
struct Options {
    parallelism: usize,
    memory_limit_per_worker: usize,
    progress_interval_ms: u64,
    stop_on_error: bool,
    job_timeout_ms: Option<u64>,
}
#[derive(Deserialize)]
#[serde(rename_all = "PascalCase", deny_unknown_fields)]
struct Request {
    jobs: Vec<Job>,
    options: Options,
}
type JobCallback = extern "C" fn(usize) -> c_int;
type ProgressCallback = extern "C" fn(*const u8, usize);
/// Execute a core batch. JSON output is released with oxidize_free_string.
/// # Safety
/// Input/output buffers must be valid for stated lengths. Callbacks must remain valid,
/// be thread-safe and never unwind, and may run on native worker/progress threads.
/// No callback is retained after this function returns, including error paths.
#[no_mangle]
pub unsafe extern "C" fn oxidize_batch(
    input: *const u8,
    length: usize,
    job_callback: Option<JobCallback>,
    progress_callback: Option<ProgressCallback>,
    output: *mut *mut c_char,
) -> c_int {
    crate::ffi_guard(move || {
        clear_last_error();
        if output.is_null() {
            return 1;
        }
        *output = std::ptr::null_mut();
        if input.is_null() {
            return 1;
        }
        if length == 0 || length > 1_048_576 {
            set_last_error("Batch request must be 1..1048576 bytes");
            return 9;
        }
        let request =
            match serde_json::from_slice::<Request>(std::slice::from_raw_parts(input, length)) {
                Ok(r) => r,
                Err(e) => {
                    set_last_error(e.to_string());
                    return 9;
                }
            };
        if request.jobs.len() > 256
            || request.options.parallelism == 0
            || request.options.parallelism > 64
            || request.options.progress_interval_ms > 60_000
        {
            set_last_error("Batch limits: 256 jobs, 1..64 workers, progress interval <=60000ms");
            return 9;
        }
        if request
            .jobs
            .iter()
            .any(|job| matches!(job, Job::Rotate { .. } | Job::Compress { .. }))
        {
            set_last_error(
                "Rotate and Compress are unsupported: core 5.5.0 only copies files for these jobs",
            );
            return ErrorCode::InvalidArgument as c_int;
        }
        let mut jobs = Vec::new();
        for (index, job) in request.jobs.into_iter().enumerate() {
            jobs.push(match job {
                Job::Split {
                    input,
                    output_pattern,
                    pages_per_file,
                } => {
                    if pages_per_file == 0 {
                        set_last_error("PagesPerFile must be positive");
                        return 9;
                    }
                    BatchJob::Split {
                        input,
                        output_pattern,
                        pages_per_file,
                    }
                }
                Job::Merge { inputs, output } => BatchJob::Merge { inputs, output },
                Job::Rotate {
                    input,
                    output,
                    rotation,
                    pages,
                } => BatchJob::Rotate {
                    input,
                    output,
                    rotation,
                    pages,
                },
                Job::Extract {
                    input,
                    output,
                    pages,
                } => BatchJob::Extract {
                    input,
                    output,
                    pages,
                },
                Job::Compress {
                    input,
                    output,
                    quality,
                } => BatchJob::Compress {
                    input,
                    output,
                    quality,
                },
                Job::Custom { name } => {
                    let Some(callback) = job_callback else {
                        set_last_error("Missing custom-job callback");
                        return 9;
                    };
                    BatchJob::Custom {
                        name,
                        operation: Box::new(move || {
                            if callback(index) == 0 {
                                Ok(())
                            } else {
                                Err(oxidize_pdf::PdfError::InvalidOperation(format!(
                                    "managed job {index} failed"
                                )))
                            }
                        }),
                    }
                }
            });
        }
        let mut options = BatchOptions {
            parallelism: request.options.parallelism,
            memory_limit_per_worker: request.options.memory_limit_per_worker,
            progress_interval: Duration::from_millis(request.options.progress_interval_ms),
            stop_on_error: request.options.stop_on_error,
            job_timeout: request.options.job_timeout_ms.map(Duration::from_millis),
            progress_callback: None,
        };
        if let Some(callback) = progress_callback {
            options=options.with_progress_callback(move |p| {
            let json=json!({"TotalJobs":p.total_jobs,"CompletedJobs":p.completed_jobs,"FailedJobs":p.failed_jobs,"RunningJobs":p.running_jobs,
                "ElapsedSeconds":p.elapsed().as_secs_f64(),"EstimatedRemainingSeconds":p.estimated_remaining.map(|d|d.as_secs_f64()),"Throughput":p.throughput,"Percentage":p.percentage(),"IsComplete":p.is_complete()}).to_string();
            callback(json.as_ptr(),json.len());
        });
        }
        let mut processor = BatchProcessor::new(options);
        processor.add_jobs(jobs);
        let summary = match processor.execute() {
            Ok(s) => s,
            Err(e) => {
                set_last_error(e.to_string());
                return ErrorCode::InvalidArgument as c_int;
            }
        };
        let results:Vec<_>=summary.results.iter().enumerate().map(|(index,r)|json!({"Index":index,"Name":r.job_name(),"Status":match r{JobResult::Success{..}=>"Success",JobResult::Failed{..}=>"Failed",JobResult::Cancelled{..}=>"Cancelled"},"DurationSeconds":r.duration().map(|d|d.as_secs_f64()),"Error":r.error(),"OutputFiles":r.output_files()})).collect();
        let json=json!({"TotalJobs":summary.total_jobs,"Successful":summary.successful,"Failed":summary.failed,"Cancelled":summary.cancelled,"DurationSeconds":summary.duration.as_secs_f64(),"Results":results}).to_string();
        match CString::new(json) {
            Ok(s) => {
                *output = s.into_raw();
                0
            }
            Err(e) => {
                set_last_error(e.to_string());
                5
            }
        }
    })
}
