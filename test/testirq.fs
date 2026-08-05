\ IRQ - running a Forth word from a VERA interrupt (asm/firq.asm, base.fs).
\
\ Nothing here asserts a TICK COUNT. The suite runs the emulator at -mhz 32
\ with -warp, so how many vertical blanks land inside a loop is a property
\ of the harness, not of the machine. What is checked is that the handler
\ runs at all, that it stops when disarmed, and - the assertion with teeth -
\ that the interrupted code comes out with exactly the answer it would have
\ had with no interrupt at all. That is the whole difficulty of calling
\ Forth from an interrupt: the data stack pointer lives in X and the
\ primitives keep their working pointers in W/W2/W3, so a handler that
\ borrowed either would corrupt whatever it landed in the middle of.

marker ---testirq---

\ Standalone-safe: REQUIRE loads the Hayes tester only if it is not already
\ in, so `include testirq` works on its own at the prompt.
require tester

decimal

variable ticks
variable spin
: tick 1 ticks +! ;
: burn 20000 0 do 1 spin +! loop ;

cr .( testirq: a Forth word runs from the vertical blank ) cr
0 ticks !  0 spin !
' tick irq
burn
T{ ticks @ 0> -> true }T                \ it ran
T{ spin @ -> 20000 }T                   \ ...and the interrupted loop is exact

cr .( testirq: disarming stops it ) cr
0 irq
ticks @
burn
T{ ticks @ swap - -> 0 }T               \ not one more after 0 IRQ

cr .( testirq: the interrupted stack survives ) cr
\ A handler that pushes and never pops would walk the stack pointer away if
\ the dispatcher did not restore X. Arm one that leaves rubbish, run a loop
\ that cares about its own depth, and check both.
variable junkruns
: junk 1 junkruns +! 111 222 333 drop drop drop ;
0 junkruns !
' junk irq
\ Through a variable, not the stack: `depth` counts the cell it is about to
\ push, so measuring depth by leaving it on the stack always reads one high.
variable d0
depth d0 !
burn
T{ depth d0 @ - -> 0 }T                 \ our depth is untouched
T{ junkruns @ 0> -> true }T
T{ spin @ -> 60000 }T                   \ three burns now, still exact
0 irq

cr .( testirq: the other three sources arm without firing ) cr
\ NOT asserted: that they fire, or that any enable bit moved. VERA2 is not
\ classic VERA here - $9F26 does not behave as IEN on this core - so the
\ enable path for raster, sprite-collision and audio-FIFO is still unknown
\ and base.fs deliberately does not pretend to set it. VSYNC needs no
\ enable from us because the kernel switches it on for its own frame
\ counter, which is exactly why IRQ above is the one that is proven.
\ What IS checked is that arming and disarming the other three neither
\ throws nor disturbs the VSYNC handler that is already running.
: noop ;
' noop 100 line-irq
' noop sprcol-irq
' noop aflow-irq
0 100 line-irq
0 sprcol-irq
0 aflow-irq
T{ depth d0 @ - -> 0 }T

cr .( testirq: a slot that does not exist is refused ) cr
\ In a colon word, not on this line: ['] is compile-only, and interpreted
\ it quietly compiles the xt into HERE and eats it - the same trap testbrk
\ and testnmi carry a note about. CATCH restores the depth it saw, so the
\ two arguments are still there under the code and get nipped away.
: badslot ( -- n ) ['] noop 9 ['] (irq!) catch nip nip ;
T{ badslot -> -9 }T

cr .( testirq ok ) cr

---testirq---
