//! Serde mirrors of `oxidize_pdf::pipeline::*` config types for JSON-based FFI.
//!
//! C# serializes a `PartitionConfig` / `HybridChunkConfig` / `SemanticChunkConfig`
//! / `MarkdownOptions` instance to UTF-8 JSON and passes the buffer across the FFI
//! boundary. We deserialize into the DTOs defined here and convert into the real
//! `oxidize_pdf::pipeline::*` and `oxidize_pdf::ai::*` types via `From` impls.
//!
//! `ExtractionProfile` is special: its variants carry no payload, so it crosses
//! the boundary as a `u8` discriminant via [`profile_from_u8`]. The Rust core
//! does NOT mark `ExtractionProfile` with `#[repr(u8)]`, so a silent reorder
//! upstream could swap the meaning of any discriminant without a compile error.
//! The exhaustive `profile_discriminants` test in this module is the contract
//! guard — every variant is asserted by name.

use oxidize_pdf::pipeline::partition::ReadingOrderStrategy as RustReadingOrder;
use oxidize_pdf::pipeline::{
    ExtractionProfile as RustProfile, HybridChunkConfig as RustHybrid,
    MergePolicy as RustMergePolicy, PartitionConfig as RustPartition,
    SemanticChunkConfig as RustSemantic,
};
use serde::Deserialize;

#[derive(Debug, Deserialize)]
#[serde(untagged)]
pub enum ReadingOrderDto {
    Unit(String),
    XyCut {
        #[serde(rename = "XYCut")]
        x: XyCutDto,
    },
}

#[derive(Debug, Deserialize)]
pub struct XyCutDto {
    pub min_gap: f64,
}

impl From<ReadingOrderDto> for RustReadingOrder {
    fn from(d: ReadingOrderDto) -> Self {
        match d {
            ReadingOrderDto::Unit(s) if s == "Simple" => RustReadingOrder::Simple,
            ReadingOrderDto::Unit(s) if s == "None" => RustReadingOrder::None,
            ReadingOrderDto::Unit(s) => panic!("unknown reading_order tag: {s}"),
            ReadingOrderDto::XyCut { x } => RustReadingOrder::XYCut { min_gap: x.min_gap },
        }
    }
}

#[derive(Debug, Deserialize)]
pub struct PartitionConfigDto {
    pub detect_tables: bool,
    pub detect_headers_footers: bool,
    pub title_min_font_ratio: f64,
    pub header_zone: f64,
    pub footer_zone: f64,
    pub reading_order: ReadingOrderDto,
    pub min_table_confidence: f64,
}

impl From<PartitionConfigDto> for RustPartition {
    fn from(d: PartitionConfigDto) -> Self {
        RustPartition {
            detect_tables: d.detect_tables,
            detect_headers_footers: d.detect_headers_footers,
            title_min_font_ratio: d.title_min_font_ratio,
            header_zone: d.header_zone,
            footer_zone: d.footer_zone,
            reading_order: d.reading_order.into(),
            min_table_confidence: d.min_table_confidence,
        }
    }
}

#[derive(Debug, Deserialize)]
pub struct HybridChunkConfigDto {
    pub max_tokens: usize,
    pub overlap_tokens: usize,
    pub merge_adjacent: bool,
    pub propagate_headings: bool,
    pub merge_policy: MergePolicyDto,
}

#[derive(Debug, Deserialize)]
pub enum MergePolicyDto {
    SameTypeOnly,
    AnyInlineContent,
}

impl From<MergePolicyDto> for RustMergePolicy {
    fn from(d: MergePolicyDto) -> Self {
        match d {
            MergePolicyDto::SameTypeOnly => RustMergePolicy::SameTypeOnly,
            MergePolicyDto::AnyInlineContent => RustMergePolicy::AnyInlineContent,
        }
    }
}

impl From<HybridChunkConfigDto> for RustHybrid {
    fn from(d: HybridChunkConfigDto) -> Self {
        RustHybrid {
            max_tokens: d.max_tokens,
            overlap_tokens: d.overlap_tokens,
            merge_adjacent: d.merge_adjacent,
            propagate_headings: d.propagate_headings,
            merge_policy: d.merge_policy.into(),
        }
    }
}

#[derive(Debug, Deserialize)]
pub struct SemanticChunkConfigDto {
    pub max_tokens: usize,
    pub overlap_tokens: usize,
    pub respect_element_boundaries: bool,
}

impl From<SemanticChunkConfigDto> for RustSemantic {
    fn from(d: SemanticChunkConfigDto) -> Self {
        RustSemantic {
            max_tokens: d.max_tokens,
            overlap_tokens: d.overlap_tokens,
            respect_element_boundaries: d.respect_element_boundaries,
        }
    }
}

/// DTO mirroring `oxidize_pdf::ai::MarkdownOptions`.
#[derive(Debug, Deserialize)]
pub struct MarkdownOptionsDto {
    pub include_metadata: bool,
    pub include_page_numbers: bool,
}

impl From<MarkdownOptionsDto> for oxidize_pdf::ai::MarkdownOptions {
    fn from(d: MarkdownOptionsDto) -> Self {
        oxidize_pdf::ai::MarkdownOptions {
            include_metadata: d.include_metadata,
            include_page_numbers: d.include_page_numbers,
        }
    }
}

/// Map the `u8` discriminant received across the FFI boundary to the Rust enum.
///
/// Order MUST match the C# `ExtractionProfile` enum and the Rust core
/// `ExtractionProfile` declaration order. Guarded by the exhaustive
/// `profile_discriminants` test below.
pub fn profile_from_u8(v: u8) -> Result<RustProfile, String> {
    Ok(match v {
        0 => RustProfile::Standard,
        1 => RustProfile::Academic,
        2 => RustProfile::Form,
        3 => RustProfile::Government,
        4 => RustProfile::Dense,
        5 => RustProfile::Presentation,
        6 => RustProfile::Rag,
        other => return Err(format!("unknown ExtractionProfile discriminant: {other}")),
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn partition_config_roundtrip_simple() {
        let json = r#"{
            "detect_tables": true,
            "detect_headers_footers": false,
            "title_min_font_ratio": 1.4,
            "header_zone": 0.05,
            "footer_zone": 0.05,
            "reading_order": "Simple",
            "min_table_confidence": 0.6
        }"#;
        let dto: PartitionConfigDto = serde_json::from_str(json).unwrap();
        let cfg: RustPartition = dto.into();
        assert!(cfg.detect_tables);
        assert!(!cfg.detect_headers_footers);
        assert_eq!(cfg.title_min_font_ratio, 1.4);
        assert_eq!(cfg.min_table_confidence, 0.6);
        assert!(matches!(cfg.reading_order, RustReadingOrder::Simple));
    }

    #[test]
    fn partition_config_xycut() {
        let json = r#"{
            "detect_tables": true,
            "detect_headers_footers": true,
            "title_min_font_ratio": 1.3,
            "header_zone": 0.05,
            "footer_zone": 0.05,
            "reading_order": {"XYCut":{"min_gap":20.0}},
            "min_table_confidence": 0.5
        }"#;
        let dto: PartitionConfigDto = serde_json::from_str(json).unwrap();
        let cfg: RustPartition = dto.into();
        match cfg.reading_order {
            RustReadingOrder::XYCut { min_gap } => assert_eq!(min_gap, 20.0),
            _ => panic!("expected XYCut"),
        }
    }

    #[test]
    fn partition_config_xycut_integer_min_gap() {
        // System.Text.Json emits integer tokens for whole-number doubles; serde_json must accept.
        let json = r#"{
            "detect_tables": true,
            "detect_headers_footers": true,
            "title_min_font_ratio": 1.3,
            "header_zone": 0.05,
            "footer_zone": 0.05,
            "reading_order": {"XYCut":{"min_gap":20}},
            "min_table_confidence": 0.5
        }"#;
        let dto: PartitionConfigDto = serde_json::from_str(json).unwrap();
        let cfg: RustPartition = dto.into();
        match cfg.reading_order {
            RustReadingOrder::XYCut { min_gap } => assert_eq!(min_gap, 20.0),
            _ => panic!("expected XYCut"),
        }
    }

    #[test]
    fn partition_config_reading_order_none() {
        let json = r#"{
            "detect_tables": false,
            "detect_headers_footers": false,
            "title_min_font_ratio": 1.0,
            "header_zone": 0.0,
            "footer_zone": 0.0,
            "reading_order": "None",
            "min_table_confidence": 0.0
        }"#;
        let dto: PartitionConfigDto = serde_json::from_str(json).unwrap();
        let cfg: RustPartition = dto.into();
        assert!(matches!(cfg.reading_order, RustReadingOrder::None));
    }

    #[test]
    fn profile_discriminants() {
        // Exhaustive: each variant must map to its declared u8 to catch silent
        // upstream reorders (Rust core does not use `#[repr(u8)]`).
        assert!(matches!(profile_from_u8(0).unwrap(), RustProfile::Standard));
        assert!(matches!(profile_from_u8(1).unwrap(), RustProfile::Academic));
        assert!(matches!(profile_from_u8(2).unwrap(), RustProfile::Form));
        assert!(matches!(profile_from_u8(3).unwrap(), RustProfile::Government));
        assert!(matches!(profile_from_u8(4).unwrap(), RustProfile::Dense));
        assert!(matches!(
            profile_from_u8(5).unwrap(),
            RustProfile::Presentation
        ));
        assert!(matches!(profile_from_u8(6).unwrap(), RustProfile::Rag));
        assert!(profile_from_u8(7).is_err());
        assert!(profile_from_u8(99).is_err());
    }

    #[test]
    fn hybrid_config_roundtrip_same_type_only() {
        let json = r#"{
            "max_tokens": 256,
            "overlap_tokens": 30,
            "merge_adjacent": true,
            "propagate_headings": true,
            "merge_policy": "SameTypeOnly"
        }"#;
        let dto: HybridChunkConfigDto = serde_json::from_str(json).unwrap();
        let cfg: RustHybrid = dto.into();
        assert_eq!(cfg.max_tokens, 256);
        assert_eq!(cfg.overlap_tokens, 30);
        assert!(cfg.merge_adjacent);
        assert!(cfg.propagate_headings);
        assert_eq!(cfg.merge_policy, RustMergePolicy::SameTypeOnly);
    }

    #[test]
    fn hybrid_config_roundtrip_any_inline_content() {
        let json = r#"{
            "max_tokens": 512,
            "overlap_tokens": 0,
            "merge_adjacent": false,
            "propagate_headings": false,
            "merge_policy": "AnyInlineContent"
        }"#;
        let dto: HybridChunkConfigDto = serde_json::from_str(json).unwrap();
        let cfg: RustHybrid = dto.into();
        assert_eq!(cfg.merge_policy, RustMergePolicy::AnyInlineContent);
        assert!(!cfg.merge_adjacent);
    }

    #[test]
    fn semantic_config_roundtrip() {
        let json = r#"{
            "max_tokens": 384,
            "overlap_tokens": 64,
            "respect_element_boundaries": true
        }"#;
        let dto: SemanticChunkConfigDto = serde_json::from_str(json).unwrap();
        let cfg: RustSemantic = dto.into();
        assert_eq!(cfg.max_tokens, 384);
        assert_eq!(cfg.overlap_tokens, 64);
        assert!(cfg.respect_element_boundaries);
    }

    #[test]
    fn markdown_options_roundtrip() {
        let json = r#"{"include_metadata":false,"include_page_numbers":true}"#;
        let dto: MarkdownOptionsDto = serde_json::from_str(json).unwrap();
        let opts: oxidize_pdf::ai::MarkdownOptions = dto.into();
        assert!(!opts.include_metadata);
        assert!(opts.include_page_numbers);
    }

    #[test]
    #[should_panic(expected = "unknown reading_order tag")]
    fn reading_order_unknown_tag_panics() {
        let json = r#""Bogus""#;
        let dto: ReadingOrderDto = serde_json::from_str(json).unwrap();
        let _: RustReadingOrder = dto.into();
    }
}
