\ BMX module tests: save/load round-trip on the throwaway test card.
\ Requires tester.fs.

marker ---testbmx---

\ Standalone-safe: REQUIRE loads the Hayes tester only if it is not already
\ in, so `include testbmx` works on its own at the prompt and costs nothing
\ inside the suite, where test.fs loaded it first.
require tester


include bmx

decimal

: setpix ( val off -- ) $8000 + 0 swap rot vpoke ;
: mkstamp 8 0 do i 10 + i setpix  i 20 + i 320 + setpix loop ;
: wipe    8 0 do 0 i setpix  0 i 320 + setpix loop ;
: rdpix ( off -- c ) $8300 + 0 swap vpeek ;

cr .( testbmx: save an 8x2 stamp ) cr
mkstamp
$0abc 3 pal!                               \ a known palette entry
8 bmx-width !  2 bmx-height !  8 bmx-bpp !
0 bmx-palstart !  256 bmx-palcount !  320 bmx-stride !
T{ s" tbmx" 8 0 $8000 bmx-save -> 0 }T

cr .( testbmx: load it back elsewhere ) cr
0 bmx-width !  0 bmx-height !
$0111 3 pal!                               \ clobber the palette entry
T{ s" tbmx" 8 0 $8300 bmx-load -> 0 }T
T{ bmx-width @ bmx-height @ bmx-bpp @ -> 8 2 8 }T
T{ 0 rdpix  7 rdpix -> 10 17 }T
T{ 320 rdpix  327 rdpix -> 20 27 }T
T{ 8 rdpix -> 0 }T                          \ stamp: outside untouched
T{ 1 $fa06 vaddr v@ v@ -> $bc $0a }T        \ palette entry 3 restored

cr .( testbmx: error paths ) cr
T{ s" nosuchbmx" 8 0 $8000 bmx-load -> 1 }T \ missing file = i/o error
T{ s" 1" 8 0 $8000 bmx-load -> 2 }T         \ not a BMX = format error

cr .( testbmx ok ) cr

---testbmx---
