\ VIDEO group tests (X16 VERA + KERNAL). Requires tester.fs and testcore.fs.
\ Round-trip tests only; pure side-effect words (SCREEN/CLS) are exercised
\ but not asserted (they change the display, not readable state).

marker ---testvideo---

decimal

cr .( testvideo: VRAM data port ) cr
\ Scratch VRAM at bank 0 $8000+: the console's 128x60 tile map occupies
\ $0000-$3BFF, so pokes there would scribble on the visible screen.
T{ 0 $8100 65 vpoke 0 $8100 vpeek -> 65 }T
T{ 0 $8101 66 vpoke 0 $8101 vpeek -> 66 }T
\ VADDR sets the port with auto-increment; V! / V@ stream
T{ 0 $8200 vaddr 10 v! 20 v! 30 v! -> }T
T{ 0 $8200 vaddr v@ v@ v@ -> 10 20 30 }T
\ V!W writes a 16-bit word low byte first
T{ 0 $8300 vaddr $abcd v!w -> }T
T{ 0 $8300 vaddr v@ v@ -> $cd $ab }T

cr .( testvideo: border ) cr
\ COLOR is parked with the console-attribute API (RVS is a no-op for the
\ same reason - the X816 console owns the attribute byte, x816.asm).
\ BORDER writes VERA DC_BORDER (DCSEL 0); IOC@ reads it back from the
\ I/O page in bank 0 (a plain C@ would read the program bank).
T{ 7 border $9f2c ioc@ -> 7 }T

cr .( testvideo: tile cells ) cr
\ TILE writes code+attr at (x,y); TDATA/TATTR read them back
T{ 5 3 90 12 tile 5 3 tdata 5 3 tattr -> 90 12 }T

cr .( testvideo: cursor ) cr
T{ 7 12 locate cursor -> 7 12 }T
T{ 7 12 locate pos -> 12 }T

\ Side-effect-only words: just make sure they run without crashing.
0 border

cr .( testvideo ok ) cr

---testvideo---
