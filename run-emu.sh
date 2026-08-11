#!/usr/bin/env bash
# Boot durexForth on the X816 emulator and type at the REPL.
#
# The whole stage-A stack is under test at once: the resident kernel boots
# from the firmware region, its shell EXECs FORTH.BIN off a real FAT32 card,
# the Forth comes up on the kernel console, and every keystroke travels the
# real SMC path (-autokeys). The checks:
#
#   1. the boot banner prints        - PUTCHR crossing works at all
#   2. `1 2 + .` answers `3`         - line input, FIND, number parse,
#                                      the data stack, and . through EMIT
#   3. `xyzzy` answers `xyzzy?`      - the error path (RVS/TYPE/THROW)
#
#   ./run-emu.sh              build and run
#   ./run-emu.sh --negative   corrupt the image magic: EXEC must refuse it
#                             and no banner may print, proving check 1 can
#                             fail
#
# Requires: pip install pillow pyfatfs, and a built X816_Calypsi
# programs/shell/kernel.bin (sh build.sh there).
set -u

# EMU, CORE and RT come from the same place every X816_Calypsi script gets
# them, so a moved checkout moves once.
. "$(dirname "$0")/../X816_Calypsi/runtime/calypsi.sh"
cd "$(dirname "$0")"
OUT=$(mktemp -d)
trap 'rm -rf "$OUT"' EXIT
WOUT=$(cygpath -m "$OUT" 2>/dev/null || echo "$OUT")

KERNEL="../X816_Calypsi/programs/shell/kernel.bin"
[ -f "$KERNEL" ] || { echo "kernel.bin missing -- run sh build.sh in X816_Calypsi/programs/shell"; exit 1; }

./build.sh || exit 1
cp build/forth.bin "$OUT/forth.bin"

NEG=0
if [ "${1:-}" = "--negative" ]; then
    NEG=1
    # Break the image magic: EXEC must refuse the file, so no banner.
    printf 'Y' | dd of="$OUT/forth.bin" bs=1 count=1 conv=notrunc 2>/dev/null
    echo "negative control: corrupted image magic, expecting NO banner"
fi

# A scratch card with FORTH.BIN on it, written by pyfatfs -- an independent
# FAT32 implementation, as everywhere else in the tree.
cp "$CORE/boot/fat32.img" "$OUT/scratch.img"
python - "$WOUT/scratch.img" "$WOUT/forth.bin" <<'PY'
import sys
from pyfatfs.PyFatFS import PyFatFS
img, binpath = sys.argv[1], sys.argv[2]
fs = PyFatFS(img)
with open(binpath, "rb") as f:
    data = f.read()
with fs.open("/FORTH.BIN", "wb") as g:
    g.write(data)
fs.close()
print("card: FORTH.BIN = %d bytes" % len(data))
PY
[ $? -eq 0 ] || exit 1

SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy timeout 90 \
    "$EMU/build/x16emu.exe" -boot "$(cygpath -m "$CORE/boot/boot.rom")" \
    -load "F00000,$(cygpath -m "$(pwd)/$KERNEL")" \
    -sdcard "$WOUT/scratch.img" \
    -autokeys 'run FORTH.BIN\n                                        1 2 + .\nxyzzy\n' \
    -warp -gif "$WOUT/out.gif" >/dev/null 2>&1

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
    for col in range(60):
        bits = []
        for y in range(8):
            b = 0
            for x in range(8):
                if px[col * 8 + x, r * 8 + y] != (0, 0, 0):
                    b |= 0x80 >> x
            bits.append(b)
        out += glyph.get(tuple(bits), '?')
    return out.rstrip()

rows = [row_text(r) for r in range(34)]
body = "\n".join(r for r in rows if r)

def fail(msg):
    print("FAIL:", msg)
    for i, r in enumerate(rows):
        print(f"  {i}: {r!r}")
    sys.exit(1)

banner = any("durexForth" in r for r in rows)

if negative:
    if banner:
        fail("banner printed despite a corrupted image magic -- "
             "EXEC's magic check is not what admitted the program")
    print("PASS (negative control): corrupted magic, no banner -- "
          "EXEC refused the image as designed")
    sys.exit(0)

if not banner:
    fail("no boot banner -- FORTH.BIN did not run or PUTCHR is broken")
if "3" not in body:
    fail("`1 2 + .` did not answer 3")
if "xyzzy?" not in body:
    fail("`xyzzy` did not report as an undefined word")

print("PASS: durexForth booted from the card, did arithmetic, and refused")
print("      an undefined word -- all through the kernel console")
for r in rows:
    if r:
        print("   ", r)
PY
