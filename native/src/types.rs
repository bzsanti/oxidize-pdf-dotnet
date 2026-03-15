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
