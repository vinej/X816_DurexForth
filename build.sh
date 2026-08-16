#!/usr/bin/env bash
# Build durexForth for the X816 (see X816_core doc/DUREXFORTH.md).
#
#   ./build.sh          assemble build/forth.bin (an X816 image: "X816"
#                       magic at $01:0000, entry at $01:0004)
#
# The X16 build steps this script used to run - SD-card population and
# x16emu - are gone with the platform. Their X816 successors (FORTH.BIN on
# the card image, a run-emu.sh over the X816 emulator with -autokeys and a
# negative control) arrive with the test-harness phase.
#
# Requirements (paths relative to the repo root):
#   acme/acme.exe       ACME assembler (0.97+; assembles the 65816 natively)
set -euo pipefail
cd "$(dirname "$0")"

ACME="${ACME:-acme/acme.exe}"

echo "==> assembling forth.bin"
mkdir -p build
[ -f build/version.asm ] || printf '!text "durexForth 1.0"\n' > build/version.asm
"$ACME" -I asm asm/durexforth.asm
echo "    forth.bin = $(stat -c%s build/forth.bin) bytes"
echo "==> done"
