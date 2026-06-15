using System;
using System.Collections.Generic;
using Armagetron.Game.Audio;
using Armagetron.Game.UI;
using Microsoft.Xna.Framework;

namespace Armagetron.Game
{
    /// <summary>
    /// Standalone SFX audition (<c>--audition</c>): boots only the MonoGame audio device and
    /// plays each manifest sound in turn, printing its name, so the whole pack can be heard
    /// without connecting to a server or hitting every in-game trigger. Each one-shot fires
    /// once; the two looping cues run as a short single pass, then the engine hum is started
    /// for a couple seconds (its real looped form) before the window exits.
    /// </summary>
    public sealed class AuditionGame : Microsoft.Xna.Framework.Game
    {
        private const double StepSeconds = 1.4;     // gap between one-shots
        private const double EngineHold = 2.5;      // how long to run the looping engine hum

        // Manifest order, so the console reads top-to-bottom like the audio_manifest.md table.
        private static readonly SfxId[] Order =
        {
            SfxId.EngineLoop, SfxId.Turn, SfxId.WallGrind, SfxId.Explosion, SfxId.CountdownBeep,
            SfxId.Go, SfxId.UiTap, SfxId.ConnectOk, SfxId.ConnectFail, SfxId.Win, SfxId.Lose,
        };

        private readonly GraphicsDeviceManager _graphics;
        private SfxController _sfx = null!;
        private int _index = -1;
        private double _next;
        private double _engineUntil = -1;
        private bool _engineDone;

        public AuditionGame()
        {
            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = 320,
                PreferredBackBufferHeight = 120,
            };
            Window.Title = "Armagetron — SFX audition";
            IsMouseVisible = true;
        }

        protected override void LoadContent()
        {
            _sfx = new SfxController();
            Console.WriteLine("[audition] playing each SFX once (Sound ON)…");
        }

        protected override void Update(GameTime gameTime)
        {
            double t = gameTime.TotalGameTime.TotalSeconds;

            if (t >= _next && _index + 1 < Order.Length)
            {
                _index++;
                SfxId id = Order[_index];
                Console.WriteLine($"  {_index + 1,2}/{Order.Length}  {id}");
                _sfx.PlayCues(new List<SfxId> { id }, soundOn: true);
                _next = t + StepSeconds;
            }
            else if (_index + 1 >= Order.Length && _engineUntil < 0)
            {
                // After the roll-call, demonstrate the engine hum in its real looped form.
                Console.WriteLine("[audition] engine_loop (looped) for a couple seconds…");
                _sfx.SetEngine(running: true, soundOn: true);
                _engineUntil = t + EngineHold;
            }
            else if (_engineUntil >= 0 && t >= _engineUntil && !_engineDone)
            {
                _sfx.SetEngine(running: false, soundOn: true);
                _engineDone = true;
                Console.WriteLine("[audition] done.");
                Exit();
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            base.Draw(gameTime);
        }

        protected override void UnloadContent()
        {
            _sfx?.Dispose();
            base.UnloadContent();
        }
    }
}
