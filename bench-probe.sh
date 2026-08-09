#!/usr/bin/env bash
# ============================================================================
# run-bench.sh -- how fast is durexForth, in the units the period used.
#
# The same benchmarks ../X816_SuperBasic/bench/ runs on SuperBasic --
# Rugg & Feldman BM1-BM8 (Kilobaud 1977) and Ahl's (Creative Computing
# 1983) -- in Forth, on the same machine, the same emulator, and the
# same millisecond counter, so the two tables can sit side by side.
# bench/bench.fs states what is being compared and what is not; read it
# before quoting numbers.
#
# EIGHT MHZ, NOT -mhz 32. run-tests.sh overclocks because a correctness
# suite is allowed to; the ms counter counts EXECUTED CYCLES at the -mhz
# rate, so an overclocked benchmark reads exactly 4x fast. -warp is fine
# for the same reason: it changes wall time, not the reading. Measured
# against the FPGA on 2026-08-09 (SuperBasic BM1), the emulator's cycle
# model runs about 8 percent optimistic -- deltas are sound, absolute
# times worth quoting come from hardware.
#
# NO AUTOKEYS BEYOND THE RUN COMMAND. The SuperBasic bench harness lost
# three rounds to -autokeys typing into a running program (the SMC FIFO
# drops what does not fit); durexForth's AUTORUN hook runs the whole
# bench from the card with nothing typed at all, which is the structural
# fix the BASIC side had to build by hand.
#
#   ./run-bench.sh            build, run, print the table
#   ./run-bench.sh --raw      also dump the decoded screen
# ============================================================================
set -u

. "$(dirname "$0")/../X816_Calypsi/runtime/calypsi.sh"
cd "$(dirname "$0")"
OUT=$(mktemp -d)
trap 'rm -rf "$OUT"' EXIT
WOUT=$(cygpath -m "$OUT" 2>/dev/null || echo "$OUT")

KERNEL="../X816_Calypsi/examples/shell/kernel.bin"
[ -f "$KERNEL" ] || { echo "kernel.bin missing -- run sh build.sh in X816_Calypsi/examples/shell"; exit 1; }

RAW=0
[ "${1:-}" = "--raw" ] && RAW=1

python build/parencheck.py bench/probe.fs || exit 1
./build.sh || exit 1
(cd fpengine && bash build.sh) || exit 1

# The card: FORTH.BIN, the boot sources, FLOAT and RND, and the bench.
# Names are BARE 8.3, as the kernel's FAT32 reader wants them.
python - "$WOUT" <<'PY'
import os, sys
from pyfatfs.PyFat import PyFat
from pyfatfs.PyFatFS import PyFatFS

out = os.path.join(sys.argv[1], "card.img")
with open(out, "wb") as f:
    f.truncate(64 * 1024 * 1024)
fat = PyFat()
fat.mkfs(out, fat_type=PyFat.FAT_TYPE_FAT32, sector_size=512, label="FORTHBEN")
fat.close()

fs = PyFatFS(out)
with open("build/forth.bin", "rb") as f, fs.open("/FORTH.BIN", "wb") as g:
    g.write(f.read())
with open("fpengine/fpengine.bin", "rb") as f, fs.open("/FPENGINE.BIN", "wb") as g:
    g.write(f.read())

SRC = [("forth", n) for n in ["base", "asm", "wordlist", "labels", "doloop",
                              "debug", "require", "accept", "compat", "rnd"]] + \
      [("forth/mod", "float")] + \
      [("bench", n) for n in ["probe"]]
for d, n in SRC:
    with open(os.path.join(d, n + ".fs"), "rb") as f:
        data = f.read()
    with fs.open("/" + n.upper(), "wb") as g:
        g.write(data)

with fs.open("/AUTORUN", "wb") as g:
    g.write(b"include probe\n")
fs.close()
print("card: %d files + FORTH.BIN + AUTORUN" % len(SRC))
PY
[ $? -eq 0 ] || exit 1

echo "running (8 MHz, warped; the wall clock is not the measurement) ..."
SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy timeout 600 \
    "$EMU/build/x16emu.exe" -boot "$(cygpath -m "$CORE/boot/boot.rom")" \
    -load "F00000,$(cygpath -m "$(pwd)/$KERNEL")" \
    -sdcard "$WOUT/card.img" \
    -autokeys 'run FORTH.BIN\n' \
    -mhz 32 -warp -gif "$WOUT/out.gif" >/dev/null 2>&1

[ -f "$OUT/out.gif" ] || { echo "no recording -- the emulator produced nothing"; exit 1; }

python - "$WOUT/out.gif" "$RT/font_cp437.s" "$RAW" <<'PY'
import sys, re, io
import numpy as np
from PIL import Image, ImageFile
ImageFile.LOAD_TRUNCATED_IMAGES = True
gif, fontinc, raw = sys.argv[1], sys.argv[2], sys.argv[3] == "1"

vals = []
for line in io.open(fontinc, encoding='utf-8'):
    m = re.match(r'\s*\.byte\s+(.*)$', line.split(';')[0])
    if m:
        vals += [int(x.strip().lstrip('$'), 16)
                 for x in m.group(1).split(',') if x.strip()]
def _key(rows8):
    k = 0
    for b in rows8:
        k = (k << 8) | int(b)
    return k
glyph = {}
for _c in range(0x20, 0x7F):
    glyph[_key(vals[_c * 8:(_c + 1) * 8])] = chr(_c)
glyph[(1 << 64) - 1] = ' '
BIT = (0x80 >> np.arange(8)).astype(np.uint64)
PLACE = (np.uint64(256) ** np.arange(7, -1, -1, dtype=np.uint64))

im = Image.open(gif)
n = 0
while True:
    try:
        im.seek(n); im.load(); n += 1
    except (EOFError, OSError, IndexError):
        break
im.seek(n - 1)
a = np.asarray(im.convert('RGB'))[:480, :640]
cells = a.any(axis=2).reshape(60, 8, 80, 8).transpose(0, 2, 1, 3)
keys = ((cells * BIT).sum(axis=3).astype(np.uint64) * PLACE).sum(axis=2)
rows = ["".join(glyph.get(int(k), '?') for k in keys[r]).rstrip()
        for r in range(60)]

if raw:
    for i, r in enumerate(rows):
        if r:
            print("%2d: %r" % (i, r))
    print()

WHAT = {
    "BM1": "empty DO LOOP, 1000 times (integer; BASIC's BM1I)",
    "BM2": "a VARIABLE incremented, BEGIN..UNTIL branch",
    "BM3": "  + K/K*K+K-K, five fetches, four ops",
    "BM4": "  + constants instead of K (the classic one)",
    "BM5": "  + a call to an empty word each pass",
    "BM6": "  + an empty DO LOOP of 5 inside",
    "BM7": "  + an array store inside that loop",
    "BM8": "K^2, LN, SIN via FLOAT (MFLPT, ~9 digits)",
    "AHL": "Ahl: 1000 FSQRT, 1000 F**, 2000 RND",
}
found, acc = {}, {}
for r in rows:
    if r.startswith(" "):
        continue
    m = re.match(r'^(BM[1-8]|AHL)\s+(-?\d+)\s*$', r.strip())
    if m:
        found[m.group(1)] = int(m.group(2))
    m = re.match(r'^(AHLA|AHLR)\s+(\S+)$', r.strip())
    if m:
        acc[m.group(1)] = m.group(2)

if not found:
    print("no results on the screen -- run with --raw. A Forth error")
    print("aborts the include, so the table stops at the word that broke.")
    sys.exit(1)

print()
print("durexForth on the X816, 8 MHz                Rugg/Feldman and Ahl")
print("=" * 68)
print("%-5s %9s %9s   %s" % ("", "ms", "s", "what it adds"))
total = 0
for tag in ("BM1", "BM2", "BM3", "BM4", "BM5", "BM6", "BM7", "BM8", "AHL"):
    if tag not in found:
        print("%-5s %9s %9s   %s" % (tag, "-", "-", "DID NOT RUN -- see --raw"))
        continue
    ms = found[tag]
    total += ms
    print("%-5s %9d %9.2f   %s" % (tag, ms, ms / 1000.0, WHAT[tag]))
print("-" * 68)
print("%-5s %9d %9.2f   all nine" % ("", total, total / 1000.0))
if acc:
    print()
    print("Ahl's accuracy, 0 being perfect (MFLPT, about nine digits):")
    print("   arithmetic error  %s" % acc.get("AHLA", "?"))
    print("   RND sum error     %s" % acc.get("AHLR", "?"))
print()
print("Read beside ../X816_SuperBasic/run-bench.sh's table: same machine,")
print("same emulator, same counter. bench/bench.fs says what is and is")
print("not comparable -- compiled Forth against interpreted BASIC is the")
print("point, integer BM1-BM7 against BASIC's floats is not.")
PY
