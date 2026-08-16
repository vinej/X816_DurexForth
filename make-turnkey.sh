#!/usr/bin/env bash
# Produce build/forth-turnkey.bin: durexForth with base.fs and its seven
# includes ALREADY COMPILED, so the machine starts at the prompt instead of
# spending 20.25 emulated seconds recompiling them at every boot.
#
#   ./make-turnkey.sh
#
# HOW: there is no way to compile Forth except by running Forth, so this boots
# the real emulator on a scratch card, lets it compile, and has it write its
# own memory back out with SAVE-IMAGE. The result is the machine's whole state
# -- HERE, LATEST and every VALUE are immediates inside the image itself, so
# there is nothing to serialise and nothing to keep in step.
#
# THE STALE-IMAGE HOLE IS CLOSED AT THE TOP: the output is DELETED before
# anything else happens. A failed generation therefore leaves NO file, and
# mksdcard.py falls back to the plain assembled forth.bin and says so, rather
# than shipping yesterday's dictionary compiled from sources that have since
# changed. That failure mode -- a stale artefact silently preferred -- is the
# one this tree has paid for repeatedly.
#
# The checks below are the same ones run-saveimage.sh makes, because a release
# is exactly where an unverified image must not go.
set -u

. "$(dirname "$0")/../X816_Calypsi/runtime/calypsi.sh"
cd "$(dirname "$0")"

OUTBIN="build/forth-turnkey.bin"
rm -f "$OUTBIN"

KERNEL="../X816_Calypsi/programs/shell/kernel.bin"
if [ ! -f "$KERNEL" ]; then
    echo "turnkey: SKIPPED -- $KERNEL missing (build the shell first)" >&2
    exit 1
fi

./build.sh >/dev/null || { echo "turnkey: forth.bin failed to assemble" >&2; exit 1; }

OUT=$(mktemp -d)
trap 'rm -rf "$OUT"' EXIT
WOUT=$(cygpath -m "$OUT" 2>/dev/null || echo "$OUT")

cp build/forth.bin "$OUT/forth.bin"
cp "$CORE/boot/fat32.img" "$OUT/scratch.img"

python - "$WOUT/scratch.img" "$WOUT/forth.bin" "$(pwd)" <<'PY' || exit 1
import sys, os
from pyfatfs.PyFatFS import PyFatFS
img, binpath, repo = sys.argv[1], sys.argv[2], sys.argv[3]
fs = PyFatFS(img)
with open(binpath, "rb") as f:
    fs.open("/FORTH.BIN", "wb").write(f.read())
# The whole boot chain: COLD compiles `base`, and base.fs includes the rest.
#
# THE MODULES ARE NOT ALL IN forth/. `system` lives in forth/mod/, so this
# looks in both places rather than assuming one. Getting that wrong does not
# fail here: the card is built happily without the file, COLD then aborts on
# the missing include, SAVE-IMAGE never runs, and the only symptom is this
# script's "produced no image" at the very end -- which reads like an emulator
# or timing problem and is neither.
for name in ("base", "asm", "wordlist", "labels", "doloop", "debug",
             "require", "accept", "system"):
    for sub in ("forth", os.path.join("forth", "mod")):
        path = os.path.join(repo, sub, name + ".fs")
        if os.path.exists(path):
            break
    else:
        sys.exit("make-turnkey: no source for module %r" % name)
    with open(path, "rb") as f:
        fs.open("/" + name.upper(), "wb").write(f.read())
fs.close()
PY

# 500 spaces of padding rides out the compile; see run-emu.sh for the
# measurement that sets the number.
PAD=$(printf '%.0s ' $(seq 1 500))

SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy timeout 240 \
    "$EMU/build/x16emu.exe" -boot "$(cygpath -m "$CORE/boot/boot.rom")" \
    -load "F00000,$(cygpath -m "$(pwd)/$KERNEL")" \
    -sdcard "$WOUT/scratch.img" \
    -autokeys "run FORTH.BIN\n${PAD}s\" /FORTHC.BIN\" save-image .\n" \
    -warp >/dev/null 2>&1

python - "$WOUT/scratch.img" "$(pwd)/build/forth.bin" "$(pwd)/$OUTBIN" <<'PY'
import sys
from pyfatfs.PyFatFS import PyFatFS

img, assembled_path, outpath = sys.argv[1], sys.argv[2], sys.argv[3]
assembled = open(assembled_path, "rb").read()

fs = PyFatFS(img)
try:
    saved = fs.open("/FORTHC.BIN", "rb").read()
except Exception:
    saved = None
fs.close()


def refuse(msg):
    print("turnkey: REFUSING -- " + msg, file=sys.stderr)
    sys.exit(1)


if saved is None:
    refuse("the emulator run produced no image (SAVE-IMAGE never ran?)")
if len(saved) != 0xFF00:
    refuse("the image is %d bytes, expected 65280 (X816_EXEC_MAX)" % len(saved))
if saved[:4] != b"X816":
    refuse("the image does not start with the X816 magic (%r)" % saved[:4])

# The check with teeth: words that exist only AFTER base.fs is compiled. An
# image that is really just a copy of the assembled binary passes size and
# magic and fails this.
for w in (b"cpu-mhz", b"turbo?", b"far-unused"):
    if w in assembled:
        refuse("%r is in the assembled binary too, so it cannot witness the "
               "compile -- pick another word" % w)
    if w not in saved:
        refuse("the image is missing %r, so it is not the compiled dictionary"
               % w)

with open(outpath, "wb") as f:
    f.write(saved)
print("turnkey: build/forth-turnkey.bin  (%d bytes, dictionary compiled)"
      % len(saved))
PY
