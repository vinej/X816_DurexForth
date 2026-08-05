\ FLOAT - floating point in software (FLOAT.TXT).
\ Cart: NEEDS FLOAT      SD card: INCLUDE FLOAT      extended set: FLOATX
\
\ 5-byte MFLPT floats (~9 digits) on a separate 16-deep float stack.
\
\ THE X16 VERSION OF THIS FILE CALLED THE ROM. Every operation was a BCALL
\ into the X16 Math Library's jump table in ROM bank 4. There is no such ROM
\ on the X816 - there is no ROM at all past the boot page - so the arithmetic
\ is done here, in Forth, on the same packed format.
\
\ THE FORMAT, and why it suits this machine. A float is five bytes: an
\ exponent (excess-128, and 0 means the value is zero), then four mantissa
\ bytes, big-endian, normalised so the leading 1 is implied - the bit it
\ would occupy carries the SIGN instead. Restoring that 1 gives a 32-bit
\ mantissa that fills a cell exactly, and the value is
\
\       m * 2^(e-160)          with m in [2^31, 2^32)
\
\ so the whole of MFLPT arithmetic is 32-bit integer work. UM* gives the
\ 64-bit product two mantissas need, and UM/MOD the 64-by-32 divide, both as
\ primitives - which is why multiply and divide below are a dozen lines
\ rather than a page.
\
\ Rounding is TRUNCATION, not round-to-nearest. Results are good to about
\ nine digits, and values that are exact in binary (0.5, 2.5, 100.0) stay
\ exact, which is what testfloat's F= checks rely on.
\
\ After loading, float literals work: 3.14  -2.5e-3  1e6

decimal

\ --- float stack: 16 x 5 bytes, growing downward -----------------------------
80 buffer: fstk   fstk 80 + constant fstk0   variable fsp
create ftmp 5 allot
: fclear ( -- ) fstk0 fsp ! ;  fclear
: fdepth ( -- n ) fstk0 fsp @ - 5 / ;
: fdrop ( F: r -- ) 5 fsp +! ;
: (fpush) ( -- addr ) fsp @ 5 - dup fsp ! ;

\ --- memory access, stack shuffles -------------------------------------------
: f@ ( f-addr -- ) ( F: -- r )  (fpush) 5 cmove ;
: f! ( f-addr -- ) ( F: r -- )  fsp @ swap 5 cmove fdrop ;
: fdup  ( F: r -- r r )        fsp @ (fpush) 5 cmove ;
: fover ( F: a b -- a b a )    fsp @ 5 + (fpush) 5 cmove ;
: fswap ( F: a b -- b a )
  fsp @ ftmp 5 cmove  fsp @ 5 + fsp @ 5 cmove  ftmp fsp @ 5 + 5 cmove ;
: fnip  ( F: a b -- b )        fswap fdrop ;

\ === unpacked form ===========================================================
\ ( m e s ): mantissa with its implied 1 restored, exponent byte, sign 1 or 0.
\ Zero is m = 0, and the pack below writes that back as an exponent byte of 0.
variable (pm)  variable (pe)  variable (ps)

: (fu) ( f-addr -- m e s )
  dup c@ 0= if drop 0 0 0 exit then
  dup 1+ c@ 128 or 24 lshift
  over 2 + c@ 16 lshift or
  over 3 + c@ 8 lshift or
  over 4 + c@ or
  over c@
  rot 1+ c@ 128 and 0<> 1 and ;

\ Shift the mantissa up until its top bit is set, paying for it in exponent.
\ Running the exponent down to zero IS underflow, and the answer is zero:
\ this format has no subnormals to fall back on.
: (fnorm) ( -- )
  (pm) @ 0= if 0 (pe) ! exit then
  begin (pm) @ 0< 0= while
    (pm) @ 2* (pm) !
    (pe) @ 1- (pe) !
    (pe) @ 0<= if 0 (pm) ! 0 (pe) ! exit then
  repeat ;

: (fp) ( m e s f-addr -- )
  >r (ps) ! (pe) ! (pm) ! (fnorm)
  (pm) @ 0= if
    0 r@ c! 0 r@ 1+ c! 0 r@ 2 + c! 0 r@ 3 + c! 0 r@ 4 + c!
    r> drop exit then
  (pe) @ 255 > if 255 (pe) ! -1 (pm) ! then     \ overflow: the largest finite
  (pe) @ r@ c!
  (pm) @ 24 rshift 127 and  (ps) @ if 128 or then  r@ 1+ c!
  (pm) @ 16 rshift 255 and r@ 2 + c!
  (pm) @ 8 rshift 255 and r@ 3 + c!
  (pm) @ 255 and r@ 4 + c!
  r> drop ;

\ --- conversion ---------------------------------------------------------------
\ NEGATE, not ABS: the most negative cell has no positive counterpart, and its
\ negation already carries the right bits to be read as an unsigned magnitude.
: s>f ( n -- ) ( F: -- r )
  dup 0< 1 and                          ( n s )
  swap dup 0< if negate then            ( s u )
  swap                                  ( u s )
  160 swap                              ( u 160 s )
  (fpush) (fp) ;

: f>s ( -- n ) ( F: r -- )
  fsp @ (fu) fdrop                      ( m e s )
  >r                                    ( m e )
  dup 0= if 2drop r> drop 0 exit then
  160 swap -                            ( m shift )
  dup 0< if negate lshift                       \ e > 160: too big to hold
  else dup 32 < if rshift else 2drop 0 then then
  r> if negate then ;

\ === arithmetic ===============================================================
variable fm1 variable fe1 variable fs1
variable fm2 variable fe2 variable fs2
: (f2) ( -- )
  fsp @ (fu) fs2 ! fe2 ! fm2 !
  fsp @ 5 + (fu) fs1 ! fe1 ! fm1 ! ;
: (f2!) ( m e s -- ) fdrop fsp @ (fp) ;

\ The 64-bit product of two normalised mantissas lands in [2^62, 2^64), so its
\ high cell is either the answer already or one shift short of it.
: f* ( F: a b -- a*b )
  (f2)
  fm1 @ 0= fm2 @ 0= or if 0 0 0 (f2!) exit then
  fm1 @ fm2 @ um*                       ( lo hi )
  dup 0< if
    nip fe1 @ fe2 @ + 128 -
  else
    swap 31 rshift swap 2* or
    fe1 @ fe2 @ + 129 -
  then
  fs1 @ fs2 @ xor (f2!) ;

\ a/b is in (1/2, 2), so m1 shifted up 31 places divided by m2 fits in a cell -
\ which is the whole reason the dividend is built as a double.
: f/ ( F: a b -- a/b )
  (f2)
  fm2 @ 0= if 0 0 0 (f2!) exit then     \ division by zero is not trapped
  fm1 @ 0= if 0 0 0 (f2!) exit then
  fm1 @ 31 lshift  fm1 @ 1 rshift       ( lo hi )
  fm2 @ um/mod nip                      ( q )
  dup 0< if fe1 @ fe2 @ - 129 +
  else 2* fe1 @ fe2 @ - 128 + then
  fs1 @ fs2 @ xor (f2!) ;

\ Line the exponents up, then add or subtract magnitudes. A carry out of the
\ top costs one shift; a subtraction that cancels leading bits is put right by
\ (fnorm) inside the pack.
: f+ ( F: a b -- a+b )
  (f2)
  fm1 @ 0= if fm2 @ fe2 @ fs2 @ (f2!) exit then
  fm2 @ 0= if fm1 @ fe1 @ fs1 @ (f2!) exit then
  fe1 @ fe2 @ -                         ( d )
  dup 0> if
    dup 32 >= if drop fm1 @ fe1 @ fs1 @ (f2!) exit then
    fm2 @ swap rshift fm2 !  fe1 @ fe2 !
  else dup 0< if
    negate dup 32 >= if drop fm2 @ fe2 @ fs2 @ (f2!) exit then
    fm1 @ swap rshift fm1 !  fe2 @ fe1 !
  else drop then then
  fs1 @ fs2 @ = if
    fm1 @ fm2 @ +
    dup fm1 @ u< if 1 rshift 1 31 lshift or fe1 @ 1+ else fe1 @ then
    fs1 @ (f2!)
  else
    fm1 @ fm2 @ u< if fm2 @ fm1 @ - fe1 @ fs2 @
    else fm1 @ fm2 @ - fe1 @ fs1 @ then
    (f2!)
  then ;

\ --- sign ops on the packed top (byte 0 = exponent, byte 1 bit 7 = sign) -----
: fnegate ( F: r -- -r )
  fsp @ c@ if fsp @ 1+ dup c@ 128 xor swap c! then ;
: fabs ( F: r -- |r| ) fsp @ 1+ dup c@ 127 and swap c! ;

: f- ( F: a b -- a-b ) fnegate f+ ;

\ --- tests and comparison -----------------------------------------------------
: f0= ( -- flag ) ( F: r -- ) fsp @ c@ 0=  fdrop ;
: f0< ( -- flag ) ( F: r -- )
  fsp @ c@ 0<>  fsp @ 1+ c@ 128 and 0<> and  fdrop ;
: f<  ( -- flag ) ( F: r1 r2 -- ) f- f0< ;
: f=  ( -- flag ) ( F: r1 r2 -- ) f- f0= ;
: f<> ( -- flag ) ( F: r1 r2 -- ) f= 0= ;
: f>  ( -- flag ) ( F: r1 r2 -- ) fswap f< ;
: f0<> ( -- flag ) ( F: r -- ) f0= 0= ;
: f0> ( -- flag ) ( F: r -- ) fnegate f0< ;
: fmax ( F: a b -- max ) fover fover f< if fnip else fdrop then ;
: fmin ( F: a b -- min ) fover fover f< if fdrop else fnip then ;

\ --- defining words -----------------------------------------------------------
: fvariable ( "name" -- ) create 5 allot ;
: fconstant ( "name" -- ) ( F: r -- ) create here f! 5 allot does> f@ ;

\ --- integer square root (no float stack use) ---------------------------------
variable sq-n  variable sq-r  variable sq-b
: isqrt ( u -- root )
  sq-n !  0 sq-r !  16384 sq-b !
  begin sq-b @ while
    sq-r @ sq-b @ +  dup sq-n @ swap < 0= if
      sq-n @ swap - sq-n !  sq-r @ 2/ sq-b @ + sq-r !
    else drop sq-r @ 2/ sq-r ! then
    sq-b @ 2 rshift sq-b !
  repeat sq-r @ ;

\ --- string -> float ----------------------------------------------------------
fvariable f-ten   10 s>f f-ten f!
: (f10^*) ( n -- ) ( F: r -- r*10^n )
  dup 0< if negate 0 ?do f-ten f@ f/ loop
  else 0 ?do f-ten f@ f* loop then ;

variable >fa  variable >fn  variable >fok  variable >fdp  variable >fng
: (f>f+) ( -- ) 1 >fa +!  -1 >fn +! ;
: (fdigit) ( -- n true | false )       \ consume one digit if present
  >fn @ if >fa @ c@ dup '0' '9' 1+ within
    if '0' - (f>f+) -1 exit then drop then 0 ;
: (fdigits) ( -- ) ( F: acc -- acc' )  \ digits into the float accumulator
  begin (fdigit) while
    f-ten f@ f* s>f f+  1 >fok !
  repeat ;
: (fchar?) ( c -- flag )               \ consume the char if it is next
  >fn @ if >fa @ c@ = if (f>f+) -1 exit then else drop then 0 ;

: >float ( c-addr u -- flag ) ( F: -- r | )
  >fn ! >fa !  0 >fok !  0 >fdp !  0 >fng !
  0 s>f
  '-' (fchar?) if 1 >fng ! else '+' (fchar?) drop then
  (fdigits)
  '.' (fchar?) if
    >fn @ (fdigits) >fn @ - >fdp ! then       \ count of fraction digits
  0
  'e' (fchar?) 'E' (fchar?) or >fok @ and if
    1 swap                                    \ ( esgn e=0 )
    '-' (fchar?) if swap negate swap else '+' (fchar?) drop then
    0 >fok !
    begin (fdigit) while swap 10 * + 1 >fok ! repeat
    * then                                    \ e * esgn
  >fn @ 0<>  >fok @ 0=  or if drop fdrop 0 exit then
  >fdp @ - (f10^*)
  >fng @ if fnegate then  -1 ;

\ --- constants ------------------------------------------------------------------
\ Parsed rather than computed: the series below need them before there is
\ anything left to compute them with.
: (fk) ( c-addr u -- ) ( F: -- r ) >float drop ;
fvariable fln2c    s" 0.6931471805599453"  (fk) fln2c f!
fvariable fpic     s" 3.1415926535897932"  (fk) fpic f!
fvariable fpi2c    s" 1.5707963267948966"  (fk) fpi2c f!
fvariable fpi4c    s" 0.7853981633974483"  (fk) fpi4c f!
fvariable f2pic    s" 6.2831853071795865"  (fk) f2pic f!
fvariable ftan8c   s" 0.4142135623730950"  (fk) ftan8c f!
fvariable frt2c    s" 0.7071067811865476"  (fk) frt2c f!
fvariable fhalfc   s" 0.5"                 (fk) fhalfc f!
fvariable f1e8c    s" 100000000.0"         (fk) f1e8c f!
fvariable f1e9c    s" 1000000000.0"        (fk) f1e9c f!

\ --- helpers the series need ----------------------------------------------------
: (fround) ( -- n ) ( F: r -- )        \ nearest integer, halves away from zero
  fdup f0< if fhalfc f@ f- else fhalfc f@ f+ then f>s ;

\ Multiplying by a power of two is a walk along the exponent byte: no
\ arithmetic, and exact.
: (fscale2) ( n -- ) ( F: r -- r*2^n )
  fsp @ c@ 0= if drop exit then
  fsp @ c@ +
  dup 1 < if drop 0 0 0 fsp @ (fp) exit then
  dup 255 > if drop 255 then
  fsp @ c! ;

\ --- square root: Newton, from a guess made by halving the exponent -----------
\ A value with exponent byte e lies in [2^(e-129), 2^(e-128)), so 2^((e-128)/2)
\ is never more than a factor of sqrt(2) out - which is what Newton needs to
\ land in seven steps. Getting this exponent wrong does not fail loudly: the
\ iteration still converges, just not within the loop, and sqrt(16) comes out
\ around 130000.
: (fsqrt0) ( F: r -- r guess )
  fsp @ c@ 128 - 2 / 129 +              ( E )
  1 31 lshift swap 0
  (fpush) (fp) ;
: fsqrt ( F: r -- sqrt r )
  fdup f0= if exit then
  fdup f0< if fdrop 0 s>f exit then
  (fsqrt0)
  7 0 do fover fover f/ f+ 2 s>f f/ loop
  fnip ;

\ --- natural log ----------------------------------------------------------------
\ ln(m * 2^k) = k*ln2 + ln m. The mantissa is pulled out by overwriting the
\ exponent byte with 128, which leaves the value in [0.5, 1) exactly; doubling
\ the small half moves it into [0.7071, 1.4142), which holds
\ z = (m-1)/(m+1) under 0.172 and keeps the series short.
fvariable fla fvariable flt fvariable flz2
: (flnser) ( F: z -- twice the series z + z^3/3 + z^5/5 ... )
  fdup fdup f* flz2 f!
  fdup fla f!
  flt f!
  9 0 do
    flt f@ flz2 f@ f* flt f!
    fla f@ flt f@ i 2* 3 + s>f f/ f+ fla f!
  loop
  fla f@ 2 s>f f* ;

: fln ( F: r -- ln r )
  fdup f0= if fdrop 0 s>f exit then
  fdup f0< if fabs then
  fsp @ c@ 128 -                        ( k )
  128 fsp @ c!
  fdup frt2c f@ f< if 2 s>f f* 1- then
  fdup 1 s>f f- fswap 1 s>f f+ f/
  (flnser)
  s>f fln2c f@ f* f+ ;

\ --- exponential -----------------------------------------------------------------
\ e^r = 2^i * e^g, with i the nearest integer to r/ln2 and |g| <= ln2/2, which
\ the Taylor series eats in a dozen terms. The 2^i is an exponent-byte add.
fvariable fea fvariable fet fvariable feg
: (fexpser) ( F: g -- e^g )
  feg f!
  1 s>f fea f!  1 s>f fet f!
  13 1 do
    fet f@ feg f@ f* i s>f f/ fet f!
    fea f@ fet f@ f+ fea f!
  loop
  fea f@ ;

: fexp ( F: r -- e^r )
  fdup fln2c f@ f/ (fround)             ( i )
  dup s>f fln2c f@ f* f-
  (fexpser)
  (fscale2) ;

: fpow ( F: x y -- x^y ) fswap fln f* fexp ;      \ x > 0
: f** fpow ;

\ --- trigonometry -----------------------------------------------------------------
\ Reduced mod 2pi, then folded into [-pi/2, pi/2], where fifteen terms of the
\ Taylor series reach the last bit this format carries.
fvariable fsa fvariable fst fvariable fsx
: (fsinser) ( F: x -- sin x )
  fdup fsa f!
  fdup fst f!
  fdup f* fsx f!
  8 1 do
    fst f@ fsx f@ f* fnegate  i 2* dup 1+ * s>f f/  fst f!
    fsa f@ fst f@ f+ fsa f!
  loop
  fsa f@ ;

: fsin ( F: r -- sin r )
  fdup f2pic f@ f/ (fround) s>f f2pic f@ f* f-
  fdup fpi2c f@ f> if fpic f@ fswap f- then
  fdup fpi2c f@ fnegate f< if fpic f@ fnegate fswap f- then
  (fsinser) ;

: fcos ( F: r -- cos r ) fpi2c f@ f+ fsin ;
: ftan ( F: r -- tan r ) fdup fsin fswap fcos f/ ;

\ --- arctangent ---------------------------------------------------------------------
\ Two reductions before the series: |x| > 1 becomes 1/x, and whatever is left
\ above tan(pi/8) becomes (x-1)/(x+1). That caps |z| at 0.4143, where eleven
\ terms hold to nine digits.
fvariable faa fvariable fat fvariable faz
variable fainv variable faoct variable fasgn
: (fatanser) ( F: z -- atan z )
  fdup faa f!
  fdup fat f!
  fdup f* faz f!
  12 1 do
    fat f@ faz f@ f* fnegate fat f!
    faa f@ fat f@ i 2* 1+ s>f f/ f+ faa f!
  loop
  faa f@ ;

: fatan ( F: r -- atan r )
  fdup f0= if exit then
  fdup f0< 1 and fasgn !
  fabs
  0 fainv !  0 faoct !
  fdup 1 s>f f> if 1 s>f fswap f/ 1 fainv ! then
  fdup ftan8c f@ f> if
    fdup 1 s>f f- fswap 1 s>f f+ f/ 1 faoct ! then
  (fatanser)
  faoct @ if fpi4c f@ f+ then
  fainv @ if fpi2c f@ fswap f- then
  fasgn @ if fnegate then ;

\ --- output -------------------------------------------------------------------------
\ Scaled into [1e8, 1e9) so every digit comes out of ONE integer conversion -
\ 999999999 still fits a cell - and the point is then placed by counting
\ rather than by more arithmetic.
create f#buf 12 allot
variable f#d  variable f#p  variable f#last
: (f#split) ( n -- )                   \ nine digits, most significant first
  9 0 do
    dup 10 mod '0' + f#buf 8 i - + c!
    10 /
  loop drop ;
: (f#trim) ( -- )                      \ last digit worth printing
  8 f#last !
  begin f#last @ 0> f#buf f#last @ + c@ '0' = and while
    -1 f#last +! repeat ;
: (f#emit) ( from to -- ) 1+ swap ?do f#buf i + c@ emit loop ;

: f. ( F: r -- )
  fdup f0= if fdrop '0' emit space exit then
  fdup f0< if '-' emit fabs then
  0 f#d !
  begin fdup f1e9c f@ f< 0= while 10 s>f f/ 1 f#d +! repeat
  begin fdup f1e8c f@ f< while 10 s>f f* -1 f#d +! repeat
  \ Round the ninth digit rather than truncating it: the arithmetic itself
  \ truncates, so 1/3 would otherwise print as 0.333333332. Rounding can
  \ carry into a tenth digit, which is one more decade.
  fhalfc f@ f+ f>s
  dup 1000000000 >= if 10 / 1 f#d +! then
  (f#split) (f#trim)
  f#d @ 9 + f#p !                      \ digits that belong before the point
  f#p @ 0> f#p @ 10 <= and if
    0 f#p @ 1- (f#emit)
    f#last @ f#p @ >= if '.' emit f#p @ f#last @ (f#emit) then
  else f#p @ 0<= f#p @ -3 > and if
    '0' emit '.' emit
    f#p @ negate 0 ?do '0' emit loop
    0 f#last @ (f#emit)
  else
    0 0 (f#emit) '.' emit
    f#last @ 0> if 1 f#last @ (f#emit) else '0' emit then
    'e' emit f#p @ 1- dup 0< if '-' emit negate else '+' emit then 0 .r
  then then
  space ;

\ --- float literals: hook the interpreter's not-found vector -------------------
\ Chains to the handler it replaced (the core's (dnum) double-literal parser),
\ and hands trailing-dot tokens straight back to it: 12. stays a DOUBLE,
\ 12.0 / 12.12 / 1e5 are floats.
'notfound @ constant (fnf)
: (flit) ( F: -- r )  r> 1+ dup 5 + 1- >r  f@ ;
: fliteral ( F: r -- ) ['] (flit) compile,  here 5 allot f! ;
: (fnum) ( c-addr u -- )
  2dup + 1- c@ '.' = if (fnf) execute exit then
  2dup >float if 2drop state @ if fliteral then exit then
  (fnf) execute ;
' (fnum) 'notfound !
