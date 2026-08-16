\ SYSTEM module tests. Requires tester.fs.  BYE only gets an existence check
\ (it reboots); FREE only gets a smoke run (it prints).

marker ---testsystem---

\ Standalone-safe: REQUIRE loads the Hayes tester only if it is not already
\ in, so `include test/testsyst` works on its own at the prompt and costs nothing
\ inside the suite, where test.fs loaded it first.
require test/tester


include system

decimal

cr .( testsystem: platform flags / ver ) cr
\ X16 reads FALSE and X816 true. The old file asserted x16 = -1, which was a
\ claim about a machine this is not: same VERA, same VIAs, same YM, no
\ KERNAL anywhere. Code that branches on X16 to decide whether a KERNAL
\ exists has to get this right or it calls into nothing.
T{ x816 x16 c64 f256 -> -1 0 0 0 }T
T{ ver 0<> -> -1 }T

cr .( testsystem: random ) cr
\ A 32-bit xorshift seeded from the millisecond timer, not the KERNAL
\ entropy call that used to be here. Asserting actual randomness is not
\ possible in one run; what IS checkable is that it moves, stays in range,
\ and never returns zero - a zero would stick for ever in an xorshift.
T{ random random = random random = and -> 0 }T
T{ random8 0 256 within -> true }T
T{ random16 0 65536 within -> true }T
T{ random 0<> -> true }T
T{ random drop depth -> 0 }T

cr .( testsystem: free / bye exist ) cr
\ BYE is NOT called: it resets the machine through the SMC, which would end
\ the suite rather more thoroughly than intended.
free
T{ ' bye 0<> -> -1 }T

cr .( testsystem: irq - per-frame forth word ) cr
variable icnt  0 icnt !
: itick 1 icnt +! ;
' itick irq
500 ms                                            \ ~30 frames at 60 Hz
0 irq
\ Ran at all, and no count: the suite runs the emulator at -mhz 32 with
\ -warp, so how many vertical blanks fit inside 500 ms of timer is a
\ property of the harness. testirq makes the same point at more length.
T{ icnt @ 0> -> true }T
icnt @
100 ms
T{ icnt @ swap - -> 0 }T                          \ disarmed: counter frozen

cr .( testsystem: line-irq - per-scanline word ) cr
variable lcnt  0 lcnt !
: ltick 1 lcnt +! ;
' ltick 100 line-irq
300 ms                                            \ ~18 frames, one hit each
0 0 line-irq
T{ lcnt @ 0> -> true }T   \ same reasoning as the frame count above
lcnt @
100 ms
T{ lcnt @ swap - -> 0 }T                          \ disarmed: frozen

cr .( testsystem: sprcol-irq arm/disarm + collisions ) cr
T{ collisions -> 0 }T
' itick sprcol-irq
50 ms
0 sprcol-irq
T{ depth -> 0 }T
T{ ' aflow-irq 0<> -> -1 }T                       \ exists (armed only by ADVSND)

cr .( testsystem: irq preserves W across the armed word ) cr
variable ierr  0 ierr !
: iw 77 99 um* 2drop 300 9 / drop ;               \ hammer the W-using natives
' iw irq
: ichk 2000 0 do
    123 456 um* 0 <> swap 56088 <> or if 1 ierr +! then
  loop ;
ichk
0 irq                                             \ ALWAYS disarm before forget
T{ ierr @ -> 0 }T

cr .( testsystem: help ) cr
page help bit                                     \ types the short BIT page
T{ depth -> 0 }T
help nosuchtopic                                  \ -> dos error + "no help page"
T{ depth -> 0 }T

cr .( testsystem ok ) cr

---testsystem---
