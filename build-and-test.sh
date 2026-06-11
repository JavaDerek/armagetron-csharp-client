#!/usr/bin/env bash
# Canonical pre-commit check: restore, build, and run the test suite with the
# hard 90% line-coverage gate (see CLAUDE.md, TDD-loop step 3).
# A non-zero exit means DO NOT COMMIT — fix tests/coverage first.
set -euo pipefail

cd "$(dirname "$0")"

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
