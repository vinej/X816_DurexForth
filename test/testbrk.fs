\ BRK aborts to the interpreter. The kernel dispatches a brk trap to
\ whatever sits in its KIRQ_BRK slot (X816_core doc/KERNEL.md 5.6); COLD
\ puts brk_handler there (asm/durexforth.asm -> install_brk,
\ asm/interpreter.asm -> brk_handler), and that handler never returns - it
\ rebuilds the Forth's register environment and unwinds into THROW with -28,
\ "user interrupt". Requires tester.fs.
\
\ Everything here is about the abort being CLEAN, not merely survivable: the
\ throw code arrives, the interpreter still works afterwards, it can happen
\ twice, and the machine's interrupts are still on when the dust settles.

marker ---testbrk---

decimal

\ brk is TWO bytes on the 65816 - the opcode plus a signature byte - and the
\ return address it pushes skips both. The nop IS that signature: with no
\ handler installed the dispatcher simply returns, and execution then has to
\ land on the rtl rather than inside it.
code (brk) brk, nop, rtl, end-code

cr .( testbrk: brk throws -28 and CATCH gets it ) cr
: (trybrk) ['] (brk) catch ;
T{ (trybrk) -> -28 }T

cr .( testbrk: the interpreter is intact afterwards ) cr
T{ 2 3 + -> 5 }T
: (after) 7 8 * ;
T{ (after) -> 56 }T
T{ (trybrk) -> -28 }T             \ repeatable, not a one-shot

cr .( testbrk: the data stack is left in a sane state ) cr
T{ 11 22 (trybrk) -> 11 22 -28 }T \ CATCH restores the depth it saved

cr .( testbrk: the abort left interrupts ON ) cr
\ The kernel counts VSYNC frames in its own IRQ handler, so TICKS only moves
\ while interrupts are enabled. The BRK sequence sets I and this handler
\ never rti's, so without its CLI the counter freezes from the first abort
\ onwards - and the cursor and the clock freeze with it. The wait is bounded
\ because the failure being guarded against is exactly "never advances".
: (tick) ticks drop ;
: (moved?) ( -- f )
  (tick)
  500 0 do
    dup (tick) <> if drop unloop -1 exit then
    1 ms
  loop
  drop 0 ;
T{ (moved?) -> -1 }T

cr .( testbrk ok ) cr

---testbrk---
