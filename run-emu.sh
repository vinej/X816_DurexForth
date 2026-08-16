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
# THE CARD MUST CARRY base.fs. This script was written at stage A, when the
# Forth was a bare assembled REPL and FORTH.BIN alone was a complete machine.
# It is not any more: COLD calls load_base, which INCLUDEs `base` off the card,
# so a card holding only FORTH.BIN makes the boot throw -37 and the uncaught-
# error path calls emu-exit 1 -- the emulator stops with status 1 and the
# banner check fails with nothing to say why. That is not a Forth fault and it
# was not one: it went unnoticed because run-tests.sh writes the sources and is
# the suite anybody actually reads. Anything COLD needs goes on the card here.
#
# Requires: pip install pillow pyfatfs, and a built X816_Calypsi
# programs/shell/kernel.bin (sh build.sh there).
set -u

# EMU, CORE and RT come from the same place every X816_Calypsi script gets
# them, so a moved checkout moves once.
. "$(dirname "$0")/../X816_Calypsi/runtime/calypsi.sh"
cd "$(dirname "$0")"
# KEEP_OUT=<dir> keeps the card image and the GIF for inspection. The boot
# compile's LENGTH is the thing worth measuring here -- how many frames pass
# before the report appears is how long durexForth takes to start -- and that
# cannot be measured from a temp directory that is deleted on the way out.
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
    # Break the image magic: EXEC must refuse the file, so no banner.
    printf 'Y' | dd of="$OUT/forth.bin" bs=1 count=1 conv=notrunc 2>/dev/null
    echo "negative control: corrupted image magic, expecting NO banner"
fi

# A scratch card with FORTH.BIN on it, written by pyfatfs -- an independent
# FAT32 implementation, as everywhere else in the tree.
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
print("card: FORTH.BIN = %d bytes" % len(data))

# THE WHOLE BOOT CHAIN, not just base.fs. COLD compiles `base` off the card
# (asm/durexforth.asm load_base) and base.fs then includes seven more: `asm` at
# its line 114, then wordlist, labels, doloop, debug, require and accept. Miss
# any one and the compile stops mid-banner -- the first attempt at this fix
# carded `base` alone and got as far as "compile base..asm.." before dying,
# which looks exactly like a broken Forth and is not one.
#
# `compat` is deliberately NOT here: it is the test suite's compatibility
# layer, included by test.fs, not by COLD. No AUTORUN and no test sources
# either -- the three checks below are about booting and typing.
for name in ("base", "asm", "wordlist", "labels", "doloop", "debug",
             "require", "accept"):
    path = os.path.join(repo, "forth", name + ".fs")
    with open(path, "rb") as f:
        src = f.read()
    with fs.open("/" + name.upper(), "wb") as g:
        g.write(src)
    print("card: %-9s = %d bytes" % (name.upper(), len(src)))
fs.close()
PY
[ $? -eq 0 ] || exit 1

# PADDING, AND WHY IT IS THIS LONG. -autokeys types on a 25 ms emulated clock
# whatever the guest is doing, and the SMC key FIFO is sixteen entries -- so
# anything sent while durexForth is busy is dropped. It is busy for a LONG
# time: measured off this test's own GIF, the banner appears at 3.25 s and the
# boot report at 23.50 s, so COMPILING BASE AND ITS SEVEN INCLUDES TAKES 20.25
# EMULATED SECONDS. The old padding was 40 spaces, about 2 s, so `1 2 + .` was
# typed at ~2.7 s into a machine that would not read a key until 23.5 s. That
# check could never have passed; nothing noticed because the banner check
# ahead of it failed first and stopped the script.
#
# Spaces rather than newlines: an empty line at the REPL scrolls the screen and
# would push the banner out of the rows this test decodes. Leading spaces are
# ignored by the interpreter, and only the four or so still in the FIFO when
# the compile ends get echoed, so the line buffer never fills.
#
# 500 covers 25 s from the end of "run FORTH.BIN". If the pre-compiled image
# lands, this padding is exactly what stops being necessary.
PAD=$(printf '%.0s ' $(seq 1 500))

# A literal backspace, which -autokeys maps to the Backspace KEY (position 15
# in autokey_ascii). Typing "99 xx" and then two of these must leave "99 " on
# screen: the X816 console's $08 only steps the cursor left, so ACCEPT has to
# emit backspace-space-backspace or the deleted characters stay visible while
# the line buffer shortens underneath them. Retyping over them would hide the
# bug -- the new glyphs would cover the old - so nothing is retyped here.
BS=$'\b'

SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy timeout 90 \
    "$EMU/build/x16emu.exe" -boot "$(cygpath -m "$CORE/boot/boot.rom")" \
    -load "F00000,$(cygpath -m "$(pwd)/$KERNEL")" \
    -sdcard "$WOUT/scratch.img" \
    -autokeys "run FORTH.BIN\n${PAD}1 2 + .\n99 xx${BS}${BS}\nxyzzy\n" \
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
# The prompt is CP437 $AF, the chevron from the boot mark, NOT '>'. The
# table above stops at $7E, so decode $AF as '>' and every "the prompt is
# back" assertion below keeps reading as what it means.
glyph[tuple(vals[0xAF * 8:0xB0 * 8])] = ">"

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

# The banner is matched as a WHOLE LINE, not by looking for "durexForth"
# anywhere. The loose form passed on "durexForth 1.08 MHz cpu." -- a missing
# ", " separator between the version and the speed, which is exactly the kind
# of thing a substring check is blind to. The speed alternatives are the two
# the machine can report (8 MHz paced, 14 MHz TURBO), so a banner that printed
# a stale or garbage number fails here too.
banner_re = re.compile(r"^durexForth \S+, (8|14) MHz cpu\.$")
banner = any(banner_re.match(r.strip()) for r in rows)

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

# BACKSPACE MUST ERASE. "99 xx" then two backspaces: if the console editor
# echoes a bare $08 the cursor moves and the glyphs stay, so an 'x' is still
# on the line. Checked on the ECHOED LINE rather than on what the interpreter
# did, because the TIB was always right -- it was the screen that lied.
#
# FIND THE LINE FIRST. "no row contains 99 x" is also true when the line was
# never typed at all -- keys dropped, machine still busy, card wrong -- and
# that green would prove nothing. So require the line to exist, and then
# require it to be exactly "99".
bs_rows = [r for r in rows if r.strip().upper().startswith("99")]
if not bs_rows:
    fail("the '99 xx<bs><bs>' line never reached the screen, so the "
         "backspace check has nothing to prove -- keys dropped?")
if bs_rows[0].strip().upper() != "99":
    fail("backspace did not erase: the echoed line is %r, expected '99' -- "
         "the console editor emitted a bare $08 and only moved the cursor"
         % bs_rows[0].strip())

# FREE is deliberately NOT checked here. It lives in forth/mod/system.fs,
# which the boot does not include and this test's card does not carry, so
# typing it answers `free?` -- and a check that passed anyway would only be
# matching the boot report's own two lines, which say the same words. The
# figures themselves ARE covered: FREE and the boot report both read UNUSED
# and FAR-UNUSED, and the report is on screen above.

print("PASS: durexForth booted from the card, did arithmetic, erased a")
print("      backspaced character, and refused an undefined word -- all")
print("      through the kernel console")
for r in rows:
    if r:
        print("   ", r)
PY
