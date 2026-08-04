# X816_DurexForth

durexForth ported to the X816: a flat-16 MB 65816 core (8 MHz average,
14 MHz with SYSCTL[2] TURBO set, MiSTer DE10-Nano) with its own kernel. This repo is the Forth; the machine
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
  reserved), handed out by a bump allocator in base.fs: `far-here`
  (VALUE), `far-unused`, `far-allot` (throws -8 on exhaustion),
  `far-buffer:`, `far-empty`; `marker` saves/restores `far-here` too.
  No far FREE — restore `far-here` with TO, or unwind a marker. It
  overlaps the kernel's dormant `MEM_ALLOC` arena ($20:0000 up):
  nothing here binds that call, and whoever does must carve the two
  apart. Proven by `test/testfar.fs`. Boot banner prints both spaces.
- **BRK aborts to the interpreter** (2026-08-04): COLD installs
  `brk_handler` in the kernel's `KIRQ_BRK` slot (`install_brk` +
  `kern_irq_set`, entry 49 at `$00:FEC4`). The handler never returns —
  legitimate for BRK alone, because `kirq_brk` has nothing to
  acknowledge; **do not copy its shape to an IRQ slot**. It rebuilds
  `X=1` and `DBR=$01`, `cli`s (BRK sets `I` and there is no rti), and
  takes the data stack pointer from the CATCH frame at `5,s` — NOT
  `X_INIT`, which puts THROW's own scratch on top of the caller's live
  cells. Proven by `test/testfar.fs`'s neighbour, `test/testbrk.fs`.
- **The bug BRK exposed — `OF` compiled a 4-byte branch operand**
  (base.fs, and `AHEAD` in mod/extras.fs). THEN patches two bytes, so
  two `$00`s stayed in the instruction stream and every matching `OF`
  executed a BRK. The kernel's default for an unhandled brk is "resume
  at PC+2", which skipped exactly those two bytes — so CASE worked and
  the suite was green while the trap fired on every hit. A stage-B
  leftover: `,` compiled two bytes when cells were 16-bit. **Any
  `here 0 ,` reserving a BRANCH operand is this bug**; data cells are
  fine.
- **Post-suite fix (f2d14ef)**: the machine survives `include test` —
  test.fs's stage-A halt loop removed, and the TIB_TOP fix (io.asm)
  stops nested INCLUDED-under-EVALUATE from clobbering ancestor input
  lines. **Board-confirmed 2026-08-04**: the user ran the refreshed
  card on the MiSTer and reported all tests passed — that run included
  the new far-data suite (testfar), so the SDRAM allocator is
  hardware-proven too, not just emulator-proven.
- **TURBO / 14 MHz** (2026-08-04, emulator-green, awaiting board run):
  the core grew SYSCTL[2] TURBO ($9F80 bit 2: 0 = exact 8 MHz average,
  1 = 14 MHz; reads return the EFFECTIVE speed — the MiSTer OSD's CPU
  Turbo ORs over it). base.fs: `turbo?`, `turbo` ( flag -- ),
  `cpu-mhz`; the boot banner leads with the speed. `ioc!` (video.asm)
  is the store half of `ioc@`, added for the writable SYSCTL bits.
  **MS was rewritten** (coreadd.asm) onto the free-running ms timer
  $9F90 (1 kHz wall clock at either speed, $9F90 read FIRST — it
  latches bits 31:8): the old calibrated 8 MHz busy loop ran 1.75x
  fast under turbo. Proven by `test/testturbo.fs`; assertions hold
  under either OSD setting.
- **NMI aborts runaway loops** (2026-08-04): Ctrl+Alt+PrtScr (or SMC
  I2C command $03) raises an NMI — the old backlog note "nothing
  raises an NMI" was stale; `rtl/smc_x16.sv` does both. COLD installs
  `nmi_handler` (interpreter.asm) in `KIRQ_NMI` slot 8: it reads the
  interrupted PBR from the dispatcher's frame at `18,s` (a DELIBERATE
  coupling to kirq.s's KPROLOGUE+kirq_call shape, commented in place)
  and falls into `brk_handler` (throw -28) ONLY for program banks
  $01-$04; an NMI landing in the kernel (idle prompt, SD transfer,
  VSYNC IRQ) is resumed untouched — press again. `test/testnmi.fs`
  raises the real NMI from Forth by bit-banging SMC command $03 on
  VIA1, mirroring runtime/smc.s's exact edges; both the RTL and the
  emulator pulse the NMI at the value byte's ACK, so the test's
  post-catch `(i2c-stop)` is what resyncs the abandoned bus.
- **28 documented-but-missing words implemented** (2026-08-04, the
  helpdoc pass): probing every page against the live dictionary found
  entries ticked `[x]` with nothing behind them. base.fs now defines
  `true false 0> cell+ cells char+ chars align aligned >body defer
  defer! defer@ is action-of environment? sm/rem >number` and the
  eight LOGIC comparisons `0<= 0>= <= >= u<= u>= u<> u=` (the last
  two are honest aliases of `<>`/`=`). Tests in testcoreadd.fs.
  **compat.fs is now a note, not code**, and that is the real find:
  it redefined ~15 of these AFTER base.fs, so everything that
  included it got the weaker copy and the suite could not see the
  machine. Its `>number` mapped `'f'` to 47, so `"ff"` converted in
  HEX only when typed uppercase — invisible for as long as both
  definitions existed. One word, one definition.
- **Cursor policy** (2026-08-04, user report: cursor should be off
  while code runs, and scrolling left reversed-cell droppings): new
  kernel call `CON_CURSOR` (entry 8, `$00:FE20`; added end to end:
  contract.py row + `--write`, `k_con_cursor` thunk in kerntab.s over
  ccur_on/ccur_off, and `const_kernel.s` — the contract check demands
  that site too). `kern_getc` brackets its poll loop with it (on at
  wait entry, off when a key is taken or the wait aborts), COLD turns
  it off at boot, `kern_getin` stays cursor-free on purpose (game
  loops). Kernel-side, `scroll()` runs inside `ccur_suspend`/
  `ccur_resume` (ccursor.s): the cursor's reversed attribute is the
  one cell violating scroll's "every attribute is the same" premise,
  and a blink mid-copy would be duplicated a row up and stranded —
  that WAS the stray `?` cells in old suite GIF rows; they are gone
  now. Every `console.o` link needs `ccursor.o` (shell build.sh and
  nine `examples/kernel/run-*.sh` were patched). CURTEST, IRQTEST,
  KFSTEST, MEMTEST and the Forth suite are green on the new kernel.
  Two traps found on the way: the kernel call clobbers X/Y like every
  kernel call (COLD's bare jsl cost a boot — phx/phy like the shims),
  and a reversed cell in the SUITE's final GIF frame is NOT a bug:
  the `$9FBC` exit is deferred two frames, the machine falls through
  `emu-exit` to the prompt, and the freshly-armed key-wait cursor is
  photographed. Possible follow-up: a user-facing `cursor` word so a
  program can veto the KEY-wait cursor entirely.
- **Parked NMI** (2026-08-04, after the board run): the user reported
  the combo "does nothing" — at an idle prompt the machine sits inside
  the kernel's key poll ~99.3% of the time, so the bank guard declined
  essentially every press. A declined NMI now parks `nmi_pending`
  (x816.asm, defined in the FIRST !src so all refs are backward);
  `kern_getc`'s poll loop consumes it and throws the same -28 from
  safe ground, and `brk_handler` clears it so one press never delivers
  twice. The parked path has NO automated test — neither the RTL nor
  the emulator can land the i2c-triggered NMI outside the bitbang word
  itself — it is verified by pressing the combo at the board's prompt.
  If the board STILL does nothing: `: spin begin again ; spin` + combo
  tests the (suite-proven) direct path, and Ctrl+Alt+Del (SMC reset,
  same tracker in rtl/smc_x16.sv) isolates modifier tracking from
  PrtScr delivery — KEYSCAN measured bare PrtScr arriving as ext $7C,
  but MiSTer Main's behavior with Ctrl+Alt HELD is unverified.

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
- Release card: **regenerate after EVERY change, unasked.** `bash
  ../X816_core/tools/mkrelease.sh` rebuilds
  `X816_core/releases/mister/` (boot0.img = the card, boot1.rom =
  shell, boot2.rom = kernel, plus the .rbf); copy that tree to
  `release/mister/` here, which is the one the user takes to the
  MiSTer. Hash-verify card files against sources with pyfatfs after
  every refresh. A stale release is indistinguishable from a change
  that did not work.
- **A new test file must be added to THREE places**, two of them
  outside this repo's test dir: `test/test.fs` (the include), the
  `SRC` list in `run-tests.sh` (emulator card), and `FORTH_SRC` in
  `../X816_core/tools/mksdcard.py` (release card). Miss the third and
  the suite is green in the emulator while the hardware card dies on
  a missing file.

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

1. **Platform hooks**: charset. (BRK and NMI are DONE — see State.)
2. **Board run**: the turbo/MS/NMI batch is emulator-green only; the
   user takes `release/mister/` to the MiSTer (the card already
   carries TURBO and TESTNMI in `include test`).
3. **Modules**: float (software math — no ROM FP on X816), ANS file
   words beyond INCLUDED, replacements for the parked C64 words
   (`ls`, `open`, `help`, `turnkey` — see base.fs comments).
4. **helpdoc tracker: DONE and machine-verified (2026-08-04)** —
   339/567 ticked, and the checkboxes are no longer a hand-kept
   promise: a generated probe card runs `find-name` over every entry,
   both directions, so `[x]`-but-absent and `[ ]`-but-present are both
   empty. Re-run it after any word changes (the generator is a few
   lines of python over `^\[( |x)\]\s+(\S+)` plus an AUTORUN of `chk`
   lines). **Watch the regex**: `\] ` with one space silently skips
   every indented entry — TILE/VERAFX use `[x]   NAME` — which is how
   a first pass "proved" 57 entries that it had never looked at.
   Remaining unticked is honest: whole modules (FLOAT, GRAPHIC,
   AUDIO*, LOADSAVE, FILE) plus the four files durexforth.asm does
   NOT assemble — `irq.asm`, `input.asm`, `clock.asm`, `sysx.asm`
   (their words reached KERNAL entries; INPUT/KEYBOARD/CONTROL now
   say so on the page, and the SMC already carries joystick+mouse
   data for whoever rewrites them).
5. **Emulator autokeys char-drop** (fix lives in `../X816_Emulator`).

Traps worth keeping: durexForth's `(` does NOT nest, and it ends at the
first `)` — a comment containing "(HERE/ALLOT)" silently ends there and
the rest of the prose gets interpreted as code. Cost one build cycle,
then cost a second one on 2026-08-04 for `not(a>b)` inside a base.fs
comment: boot died at `for?`, the next word after that stray `)`. When
a boot dies on a word that appears nowhere in the code, look for a `)`
in the prose above it.

A second: `[']` on an INTERPRETED line (`: ['] ' postpone literal ;`)
quietly compiles the xt into HERE as junk and eats it off the stack —
the following `catch`/`execute` then runs a data cell as code, which
tends to hit a $00 byte, BRK, and "work" via the -28 abort with one
cell missing. Looks exactly like a broken handler; it is a compile-only
word misused. Wrap the catch in a colon word (testbrk/testnmi shape).
Cost one debug cycle. Base literals before `decimal` bite the same way:
`14` compiled while hex is 20 — the turbo words sit AFTER `decimal` in
base.fs for that reason.
