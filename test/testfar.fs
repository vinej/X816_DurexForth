\ FAR data space: the SDRAM bump allocator defined at the head of base.fs.
\ Requires tester.fs. X816-specific - there is no C64 equivalent.
\
\ What is worth proving here, in order: that the space is where the memory
\ map says it is, that the pointer arithmetic is exact, that ordinary
\ fetch/store/move/fill really do reach 24-bit addresses outside the
\ program banks, that a block may cross a bank boundary (the whole point
\ of a flat data space), that exhaustion is refused LOUDLY rather than
\ walking into the kernel's writable-data region above it, and that MARKER
\ and FAR-EMPTY give the space back.

marker ---testfar---

\ Standalone-safe: REQUIRE loads the Hayes tester only if it is not
\ already in, so `include test/testfar` works on its own at the prompt and
\ costs nothing inside the suite, where test.fs loaded it first.
require test/tester

decimal

\ THE TOP IS NOT A LITERAL ANY MORE, and three assertions below used to spell
\ it $e00000. The kernel's writable-data region ($C0:0000-$DF:FFFF, the
\ resident editor's page pool) is reserved at boot and MEM-RELEASE hands it to
\ the heap for the rest of a session, so sdram-size is a VALUE set from MEM-TOP
\ rather than a constant. An invariant like "far-here and far-unused always sum
\ to the top" is about the SUM, not about which address the top happens to be -
\ writing the literal conflated the two, and moving the boundary failed a test
\ that was not actually about the boundary.
sdram sdram-size + constant far-top

cr .( testfar: the space is where the memory map says ) cr
T{ sdram -> $50000 }T
\ The BOOT ceiling, with the region reserved. Not tautological: it checks that
\ the kernel's default is the one MEMORY_MAP.md 1.1 documents and that far-init
\ turned MEM-TOP's last-usable-byte into a size correctly (the 1+).
T{ mem-top -> $bfffff }T
T{ far-top -> $c00000 }T
\ No release here on purpose: MEM-RELEASE is one way for the session, so a test
\ that took the 2 MB would change the space every later case runs in. The
\ release path is covered on the kernel side by
\ X816_Calypsi/programs/shell/run-mem.sh, with a negative control.
T{ far-here sdram u< -> 0 }T                  \ never below the space
T{ far-here far-top u< -> -1 }T               \ never past its end

cr .( testfar: far-allot bumps far-here and drains far-unused ) cr
far-here constant f-a
far-unused constant u-a
100 far-allot
T{ far-here f-a - -> 100 }T
T{ u-a far-unused - -> 100 }T
T{ far-here far-unused + -> far-top }T        \ the two always sum to the top

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
T{ far-here -> far-top }T
T{ 0 far-allot far-here -> far-top }T         \ zero at the top is still legal
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
