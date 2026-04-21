//! FFI for document-level metadata: open actions, viewer preferences,
//! named destinations, page labels, save-with-WriterConfig.
//!
//! Module scaffold for Milestone M1 — individual feature functions will be
//! added per-task following the plan at
//! `docs/superpowers/plans/2026-04-21-m1-document-metadata.md`.

#![allow(unused_imports)]

use std::ffi::CStr;
use std::os::raw::{c_char, c_int};

use crate::document::DocumentHandle;
use crate::{clear_last_error, set_last_error, ErrorCode};
