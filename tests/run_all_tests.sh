#!/usr/bin/env bash
# Runs every automated suite of the repository. Used by docker-compose.test.yml
# (service "tests") and directly on a machine with the .NET 10 SDK.
set -euo pipefail

# CombatMeter's named pipe must fit the 108-char Unix socket limit.
export TMPDIR="${TMPDIR:-/tmp}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

cd "$(dirname "$0")/.."

echo "== build solution"
dotnet build src/Blizzless-D3.sln -c Release --nologo -v q

echo "== xUnit (DiIiS.Tests)"
dotnet test src/DiIiS.Tests/DiIiS.Tests.csproj -c Release --nologo --no-build

echo "== combat regression suite"
dotnet run --project tests/CombatRegression/CombatRegression.csproj -c Release --nologo --no-build

echo "== cube regression suite"
dotnet run --project tests/CubeRegression/CubeRegression.csproj -c Release --nologo --no-build

echo "== all suites passed"
