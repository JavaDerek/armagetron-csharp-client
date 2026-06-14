#!/usr/bin/env bash
# Canonical pre-commit check: restore, build, and run the test suite with the
# hard 90% line-coverage gate (see CLAUDE.md, TDD-loop step 3).
# A non-zero exit means DO NOT COMMIT — fix tests/coverage first.
set -euo pipefail

cd "$(dirname "$0")"

# The Game.Android head needs a valid JDK to build. If JAVA_HOME is unset or points at a
# missing directory (e.g. a moved Android Studio JBR), fall back to the system default via
# java_home rather than failing the whole solution build. No-op on non-macOS / when already
# valid; the rest of the projects don't need a JDK at all.
if [ -z "${JAVA_HOME:-}" ] || [ ! -x "${JAVA_HOME}/bin/javac" ]; then
  if [ -x /usr/libexec/java_home ]; then
    if discovered="$(/usr/libexec/java_home 2>/dev/null)"; then
      export JAVA_HOME="$discovered"
      echo "==> JAVA_HOME was invalid; using $JAVA_HOME"
    fi
  fi
fi

echo "==> dotnet restore"
dotnet restore ArmagetronClient.sln

echo "==> dotnet build (warnings as errors)"
dotnet build ArmagetronClient.sln -c Debug --no-restore -warnaserror

echo "==> dotnet test (xUnit + 90% coverage gate)"
dotnet test ArmagetronClient.sln --no-build

echo
echo "✅ Build, tests, and 90% coverage gate all passed."
echo "   Remaining before commit: live-server verification (CLAUDE.md step 4):"
echo "   dotnet run --project Bot.Console -- --host 192.168.68.61 --port 4534 --name AaBot"
