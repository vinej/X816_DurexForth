#!/usr/bin/env bash
# SAVE-IMAGE: write the live, compiled Forth image back to the card.
#
#   ./run-saveimage.sh              build and run
#   ./run-saveimage.sh --negative   never type SAVE-IMAGE, and require the
#                                   checks to fail
#
# This is the foundation of the pre-compiled boot: durexForth spends 20.25
# emulated seconds recompiling base.fs and its seven includes at every start
# (measured in run-emu.sh), and the way out is to save the machine once and
# load that instead. Everything the interpreter has compiled lives in the
# program banks, and so does the state describing it -- HERE, LATEST and every
# VALUE are immediates inside the image -- so writing the banks out writes the
# whole Forth state, with nothing to serialise.
#
# THE CHECK THAT MATTERS is not "a file appeared". It is that the file
# contains WORDS THAT DO NOT EXIST UNTIL BASE.FS HAS BEEN COMPILED --
# `cpu-mhz`, `turbo?`, `far-unused`. None of them is in the assembled
# forth.bin, which was verified before this test was written. A save that
# copied the original image, or wrote from the wrong bank, or wrote before the
# compile finished, produces a file of exactly the right size that starts with
# the right magic and passes every other check here. This one it cannot pass.
#
# Requires Pillow and pyfatfs.
set -u

. "$(dirname "$0")/../X816_Calypsi/runtime/calypsi.sh"
cd "$(dirname "$0")"

if [ -n "${KEEP_OUT:-}" ]; then
    OUT="$KEEP_OUT"
    mkdir -p "$OUT"
else
    OUT=$(mktemp -d)
    trap 'rm -rf "$OUT"' EXIT
fi
WOUT=$(cygpath -m "$OUT" 2>/dev/null || echo "$OUT")

KERNEL="../X816_Calypsi/programs/shell/kernel.bin"
[ -f "$KERNEL" ] || { echo "kernel.bin missing -- run sh build.sh in X816_Calypsi/programs/shell"; exit 1; }

./build.sh || exit 1
cp build/forth.bin "$OUT/forth.bin"

NEG=0
if [ "${1:-}" = "--negative" ]; then
    NEG=1
    echo "negative control: booting without typing SAVE-IMAGE,"
    echo "expecting no image on the card and the checks to fail"
fi

cp "$CORE/boot/fat32.img" "$OUT/scratch.img"
python - "$WOUT/scratch.img" "$WOUT/forth.bin" "$(pwd)" <<'PY'
import sys, os
from pyfatfs.PyFatFS import PyFatFS
img, binpath, repo = sys.argv[1], sys.argv[2], sys.argv[3]
fs = PyFatFS(img)
with open(binpath, "rb") as f:
    data = f.read()
with fs.open("/FORTH.BIN", "wb") as g:
    g.write(data)
# The same boot chain run-emu.sh cards: COLD compiles `base`, and base.fs
# includes seven more. Miss one and the compile stops part way, which looks
# like a broken Forth and is not one.
for name in ("base", "asm", "wordlist", "labels", "doloop", "debug",
             "require", "accept"):
    with open(os.path.join(repo, "forth", name + ".fs"), "rb") as f:
        src = f.read()
    with fs.open("/" + name.upper(), "wb") as g:
        g.write(src)
fs.close()
PY
[ $? -eq 0 ] || exit 1

# 500 spaces: the compile takes 20.25 emulated seconds and -autokeys types
# through it into a 16-entry FIFO, so everything sent meanwhile is dropped.
# See the same padding, and the measurement behind it, in run-emu.sh.
PAD=$(printf '%.0s ' $(seq 1 500))

if [ "$NEG" = "1" ]; then
    KEYS="run FORTH.BIN\n${PAD}1 2 + .\n"
else
    KEYS="run FORTH.BIN\n${PAD}s\" /FORTHC.BIN\" save-image .\n"
fi

SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy timeout 180 \
    "$EMU/build/x16emu.exe" -boot "$(cygpath -m "$CORE/boot/boot.rom")" \
    -load "F00000,$(cygpath -m "$(pwd)/$KERNEL")" \
    -sdcard "$WOUT/scratch.img" \
    -autokeys "$KEYS" \
    -warp -gif "$WOUT/out.gif" >/dev/null 2>&1

python - "$WOUT/scratch.img" "$WOUT/forth.bin" "$NEG" <<'PY'
import sys
from pyfatfs.PyFatFS import PyFatFS

img, binpath, neg = sys.argv[1], sys.argv[2], sys.argv[3] == "1"
assembled = open(binpath, "rb").read()

fs = PyFatFS(img)
try:
    saved = fs.open("/FORTHC.BIN", "rb").read()
except Exception:
    saved = None
fs.close()


def fail(msg):
    print("FAIL:", msg)
    sys.exit(1)


if neg:
    if saved is None:
        print("PASS (negative control): nothing typed SAVE-IMAGE, so no image "
              "was written and the checks correctly cannot pass")
        sys.exit(0)
    fail("/FORTHC.BIN exists although SAVE-IMAGE was never typed (%d bytes) "
         "-- this test is not measuring the save" % len(saved))

if saved is None:
    fail("SAVE-IMAGE wrote no /FORTHC.BIN at all")

# X816_EXEC_MAX: code from $01:0000 up, headers from the top of the image
# down, and the hole between them -- and the most `run` will load.
if len(saved) != 0xFF00:
    fail("/FORTHC.BIN is %d bytes, expected 65280 (X816_EXEC_MAX) -- the "
         "length came from HERE alone and stopped below the headers?" % len(saved))

if saved[:4] != b"X816":
    fail("/FORTHC.BIN does not start with the X816 magic (%r), so EXEC would "
         "refuse to run it" % saved[:4])

# THE ONE WITH TEETH. These three words are compiled by base.fs at runtime and
# are NOT in the assembled image -- asserted here rather than assumed, so this
# check cannot quietly stop testing anything if a word moves into the .asm.
compiled_only = [b"cpu-mhz", b"turbo?", b"far-unused"]
for w in compiled_only:
    if w in assembled:
        fail("%r is in the ASSEMBLED image, so its presence in the saved one "
             "proves nothing -- pick another compiled-only word" % w)
missing = [w.decode() for w in compiled_only if w not in saved]
if missing:
    fail("the saved image is missing %s -- these exist only after base.fs is "
         "compiled, so what was written is not the compiled dictionary"
         % ", ".join(missing))

print("PASS: SAVE-IMAGE wrote a 65280-byte image with the X816 magic and the")
print("      COMPILED dictionary in it (cpu-mhz, turbo?, far-unused -- none")
print("      of which is in the assembled forth.bin)")
PY
