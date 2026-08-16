#!/usr/bin/env bash
# TURNKEY: save the compiled machine, then BOOT FROM IT and prove the compile
# did not happen.
#
#   ./run-turnkey.sh              build and run
#   ./run-turnkey.sh --negative   boot the ordinary FORTH.BIN in run 2 instead
#                                 of the saved image, and require the checks
#                                 to fail
#
# TWO RUNS, ONE CARD, and the second machine is FRESH -- it boots from scratch
# and finds the image, so what is being tested is a file on a card and not
# something still in memory from the save.
#
# THE THREE CHECKS, and why each is here:
#
#   * THE COMPILE CHATTER MUST BE ABSENT. base.fs prints "compile base..asm.."
#     as it goes, so its presence is proof the includes ran. This is the check
#     the whole feature exists for.
#
#   * THE PAD IS SHORT ON PURPOSE -- 40 spaces, about two emulated seconds.
#     Run 1 needs 500 of them because compiling takes 20.25 s; if run 2 answers
#     with 40, the machine was ready inside a tenth of that. This is the timing
#     claim, made as a test rather than as a stopwatch reading.
#
#   * A WORD THAT ONLY BASE.FS DEFINES must still work. `cpu-mhz` is not in the
#     assembled image (asserted, not assumed, in run-saveimage.sh), so if it
#     answers, the saved dictionary came back -- not just a bootable stub.
#
# Requires Pillow and pyfatfs.
set -u

. "$(dirname "$0")/../X816_Calypsi/runtime/calypsi.sh"
cd "$(dirname "$0")"

if [ -n "${KEEP_OUT:-}" ]; then
    OUT="$KEEP_OUT"; mkdir -p "$OUT"
else
    OUT=$(mktemp -d); trap 'rm -rf "$OUT"' EXIT
fi
WOUT=$(cygpath -m "$OUT" 2>/dev/null || echo "$OUT")

KERNEL="../X816_Calypsi/programs/shell/kernel.bin"
[ -f "$KERNEL" ] || { echo "kernel.bin missing"; exit 1; }

./build.sh || exit 1
cp build/forth.bin "$OUT/forth.bin"

NEG=0
if [ "${1:-}" = "--negative" ]; then
    NEG=1
    echo "negative control: run 2 boots the ordinary FORTH.BIN, which DOES"
    echo "compile -- the checks must fail"
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
for name in ("base", "asm", "wordlist", "labels", "doloop", "debug",
             "require", "accept"):
    with open(os.path.join(repo, "forth", name + ".fs"), "rb") as f:
        src = f.read()
    with fs.open("/" + name.upper(), "wb") as g:
        g.write(src)
fs.close()
PY
[ $? -eq 0 ] || exit 1

emu() {  # emu <gif> <keys> <timeout>
    SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy timeout "$3" \
        "$EMU/build/x16emu.exe" -boot "$(cygpath -m "$CORE/boot/boot.rom")" \
        -load "F00000,$(cygpath -m "$(pwd)/$KERNEL")" \
        -sdcard "$WOUT/scratch.img" \
        -autokeys "$2" \
        -warp -gif "$1" >/dev/null 2>&1
}

LONGPAD=$(printf '%.0s ' $(seq 1 500))
SHORTPAD=$(printf '%.0s ' $(seq 1 40))

# Run 1: the ordinary boot, compiling, then save the machine.
emu "$WOUT/save.gif" "run FORTH.BIN\n${LONGPAD}s\" /FORTHC.BIN\" save-image .\n" 180

# Run 2: a fresh machine boots the SAVED image, with a short pad.
BOOTFILE=FORTHC.BIN
[ "$NEG" = "1" ] && BOOTFILE=FORTH.BIN
emu "$WOUT/boot.gif" "run ${BOOTFILE}\n${SHORTPAD}1 2 + .\ncpu-mhz .\n" 120

python - "$WOUT/boot.gif" "$RT/font_cp437.s" "$NEG" <<'PY'
import sys, re, io
from PIL import Image, ImageFile
ImageFile.LOAD_TRUNCATED_IMAGES = True

gif, fontinc, neg = sys.argv[1], sys.argv[2], sys.argv[3] == "1"

vals = []
for line in io.open(fontinc, encoding="utf-8"):
    m = re.match(r"\s*[.]byte\s+(.*)", line.split(";")[0])
    if m:
        vals += [int(x.strip().lstrip("$"), 16)
                 for x in m.group(1).split(",") if x.strip()]
glyph = {}
for c in range(0x20, 0x7F):
    glyph[tuple(vals[c * 8:(c + 1) * 8])] = chr(c)

im = Image.open(gif)
n = 0
while True:
    try:
        im.seek(n); im.load(); n += 1
    except Exception:
        break
if n == 0:
    print("FAIL: no decodable frame -- did the emulator run?")
    sys.exit(1)
im.seek(n - 1); im.load()
px = im.convert("RGB").load()


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
        out += glyph.get(tuple(bits), "?")
    return out.rstrip()


rows = [row_text(r) for r in range(34)]
body = "\n".join(r for r in rows if r)

banner_re = re.compile(r"^durexForth \S+, (8|14) MHz cpu\.$")
banner    = any(banner_re.match(r.strip()) for r in rows)
compiled  = any("compile base" in r for r in rows)
arith     = any(r.strip().startswith("3 ok") for r in rows)
mhz       = any(re.match(r"^(8|14) ok$", r.strip()) for r in rows)


def dump():
    for i, r in enumerate(rows):
        if r:
            print("  %2d: %r" % (i, r))


if neg:
    problems = []
    if not compiled:
        problems.append("the ordinary FORTH.BIN did not print the compile "
                        "chatter, so 'chatter absent' proves nothing")
    if problems:
        print("FAIL (negative control):")
        for p in problems:
            print("   -", p)
        dump()
        sys.exit(1)
    print("PASS (negative control): the ordinary image DOES compile, so the")
    print("      absence of that chatter in the real run is meaningful")
    sys.exit(0)

problems = []
if not banner:
    problems.append("no banner -- the saved image did not run")
if compiled:
    problems.append("'compile base..' is on screen: the saved image RECOMPILED "
                    "rather than starting from its dictionary")
if not arith:
    problems.append("`1 2 + .` did not answer within a 40-space pad, so the "
                    "machine was not ready in ~2 emulated seconds")
if not mhz:
    problems.append("`cpu-mhz .` did not answer 8 or 14 -- a word that only "
                    "base.fs defines is missing, so the saved dictionary did "
                    "not come back")

if problems:
    print("FAIL:")
    for p in problems:
        print("   -", p)
    dump()
    sys.exit(1)

print("PASS: a fresh machine booted the SAVED image -- banner, no compile,")
print("      arithmetic inside a 40-space pad, and a base.fs-only word alive")
dump()
PY
