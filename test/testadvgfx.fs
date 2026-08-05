\ ADVGFX module tests (needs GRAPHIC). Requires tester.fs.
\ Probe rows stay under y=204 so pixel addresses fit VRAM bank 0.

marker ---testadvgfx---

\ Standalone-safe: REQUIRE loads the Hayes tester only if it is not already
\ in, so `include testadvgfx` works on its own at the prompt and costs nothing
\ inside the suite, where test.fs loaded it first.
require tester


include graphic
include advgfx

decimal

: pix ( x y -- c ) 320 * + 0 swap vpeek ;

cr .( testadvgfx: clip-line ) cr
T{ 10 10 50 50 clip-line -> 10 10 50 50 -1 }T          \ fully inside: unchanged
T{ -400 10 -350 20 clip-line 0= nip nip nip nip -> -1 }T   \ fully left: rejected
T{ -10 100 10 100 clip-line -> 0 100 10 100 -1 }T      \ clipped at x=0
T{ 300 -20 300 20 clip-line -> 300 0 300 20 -1 }T      \ clipped at y=0
T{ 10 20 300 250 clip-rect 100 10 100 300 clip-line -> 100 20 100 250 -1 }T
0 0 319 239 clip-rect

cr .( testadvgfx: cline draws only the visible part ) cr
ginit gcls
-20 50 40 50 5 cline
T{ 0 50 pix  40 50 pix  319 50 pix -> 5 5 0 }T

cr .( testadvgfx: flood fill ) cr
gcls
20 20 60 60 3 frame                     \ hollow box outline in colour 3
40 40 7 flood                           \ fill the inside
T{ 40 40 pix  21 21 pix  59 59 pix -> 7 7 7 }T
T{ 20 40 pix  10 40 pix -> 3 0 }T       \ border kept, outside untouched
70 70 9 flood                           \ seed on the outside: fills the screen
T{ 0 0 pix  200 100 pix  10 40 pix -> 9 9 9 }T
T{ 40 40 pix -> 7 }T                    \ ...but not the earlier region

cr .( testadvgfx: fx-copy ) cr
gcls
0 100 8 pset  1 100 8 pset  2 100 8 pset  3 100 8 pset
8 100 4 pset  9 100 4 pset
\ copy 10 bytes of row 100 (offset 32000, 4-aligned) to row 200 (64000):
\ 2 cache quads + a 2-byte plain tail
0 32000 0 64000 10 fx-copy
T{ 0 200 pix  3 200 pix  8 200 pix  9 200 pix -> 8 8 4 4 }T
T{ 10 200 pix -> 0 }T
\ unaligned length tail (5 = 1 quad + 1 single)
0 32000 0 64320 5 fx-copy
T{ 0 201 pix  3 201 pix  4 201 pix -> 8 8 0 }T

cr .( testadvgfx: fx affine ) cr
\ 16x16-texel texture above the bitmap: tiles at 1:$3000, 2x2 map at 1:$3800
: t! ( val off -- ) $3000 + 1 swap rot vpoke ;
: mktex ( -- )
  64 0 do i i t! loop                       \ tile 0: texel value = y*8+x
  64 0 do $99 i 64 + t! loop                \ tile 1: solid $99
  0 $800 t!  1 $801 t!  1 $802 t!  0 $803 t! ;
mktex
1 $3000 1 $3800 0 0 affine-on
0 0 512 0 affine-ray  0 $f000 10 affine-line     \ straight along x
T{ 0 $f000 vpeek  0 $f003 vpeek  0 $f007 vpeek -> 0 3 7 }T
T{ 0 $f008 vpeek  0 $f009 vpeek -> $99 $99 }T    \ crossed into tile 1
0 0 1024 0 affine-ray  0 $f010 6 affine-line     \ zoomed out x2
T{ 0 $f010 vpeek  0 $f011 vpeek  0 $f013 vpeek  0 $f014 vpeek -> 0 2 6 $99 }T
0 0 0 512 affine-ray  0 $f020 4 affine-line      \ straight down
T{ 0 $f020 vpeek  0 $f021 vpeek  0 $f023 vpeek -> 0 8 24 }T
0 0 512 512 affine-ray  0 $f030 4 affine-line    \ the 45-degree diagonal
T{ 0 $f030 vpeek  0 $f031 vpeek  0 $f033 vpeek -> 0 9 27 }T
8 0 512 0 affine-ray  0 $f040 10 affine-line     \ wrap at the 16-texel edge
T{ 0 $f040 vpeek  0 $f047 vpeek  0 $f048 vpeek  0 $f049 vpeek -> $99 $99 0 1 }T
affine-off

cr .( testadvgfx ok ) cr

---testadvgfx---
