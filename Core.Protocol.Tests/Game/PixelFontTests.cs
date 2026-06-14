using Armagetron.Game;
using Xunit;

namespace Armagetron.Protocol.Tests.Game
{
    /// <summary>
    /// Tests for the placeholder <see cref="PixelFont"/>: measurement (used by the view
    /// builders to lay out and centre text) and glyph lookup (lit-cell patterns, lowercase
    /// folding, and the visible box fallback for unknown characters).
    /// </summary>
    public class PixelFontTests
    {
        [Fact]
        public void MeasureWidth_Empty_IsZero()
        {
            Assert.Equal(0, PixelFont.MeasureWidth("", 3));
            Assert.Equal(0, PixelFont.MeasureWidth(null!, 3));
        }

        [Fact]
        public void MeasureWidth_SingleGlyph_IsCellWidthTimesScale()
        {
            Assert.Equal(PixelFont.GlyphWidth * 2, PixelFont.MeasureWidth("A", 2));
        }

        [Fact]
        public void MeasureWidth_TwoGlyphs_IncludesOneInterGlyphGap()
        {
            // (2 * advance - spacing) * scale = (2*6 - 1) * 1 = 11
            Assert.Equal(11, PixelFont.MeasureWidth("AB", 1));
            // and scales linearly
            Assert.Equal(33, PixelFont.MeasureWidth("AB", 3));
        }

        [Fact]
        public void Height_IsSevenRowsTimesScale()
        {
            Assert.Equal(GlyphHeightTimes(4), PixelFont.Height(4));
            static int GlyphHeightTimes(int s) => PixelFont.GlyphHeight * s;
        }

        [Fact]
        public void Space_HasNoLitCells()
        {
            Assert.Equal(0, PixelFont.Get(' ').LitCount());
        }

        [Fact]
        public void Letters_And_Digits_HaveLitCells()
        {
            foreach (char c in "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789")
                Assert.True(PixelFont.Get(c).LitCount() > 0, $"glyph '{c}' is blank");
        }

        [Fact]
        public void Lowercase_FoldsToUppercaseGlyph()
        {
            Glyph upper = PixelFont.Get('G');
            Glyph lower = PixelFont.Get('g');
            for (int r = 0; r < PixelFont.GlyphHeight; r++)
                for (int col = 0; col < PixelFont.GlyphWidth; col++)
                    Assert.Equal(upper.IsLit(col, r), lower.IsLit(col, r));
        }

        [Fact]
        public void UnknownChar_RendersHollowBox()
        {
            // '~' is not in the table → the box fallback: corners lit, centre hollow.
            Glyph g = PixelFont.Get('~');
            Assert.True(g.IsLit(0, 0));                              // top-left corner
            Assert.True(g.IsLit(PixelFont.GlyphWidth - 1, 0));      // top-right corner
            Assert.False(g.IsLit(2, 3));                            // hollow middle
        }

        [Fact]
        public void KnownGlyph_HasExpectedTopRow()
        {
            // 'I' top row is fully lit ("#####").
            Glyph i = PixelFont.Get('I');
            for (int col = 0; col < PixelFont.GlyphWidth; col++)
                Assert.True(i.IsLit(col, 0), $"'I' top row col {col} should be lit");
        }

        [Fact]
        public void RenderText_KeepsItsFields()
        {
            var t = new RenderText("HI", 12, 34, new RenderColor(1, 2, 3), scale: 5);
            Assert.Equal("HI", t.Text);
            Assert.Equal(12, t.X);
            Assert.Equal(34, t.Y);
            Assert.Equal(5, t.Scale);
            Assert.Equal(new RenderColor(1, 2, 3), t.Color);
        }

        [Fact]
        public void Scene_DefaultsToNoTexts_AndCanCarryThem()
        {
            var empty = new Scene(new RenderSegment[0], new RenderRect[0]);
            Assert.Empty(empty.Texts);

            var withText = new Scene(new RenderSegment[0], new RenderRect[0],
                                     new[] { new RenderText("X", 0, 0, RenderColor.White) });
            Assert.Single(withText.Texts);
        }
    }
}
