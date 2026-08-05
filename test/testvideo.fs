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
\ BORDER writes VERA DC_BORDER (DCSEL 0); IOC@ reads it back from the
\ I/O page in bank 0 - a plain C@ would read the program bank.
T{ 7 border $9f2c ioc@ -> 7 }T

cr .( testvideo: tile cells ) cr
\ TILE writes code+attr at (x,y); TDATA/TATTR read them back
T{ 5 3 90 12 tile 5 3 tdata 5 3 tattr -> 90 12 }T

cr .( testvideo: cursor ) cr
T{ 7 12 locate cursor -> 7 12 }T
T{ 7 12 locate pos -> 12 }T

cr .( testvideo: colour ) cr
\ The attribute byte is foreground in the low nibble, background in the
\ high one, and it is the KERNEL's (runtime/console.c con_attr) because
\ the blinking cursor has to undraw with it. These read back the cell the
\ character actually landed in, which is the only proof that the value
\ reached VERA rather than just a variable.
: (lastattr) ( -- attr ) cursor 1- swap tattr ;
0 value (c1) 0 value (c2) 0 value (c3) 0 value (c4)
.( .) (lastattr) to (c1)                \ whatever the suite is running in
7 0 color .( .) (lastattr) to (c2)
1 6 color .( .) (lastattr) to (c3)
2 4 color cls 10 10 tattr to (c4)       \ CLS fills with the CURRENT colour
\ Restore BEFORE asserting: a failure here must be readable, and the pass
\ banner is decoded off the screen at the end of the run.
1 0 color cls
T{ (c2) -> $07 }T                       \ fg 7, bg 0
T{ (c3) -> $61 }T                       \ fg 1, bg 6
T{ (c4) -> $42 }T                       \ CLS used fg 2, bg 4
T{ (c1) -> $01 }T                       \ and it started at the default
\ Out-of-range nibbles are masked, not smeared into the other colour.
T{ 255 0 color (lastattr) drop 1 0 color -> }T

\ Side-effect-only words: just make sure they run without crashing.
0 border

cr .( testvideo ok ) cr

---testvideo---
