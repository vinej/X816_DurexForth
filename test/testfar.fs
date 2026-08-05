\ FAR data space: the SDRAM bump allocator defined at the head of base.fs.
\ Requires tester.fs. X816-specific - there is no C64 equivalent.
\
\ What is worth proving here, in order: that the space is where the memory
\ map says it is, that the pointer arithmetic is exact, that ordinary
\ fetch/store/move/fill really do reach 24-bit addresses outside the
\ program banks, that a block may cross a bank boundary (the whole point
\ of a flat data space), that exhaustion is refused LOUDLY rather than
\ walking into the VERA2 window, and that MARKER and FAR-EMPTY give the
\ space back.

marker ---testfar---

\ Standalone-safe: REQUIRE loads the Hayes tester only if it is not
\ already in, so `include testfar` works on its own at the prompt and
\ costs nothing inside the suite, where test.fs loaded it first.
require tester

decimal

cr .( testfar: the space is where the memory map says ) cr
T{ sdram -> $50000 }T
T{ sdram sdram-size + -> $e00000 }T
T{ far-here sdram u< -> 0 }T                  \ never below the space
T{ far-here sdram sdram-size + u< -> -1 }T    \ never past its end

cr .( testfar: far-allot bumps far-here and drains far-unused ) cr
far-here constant f-a
far-unused constant u-a
100 far-allot
T{ far-here f-a - -> 100 }T
T{ u-a far-unused - -> 100 }T
T{ far-here far-unused + -> $e00000 }T        \ the two always sum to the top

cr .( testfar: a far block holds cells and bytes ) cr
T{ $12345678 f-a ! f-a @ -> $12345678 }T      \ 32-bit cell, in SDRAM
T{ $ab f-a 99 + c! f-a 99 + c@ -> $ab }T      \ the block's last byte
T{ f-a @ -> $12345678 }T                      \ ...and the first is intact

cr .( testfar: fill, move and erase reach into SDRAM ) cr
64 far-buffer: fb1
64 far-buffer: fb2
T{ fb2 fb1 - -> 64 }T                         \ adjacent, non-overlapping
fb1 64 $5a fill
T{ fb1 c@ fb1 63 + c@ -> $5a $5a }T
fb1 fb2 64 move
T{ fb2 c@ fb2 63 + c@ -> $5a $5a }T
fb2 64 erase
T{ fb2 c@ fb1 63 + c@ -> 0 $5a }T             \ neighbour untouched

cr .( testfar: a block crosses a bank boundary ) cr
\ 74565 bytes: longer than a bank whatever the pointer's offset, so the far
\ end carries a different bank byte than the near end.
$12345 far-buffer: fbig
T{ fbig $12344 + 16 rshift  fbig 16 rshift  = -> 0 }T
T{ $a5 fbig ! fbig @ -> $a5 }T
T{ $5a fbig $12341 + ! fbig $12341 + @ -> $5a }T
T{ fbig @ -> $a5 }T                           \ the far store did not alias

cr .( testfar: exhaustion throws -8 and the pointer stays put ) cr
: (over) far-unused 1+ far-allot ;
: (try) ['] (over) catch ;
far-here constant f-b
T{ (try) -> -8 }T
T{ far-here -> f-b }T                         \ a refused claim moves nothing
far-unused constant f-left
f-left far-allot                              \ the exact fit IS allowed
T{ far-unused -> 0 }T
T{ far-here -> $e00000 }T
T{ 0 far-allot far-here -> $e00000 }T         \ zero at the top is still legal
T{ (try) -> -8 }T
f-b to far-here                               \ hand the tail back by hand
T{ fb2 c@ fbig @ -> 0 $a5 }T                  \ and the live blocks survived

cr .( testfar: MARKER puts the far pointer back ) cr
far-here constant f-c
marker ---farprobe---
$1000 far-buffer: ftmp
T{ far-here f-c - -> $1000 }T
---farprobe---
T{ far-here -> f-c }T

cr .( testfar: FAR-EMPTY drops the lot ) cr
far-empty
T{ far-here -> sdram }T
T{ far-unused -> sdram-size }T
64 far-buffer: fb3
T{ fb3 -> sdram }T                            \ handed out from the bottom again

cr .( testfar ok ) cr

---testfar---
