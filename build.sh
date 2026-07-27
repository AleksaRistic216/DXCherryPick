#!/usr/bin/env bash
set -e

OUTPUT="$(dirname "$0")/release"

rm -rf "$OUTPUT"

dotnet publish src/DXCP.WinForms/DXCP.WinForms.csproj \
    -c Release \
    -r win-x64 \
    --self-contained false \
    -o "$OUTPUT"

dotnet publish src/DXCP.Cli/DXCP.Cli.csproj \
    -c Release \
    -r win-x64 \
    --self-contained false \
    -o "$OUTPUT"

echo "Build complete -> $OUTPUT"
