\ ADVANCED module tests. Requires tester.fs.

marker ---testadv---

\ Standalone-safe: REQUIRE loads the Hayes tester only if it is not already
\ in, so `include test/testadv` works on its own at the prompt and costs nothing
\ inside the suite, where test.fs loaded it first.
require test/tester


include advanced

decimal

cr .( testadv: xorshift prng ) cr
T{ 123 rnd-seed rnd16 rnd16
   123 rnd-seed rnd16 rnd16 d= -> -1 }T    \ reseeding replays the sequence
T{ 123 rnd-seed rnd16 rnd16 = -> 0 }T      \ ...and the stream moves
T{ 0 rnd-seed rnd16 0<> -> -1 }T           \ zero seed nudged off the fixed point
T{ rnd8 256 < rnd8 0 < 0= and -> -1 }T

cr .( testadv: sin8 / cos8 ) cr
T{ 0 sin8  64 sin8  128 sin8  192 sin8 -> 0 127 0 -127 }T
T{ 0 cos8  64 cos8 -> 127 0 }T
T{ 64 sin8u  192 sin8u -> 255 1 }T
T{ 300 sin8 44 sin8 = -> -1 }T             \ angles wrap at 256

cr .( testadv: atan2 ) cr
T{ 10 0 atan2  0 10 atan2 -> 0 64 }T
T{ -10 0 atan2  0 -10 atan2 -> 128 192 }T
T{ 10 10 atan2  10 -10 atan2 -> 32 224 }T
T{ -10 10 atan2 -> 96 }T
T{ 0 0 atan2 -> 0 }T

cr .( testadv: lerp ) cr
T{ 10 200 0 lerp  10 200 255 lerp -> 10 200 }T
T{ 0 200 127 lerp -> 100 }T
T{ 200 0 255 lerp  200 100 0 lerp -> 0 200 }T

cr .( testadv: ring buffer ) cr
32 ring rq
T{ rq ring# -> 0 }T
T{ 7 rq >ring  9 rq >ring  rq ring# -> 2 }T
T{ rq ring>  rq ring>  rq ring# -> 7 9 0 }T

cr .( testadv: zx0 decompression ) cr
create zv1 $75 c, $41 c, $42 c, $43 c, $55 c, $58 c,
create zv2 $78 c, $41 c, $42 c, $43 c, $fa c, $d5 c, $55 c, $60 c,
create zb 16 allot
T{ zv1 zb zx0-decompress zb - -> 3 }T      \ literals-only stream -> "ABC"
T{ zb c@ zb 1+ c@ zb 2 + c@ -> 65 66 67 }T
T{ zv2 zb zx0-decompress zb - -> 8 }T      \ new-offset match -> "ABCABCAB"
T{ zb 3 + c@ zb 6 + c@ zb 7 + c@ -> 65 65 66 }T

\ MEM-DECOMPRESS is not tested because it no longer exists: it was the
\ KERNAL's LZSA2 depacker in banked ROM, and this machine has neither.

cr .( testadv ok ) cr

---testadv---
