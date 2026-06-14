namespace Armagetron.Game.UI
{
    /// <summary>
    /// An axis-aligned rectangle in screen pixels — the unit of UI layout and hit-testing.
    /// Distinct from <see cref="RenderRect"/> (which carries a fill color for drawing): a
    /// <see cref="UiRect"/> is pure geometry, shared by a screen's view builder (to draw) and
    /// its input router (to hit-test a tap), so the two can never disagree on where a widget is.
    /// </summary>
    public readonly struct UiRect
    {
        public readonly int X, Y, W, H;
        public UiRect(int x, int y, int w, int h) { X = x; Y = y; W = w; H = h; }

        public int Right => X + W;
        public int Bottom => Y + H;
        public int CenterX => X + W / 2;
        public int CenterY => Y + H / 2;

        /// <summary>True if the pixel (px, py) falls inside the rectangle (left/top inclusive,
        /// right/bottom exclusive — adjacent rects never both claim a tap).</summary>
        public bool Contains(int px, int py) =>
            px >= X && px < Right && py >= Y && py < Bottom;

        /// <summary>A copy inset by <paramref name="d"/> pixels on every side.</summary>
        public UiRect Inset(int d) => new UiRect(X + d, Y + d, W - 2 * d, H - 2 * d);
    }
}
