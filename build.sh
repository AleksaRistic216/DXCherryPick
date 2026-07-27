#!/usr/bin/env bash
set -e

PROJECT="src/DXCP.WinForms/DXCP.WinForms.csproj"
OUTPUT="$(dirname "$0")/release"

rm -rf "$OUTPUT"

dotnet publish "$PROJECT" \
    -c Release \
    -r win-x64 \
    --self-contained false \
    -o "$OUTPUT"

echo "Build complete -> $OUTPUT"
