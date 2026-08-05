\ ADVANCED - game math, seedable PRNG, decompression (ADVANCED.TXT).
\ Cart: NEEDS ADVANCED      SD card: INCLUDE ADVANCED
\ Ported from C:\quartus\projects\x16_library (util/math.asm, util/zx0.asm).
\ Angles are bytes: a full circle is 256 (64 = 90 degrees); with the X16 y
\ axis pointing down, angle 0 = east (+x), 64 = south (+y), and
\   x +=  speed cos8 * 128 /   y += speed sin8 * 128 /
\ moves along the heading ATAN2 returned.

decimal

\ --- seedable PRNG: John Metcalf's 16-bit xorshift (7,9,8) --------------------
variable (rnd)  $2a56 (rnd) !
: rnd-seed ( n -- ) ?dup 0= if 1 then (rnd) ! ;   \ 0 is the fixed point
: rnd16 ( -- u )
  (rnd) @ dup 7 lshift xor dup 9 rshift xor dup 8 lshift xor
  dup (rnd) ! ;
: rnd8 ( -- c ) rnd16 255 and ;

\ --- sine / cosine, 256-entry table, out -127..127 signed ----------------------
create (sintab)
0 c, 3 c, 6 c, 9 c, 12 c, 16 c, 19 c, 22 c, 25 c, 28 c, 31 c, 34 c, 37 c, 40 c, 43 c, 46 c,
49 c, 51 c, 54 c, 57 c, 60 c, 63 c, 65 c, 68 c, 71 c, 73 c, 76 c, 78 c, 81 c, 83 c, 85 c, 88 c,
90 c, 92 c, 94 c, 96 c, 98 c, 100 c, 102 c, 104 c, 106 c, 107 c, 109 c, 111 c, 112 c, 113 c, 115 c, 116 c,
117 c, 118 c, 120 c, 121 c, 122 c, 122 c, 123 c, 124 c, 125 c, 125 c, 126 c, 126 c, 126 c, 127 c, 127 c, 127 c,
127 c, 127 c, 127 c, 127 c, 126 c, 126 c, 126 c, 125 c, 125 c, 124 c, 123 c, 122 c, 122 c, 121 c, 120 c, 118 c,
117 c, 116 c, 115 c, 113 c, 112 c, 111 c, 109 c, 107 c, 106 c, 104 c, 102 c, 100 c, 98 c, 96 c, 94 c, 92 c,
90 c, 88 c, 85 c, 83 c, 81 c, 78 c, 76 c, 73 c, 71 c, 68 c, 65 c, 63 c, 60 c, 57 c, 54 c, 51 c,
49 c, 46 c, 43 c, 40 c, 37 c, 34 c, 31 c, 28 c, 25 c, 22 c, 19 c, 16 c, 12 c, 9 c, 6 c, 3 c,
0 c, 253 c, 250 c, 247 c, 244 c, 240 c, 237 c, 234 c, 231 c, 228 c, 225 c, 222 c, 219 c, 216 c, 213 c, 210 c,
207 c, 205 c, 202 c, 199 c, 196 c, 193 c, 191 c, 188 c, 185 c, 183 c, 180 c, 178 c, 175 c, 173 c, 171 c, 168 c,
166 c, 164 c, 162 c, 160 c, 158 c, 156 c, 154 c, 152 c, 150 c, 149 c, 147 c, 145 c, 144 c, 143 c, 141 c, 140 c,
139 c, 138 c, 136 c, 135 c, 134 c, 134 c, 133 c, 132 c, 131 c, 131 c, 130 c, 130 c, 130 c, 129 c, 129 c, 129 c,
129 c, 129 c, 129 c, 129 c, 130 c, 130 c, 130 c, 131 c, 131 c, 132 c, 133 c, 134 c, 134 c, 135 c, 136 c, 138 c,
139 c, 140 c, 141 c, 143 c, 144 c, 145 c, 147 c, 149 c, 150 c, 152 c, 154 c, 156 c, 158 c, 160 c, 162 c, 164 c,
166 c, 168 c, 171 c, 173 c, 175 c, 178 c, 180 c, 183 c, 185 c, 188 c, 191 c, 193 c, 196 c, 199 c, 202 c, 205 c,
207 c, 210 c, 213 c, 216 c, 219 c, 222 c, 225 c, 228 c, 231 c, 234 c, 237 c, 240 c, 244 c, 247 c, 250 c, 253 c,
: sin8  ( angle -- n ) 255 and (sintab) + c@ dup 127 > if 256 - then ;
: cos8  ( angle -- n ) 64 + sin8 ;
: sin8u ( angle -- u ) sin8 128 + ;               \ 1..255: volumes / scales
: cos8u ( angle -- u ) cos8 128 + ;

\ --- atan2: the angle of a vector ----------------------------------------------
create (atantab)
0 c, 1 c, 3 c, 4 c, 5 c, 6 c, 8 c, 9 c, 10 c, 11 c, 12 c, 13 c, 15 c, 16 c, 17 c, 18 c,
19 c, 20 c, 21 c, 22 c, 23 c, 24 c, 25 c, 25 c, 26 c, 27 c, 28 c, 29 c, 29 c, 30 c, 31 c, 31 c,
32 c,
variable at-nx  variable at-ny
: (ratio32) ( num den -- 0..32 ) swap 32 * swap / ;
: atan2 ( dx dy -- angle )                        \ 0 = +x/east, 64 = +y/down
  2dup or 0= if 2drop 0 exit then
  dup 0< at-ny !  abs
  swap dup 0< at-nx !  abs
  swap                                            \ ( ax ay )
  2dup = if 2drop 32 else
    2dup > if swap (ratio32) (atantab) + c@       \ shallow: atan(ay/ax)
    else (ratio32) (atantab) + c@ 64 swap - then  \ steep: 64 - atan(ax/ay)
  then
  at-nx @ if 128 swap - then                      \ dx < 0: mirror
  at-ny @ if 256 swap - then                      \ dy < 0: negate
  255 and ;

\ --- lerp: linear interpolation between unsigned bytes --------------------------
: (t*) ( d t1 -- n ) um* 8 lshift swap 8 rshift or ;
: lerp ( a b t -- c )                             \ t: 0 = a ... 255 = b exactly
  1+ >r 2dup swap - dup 0< if
    negate r> (t*) nip -
  else r> (t*) nip + then ;

\ --- ring buffer (power-of-2 capacity) ----------------------------------------
\   32 ring myq    7 myq >ring    myq ring> . -> 7    myq ring# . -> 0
\ Free-running head/tail counters; keep ring# below the capacity yourself.
\ Layout: mask at +0, head at +4, tail at +8, the bytes from +12. Those
\ offsets were 0/2/4/6 - three CELLS, written when a cell was two bytes.
\ A cell is four now, so head and tail overlapped each other and the data,
\ and RING# answered nonsense the first time it was ever run.
: ring ( size "name" -- ) create dup 1- , 0 , 0 , allot ;
: ring# ( rng -- n ) dup 4 + @ swap 8 + @ - ;     \ bytes queued
: >ring ( c rng -- )
  dup >r 4 + dup @ dup 1+ rot !
  r@ @ and r> 12 + + c! ;
: ring> ( rng -- c )
  dup >r 8 + dup @ dup 1+ rot !
  r@ @ and r> 12 + + c@ ;

\ --- decompression ----------------------------------------------------------------
\ MEM-DECOMPRESS IS GONE, and it is the only thing this file lost. It was
\ `bcall $feed` - the X16 KERNAL's LZSA2 depacker, in banked ROM. There is
\ no ROM here past the boot page and no LZSA2 anywhere in the kernel, so the
\ word could only ever have jumped into nothing. A word that exists and does
\ nothing is worse than its absence: ZX0-DECOMPRESS below is a real depacker,
\ written in Forth, and `zx0`/`salvador` are what to compress with.

\ ZX0 v2 (what `zx0` / `salvador` emit by default). RAM to RAM, forward
\ copies, cannot decompress in place. Load-time speed (pure Forth).
variable zsrc  variable zdst  variable zoff
variable zbits variable zlast variable zbt
: (zbyte) ( -- c ) zsrc @ c@ dup zlast !  1 zsrc +! ;
: (zbit) ( -- 0|1 )
  zbt @ if 0 zbt !  zlast @ 1 and exit then
  zbits @ 2* dup 255 and dup zbits ! 0= if
    drop                                          \ the sentinel shifted out
    (zbyte) 2* 1+ dup 255 and zbits !  256 and 0<> 1 and exit then
  256 and 0<> 1 and ;
: (zgamma) ( inv -- n )
  >r 1 begin (zbit) 0= while 2* (zbit) r@ xor + repeat r> drop ;
: (zcopy) ( n -- )
  zdst @ zoff @ - swap
  0 ?do dup i + c@ zdst @ c!  1 zdst +! loop drop ;
: zx0-decompress ( src dst -- end )
  zdst ! zsrc !  0 zbits !  0 zbt !  1 zoff !
  begin
    0 (zgamma) 0 ?do (zbyte) zdst @ c!  1 zdst +! loop
    (zbit) 0= if
      0 (zgamma) (zcopy)
      (zbit)
    else 1 then
    begin dup while
      drop 1 (zgamma)
      dup 256 = if drop zdst @ exit then
      128 * (zbyte) 1 rshift - zoff !
      1 zbt !
      0 (zgamma) 1+ (zcopy)
      (zbit)
    repeat drop
  again ;
