namespace Armagetron.Game.UI
{
    /// <summary>
    /// PLACEHOLDER color palette for the whole UI. Every screen pulls its colors from one
    /// <see cref="UiTheme"/> instance so that when the designer delivers the real palette
    /// (DESIGN_BRIEF.md §3 — primary/secondary/accent/neutrals + 8 player colors) it is a
    /// one-file swap. These values are a neutral dark scheme chosen only to be legible, not
    /// final. Player/trail colors continue to live in <see cref="CyclePalette"/>.
    /// </summary>
    public sealed class UiTheme
    {
        public RenderColor Background    = new RenderColor(10, 10, 16);
        public RenderColor Panel         = new RenderColor(28, 28, 40);
        public RenderColor PanelBorder   = new RenderColor(70, 70, 90);
        public RenderColor Field         = new RenderColor(18, 18, 28);
        public RenderColor FieldFocused  = new RenderColor(24, 40, 60);
        public RenderColor FieldBorder   = new RenderColor(90, 90, 120);
        public RenderColor Button        = new RenderColor(40, 60, 90);
        public RenderColor ButtonPressed = new RenderColor(70, 110, 160);
        public RenderColor ButtonDisabled= new RenderColor(30, 30, 38);
        public RenderColor Text          = new RenderColor(235, 235, 240);
        public RenderColor TextMuted     = new RenderColor(150, 150, 165);
        public RenderColor Accent        = new RenderColor(255, 200, 0);
        public RenderColor Danger        = new RenderColor(220, 70, 70);
        public RenderColor Success       = new RenderColor(90, 200, 130);

        /// <summary>The shared default placeholder theme.</summary>
        public static UiTheme Default => new UiTheme();
    }
}
