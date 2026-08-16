# durexforth (Commander X16 port) Version X16_V1

A Commander X16 port of durexForth, a fast, [Forth 2012](http://forth-standard.org/standard/words) core-compatible Forth for 6502-family machines.

This is the **first-version, plain-Forth core**: everything C64-specific has been
removed, leaving the interpreter/compiler, the 6502/65C02 assembler (`asm`), the
decompiler (`see`), disk/file words, and standard utility libraries. Hardware
features (VIC-II graphics, SID/MML sound, sprites, the vi editor) are gone and
will be re-added on top using X16 facilities (VERA, etc.). Upstream durexForth
(C64) lives at <https://github.com/jkotlinski/durexforth>.

## Prebuilt binaries

Don't want to build? Ready-made binaries are in **`release/`**:

| File | Run with | Notes |
|------|----------|-------|
| `durexforth_full.crt` | `x16emu -cart durexforth_full.crt` | everything resident; **no SD card needed** |
| `durexforth.crt` | `x16emu -cart durexforth.crt -sdcard sdcard.img` | core cart; modules via `NEEDS`, libraries from the card |
| `durexforth.bin`, `durexforth_full.bin` | MiSTer X16 core | the same cartridges as **raw ROM bank images** (no .crt header - the MiSTer core maps them straight at bank 32; x16emu wants the headered `.crt`) |
| `durexforth.prg` | `x16emu -prg durexforth.prg -run -sdcard sdcard.img` | RAM program; compiles the core from the card at boot |
| `sdcard.img` | (mount as the SD card) | FAT32 card with the Forth libraries; `INCLUDE` loads from here |

On Windows, double-click one of the launchers:

| Launcher | Boots |
|----------|-------|
| `run-cart.bat` | the **core cartridge** + SD card |
| `run_cartfull.bat` | the **full cartridge** + SD card |
| `run-prg.bat` | the **RAM program** + SD card |

## The three flavours in detail

All three run the identical Forth kernel — they differ in **where the code
comes from at boot** and **what is already in the dictionary**.

| | `durexforth_full.crt` | `durexforth.crt` (core) | `durexforth.prg` |
|---|---|---|---|
| Boot | instant (ROM → RAM copy) | instant (ROM → RAM copy) | recompiles `base` from the card (a few seconds, warp helps) |
| Resident at the prompt | core **+ compat, io, dos, rnd, timer, audio, loadsave, vramdisk** | core only | core only |
| On-demand modules | `NEEDS <mod>` from cart ROM | `NEEDS <mod>` from cart ROM | `INCLUDE <mod>` from the card (**no `NEEDS`** — there is no cart ROM) |
| SD card | optional (HELP pages, your files, AUTORUN) | recommended (same + extra libraries) | **required** |
| Free dictionary after boot | ≈ 18.6 KB | ≈ 22.5 KB | ≈ 22.5 KB |
| Use it for | turn-key machines, quickest start | most head-room + instant boot | hacking on durexForth itself (freshly compiled core every boot) |

The on-demand modules (`GRAPHIC FLOAT FLOATX FILE STRING SYSTEM EXTRAS
ADVANCED ADVGFX BMX ADVSND`, ≤ 8 KB source each) exist in **both** forms:
packed into cart ROM banks 40+ for `NEEDS`, and as plain `.FS` files on the
card for `INCLUDE`. Loading one only costs dictionary space for the compiled
words. `HELP <topic>` documents each; `tutorial/userguide.md` is the full
manual.

## Where things live (X16 memory map)

### CPU RAM (low 64 KB)

| Range | What durexForth keeps there |
|---|---|
| `$0000-$0001` | RAM-bank ($00) / ROM-bank ($01) select registers |
| zero page | the **data stack**: two split byte arrays (low/high), X register = stack pointer, ~56 cells for the program; the deep end doubles as the IRQ dispatcher's private 16-cell stack; `W W2 W3` scratch at `$9C-$A1` |
| `$0100-$01FF` | 6502 hardware (return-address) stack |
| `$0200-$03FF` | KERNAL variables and vectors (the IRQ dispatcher hooks CINV `$0314`) |
| `$0400-$07FF` | "golden RAM": cart-boot loader scratch (`$0400`), dictionary-lookup buffer (`$0480`), `PAD` (`$0500`), pictured-number hold area (`$05C0`), and the input buffer TIB (`$0600-$07FF`) |
| `$0801` ↑ | the durexForth kernel + everything you compile: **code grows up** from `HERE` |
| ↓ `$9EFF` | word **headers grow down** from `TOP` toward the code; `UNUSED`/`FREE` is the gap between them. `SAVE-PACK` slides the headers down against the code to ship a compact image (the boot restores them) |
| `$9F00-$9FFF` | I/O: VERA (`$9F20-$9F3F`) and the other devices |
| `$A000-$BFFF` | the **banked-RAM window** (see below) |
| `$C000-$FFFF` | the current **ROM bank** (KERNAL = bank 0 while Forth runs) |

### Banked RAM (512 KB, 8 KB pages at `$A000-$BFFF`)

Bank 0 is KERNAL-reserved. Banks 1..`DATABANK` are yours: `BANKLOAD` streams
files into them, `BANK>MEM`/`MEM>BANK`/`B@`/`B!` move data, `PCM-PLAY`
streams audio out of them. `NEEDS` briefly stages a module's source text in
the highest bank (`DATABANK`) while compiling it. The dictionary itself never
lives in banked RAM.

### ROM banks (at `$C000-$FFFF`)

| Bank | Contents |
|---|---|
| 0 | KERNAL (`SYSCALL` targets) |
| 4 | ROM math library (drives the FLOAT module via `BCALL`) |
| 10 | audio driver (`FMNOTE`, `FMINST`, ... via `BCALL`) |
| 32-33 | **cartridge**: 256-byte boot stub (`CX16` signature) + the packed durexForth image, copied to RAM `$0801` at power-on |
| 34-39 | cartridge: spare (zero-filled) |
| 40-43 | cartridge: the **module store** that `NEEDS` reads |

### VERA video RAM (separate 128 KB)

The bitmap (`GINIT`) sits at VRAM `$00000` (320×240 = 75 KB); the text
screen/tilemap follows the KERNAL defaults; the palette is at `$1FA00`
(`PAL!`), the PSG registers at `$1F9C0` (`PSGVOL`...), and the sprite
attributes at `$1FC00` (`SPRITE-...`). `VPOKE/VPEEK/VADDR/V!/V@` reach all of
it; `VLOAD/VSAVE/BMX-LOAD` move it to and from disk.

## Building

Requirements (relative to the repo root): `acme/acme.exe`, `emulator/x16emu.exe`
(+ `rom.bin`, `makecart.exe`), the committed FAT32 `release/sdcard.img`, and Python 3.

**Windows — PowerShell (no Git Bash needed):**

```powershell
# build all distributables into release\ (both carts, the prg, and the sdcard image)
powershell -ExecutionPolicy Bypass -File .\make-release.ps1
# add -Python "C:\path\to\python.exe" if python isn't on PATH

.\run-cart.bat            # launch the freshly built cart + SD card
```

**Git Bash / Linux:**

```bash
./build.sh            # assemble with ACME and populate release/sdcard.img
./build.sh run        # ...and launch the emulator
./build-cart.sh       # build a bootable core cartridge (release .crt)
./build-cart.sh full  # ...with audio/graphics/vramdisk/see baked in
./make-release.sh     # build every distributable into release/
```

On boot the kernel (`build/durexforth.prg`, load address `$0801`) loads `base`
from the SD card, compiles the core, and saves the turnkey image `durexfth`
back to the card. durexForth runs in the X16 ISO charset (standard ASCII);
source files are kept as plain ASCII.

## Testing

```powershell
powershell -ExecutionPolicy Bypass -File .\run-tests.ps1   # Windows
./run-tests.sh                                             # Git Bash / Linux
```

Both assemble, boot the kernel with an AUTORUN of `include test/test`,
runs the Forth 2012 core / core-ext / core-plus / exception conformance suites
plus `test/testx16.fs` (port-specific: number parsing, the relocated zero-page
stack, the golden-RAM buffers, and the inline assembler), and checks the
emulator's echoed output for the pass banner. `test/testsee.fs` is omitted (it
scrapes VIC-II screen RAM, which the X16 lacks).

The cartridges have their own boot-path test — it types into the emulated
keyboard (`x16emu -bas`), `NEEDS`-loads every module from cart ROM with a
probe each, checks the dictionary restored correctly, and verifies the full
cart's baked-in libraries:

```powershell
powershell -ExecutionPolicy Bypass -File .\test-carts.ps1   # Windows
./test-carts.sh                                             # Git Bash / Linux
```

`make-release.sh` / `make-release.ps1` run it automatically and abort the
release if either cart fails.


see the original site of durexforth here: 

https://github.com/jkotlinski/durexforth

I am not the creator of durexforth, see the contributor section into the durexforth site

All the coding for the X16 version is done by Claude Opus 4.8

Thanks
