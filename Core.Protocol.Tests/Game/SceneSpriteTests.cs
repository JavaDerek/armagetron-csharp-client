using System.Linq;
using Armagetron.Game;
using Armagetron.Game.UI;
using Xunit;

namespace Armagetron.Protocol.Tests.Game
{
    /// <summary>
    /// Tests for the textured-sprite + ordered-command extensions to the neutral render model:
    /// <see cref="RenderSprite"/>, nine-slice/sheet emission from <see cref="SceneBuf"/>, and the
    /// insertion-ordered <see cref="Scene.Commands"/> stream that lets layered chrome composite
    /// correctly. The front-end (head) owns the actual GPU draw; here we assert the geometry.
    /// </summary>
    public class SceneSpriteTests
    {
        [Fact]
        public void Sprite_RecordsDestTintAndBlend()
        {
            var buf = new SceneBuf();
            buf.Sprite("ingame/cycle", new UiRect(10, 20, 30, 40),
                       new RenderColor(1, 2, 3), BlendKind.Additive, rotation: 1.5f);
            RenderSprite s = buf.Sprites.Single();
            Assert.Equal("ingame/cycle", s.Key);
            Assert.Equal((10, 20, 30, 40), (s.X, s.Y, s.W, s.H));
            Assert.Equal(new RenderColor(1, 2, 3), s.Tint);
            Assert.Equal(BlendKind.Additive, s.Blend);
            Assert.Equal(1.5f, s.Rotation);
            Assert.False(s.NineSlice);
            Assert.False(s.HasSource);
        }

        [Fact]
        public void SpriteFrame_CarriesSourceSubRect()
        {
            var buf = new SceneBuf();
            buf.SpriteFrame("ingame/explosion", new UiRect(0, 0, 64, 64),
                            RenderColor.White, srcX: 128, srcY: 256, srcW: 128, srcH: 128,
                            blend: BlendKind.Additive);
            RenderSprite s = buf.Sprites.Single();
            Assert.True(s.HasSource);
            Assert.Equal((128, 256, 128, 128), (s.SrcX, s.SrcY, s.SrcW, s.SrcH));
            Assert.Equal(BlendKind.Additive, s.Blend);
        }

        [Fact]
        public void NineSlice_FlagsNinePatch()
        {
            var buf = new SceneBuf();
            buf.NineSlice("nine/panel", new UiRect(5, 5, 200, 100), RenderColor.White);
            RenderSprite s = buf.Sprites.Single();
            Assert.True(s.NineSlice);
            Assert.Equal("nine/panel", s.Key);
        }

        [Fact]
        public void Commands_PreserveInsertionOrder_AcrossTypes()
        {
            var buf = new SceneBuf();
            buf.Fill(new UiRect(0, 0, 800, 600), RenderColor.White)   // background rect
               .NineSlice("nine/panel", new UiRect(10, 10, 100, 100), RenderColor.White) // panel over it
               .TextLeft("HI", 20, 20, RenderColor.White, 2);          // text over panel
            var cmds = buf.ToScene().Commands;
            Assert.Equal(3, cmds.Count);
            Assert.IsType<RenderRect>(cmds[0]);
            Assert.IsType<RenderSprite>(cmds[1]);
            Assert.IsType<RenderText>(cmds[2]);
        }

        [Fact]
        public void Scene_FromTypedLists_OrdersSegmentsThenHeadsThenText()
        {
            var scene = new Scene(
                new[] { new RenderSegment(new Vec2(0, 0), new Vec2(1, 1), RenderColor.White) },
                new[] { new RenderRect(0, 0, 2, 2, RenderColor.White) },
                new[] { new RenderText("X", 0, 0, RenderColor.White) });
            Assert.IsType<RenderSegment>(scene.Commands[0]);
            Assert.IsType<RenderRect>(scene.Commands[1]);
            Assert.IsType<RenderText>(scene.Commands[2]);
            Assert.Empty(scene.Sprites);
        }

        [Fact]
        public void Append_PreservesOtherScenesCommandOrder()
        {
            var under = new SceneBuf();
            under.Sprite("a", new UiRect(0, 0, 1, 1), RenderColor.White);
            var over = new SceneBuf();
            over.TextLeft("T", 0, 0, RenderColor.White, 2)
                .Append(under.ToScene());
            var cmds = over.ToScene().Commands;
            Assert.IsType<RenderText>(cmds[0]);
            Assert.IsType<RenderSprite>(cmds[1]);
        }

        [Fact]
        public void RenderText_DefaultCtor_IsLeftBody()
        {
            var t = new RenderText("HI", 1, 2, RenderColor.White, 3);
            Assert.Equal(TextAlign.Left, t.Align);
            Assert.Equal(FontRole.Body, t.Role);
            Assert.False(t.Middle);
        }

        [Fact]
        public void TextCentered_AnchorsRectCenter_WithMiddle()
        {
            var buf = new SceneBuf();
            buf.TextCentered("OK", new UiRect(0, 0, 100, 40), RenderColor.White, 2, FontRole.Label);
            RenderText t = buf.Texts.Single();
            Assert.Equal(50, t.X);
            Assert.Equal(20, t.Y);
            Assert.True(t.Middle);
            Assert.Equal(TextAlign.Center, t.Align);
            Assert.Equal(FontRole.Label, t.Role);
        }
    }
}
