#!/usr/bin/env bash
# The durexForth test suite, end to end on the X816 emulator.
#
# Builds forth.bin, puts it on a scratch card together with the Forth
# sources and an AUTORUN of "include test", and boots. base.fs's autorun
# hook picks the tests up with no typing at all - so there is no autokeys
# type-ahead to race the 16-byte SMC FIFO. On the first failed assertion
# the Hayes tester prints "INCORRECT RESULT" / "WRONG NUMBER" and QUITs;
# the +++ ALL TESTS PASSED +++ banner appears only if nothing failed.
#
#   ./run-tests.sh              build and run the suite
#   ./run-tests.sh --negative   AUTORUN a test that must fail, and require
#                               the tester to say so - proving the banner
#                               check can go red
#
# Requires: pip install pillow pyfatfs, and a built X816_Calypsi
# examples/shell/kernel.bin (sh build.sh there).
set -u

. "$(dirname "$0")/../X816_Calypsi/runtime/calypsi.sh"
cd "$(dirname "$0")"
OUT=$(mktemp -d)
trap 'rm -rf "$OUT"' EXIT
WOUT=$(cygpath -m "$OUT" 2>/dev/null || echo "$OUT")

KERNEL="../X816_Calypsi/examples/shell/kernel.bin"
[ -f "$KERNEL" ] || { echo "kernel.bin missing -- run sh build.sh in X816_Calypsi/examples/shell"; exit 1; }

./build.sh || exit 1

NEG=0
[ "${1:-}" = "--negative" ] && NEG=1 && \
    echo "negative control: AUTORUN a failing assertion, expecting INCORRECT RESULT"

# Fresh FAT32 card: FORTH.BIN, the Forth sources (8.3 names, no extension,
# exactly how `include` asks for them), and the AUTORUN hook.
python - "$WOUT" "$NEG" <<'PY'
import os, sys
from pyfatfs.PyFat import PyFat
from pyfatfs.PyFatFS import PyFatFS

out, neg = os.path.join(sys.argv[1], "card.img"), sys.argv[2] == "1"

with open(out, "wb") as f:
    f.truncate(64 * 1024 * 1024)
fat = PyFat()
fat.mkfs(out, fat_type=PyFat.FAT_TYPE_FAT32, sector_size=512, label="FORTHTST")
fat.close()

fs = PyFatFS(out)
with open("build/forth.bin", "rb") as f, fs.open("/FORTH.BIN", "wb") as g:
    g.write(f.read())

# (repo file, card name): card names are BARE 8.3 - the kernel's FAT32
# reader refuses anything longer than 8 characters and skips long
# filenames on purpose, so "testcoreplus" travels as "coreplus".
SRC = [("forth", n, n) for n in ["base", "asm", "wordlist", "labels",
                                 "doloop", "debug", "require", "accept",
                                 "compat"]] + \
      [("test", n, c) for n, c in [
          ("tester", "tester"), ("testcore", "testcore"),
          ("testcoreplus", "coreplus"), ("testcoreext", "coreext"),
          ("testexception", "testexc"), ("testdouble", "testdbl"),
          ("testvideo", "testvid"), ("testsprite", "testspr"),
          ("testtile", "testtile"), ("testpalfx", "testpal"),
          ("testcoreadd", "coreadd"), ("testfar", "testfar"),
          ("testbrk", "testbrk"), ("testturbo", "turbo"),
          ("testnmi", "testnmi"), ("test", "test"), ("1", "1")]]
for d, n, card in SRC:
    with open(os.path.join(d, n + ".fs"), "rb") as f:
        data = f.read()
    with fs.open("/" + card.upper(), "wb") as g:
        g.write(data)

if neg:
    # A failing assertion through the same tester the real suite uses: the
    # control proves the detector, not the plumbing around it.
    autorun = "include compat\ninclude tester\nt{ 1 -> 2 }t\n"
else:
    autorun = "include test\n"
with fs.open("/AUTORUN", "wb") as g:
    g.write(autorun.encode())
fs.close()
print("card: %d source files + FORTH.BIN + AUTORUN" % len(SRC))
PY
[ $? -eq 0 ] || exit 1

# No -autokeys beyond the run command: AUTORUN does the rest.
# -mhz 32: the suite is a CORRECTNESS run, and at the real 8 MHz it needs
# ~half an hour of emulated time (mostly compiling 50 KB of source read a
# byte at a time through FS_READ). Overclocking the emulated CPU is fine
# here and NOWHERE that measures time - benchmarks stay at 8.
SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy timeout 480 \
    "$EMU/build/x16emu.exe" -boot "$(cygpath -m "$CORE/boot/boot.rom")" \
    -load "F00000,$(cygpath -m "$(pwd)/$KERNEL")" \
    -sdcard "$WOUT/card.img" \
    -autokeys 'run FORTH.BIN\n' \
    -mhz 32 -warp -gif "$WOUT/out.gif" >/dev/null 2>&1

python - "$WOUT/out.gif" "$RT/font_cp437.s" "$NEG" <<'PY'
import sys, re, io
from PIL import Image, ImageFile
ImageFile.LOAD_TRUNCATED_IMAGES = True

gif, fontinc, negative = sys.argv[1], sys.argv[2], sys.argv[3] == "1"

vals = []
for line in io.open(fontinc, encoding='utf-8'):
    m = re.match(r'\s*\.byte\s+(.*)$', line.split(';')[0])
    if m:
        vals += [int(x.strip().lstrip('$'), 16)
                 for x in m.group(1).split(',') if x.strip()]
glyph = {}
for _c in range(0x20, 0x7F):
    glyph[tuple(vals[_c * 8:(_c + 1) * 8])] = chr(_c)

im = Image.open(gif)
n = 0
while True:
    try:
        im.seek(n); im.load(); n += 1
    except (EOFError, OSError, IndexError):
        break
if n == 0:
    sys.exit("no decodable frame -- did the emulator run?")
im.seek(n - 1)
px = im.convert('RGB').load()

def row_text(r):
    out = ""
    for col in range(80):
        bits = []
        for y in range(8):
            b = 0
            for x in range(8):
                if px[col * 8 + x, r * 8 + y] != (0, 0, 0):
                    b |= 0x80 >> x
            bits.append(b)
        out += glyph.get(tuple(bits), '?')
    return out.rstrip()

rows = [row_text(r) for r in range(60)]
body = "\n".join(r for r in rows if r)

def fail(msg):
    print("FAIL:", msg)
    for i, r in enumerate(rows):
        if r:
            print(f"  {i}: {r!r}")
    sys.exit(1)

passed = "ALL TESTS PASSED" in body
broke  = ("INCORRECT RESULT" in body) or ("WRONG NUMBER" in body)

if negative:
    if passed:
        fail("the banner printed around a failing assertion -- "
             "the tester is not what gates it")
    if not broke:
        fail("the failing assertion produced no INCORRECT RESULT")
    print("PASS (negative control): the tester caught the bad assertion "
          "and the banner stayed away")
    sys.exit(0)

if broke:
    fail("a test failed")
if not passed:
    fail("the suite did not reach the ALL TESTS PASSED banner")

print("PASS: the durexForth test suite is green on the X816")
for r in rows:
    if r:
        print("   ", r)
PY
