#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

UNICODE_VERSION="17.0.0"
BASE="https://www.unicode.org/Public/${UNICODE_VERSION}/ucd"
OUTPUT_DIR="${1:-data}"
mkdir -p "$OUTPUT_DIR"

fetch() {
  local url="$1" out="$2" token="$3"
  curl -fsSL -o "$OUTPUT_DIR/$out" "$url"
  if ! head -20 "$OUTPUT_DIR/$out" | grep -qF "$token"; then
    echo "version mismatch in $out: expected header containing '$token'"
    exit 1
  fi
}

fetch "$BASE/extracted/DerivedLineBreak.txt"       DerivedLineBreak.txt       "DerivedLineBreak-${UNICODE_VERSION}.txt"
fetch "$BASE/EastAsianWidth.txt"                   EastAsianWidth.txt         "EastAsianWidth-${UNICODE_VERSION}.txt"
fetch "$BASE/emoji/emoji-data.txt"                 emoji-data.txt             "Version: 17.0"
fetch "$BASE/extracted/DerivedGeneralCategory.txt" DerivedGeneralCategory.txt "DerivedGeneralCategory-${UNICODE_VERSION}.txt"
fetch "$BASE/auxiliary/LineBreakTest.txt"          LineBreakTest.txt          "LineBreakTest-${UNICODE_VERSION}.txt"

echo "reference data for Unicode ${UNICODE_VERSION} written to $OUTPUT_DIR"
