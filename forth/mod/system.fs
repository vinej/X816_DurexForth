\ SYSTEM - platform and system words (SYSTEM.TXT).
\ Cart: NEEDS SYSTEM      SD card: INCLUDE SYSTEM
\
\ SYSCALL AND USR ARE GONE, and both for reasons rather than for effort.
\
\ SYSCALL called a KERNAL routine through the X16's banked ROM with BCALL.
\ There is no KERNAL here and no ROM past the boot page, so there is nothing
\ for it to call. The kernel this machine does have is reached by jsl through
\ the table at $00:FE00, and asm/x816.asm already wraps the entries Forth
\ needs; a word letting you invent a 24-bit kernel call from the prompt would
\ mostly be a way to hang the machine.
\
\ USR ran machine code in RAM with A/X/Y in and out. durexForth carries an
\ ASSEMBLER - `code myword ... end-code` - so machine code here is an ordinary
\ word you call by name, which is strictly more than USR offered. It also gets
\ the calling convention right for free: words are entered by jsl and must
\ leave by rtl, the registers are sixteen bits wide, and X is the data stack
\ pointer and has to come back untouched. USR's `lda #7 / rts` example gets
\ all three of those wrong.

decimal

\ --- randomness ---------------------------------------------------------------
\ The X16 took this from a KERNAL entropy routine. Here it is a 32-bit
\ xorshift, seeded once from the free-running millisecond timer so two runs of
\ the same program do not deal the same cards. $9F90 is read FIRST because
\ reading it latches bits 31:8 - read the high bytes without it and you get
\ whatever the previous latch held.
variable (rndseed)
: (seed!) ( -- )
  $9f90 ioc@ $9f91 ioc@ 8 lshift or
  $9f92 ioc@ 16 lshift or $9f93 ioc@ 24 lshift or
  dup 0= if drop 2463534242 then      \ a zero seed would stay zero for ever
  (rndseed) ! ;
(seed!)
: random ( -- n )                     \ 32-bit xorshift, never zero
  (rndseed) @
  dup 13 lshift xor
  dup 17 rshift xor
  dup 5 lshift xor
  dup (rndseed) ! ;
: random8  ( -- b ) random 255 and ;
: random16 ( -- u ) random 65535 and ;
\ BASIC-style: a number in 0..u-1. The sign bit is masked off first,
\ because MOD on a negative dividend would hand back a negative "index".
: rnd ( u -- n ) random 2147483647 and swap mod ;

\ --- leaving ---------------------------------------------------------------------
\ BYE HANDS THE MACHINE BACK. It does not reset it.
\
\ It used to raise the SMC's reset line - the same edge Ctrl+Alt+Del sends
\ (X816_core rtl/smc_x16.sv) - because when that was written there was nothing
\ to come back to. There is now: the prompt is resident in firmware, and REBOOT
\ (coreadd.asm) already reaches it with K_EXIT, status 0. So BYE returns to
\ whoever started Forth - the desktop's tiles if it was launched from there,
\ the console prompt if it was typed there - instead of cold-booting the
\ machine and re-reading the card.
\
\ THIS DID NOT WORK UNTIL 2026-08-14, and the reason is worth keeping. Routing
\ BYE through K_EXIT left a blank, dead machine, which looked like REBOOT being
\ broken - it has never had a test, because testsystem.fs skips BYE on purpose
\ ("it reboots"). REBOOT was fine. K_EXIT restarts the kernel through cstartup,
\ whose first act is to copy the data-init table over the kernel's own
\ variables, and it did that with INTERRUPTS STILL ENABLED: an IRQ arriving
\ mid-copy dispatched through a half-written kirq vector into the kernel's
\ direct page and ran away through bank $00. Forth enables interrupts, so
\ Forth met it every time; the Calypsi programs never clear I and so never did.
\ Fixed in the kernel (X816_Calypsi runtime/kerntab.s, `sei` in k_exit), which
\ is where it belonged - SuperBasic's QUIT was dying of the same thing.
: bye ( -- ) reboot ;

\ --- what machine is this ---------------------------------------------------------
\ X16 read -1 here, which was simply untrue. The hardware is X16-SHAPED - the
\ same VERA, VIAs and YM - and is not an X16: code switching on X16 to decide
\ whether a KERNAL exists would get the wrong answer and call into nothing.
$0100 constant ver                    \ version 1.0: high byte * 256 + low
-1 constant x816
0 constant x16
0 constant c64
0 constant f256

( FREE reports the machine's TWO spaces, in the units the boot report uses,
  and it reports them from the same words - so the prompt and the banner
  cannot disagree.

  What it used to say was `latest here -`, the C64 measure: the gap between
  HERE and the dictionary headers growing down towards it. On X816 that is
  not the free space, it is a THIRD of it. Program space is four banks, and
  while HERE is still in bank $01 the three banks above it are untouched -
  so the old word answered ~27 KB on a machine with ~219 KB free and gave no
  hint that the missing 192 KB existed. UNUSED already knows both cases;
  this just asks it. )
: free ( -- )
  unused 1024 / u. ." K program, fast ram" cr
  far-unused 1048576 / u. ." M data, sdram" cr ;
