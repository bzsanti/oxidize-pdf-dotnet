/// Standard PDF fonts — C-compatible enum for FFI.
#[repr(C)]
#[derive(Clone, Copy)]
pub enum StandardFont {
    Helvetica = 0,
    HelveticaBold = 1,
    HelveticaOblique = 2,
    HelveticaBoldOblique = 3,
    TimesRoman = 4,
    TimesBold = 5,
    TimesItalic = 6,
    TimesBoldItalic = 7,
    Courier = 8,
    CourierBold = 9,
    CourierOblique = 10,
    CourierBoldOblique = 11,
    Symbol = 12,
    ZapfDingbats = 13,
}

impl StandardFont {
    pub(crate) fn to_oxidize(self) -> oxidize_pdf::Font {
        match self {
            StandardFont::Helvetica => oxidize_pdf::Font::Helvetica,
            StandardFont::HelveticaBold => oxidize_pdf::Font::HelveticaBold,
            StandardFont::HelveticaOblique => oxidize_pdf::Font::HelveticaOblique,
            StandardFont::HelveticaBoldOblique => oxidize_pdf::Font::HelveticaBoldOblique,
            StandardFont::TimesRoman => oxidize_pdf::Font::TimesRoman,
            StandardFont::TimesBold => oxidize_pdf::Font::TimesBold,
            StandardFont::TimesItalic => oxidize_pdf::Font::TimesItalic,
            StandardFont::TimesBoldItalic => oxidize_pdf::Font::TimesBoldItalic,
            StandardFont::Courier => oxidize_pdf::Font::Courier,
            StandardFont::CourierBold => oxidize_pdf::Font::CourierBold,
            StandardFont::CourierOblique => oxidize_pdf::Font::CourierOblique,
            StandardFont::CourierBoldOblique => oxidize_pdf::Font::CourierBoldOblique,
            StandardFont::Symbol => oxidize_pdf::Font::Symbol,
            StandardFont::ZapfDingbats => oxidize_pdf::Font::ZapfDingbats,
        }
    }
}

/// Page size presets — C-compatible enum for FFI.
#[repr(C)]
pub enum PagePreset {
    A4 = 0,
    A4Landscape = 1,
    Letter = 2,
    LetterLandscape = 3,
    Legal = 4,
    LegalLandscape = 5,
}

/// Text alignment — C-compatible enum for FFI.
#[repr(C)]
#[derive(Clone, Copy)]
pub enum TextAlign {
    Left = 0,
    Right = 1,
    Center = 2,
    Justified = 3,
}

impl TextAlign {
    pub(crate) fn to_oxidize(self) -> oxidize_pdf::text::TextAlign {
        match self {
            TextAlign::Left => oxidize_pdf::text::TextAlign::Left,
            TextAlign::Right => oxidize_pdf::text::TextAlign::Right,
            TextAlign::Center => oxidize_pdf::text::TextAlign::Center,
            TextAlign::Justified => oxidize_pdf::text::TextAlign::Justified,
        }
    }
}

/// Line cap style — C-compatible enum for FFI.
#[repr(C)]
#[derive(Clone, Copy)]
pub enum LineCap {
    Butt = 0,
    Round = 1,
    Square = 2,
}

impl LineCap {
    pub(crate) fn to_oxidize(self) -> oxidize_pdf::graphics::LineCap {
        match self {
            LineCap::Butt => oxidize_pdf::graphics::LineCap::Butt,
            LineCap::Round => oxidize_pdf::graphics::LineCap::Round,
            LineCap::Square => oxidize_pdf::graphics::LineCap::Square,
        }
    }
}

/// Line join style — C-compatible enum for FFI.
#[repr(C)]
#[derive(Clone, Copy)]
pub enum LineJoin {
    Miter = 0,
    Round = 1,
    Bevel = 2,
}

impl LineJoin {
    pub(crate) fn to_oxidize(self) -> oxidize_pdf::graphics::LineJoin {
        match self {
            LineJoin::Miter => oxidize_pdf::graphics::LineJoin::Miter,
            LineJoin::Round => oxidize_pdf::graphics::LineJoin::Round,
            LineJoin::Bevel => oxidize_pdf::graphics::LineJoin::Bevel,
        }
    }
}

/// Blend mode — C-compatible enum for FFI.
#[repr(C)]
#[derive(Clone, Copy)]
pub enum BlendMode {
    Normal = 0,
    Multiply = 1,
    Screen = 2,
    Overlay = 3,
    SoftLight = 4,
    HardLight = 5,
    ColorDodge = 6,
    ColorBurn = 7,
    Darken = 8,
    Lighten = 9,
    Difference = 10,
    Exclusion = 11,
    Hue = 12,
    Saturation = 13,
    Color = 14,
    Luminosity = 15,
}

/// Text rendering mode — C-compatible enum for FFI.
#[repr(C)]
#[derive(Clone, Copy)]
pub enum TextRenderingMode {
    Fill = 0,
    Stroke = 1,
    FillStroke = 2,
    Invisible = 3,
    FillClip = 4,
    StrokeClip = 5,
    FillStrokeClip = 6,
    Clip = 7,
}

impl TextRenderingMode {
    pub(crate) fn to_oxidize(self) -> oxidize_pdf::text::TextRenderingMode {
        match self {
            TextRenderingMode::Fill => oxidize_pdf::text::TextRenderingMode::Fill,
            TextRenderingMode::Stroke => oxidize_pdf::text::TextRenderingMode::Stroke,
            TextRenderingMode::FillStroke => oxidize_pdf::text::TextRenderingMode::FillStroke,
            TextRenderingMode::Invisible => oxidize_pdf::text::TextRenderingMode::Invisible,
            TextRenderingMode::FillClip => oxidize_pdf::text::TextRenderingMode::FillClip,
            TextRenderingMode::StrokeClip => oxidize_pdf::text::TextRenderingMode::StrokeClip,
            TextRenderingMode::FillStrokeClip => {
                oxidize_pdf::text::TextRenderingMode::FillStrokeClip
            }
            TextRenderingMode::Clip => oxidize_pdf::text::TextRenderingMode::Clip,
        }
    }
}

impl BlendMode {
    pub(crate) fn to_oxidize(self) -> oxidize_pdf::graphics::BlendMode {
        match self {
            BlendMode::Normal => oxidize_pdf::graphics::BlendMode::Normal,
            BlendMode::Multiply => oxidize_pdf::graphics::BlendMode::Multiply,
            BlendMode::Screen => oxidize_pdf::graphics::BlendMode::Screen,
            BlendMode::Overlay => oxidize_pdf::graphics::BlendMode::Overlay,
            BlendMode::SoftLight => oxidize_pdf::graphics::BlendMode::SoftLight,
            BlendMode::HardLight => oxidize_pdf::graphics::BlendMode::HardLight,
            BlendMode::ColorDodge => oxidize_pdf::graphics::BlendMode::ColorDodge,
            BlendMode::ColorBurn => oxidize_pdf::graphics::BlendMode::ColorBurn,
            BlendMode::Darken => oxidize_pdf::graphics::BlendMode::Darken,
            BlendMode::Lighten => oxidize_pdf::graphics::BlendMode::Lighten,
            BlendMode::Difference => oxidize_pdf::graphics::BlendMode::Difference,
            BlendMode::Exclusion => oxidize_pdf::graphics::BlendMode::Exclusion,
            BlendMode::Hue => oxidize_pdf::graphics::BlendMode::Hue,
            BlendMode::Saturation => oxidize_pdf::graphics::BlendMode::Saturation,
            BlendMode::Color => oxidize_pdf::graphics::BlendMode::Color,
            BlendMode::Luminosity => oxidize_pdf::graphics::BlendMode::Luminosity,
        }
    }
}

/// Text annotation icon — C-compatible enum for FFI.
#[repr(C)]
#[derive(Clone, Copy)]
pub enum TextAnnotationIcon {
    Comment = 0,
    Key = 1,
    Note = 2,
    Help = 3,
    NewParagraph = 4,
    Paragraph = 5,
    Insert = 6,
}

impl TextAnnotationIcon {
    pub(crate) fn to_oxidize(self) -> oxidize_pdf::annotations::Icon {
        match self {
            TextAnnotationIcon::Comment => oxidize_pdf::annotations::Icon::Comment,
            TextAnnotationIcon::Key => oxidize_pdf::annotations::Icon::Key,
            TextAnnotationIcon::Note => oxidize_pdf::annotations::Icon::Note,
            TextAnnotationIcon::Help => oxidize_pdf::annotations::Icon::Help,
            TextAnnotationIcon::NewParagraph => oxidize_pdf::annotations::Icon::NewParagraph,
            TextAnnotationIcon::Paragraph => oxidize_pdf::annotations::Icon::Paragraph,
            TextAnnotationIcon::Insert => oxidize_pdf::annotations::Icon::Insert,
        }
    }
}

/// Standard stamp names — C-compatible enum for FFI.
/// Use `Custom` (14) with a separate `*const c_char` parameter for custom names.
#[repr(C)]
#[derive(Clone, Copy)]
pub enum StampNameFFI {
    Approved = 0,
    Draft = 1,
    Confidential = 2,
    Final = 3,
    NotApproved = 4,
    Experimental = 5,
    AsIs = 6,
    Expired = 7,
    NotForPublicRelease = 8,
    Sold = 9,
    Departmental = 10,
    ForComment = 11,
    TopSecret = 12,
    ForPublicRelease = 13,
    Custom = 14,
}

impl StampNameFFI {
    pub(crate) fn from_c_int(value: std::os::raw::c_int) -> Option<Self> {
        match value {
            0 => Some(StampNameFFI::Approved),
            1 => Some(StampNameFFI::Draft),
            2 => Some(StampNameFFI::Confidential),
            3 => Some(StampNameFFI::Final),
            4 => Some(StampNameFFI::NotApproved),
            5 => Some(StampNameFFI::Experimental),
            6 => Some(StampNameFFI::AsIs),
            7 => Some(StampNameFFI::Expired),
            8 => Some(StampNameFFI::NotForPublicRelease),
            9 => Some(StampNameFFI::Sold),
            10 => Some(StampNameFFI::Departmental),
            11 => Some(StampNameFFI::ForComment),
            12 => Some(StampNameFFI::TopSecret),
            13 => Some(StampNameFFI::ForPublicRelease),
            14 => Some(StampNameFFI::Custom),
            _ => None,
        }
    }

    pub(crate) fn to_oxidize(
        self,
        custom_name: Option<String>,
    ) -> oxidize_pdf::annotations::StampName {
        use oxidize_pdf::annotations::StampName;
        match self {
            StampNameFFI::Approved => StampName::Approved,
            StampNameFFI::Draft => StampName::Draft,
            StampNameFFI::Confidential => StampName::Confidential,
            StampNameFFI::Final => StampName::Final,
            StampNameFFI::NotApproved => StampName::NotApproved,
            StampNameFFI::Experimental => StampName::Experimental,
            StampNameFFI::AsIs => StampName::AsIs,
            StampNameFFI::Expired => StampName::Expired,
            StampNameFFI::NotForPublicRelease => StampName::NotForPublicRelease,
            StampNameFFI::Sold => StampName::Sold,
            StampNameFFI::Departmental => StampName::Departmental,
            StampNameFFI::ForComment => StampName::ForComment,
            StampNameFFI::TopSecret => StampName::TopSecret,
            StampNameFFI::ForPublicRelease => StampName::ForPublicRelease,
            StampNameFFI::Custom => StampName::Custom(custom_name.unwrap_or_default()),
        }
    }
}
