# Armagetron C# Client — Engineering Process

This file governs work in this repository. It **adds to** (never contradicts) the global
standards in `~/.claude/CLAUDE.md`. Where the global file speaks in Python terms
(ruff/pytest/SQLite), the C#-equivalent rules below take precedence for this repo.

## The TDD loop — mandatory for every change

Every feature, protocol-decode change, and bug fix follows these five steps **in order**.
Do not skip ahead; do not commit until step 5.

1. **Red — write the test first and watch it fail.**
   Add/adjust xUnit tests that encode the expected behavior, then run `dotnet test` and
   confirm the new test **fails** against current code. A test that passes before you've
   written the implementation is testing nothing.

2. **Green — implement until tests pass.**
   Write the minimum production code to make the failing tests pass. Re-run `dotnet test`.

3. **Coverage ≥ 90% — enforced, not aspirational.**
   `dotnet test` runs the coverlet **hard gate**: total line coverage below **90%** fails
   the build (see `Core.Protocol.Tests.csproj`). New code must carry happy-path **and**
   key error-path tests. If the gate fails, add tests — do not lower the threshold.

4. **Live-server verification.**
   Protocol/decode/bot-behavior changes must be proven against a real Armagetron
   `0.2.9.3.0` listen server before they count as done:
   ```
   dotnet run --project Bot.Console -- --host 192.168.68.61 --port 4534 --name AaBot
   ```
   Read the live log: confirm the change does what the unit test claimed (e.g. a decoded
   position is sane, no `cheating`/disconnect, turns accepted). Unit tests prove the codec;
   the live server proves the protocol. Both are required.

5. **Only now: commit and push.**
   Commit (and push, when asked) only after steps 1–4 all pass. The commit message states
   what was verified live.

## Testability: pure parsers in Core.Protocol, I/O at the edges

- Protocol decode/encode logic is **pure and lives in `Core.Protocol`** (see
  `Game/CycleDestinationSync.cs`), so it is unit-testable from captured hex with no sockets.
  `Bot.Console`/`BotSession` should **call** these parsers, not embed wire-format logic.
- The raw socket boundary (`Core.Net/UdpLink.cs`) is `[ExcludeFromCodeCoverage]`: it is the
  one piece verified by the live-server gate (step 4), not by unit tests. Keep such I/O
  boundaries thin and behind an interface (`IUdpLink`) so the logic above them is mockable.

## Commands

```
./build-and-test.sh          # restore, build, test + 90% gate (the canonical pre-commit check)
dotnet test                  # tests + coverage gate (fails under 90% line coverage)
dotnet test /p:Threshold=0   # tests only, gate disabled (use sparingly, never to commit)
```

## Clean-room provenance (protocol work)

The Armagetron server is GPL. This client must derive **only** from the wire spec, never
from copied source. When server source is consulted to understand a format, the established
process is: read source → write a strictly PCAP-derived spec into `PROTOCOL.md` → implement
in the client **from the spec alone** (ideally a fresh context with no source access). The
`/tmp/armagetronad` clone and `/tmp/aa_decode.py` are reading/analysis aids only and must
not be referenced from shipped client code.
