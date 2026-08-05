\ SYSTEM module tests. Requires tester.fs.  BYE only gets an existence check
\ (it reboots); FREE only gets a smoke run (it prints).

marker ---testsystem---

\ Standalone-safe: REQUIRE loads the Hayes tester only if it is not already
\ in, so `include testsystem` works on its own at the prompt and costs nothing
\ inside the suite, where test.fs loaded it first.
require tester


include system

decimal

cr .( testsystem: platform flags / ver ) cr
T{ x16 c64 f256 -> -1 0 0 }T
T{ ver 0<> -> -1 }T

cr .( testsystem: syscall + random ) cr
T{ 0 0 0 $fecf syscall 2drop drop depth -> 0 }T   \ entropy call round-trips
T{ random random = random random = and random random = and -> 0 }T
T{ random drop depth -> 0 }T

cr .( testsystem: usr calls RAM machine code ) cr
create mc7 $a9 c, 7 c, $60 c,                     \ lda #7 / rts
T{ 0 0 0 mc7 usr drop drop -> 7 }T                \ a' = 7
create mc9 $a2 c, 9 c, $60 c,                     \ ldx #9 / rts
T{ 0 0 0 mc9 usr drop swap drop -> 9 }T           \ x' = 9

cr .( testsystem: free / bye exist ) cr
free
T{ ' bye 0<> -> -1 }T

cr .( testsystem: irq - per-frame forth word ) cr
variable icnt  0 icnt !
: itick 1 icnt +! ;
' itick irq
500 ms                                            \ ~30 frames at 60 Hz
0 irq
T{ icnt @ 5 200 within -> -1 }T                   \ ran repeatedly (wide margin)
icnt @
100 ms
T{ icnt @ swap - -> 0 }T                          \ disarmed: counter frozen

cr .( testsystem: line-irq - per-scanline word ) cr
variable lcnt  0 lcnt !
: ltick 1 lcnt +! ;
' ltick 100 line-irq
300 ms                                            \ ~18 frames, one hit each
0 0 line-irq
T{ lcnt @ 3 120 within -> -1 }T
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
