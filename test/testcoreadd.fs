\ Core-additions tests. Requires tester.fs.

marker ---testcoreadd---

decimal

cr .( testcoreadd: arithmetic ) cr
T{ 9 2- -> 7 }T
T{ 0 2- -> -2 }T
T{ 7 sgn -> 1 }T
T{ -7 sgn -> -1 }T
T{ 0 sgn -> 0 }T
T{ $0a $05 catnib -> $a5 }T
T{ $0f $0f catnib -> $ff }T

cr .( testcoreadd: bit ops ) cr
variable bt
T{ 0   bt !  bt $0f sbit  bt @ -> $0f }T
T{ $ff bt !  bt $0f cbit  bt @ -> $f0 }T
T{ 0   bt !  -1 bt $03 fbit  bt @ -> $03 }T
T{ $ff bt !   0 bt $03 fbit  bt @ -> $fc }T

cr .( testcoreadd: stack ) cr
T{ 1 2 3 4 5 6 2rot -> 3 4 5 6 1 2 }T
T{ 1 2 3 0 roll -> 1 2 3 }T
T{ 1 2 3 1 roll -> 1 3 2 }T
T{ 1 2 3 2 roll -> 2 3 1 }T
T{ 1 2 3 4 5 6 5 roll -> 2 3 4 5 6 1 }T

cr .( testcoreadd: timing runs ) cr
1 sleep
1 ms
ticks 2drop

\ X816: no real-time clock - settime/time@/date@ went with the parked
\ clock.asm (there is no RTC on the machine; see X816_core doc/KERNEL.md
\ on the two clocks it has instead). sleep/ticks above now count VSYNC
\ frames via the kernel's IRQ_FRAMES, which these smoke lines still cover.

cr .( testcoreadd: 2 / unused / blank ) cr
T{ 2 -> 2 }T
T{ unused 0<> -> -1 }T \ unused is unsigned (ANS: u) and exceeds $7FFF here
create bbuf 8 allot
T{ bbuf 8 blank   bbuf c@  bbuf 7 + c@ -> 32 32 }T

cr .( testcoreadd: return stack 2>r 2r> 2r@ rdrop ) cr
: t2rat 2>r 2r@ 2r> ;
T{ 11 22 t2rat -> 11 22 11 22 }T
: t2rt 1 2 2>r 2r> ;
T{ t2rt -> 1 2 }T
: trdt 5 >r 6 >r rdrop r> ;
T{ trdt -> 5 }T

cr .( testcoreadd: cmove / cmove> ) cr
create cs 5 allot   create cd 5 allot
: initcs 5 0 do i 10 + cs i + c! loop ;
initcs   cs cd 5 cmove
T{ cd c@  cd 4 + c@ -> 10 14 }T
create ov 6 allot
: initov 6 0 do i ov i + c! loop ;
initov   ov  ov 1+  5 cmove>              \ overlap shift-up, high-first
T{ ov c@  ov 1+ c@  ov 5 + c@ -> 0 0 4 }T

cr .( testcoreadd: double words ) cr
T{ 5 s>d -> 5 0 }T
T{ 1234 0 d>s -> 1234 }T
T{ 10 0  20 0 d+ -> 30 0 }T
T{ 100 0  30 0 d- -> 70 0 }T
T{ 21 0 d2* -> 42 0 }T
T{ 84 0 d2/ -> 42 0 }T
T{ 0 0 d0= -> -1 }T    T{ 1 0 d0= -> 0 }T
T{ 0 0 d0< -> 0 }T     T{ -1 -1 d0< -> -1 }T
T{ 5 0  5 0 d= -> -1 }T    T{ 5 0  6 0 d= -> 0 }T
T{ 5 0  6 0 d< -> -1 }T    T{ 6 0  5 0 d< -> 0 }T
T{ -1 -1  1 0 d< -> -1 }T                 \ -1. < 1.
T{ 5 0  6 0 du< -> -1 }T
T{ 5 0  6 0 dmax -> 6 0 }T
T{ 5 0  6 0 dmin -> 5 0 }T
1234 0 d.                                 \ exercise pictured double output

cr .( testcoreadd: ud* / m*/ ) cr
T{ 100 0 7 ud* -> 700 0 }T
T{ 0 1 3 ud* -> 0 3 }T                     \ 65536*3 = 196608 = $30000
T{ 100 s>d 7 2 m*/ -> 350 s>d }T           \ 100*7/2
T{ 1000 s>d 3 4 m*/ -> 750 s>d }T          \ 3000/4
T{ -100 s>d 7 2 m*/ -> -350 s>d }T         \ sign of d
T{ 100 s>d -7 2 m*/ -> -350 s>d }T         \ sign of n1
T{ 100 s>d 7 -2 m*/ -> -350 s>d }T         \ sign of n2
T{ -100 s>d -7 2 m*/ -> 350 s>d }T         \ two negatives cancel
T{ 30000 s>d 20000 3 m*/ -> 200000000 s>d }T \ 600M/3 (double result)

\ X816: the i2c (SMC/RTC) and charset tests are parked with their words -
\ no SMC on the core, and the kernel console owns the character set.

cr .( testcoreadd: number output .r u.r d.r holds ? ) cr
T{ 0 0 <# s" xy" holds #> drop c@ -> 120 }T   \ 'x' first char
T{ 0 0 <# s" xy" holds #> nip -> 2 }T          \ length
cr T{ 5 3 .r -> }T                             \ these print; assert empty stack
T{ -5 3 .r -> }T
T{ 100 4 u.r -> }T
T{ 1000 0 6 d.r -> }T
create qv 0 ,
T{ 42 qv ! qv ? -> }T                          \ ? prints the stored cell
cr

cr .( testcoreadd: double/buffer defining words ) cr
3 4 2constant dcon
T{ dcon -> 3 4 }T
2variable dvar
T{ 5 6 dvar 2!  dvar 2@ -> 5 6 }T
80 buffer: dbuf
T{ dbuf dbuf = -> -1 }T                         \ same address each time
T{ 11 dbuf c!  dbuf c@ -> 11 }T
7 8 2value dval
T{ dval -> 7 8 }T
9 10 to dval                                    \ TO on a 2value (interpret)
T{ dval -> 9 10 }T
: setdv 100 200 to dval ;                       \ TO on a 2value (compile)
setdv
T{ dval -> 100 200 }T
: cdlit [ 21 22 ] 2literal ;
T{ cdlit -> 21 22 }T

cr .( testcoreadd: the helpdoc-promised core words ) cr
\ Each of these was ticked [x] in help/helpdoc with no definition
\ behind it until the 2026-08-04 probe pass; keep them honest.
T{ true -> -1 }T
T{ false -> 0 }T
T{ 5 0> -> true }T
T{ 0 0> -> false }T
T{ -5 0> -> false }T
T{ 3 cell+ -> 7 }T
T{ 3 cells -> 12 }T
T{ 3 char+ -> 4 }T
T{ 3 chars -> 3 }T
T{ align -> }T
T{ 42 aligned -> 42 }T
create (cb) 55 ,
T{ ' (cb) >body @ -> 55 }T                      \ >body = xt+7, CREATE shape
T{ s" X:deferred" environment? -> false }T

cr .( testcoreadd: defer and friends ) cr
defer (dtest)
T{ ' dup ' (dtest) defer! 7 (dtest) -> 7 7 }T
T{ ' (dtest) defer@ -> ' dup }T
T{ ' + is (dtest) 3 4 (dtest) -> 7 }T           \ IS, interpreting
T{ action-of (dtest) -> ' + }T                  \ ACTION-OF, interpreting
: (dset) ['] negate is (dtest) ;                \ IS, compiling
: (dget) action-of (dtest) ;                    \ ACTION-OF, compiling
(dset)
T{ 9 (dtest) -> -9 }T
T{ (dget) -> ' negate }T

cr .( testcoreadd: sm/rem - symmetric, all four sign cases ) cr
T{ 10 s>d 7 sm/rem -> 3 1 }T
T{ -10 s>d 7 sm/rem -> -3 -1 }T
T{ 10 s>d -7 sm/rem -> 3 -1 }T
T{ -10 s>d -7 sm/rem -> -3 1 }T

cr .( testcoreadd: the rest of LOGIC.TXT - boundary cases both ways ) cr
\ The equal case is the whole point of these, so every one is tested AT
\ the boundary as well as either side of it.
T{ -3 0<= -> true }T   T{ 0 0<= -> true }T   T{ 3 0<= -> false }T
T{ 3 0>= -> true }T    T{ 0 0>= -> true }T   T{ -3 0>= -> false }T
T{ 3 9 <= -> true }T   T{ 9 9 <= -> true }T  T{ 9 3 <= -> false }T
T{ 9 3 >= -> true }T   T{ 9 9 >= -> true }T  T{ 3 9 >= -> false }T
T{ 3 9 u<= -> true }T  T{ 9 9 u<= -> true }T T{ 9 3 u<= -> false }T
T{ 9 3 u>= -> true }T  T{ 9 9 u>= -> true }T T{ 3 9 u>= -> false }T
T{ 3 9 u<> -> true }T  T{ 9 9 u<> -> false }T
T{ 9 9 u= -> true }T   T{ 3 9 u= -> false }T
\ -1 is the largest UNSIGNED value: the unsigned words must not read it
\ as less than 1, which is exactly what the signed pair would do.
T{ -1 1 u>= -> true }T
T{ -1 1 >= -> false }T

cr .( testcoreadd: ut* ut/ - the triple intermediate under m*/ ) cr
\ The ARITHMETIC.TXT example, and the reason the pair exists: 1000000*3
\ overflows nothing here, but the triple is what lets m*/ divide before
\ rounding. Round-trip x*n/n = x is the property that catches a lost limb.
T{ 1000000. 3 ut* 7 ut/ -> 428571. }T
T{ 1000000. 3 ut* 3 ut/ -> 1000000. }T
T{ 123456789. 1000 ut* 1000 ut/ -> 123456789. }T
T{ 5. 4 ut* 1 ut/ -> 20. }T

cr .( testcoreadd: >number ) cr
T{ 0. s" 123" >number swap drop -> 123 0 0 }T   \ full convert, u' = 0
T{ 0. s" 12x4" >number swap drop -> 12 0 2 }T   \ stops at the x
T{ 0. s" ff" >number swap drop -> 0 0 2 }T      \ hex digits refuse in decimal
hex
T{ 0. s" ff" >number swap drop -> ff 0 0 }T     \ ...and convert in hex
decimal
T{ 5. s" 9" >number swap drop -> 59 0 0 }T      \ accumulates into ud1

cr .( testcoreadd: TYPE of an EMPTY string emits nothing ) cr
\ Regression. The stack guard branched to the emit path instead of the
\ count test, so TYPE always emitted one character - and a count of zero
\ then went to -1 and ran until it wrapped, spraying 2^32 bytes of memory
\ at the screen. Only ZERO-length strings were affected, which is why
\ every other test passed for months. Asserting the CURSOR DID NOT MOVE
\ is the check: a count that emitted anything moves the column, and a
\ count that ran away scrolls the screen and takes the pass banner with
\ it, so this fails loudly either way.
T{ pos pad 0 type pos = -> true }T
T{ pos s" " type pos = -> true }T
\ ...and a non-empty one still emits exactly its own length.
T{ pos s" abc" type pos swap - -> 3 }T

cr .( testcoreadd: the 16-bit words, W@ SW@ W! W, ) cr
\ A cell is 32 bits and a byte is 8; everything between was hand-rolled
\ until now, and the hand-rolling is where the bugs lived. These check
\ the two things that actually go wrong: how many bytes are touched, and
\ what happens to the top bit.
create wb 8 allot
$1234 wb w!  $5678 wb 2 + w!
T{ wb w@ -> $1234 }T
T{ wb 2 + w@ -> $5678 }T
\ TWO bytes, not four: the neighbour is untouched, and @ over the pair
\ sees both - which is the mistake W@ exists to prevent.
T{ wb @ -> $56781234 }T
T{ wb c@ wb 1+ c@ -> $34 $12 }T          \ little-endian, like everything here
\ W! stores only the low half of the cell it is given.
$deadbeef wb w!
T{ wb w@ -> $beef }T
T{ wb 2 + w@ -> $5678 }T                 \ ...and still does not spill

\ W@ zero-extends, SW@ sign-extends. $FFFF is 65535 one way and -1 the
\ other, and a file format decides which by what it wrote.
$ffff wb w!
T{ wb w@ -> 65535 }T
T{ wb sw@ -> -1 }T
$8000 wb w!
T{ wb w@ -> 32768 }T
T{ wb sw@ -> -32768 }T
$7fff wb w!
T{ wb w@ wb sw@ -> 32767 32767 }T        \ below the sign bit they agree

\ W, compiles two bytes, and HERE moves by two.
here $abcd w, here swap -
T{ -> 2 }T
T{ here 2 - w@ -> $abcd }T

cr .( testcoreadd: W>N and C>N, and what SPLIT really does ) cr
\ Sign extension for a value that did NOT come from memory. The bits
\ cannot say whether they are signed, so the word that knows says it.
T{ $ffff w>n -> -1 }T
T{ $8000 w>n -> -32768 }T
T{ $7fff w>n -> 32767 }T
T{ 0 w>n -> 0 }T
T{ $12345 w>n -> $2345 }T               \ only the low 16 bits are its business
T{ $ff c>n -> -1 }T
T{ $80 c>n -> -128 }T
T{ $7f c>n -> 127 }T
T{ $1ff c>n -> -1 }T
\ And the comparison that goes wrong without it, which is the reason
\ LOGIC has no 16-bit twins: $FFFF is a perfectly positive 65535 until
\ somebody says it was signed.
T{ $ffff 0< -> false }T
T{ $ffff w>n 0< -> true }T

\ SPLIT is two 16-BIT HALVES, low first - not the two bytes BIT.TXT used
\ to claim. base.fs and asm.fs have always used it that way: SPLIT NIP
\ is how a 24-bit address gives up its bank byte.
T{ $12345678 split -> $5678 $1234 }T
T{ $1234 split -> $1234 0 }T
T{ $123456 split nip -> $12 }T

cr .( testcoreadd ok ) cr

---testcoreadd---
