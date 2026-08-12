#!/usr/bin/env bash
# Launch the resident editor from durexForth and prove control returns.
set -eu

. "$(dirname "$0")/../X816_Calypsi/runtime/calypsi.sh"
cd "$(dirname "$0")"

hostpath () {
    case "$1" in
        /c/*)
            if [ -d /c ]; then
                printf '%s\n' "$1"
            else
                printf '/mnt/c/%s\n' "${1#/c/}"
            fi
            ;;
        *) printf '%s\n' "$1" ;;
    esac
}

winpath () {
    cygpath -m "$1" 2>/dev/null || wslpath -m "$(hostpath "$1")" 2>/dev/null || echo "$1"
}

pypath () {
    if [ "$PYTHON_WIN" -eq 1 ]; then
        winpath "$1"
    else
        hostpath "$1"
    fi
}

CORE_HOST=$(hostpath "$CORE")
EMU_HOST=$(hostpath "$EMU")

PYTHON_USER=${USER:-$(id -un 2>/dev/null || echo jyv)}
PYTHON_LOCAL_MSYS="/c/Users/$PYTHON_USER/AppData/Local/Programs/Python/Python312/python.exe"
PYTHON_LOCAL_WSL=$(hostpath "$PYTHON_LOCAL_MSYS")
PYTHON_CMD=
PYTHON_WIN=0
for candidate in ${PYTHON:-} python python3 python.exe "$PYTHON_LOCAL_MSYS" "$PYTHON_LOCAL_WSL" py.exe; do
    [ -n "$candidate" ] || continue
    command -v "$candidate" >/dev/null 2>&1 || continue
    if "$candidate" -c "from pyfatfs.PyFatFS import PyFatFS" >/dev/null 2>&1; then
        PYTHON_CMD=$candidate
        case "$candidate" in
            *.exe) PYTHON_WIN=1 ;;
        esac
        break
    fi
done
[ -n "$PYTHON_CMD" ] || { echo "python with pyfatfs missing"; exit 1; }

KERNEL="../X816_Calypsi/programs/shell/kernel.bin"
[ -f "$KERNEL" ] || { echo "kernel.bin missing -- run sh build.sh in X816_Calypsi/programs/shell"; exit 1; }

./build.sh >/dev/null 2>&1 || exit 1

mkdir -p build
OUT=$(mktemp -d -p "$(pwd)/build" edit-smoke.XXXXXX)
trap 'rm -rf "$OUT"' EXIT
WOUT=$(winpath "$OUT")

cp "$CORE_HOST/boot/fat32.img" "$OUT/scratch.img"
"$PYTHON_CMD" - "$(pypath "$OUT/scratch.img")" "$(pypath "$(pwd)/build/forth.bin")" "$(pypath "$(pwd)")" <<'PY'
import os, sys
from pyfatfs.PyFatFS import PyFatFS
img, binpath, repo = sys.argv[1:]
fs = PyFatFS(img)
try:
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

    with fs.open("/AUTORUN", "wb") as g:
        g.write(b'S" FORTH.FS" 3 $7fe c! edit $7fd c@ 66 = $7fb c@ 8 = and 0= emu-exit\n')
finally:
    fs.close()
PY
[ $? -eq 0 ] || exit 1

POWERSHELL=${POWERSHELL:-powershell.exe}
"$POWERSHELL" -NoProfile -ExecutionPolicy Bypass -File "$(winpath "$(pwd)/run-edit-capture.ps1")" \
    -Emu "$(winpath "$EMU_HOST/build/x16emu.exe")" \
    -Boot "$(winpath "$CORE_HOST/boot/boot.rom")" \
    -Kernel "$(winpath "$(pwd)/$KERNEL")" \
    -Sdcard "$WOUT/scratch.img" \
    -Keys 'run FORTH.BIN'

echo "PASS: durexForth launched the resident editor with a filename and resumed after exit"
