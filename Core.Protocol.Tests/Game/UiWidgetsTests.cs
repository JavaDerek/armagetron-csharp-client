using System.Linq;
using Armagetron.Game;
using Armagetron.Game.UI;
using Xunit;

namespace Armagetron.Protocol.Tests.Game
{
    /// <summary>
    /// Tests for the pure UI foundation: geometry/hit-testing (<see cref="UiRect"/>),
    /// widget state (<see cref="UiButton"/>, <see cref="UiTextField"/>), and the
    /// <see cref="SceneBuf"/> that lowers widgets to <see cref="Scene"/> draw commands.
    /// </summary>
    public class UiWidgetsTests
    {
        // ── UiRect ───────────────────────────────────────────────────────────────

        [Fact]
        public void Contains_IsLeftTopInclusive_RightBottomExclusive()
        {
            var r = new UiRect(10, 20, 100, 50);
            Assert.True(r.Contains(10, 20));      // top-left inclusive
            Assert.True(r.Contains(109, 69));     // last inside pixel
            Assert.False(r.Contains(110, 20));    // right edge exclusive
            Assert.False(r.Contains(10, 70));     // bottom edge exclusive
            Assert.False(r.Contains(9, 20));      // left of
        }

        [Fact]
        public void Rect_DerivedEdgesAndCenter()
        {
            var r = new UiRect(10, 20, 100, 60);
            Assert.Equal(110, r.Right);
            Assert.Equal(80, r.Bottom);
            Assert.Equal(60, r.CenterX);
            Assert.Equal(50, r.CenterY);
        }

        [Fact]
        public void Inset_ShrinksAllSides()
        {
            var r = new UiRect(10, 10, 100, 100).Inset(5);
            Assert.Equal(15, r.X);
            Assert.Equal(15, r.Y);
            Assert.Equal(90, r.W);
            Assert.Equal(90, r.H);
        }

        // ── UiButton ──────────────────────────────────────────────────────────────

        [Fact]
        public void Button_HitTest_RequiresEnabledAndInside()
        {
            var b = new UiButton("go", new UiRect(0, 0, 50, 50), "GO");
            Assert.True(b.HitTest(25, 25));
            Assert.False(b.HitTest(100, 100));
            b.Enabled = false;
            Assert.False(b.HitTest(25, 25));
        }

        // ── UiTextField ─────────────────────────────────────────────────────────

        [Fact]
        public void Field_Append_RespectsNumericFilter()
        {
            var f = new UiTextField("port", new UiRect(0, 0, 10, 10), "PORT") { Numeric = true };
            f.Append('4'); f.Append('x'); f.Append('5'); f.Append('.');
            Assert.Equal("45", f.Value);
        }

        [Fact]
        public void Field_Append_RespectsMaxLength()
        {
            var f = new UiTextField("n", new UiRect(0, 0, 10, 10), "N") { MaxLength = 3 };
            foreach (char c in "ABCDE") f.Append(c);
            Assert.Equal("ABC", f.Value);
        }

        [Fact]
        public void Field_Append_RejectsNonPrintable()
        {
            var f = new UiTextField("n", new UiRect(0, 0, 10, 10), "N");
            f.Append('\n'); f.Append('\t'); f.Append('A');
            Assert.Equal("A", f.Value);
        }

        [Fact]
        public void Field_Backspace_RemovesLast_AndIsSafeWhenEmpty()
        {
            var f = new UiTextField("n", new UiRect(0, 0, 10, 10), "N") { Value = "AB" };
            f.Backspace();
            Assert.Equal("A", f.Value);
            f.Backspace();
            f.Backspace(); // no throw on empty
            Assert.Equal("", f.Value);
        }

        // ── SceneBuf ──────────────────────────────────────────────────────────────

        [Fact]
        public void Border_EmitsFourEdges()
        {
            var buf = new SceneBuf();
            buf.Border(new UiRect(0, 0, 100, 100), RenderColor.White, 2);
            Assert.Equal(4, buf.Rects.Count);
        }

        [Fact]
        public void TextCenter_CentersHorizontally()
        {
            var buf = new SceneBuf();
            buf.TextCenter("AB", centerX: 100, y: 0, RenderColor.White, scale: 2);
            RenderText t = buf.Texts.Single();
            Assert.Equal(100 - PixelFont.MeasureWidth("AB", 2) / 2, t.X);
        }

        [Fact]
        public void DrawButton_DisabledUsesDisabledFill_AndMutedLabel()
        {
            var theme = UiTheme.Default;
            var b = new UiButton("x", new UiRect(0, 0, 80, 30), "GO") { Enabled = false };
            var buf = new SceneBuf();
            buf.DrawButton(b, theme, 2);

            Assert.Equal(theme.ButtonDisabled, buf.Rects[0].Color); // fill first
            RenderText label = buf.Texts.Single();
            Assert.Equal("GO", label.Text);
            Assert.Equal(theme.TextMuted, label.Color);
        }

        [Fact]
        public void DrawButton_PressedUsesPressedFill()
        {
            var theme = UiTheme.Default;
            var b = new UiButton("x", new UiRect(0, 0, 80, 30), "GO") { Pressed = true };
            var buf = new SceneBuf();
            buf.DrawButton(b, theme, 2);
            Assert.Equal(theme.ButtonPressed, buf.Rects[0].Color);
        }

        [Fact]
        public void DrawField_Focused_DrawsCaret_AndAccentBorder()
        {
            var theme = UiTheme.Default;
            var f = new UiTextField("n", new UiRect(10, 40, 200, 36), "NAME")
            { Focused = true, Value = "AB" };
            var buf = new SceneBuf();
            buf.DrawField(f, theme, 3);

            // Some rect is the accent caret (a thin tall rect with the accent color).
            Assert.Contains(buf.Rects, r => r.Color.Equals(theme.Accent) && r.W == SceneBuf.CaretWidth * 3);
            // The label and the value both render as text.
            Assert.Contains(buf.Texts, t => t.Text == "NAME");
            Assert.Contains(buf.Texts, t => t.Text == "AB");
        }

        [Fact]
        public void DrawField_Unfocused_NoCaret()
        {
            var theme = UiTheme.Default;
            var f = new UiTextField("n", new UiRect(10, 40, 200, 36), "NAME") { Value = "AB" };
            var buf = new SceneBuf();
            buf.DrawField(f, theme, 3);
            Assert.DoesNotContain(buf.Rects, r => r.Color.Equals(theme.Accent));
        }

        [Fact]
        public void ToScene_CarriesAllLists()
        {
            var buf = new SceneBuf();
            buf.Fill(new UiRect(0, 0, 10, 10), RenderColor.White)
               .TextLeft("HI", 0, 0, RenderColor.White, 2);
            Scene s = buf.ToScene();
            Assert.Single(s.Heads);
            Assert.Single(s.Texts);
        }
    }
}
