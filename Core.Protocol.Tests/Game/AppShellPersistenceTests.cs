using System;
using Armagetron.Game;
using Armagetron.Game.UI;
using Xunit;

namespace Armagetron.Protocol.Tests.Game
{
    /// <summary>An in-memory <see cref="IConnectStore"/>: hands back a preset choice and records
    /// the last saved one, so the shell's seed-from/save-to-store behavior is testable.</summary>
    internal sealed class FakeConnectStore : IConnectStore
    {
        private readonly ConnectChoice? _loaded;
        public ConnectChoice? Saved;
        public int Saves;

        public FakeConnectStore(ConnectChoice? loaded = null) => _loaded = loaded;
        public ConnectChoice? Load() => _loaded;
        public void Save(ConnectChoice choice) { Saved = choice; Saves++; }
    }

    /// <summary>
    /// The connect form remembers the player's last working server across sessions: it seeds the
    /// fields from the store at launch (overriding the head's baked defaults) and saves only once
    /// a connection actually succeeds. Heads now ship a BLANK host default, so the store is what
    /// pre-fills a returning player's server.
    /// </summary>
    public class AppShellPersistenceTests
    {
        private const int W = 800, H = 800;

        // A head that ships blank (the new desktop/Android/iOS default) wired to a given store.
        private static AppShell Shell(FakeUiClient c, IConnectStore? store) =>
            new AppShell(c, UiTheme.Default, "", 4534, "Vlad", touchControls: false, store: store);

        [Fact]
        public void Seeds_FormFromStore_OverridingDefaults()
        {
            var c = new FakeUiClient();
            var store = new FakeConnectStore(new ConnectChoice("remembered.host", 5000, "Zoidberg"));
            var s = Shell(c, store);

            // The remembered choice — not the blank head default — drives the next connect.
            s.RequestConnect();
            Assert.Equal("remembered.host", c.ConnHost);
            Assert.Equal(5000, c.ConnPort);
            Assert.Equal("Zoidberg", c.ConnName);
        }

        [Fact]
        public void BlankDefault_WithEmptyStore_IsInvalidForm()
        {
            // Nothing remembered + blank shipped host => the form can't connect until the player
            // types a server. (This is the intended public-build behavior: no baked-in server.)
            var s = Shell(new FakeUiClient(), new FakeConnectStore(loaded: null));
            Assert.False(s.IsFormValid());
        }

        [Fact]
        public void NullStore_FallsBackToHeadDefaults()
        {
            // No store at all (back-compat with existing heads/tests) => use the passed defaults.
            var c = new FakeUiClient();
            var s = new AppShell(c, UiTheme.Default, "default.host", 4534, "Vlad");
            s.RequestConnect();
            Assert.Equal("default.host", c.ConnHost);
        }

        [Fact]
        public void Saves_Choice_OnSuccessfulConnect()
        {
            var c = new FakeUiClient();
            var store = new FakeConnectStore();
            var s = Shell(c, store);
            s.PrefillConnect("good.host", 4534, "Vlad");
            s.RequestConnect();                 // -> Connecting

            c.Status = ConnectionStatus.Connected;
            s.Tick(Array.Empty<CycleSnapshot>(), 0);   // -> Playing, persists

            Assert.Equal(1, store.Saves);
            Assert.Equal("good.host", store.Saved!.Value.Host);
            Assert.Equal(4534, store.Saved!.Value.Port);
            Assert.Equal("Vlad", store.Saved!.Value.Name);
        }

        [Fact]
        public void DoesNotSave_OnFailedConnect()
        {
            var c = new FakeUiClient();
            var store = new FakeConnectStore();
            var s = Shell(c, store);
            s.PrefillConnect("bad.host", 4534, "Vlad");
            s.RequestConnect();

            c.Status = ConnectionStatus.Failed;
            c.LastError = "NOPE";
            s.Tick(Array.Empty<CycleSnapshot>(), 0);   // -> back to Connect, no save

            Assert.Equal(0, store.Saves);
        }

        [Fact]
        public void Saves_TrimmedValues()
        {
            // The connect identity is trimmed before BeginConnect; the remembered copy matches it
            // so a reload doesn't drift the saved host/name with stray whitespace.
            var c = new FakeUiClient();
            var store = new FakeConnectStore();
            var s = Shell(c, store);
            s.PrefillConnect("  spacey.host  ", 4534, "  Vlad  ");
            s.RequestConnect();

            c.Status = ConnectionStatus.Connected;
            s.Tick(Array.Empty<CycleSnapshot>(), 0);

            Assert.Equal("spacey.host", store.Saved!.Value.Host);
            Assert.Equal("Vlad", store.Saved!.Value.Name);
        }

        [Fact]
        public void PrefillConnect_SetsAllThreeFields()
        {
            // The live-gate seam for blank-default heads: set a concrete target, then connect.
            var c = new FakeUiClient();
            var s = Shell(c, new FakeConnectStore());
            s.PrefillConnect("seed.host", 9999, "Seeded");
            s.RequestConnect();
            Assert.Equal("seed.host", c.ConnHost);
            Assert.Equal(9999, c.ConnPort);
            Assert.Equal("Seeded", c.ConnName);
        }
    }
}
