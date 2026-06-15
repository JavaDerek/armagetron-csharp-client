# Armagetron core DLLs (generated — do not edit)

These three netstandard2.1 assemblies are the VR head's contract with the game and are produced
from the repo's own source by `Game.Oculus/build-core-dlls.sh`:

- `Armagetron.Protocol.dll` — wire codec + render/camera model (`Scene3DBuilder`, `Camera3D`, …)
- `Armagetron.Net.dll` — UDP transport
- `Armagetron.Lib.dll` — the `ArmaClient` / `UiArmaClient` facade the runner uses

They are **git-ignored** (see `.gitignore` here) because they're build output — run the script after
cloning, or whenever the core changes. The `Armagetron.Oculus` asmdef references them by name.
