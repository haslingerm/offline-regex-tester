#!/usr/bin/env bash
# Publishes RegexTester as a self-contained single-file for Windows 11 x64 and Linux x64.
# Run from the repo root (where this script lives).
set -euo pipefail

PROJ="RegexTester/RegexTester.csproj"
OUT="./publish"

echo "==> Building self-contained for linux-x64…"
dotnet publish "$PROJ" \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o "$OUT/linux-x64"

echo ""
echo "==> Building self-contained for win-x64…"
dotnet publish "$PROJ" \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o "$OUT/win-x64"

echo ""
echo "==> Done. Artifacts in $OUT/"
ls -lh "$OUT/linux-x64/RegexTester" "$OUT/win-x64/RegexTester.exe" 2>/dev/null || true
