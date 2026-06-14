namespace Armagetron.Game.UI
{
    /// <summary>
    /// The UI color palette, adopted from the approved design foundation
    /// (armagetron-advanced-design.html — neon-Tron, cyan signature on near-black). These are
    /// the REAL token values; only the bitmap font, icons and sprites remain placeholders
    /// pending the designer's exported files (see PRODUCTION_ASSETS.md). Player/trail colors
    /// live in <see cref="CyclePalette"/>. Hex → RGB straight from the design's CSS vars.
    /// </summary>
    public sealed class UiTheme
    {
        public RenderColor Background    = new RenderColor(0x07, 0x0A, 0x13); // --bg
        public RenderColor Panel         = new RenderColor(0x12, 0x1C, 0x30); // --surface2
        public RenderColor PanelBorder   = new RenderColor(0x2C, 0x3F, 0x60); // --line2
        public RenderColor Field         = new RenderColor(0x0C, 0x13, 0x22); // --surface
        public RenderColor FieldFocused  = new RenderColor(0x16, 0x22, 0x3A); // --raise
        public RenderColor FieldBorder   = new RenderColor(0x21, 0x31, 0x4C); // --line
        public RenderColor Button        = new RenderColor(0x14, 0x30, 0x4A); // deep cyan-blue
        public RenderColor ButtonPressed = new RenderColor(0x1F, 0xE3, 0xFF); // --cyan (brightens on press)
        public RenderColor ButtonDisabled= new RenderColor(0x0C, 0x13, 0x22); // flat surface
        public RenderColor Text          = new RenderColor(0xDC, 0xEA, 0xF2); // --text
        public RenderColor TextMuted     = new RenderColor(0x9B, 0xB0, 0xC8); // --dim
        public RenderColor Accent        = new RenderColor(0x1F, 0xE3, 0xFF); // --cyan (signature)
        public RenderColor Warn          = new RenderColor(0xFF, 0xC2, 0x3D); // --warn (amber)
        public RenderColor Danger        = new RenderColor(0xFF, 0x4D, 0x5E); // --danger
        public RenderColor Success       = new RenderColor(0x46, 0xE8, 0xA0); // --success

        /// <summary>The shared theme.</summary>
        public static UiTheme Default => new UiTheme();
    }
}
