( asm local labels.

n @: = label n
n @@ = branch to label n

...where n is in range[0, ff]

relative branches are resolved by
end-code - this allows for mixed
forward and backward references,
but it is not possible to branch
over end-code.

-- example --
code checkers
7f lda,# 0 ldy,# 1 @:
400 sta,y 500 sta,y
600 sta,y 700 sta,y
dey, 1 @@ bne, rts, end-code )

( refs and locs are arrays of
2-byte address + 1-byte index )
variable refs 8 3 * 2 - allot \ 8 refs
variable locs 5 3 * 2 - allot \ 5 locs
variable locp variable refp

locs locp ! refs refp ! \ init

\ reference
( X816 stage B: HERE is a flat cell; the 2-byte entry slots are written
  with w! and read back through cw@, which re-attaches THIS bank - a
  16-bit-masked address handed to C! would write into bank 0. )
: w@ ( a -- x ) @ $ffff and ;
: cw@ ( a -- flataddr ) w@ $10000 or ;
: @@ ( index -- dummy )
here refp @ w!
2 refp +! refp @ c! 1 refp +! 0 ;
\ label
: @: ( index -- )
here locp @ w!
2 locp +! locp @ c! 1 locp +! ;
: end-code
locs begin dup locp @ < while
refs begin dup refp @ < while
over 2+ c@ over 2+ c@ = if
over cw@ over cw@ 2+ - over cw@ 1+ c!
then 3 + repeat drop 3 + repeat drop
\ reset
locs locp ! refs refp ! ;

hide locs
hide locp
hide refs
hide refp
