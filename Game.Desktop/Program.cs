using Armagetron.Game;
using Armagetron.Game.UI;
using Armagetron.Lib;

// The connect screen ships with a BLANK host (no baked-in server in the public repo); the file
// store remembers the player's last server across launches. Port/name keep sensible placeholders.
// --host/--port/--name still override. Name default is 'Vlad': the server currently rejects
// 'AaBot' with a Cheater() flag (stale/ghost session), while 'Vlad' registers cleanly — see
// registration-race notes. Override with --name once 'AaBot' is clear server-side again.
string host = "";
int    port = 4534;
string name = "Vlad";
bool   hostGiven = false;

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--host" && i + 1 < args.Length) { host = args[++i]; hostGiven = true; }
    else if (args[i] == "--port" && i + 1 < args.Length) port = int.Parse(args[++i]);
    else if (args[i] == "--name" && i + 1 < args.Length) name = args[++i];
}

// The headless harness modes below (--selftest/--blankcheck) connect a probe directly with no UI,
// so a blank host would leave them nowhere to go: fall back to the dev listen server when --host
// is omitted. This fallback is dev-only and never seeds the shipped connect screen.
string harnessHost = hostGiven ? host : "192.168.68.61";

// Headless self-test (--selftest): exercise the real UiArmaClient connect path against a live
// server WITHOUT opening a window, so the new adapter can be verified from a terminal (CLAUDE.md
// step 4). Drives BeginConnect, polls Status like the shell does, reports, and exits.
if (System.Array.IndexOf(args, "--selftest") >= 0)
{
    using var probe = new UiArmaClient();
    System.Console.WriteLine($"[selftest] BeginConnect {harnessHost}:{port} as '{name}'");
    probe.BeginConnect(harnessHost, port, name);
    var sw = System.Diagnostics.Stopwatch.StartNew();
    while (probe.Status == ConnectionStatus.Connecting && sw.Elapsed.TotalSeconds < 50)
        System.Threading.Thread.Sleep(100);
    System.Console.WriteLine($"[selftest] Status={probe.Status} MyCycleId={probe.MyCycleId} " +
                             $"cycles={probe.Snapshot().Length} err={probe.LastError ?? "none"}");
    if (probe.Status == ConnectionStatus.Connected)
    {
        probe.TurnLeft(); probe.TurnRight();
        System.Threading.Thread.Sleep(1500);
        System.Console.WriteLine($"[selftest] after turns: cycles={probe.Snapshot().Length} " +
                                 $"events={probe.DrainEvents().Count}");
    }
    probe.Disconnect();
    return;
}

// Headless blank-frame check (--blankcheck): the live verification for the round-reset blanking
// bug (render_blank_and_registration_bugs). Connect, then poll Snapshot() at render cadence for
// ~20s — long enough to span several round resets in a fast arena — and count how many frames
// were EMPTY *after* the first cycle appeared. With the deferred (sample-and-hold) ClearRound a
// continuous renderer must never blank mid-stream: the post-first-cycle blank count must be 0.
if (System.Array.IndexOf(args, "--blankcheck") >= 0)
{
    using var probe = new UiArmaClient();
    System.Console.WriteLine($"[blankcheck] BeginConnect {harnessHost}:{port} as '{name}'");
    probe.BeginConnect(harnessHost, port, name);
    var sw = System.Diagnostics.Stopwatch.StartNew();
    while (probe.Status == ConnectionStatus.Connecting && sw.Elapsed.TotalSeconds < 50)
        System.Threading.Thread.Sleep(100);
    System.Console.WriteLine($"[blankcheck] Status={probe.Status} MyCycleId={probe.MyCycleId}");

    int samples = 0, maxCycles = 0, blanksBeforeFirst = 0, blanksAfterFirst = 0;
    bool seenAny = false;
    var sampleSw = System.Diagnostics.Stopwatch.StartNew();
    while (sampleSw.Elapsed.TotalSeconds < 20 && probe.Status == ConnectionStatus.Connected)
    {
        int n = probe.Snapshot().Length;
        samples++;
        if (n > maxCycles) maxCycles = n;
        if (n == 0) { if (seenAny) blanksAfterFirst++; else blanksBeforeFirst++; }
        else        seenAny = true;
        System.Threading.Thread.Sleep(16); // ~60fps render cadence
    }
    System.Console.WriteLine(
        $"[blankcheck] samples={samples} maxCycles={maxCycles} " +
        $"blanksBeforeFirstCycle={blanksBeforeFirst} blanksAfterFirstCycle={blanksAfterFirst}");
    System.Console.WriteLine(blanksAfterFirst == 0 && maxCycles > 0
        ? "[blankcheck] PASS — never blanked once cycles were live (sample-and-hold through resets)"
        : "[blankcheck] FAIL — view blanked mid-stream or no cycles ever appeared");
    probe.Disconnect();
    return;
}

// The client starts DISCONNECTED: the in-app connect screen drives BeginConnect now, so the
// SFX audition (--audition): boot just the audio device and play every manifest sound in
// sequence, naming each on the console, then exit. No server, no full UI — the quickest way to
// hear the whole pack, including cues (countdown/wall_grind) that need specific in-game triggers.
if (System.Array.IndexOf(args, "--audition") >= 0)
{
    using var audition = new AuditionGame();
    audition.Run();
    return;
}

// window opens immediately at the connect form (no blocking pre-connect). Login, the desc=201
// registration race, fresh-socket retry and the session loop all live inside UiArmaClient.
var client = new UiArmaClient();
// Blank shipped default + a file store that remembers the last successful server across launches.
var shell  = new AppShell(client, UiTheme.Default, host, port, name, touchControls: false,
                          store: new FileConnectStore());
// An explicit --host is a deliberate target for this run, so it wins over the remembered choice.
if (hostGiven) shell.PrefillConnect(host, port, name);
var input  = new DesktopShellInput();

using var game = new ArmagetronGame(client, input, shell,
    title: "Armagetron — arrows / mouse to play, Esc to pause/quit");
input.Attach(game.Window);
game.Run();
