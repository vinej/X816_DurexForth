\ GRAPHIC module tests (bitmap words, loaded from the SD card).
\ Requires tester.fs.  Pixels are verified with VPEEK: addr = y*320 + x
\ (all probe rows stay under y=204 so the address fits VRAM bank 0).

marker ---testgraphic---

\ Standalone-safe: REQUIRE loads the Hayes tester only if it is not already
\ in, so `include test/testgrap` works on its own at the prompt and costs nothing
\ inside the suite, where test.fs loaded it first.
require test/tester


include graphic

decimal

variable cx  variable cy  variable cl-n
: pix ( x y -- b )  320 * + 0 swap vpeek ;
: cell-lit ( x y -- flag )        \ any pixel set in the 8x8 cell at x,y?
  cy ! cx !  0 cl-n !
  8 0 do 8 0 do
    cx @ i +  cy @ j +  pix cl-n +!
  loop loop  cl-n @ 0<> ;

cr .( testgraphic: pset / gcls ) cr
ginit gcls
10 10 5 pset
T{ 10 10 pix  9 10 pix -> 5 0 }T

cr .( testgraphic: line endpoints ) cr
0 0 100 20 7 line
T{ 0 0 pix  100 20 pix -> 7 7 }T

cr .( testgraphic: rect / frame ) cr
30 30 40 40 3 rect                \ filled: inside + corners
T{ 35 35 pix  30 30 pix  40 40 pix -> 3 3 3 }T
60 30 80 50 2 frame               \ outline: edge yes, centre no
T{ 60 30 pix  80 50 pix  70 40 pix -> 2 2 0 }T

cr .( testgraphic: circle / fcircle / oval / ring ) cr
50 100 20 9 fcircle
T{ 50 100 pix -> 9 }T             \ centre filled
120 100 10 4 circle
T{ 130 100 pix  120 100 pix -> 4 0 }T   \ on the radius, hollow centre
160 80 200 120 6 oval
T{ 180 100 pix -> 6 }T
220 80 260 120 8 ring
T{ 240 80 pix  240 100 pix -> 8 0 }T    \ top edge set, centre hollow

cr .( testgraphic: gtext ) cr
0 130 6 s" HI" gtext
T{ 0 130 cell-lit  100 130 cell-lit -> -1 0 }T

cr .( testgraphic: pen API ) cr
14 gcolor  200 10 plot
T{ 200 10 pix -> 14 }T

gcls
T{ 10 10 pix -> 0 }T

0 screen page                     \ back to 80x60 text for the next tests

\ BACK TO TEXT before anything is printed. GINIT points layer 0 at a
\ bitmap that starts where the console's character map lives, so until
\ this runs there is no console to print `ok` on - and every later suite
\ file would report into a blank screen too.
0 screen

cr .( testgraphic ok ) cr

---testgraphic---
