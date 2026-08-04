\ NMI aborts a runaway loop - the RUN/STOP-RESTORE of this machine. COLD
\ installs nmi_handler in the kernel's KIRQ_NMI slot (asm/interpreter.asm);
\ it aborts with -28 ONLY when the NMI interrupted Forth code (program
\ banks $01-$04, read from the dispatcher's frame) and resumes anything
\ else, so a combo landing inside the kernel is ignored, not corrupting.
\
\ On hardware the user raises it with Ctrl+Alt+PrtScr. Here it is raised
\ from software: SMC I2C command $03, bit-banged on VIA1 exactly the way
\ the kernel's own keyboard poll does (X816_Calypsi runtime/smc.s - the
\ same edges are proven against both the RTL and the emulator). The poll
\ is foreground-only, so the bus is ours for the whole word. Both the SMC
\ and the emulator pulse the NMI at the value byte's ACK - BEFORE the
\ master's stop - so the abort abandons the bus mid-transaction; the
\ (i2c-stop) after the catch is what resyncs it, and it is load-bearing.
\ Requires tester.fs.

marker ---testnmi---

decimal

\ VIA1 PA0 = SDA, PA1 = SCL, open drain: DDRA 1-bit = drive low (ORA
\ holds 0), 0-bit = release to the pull-up. Whole-register writes; the
\ kernel assumes full ownership of DDRA the same way.
$9f01 constant (pa)
$9f03 constant (ddr)
: (idle) 0 (ddr) ioc! ;   \ SDA released, SCL released
: (sda)  1 (ddr) ioc! ;   \ SDA low,      SCL released
: (scl)  2 (ddr) ioc! ;   \ SDA released, SCL low
: (both) 3 (ddr) ioc! ;   \ SDA low,      SCL low

: (i2c-start) 0 (pa) ioc! (idle) (sda) (both) ;
: (i2c-stop)  (both) (sda) (idle) ;
: (i2c-bit) ( f -- )      \ data changes while SCL is low, then one clock
  if (scl) (idle) (scl) else (both) (sda) (both) then ;
: (i2c>) ( b -- )         \ send a byte MSB first, clock the ACK slot
  8 0 do dup $80 and 0<> (i2c-bit) 2* loop drop
  (scl) (idle) (scl) ;
: (smc-nmi) ( -- )        \ $42 W, command $03, value $00 -> NMI
  (i2c-start) $84 (i2c>) 3 (i2c>) 0 (i2c>) (i2c-stop) ;

\ The NMI arrives during (smc-nmi) itself or in the loop right after -
\ both are Forth code in a program bank, so either way the handler must
\ throw -28 out of the runaway BEGIN AGAIN. The catch is wrapped in a
\ colon word like testbrk's: ['] is compile-only, and used on an
\ interpreted line it quietly compiles the xt into HERE and eats it -
\ catch then executes garbage that happens to hit a BRK and "passes"
\ with one cell missing. Cost a debug cycle; do not slim this down.
: (runaway) (smc-nmi) begin again ;
: (trynmi) ['] (runaway) catch (i2c-stop) ;

cr .( testnmi: an NMI breaks a runaway loop with -28 ) cr
T{ (trynmi) -> -28 }T

cr .( testnmi: the interpreter is intact afterwards ) cr
T{ 2 3 + -> 5 }T
: (after) 7 8 * ;
T{ (after) -> 56 }T

cr .( testnmi: repeatable - the SMC pulse is not a one-shot ) cr
T{ (trynmi) -> -28 }T

cr .( testnmi: the data stack is left in a sane state ) cr
T{ 33 44 (trynmi) -> 33 44 -28 }T

cr .( testnmi: the abort left interrupts ON ) cr
: (tick) ticks drop ;
: (moved?) ( -- f )
  (tick)
  500 0 do
    dup (tick) <> if drop unloop -1 exit then
    1 ms
  loop
  drop 0 ;
T{ (moved?) -> -1 }T

cr .( testnmi ok ) cr

---testnmi---
