# X816_DurexForth

durexForth ported to the X816: a flat-16 MB 65816 core (8 MHz, MiSTer
DE10-Nano) with its own kernel. This repo is the Forth; the machine
lives in sibling repos:

- `../X816_core` — RTL, kernel, docs (`doc/KERNEL.md`, `doc/MEMORY_MAP.md`,
  `doc/DUREXFORTH.md` is the port spec), release tooling
- `../X816_Emulator` — the emulator (`build/x16emu.exe`, trace build in
  `build-trace/`)
- `../X816_Calypsi` — kernel binary + `runtime/calypsi.sh` env used by
  the test scripts

The user runs Quartus builds and MiSTer hardware tests themselves —
prepare changes, never launch FPGA builds.

## State (2026-08-04): DONE and hardware-green

All three port stages are complete, ANS-suite green in the emulator AND
user-confirmed on the MiSTer:

- **Stage A**: kernel console/FS bindings, suite running.
- **Stage B**: 32-bit cells (two 16-bit dp planes: LSB=$32, MSB=$7E),
  64-bit doubles, cells carry flat 24-bit addresses.
- **Stage C**: "one long world" — jsl/rtl threading across banks
  $01-$04 = **256 KB program space** (~245 KB free at boot), headers
  stay in bank $01 growing down from $FEFF, 24-bit HERE, bank_headroom
  bumps definitions to the next bank (never straddling). Full record
  with every trap: `doc/STAGEC.md`.
- **Data space**: SDRAM `$05:0000-$DF:FFFF` (constants `sdram`,
  `sdram-size` = 14,352,384; top 2 MB = VERA2 window + kernel firmware,
  reserved). Boot banner prints both spaces.
- **Post-suite fix (f2d14ef)**: the machine survives `include test` —
  test.fs's stage-A halt loop removed, and the TIB_TOP fix (io.asm)
  stops nested INCLUDED-under-EVALUATE from clobbering ancestor input
  lines. Awaiting one hardware re-confirmation of `include test` →
  live `ok` prompt.

## Build and test

- `./build.sh` — assembles `build/forth.bin` (ACME, `asm/durexforth.asm`
  is the root; labels via `acme -I asm --symbollist`).
- `./run-tests.sh` — full suite in the emulator + negative control.
  Card names are bare 8.3 (`testcoreplus` → `COREPLUS`).
- Verdict channel is the guest exit code via `$9FBC` (`emu-exit`);
  screen GIF decodes are diagnostics only. On hardware `$9FBC` is open
  bus — hardware-path code HIDES behind `emu-exit`; to test it, patch
  the card's TEST so `emu-exit` is a comment, then probe after.
- Emulator `-autokeys` drops characters (emulator-only nit; real
  keyboard is fine). Drive scenarios from an AUTORUN file instead.
- Release card: `bash ../X816_core/tools/mkrelease.sh` regenerates
  `X816_core/releases/mister/games/X816/boot0.img` (gitignored there —
  the user copies it to the MiSTer). Hash-verify card files against
  sources with pyfatfs after every refresh.

## Conventions that will bite you (short list — doc/STAGEC.md has all)

- Words run M=0/X=1 at every boundary; every call is `jsl BANK1+label`
  (ACME assembles a bare label with bank $00), every exit `rtl` — a
  stray `rts` ($60 vs $6b) leaks the bank byte and detonates one rtl
  later at a timing-dependent distance.
- NO `jml [dp]` exists: $DC is `jml [abs16]` (`[jmp],` in asm.fs).
- Return addresses are 3 bytes: PULL_RET macro, `rl>`/`l>r` for the
  `r> ... >r` idiom, R@ reads `4,s`/`6,s`.
- Headers: len byte + name + 3-byte xt, stride len+4. VALUEs are the
  uniform two-16-bit-immediate shape; TO patches xt+3 (lo) and xt+8
  (hi). `>body` = xt+7. Early definers use `split nip`, not rshift.
- Console text is VERA **layer 0** (X16 used layer 1); VERA register
  readbacks need `ioc@` (bank-0 fetch).

## Next steps (the backlog, in rough order)

1. **Hardware re-confirmation**: `include test` on the MiSTer must end
   at a usable prompt (the f2d14ef fix — emulator-proven, not yet
   board-proven).
2. **Far-data words for SDRAM**: `sdram`/`sdram-size` exist but there
   is no allocator — add far `allot`/`buffer:`-style words so programs
   can claim SDRAM data space instead of hardcoding addresses.
3. **Platform hooks**: IRQ_SET brk handler (brk_handler in
   interpreter.asm is written but not installed), charset.
4. **Modules**: float (software math — no ROM FP on X816), ANS file
   words beyond INCLUDED, replacements for the parked C64 words
   (`ls`, `open`, `help`, `turnkey` — see base.fs comments).
5. **helpdoc tracker**: `help/helpdoc/*.txt` checkboxes were reset and
   marked through stage A (307/539); stage B/C words (`w,`, `rl>`,
   `sdram`, …) need [x] passes and new lines.
6. **Emulator autokeys char-drop** (fix lives in `../X816_Emulator`).
