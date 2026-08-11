\ FLOAT - floating point through the SuperBasic engine (FLOAT.TXT).
\ SD card: INCLUDE FLOAT      extended set: FLOATX
\
\ IEEE-754 SINGLES, COMPUTED BY THE SAME CODE SUPERBASIC RUNS. The
\ arithmetic lives in fpengine.bin -- SuperBasic's software float
\ engine and transcendental library assembled unchanged into a 5 KB
\ blob (fpengine/ in this repo builds it) -- loaded once to $00:5000
\ and reached by JSL through a jump table. BASIC and Forth answering
\ the same bits for the same expression is the point; the 40x speed
\ over the old Forth-coded MFLPT series is the bonus. The card must
\ carry FPENGINE.BIN in its root.
\
\ A float is FOUR bytes now -- one cell -- on the same separate
\ 16-deep float stack as before. Everything the old file computed
\ with unpack/series loops is either one engine call or one cell
\ operation: the sign lives in bit 31, so FABS is a single AND.
\
\ Results truncate (no rounding), denormals flush to zero -- the
\ engine's documented behaviour, identical in BASIC. Overflow throws
\ -43, division by zero -42, a domain error (FSQRT of a negative,
\ FASIN of 2) -46.

decimal

\ --- the engine ---------------------------------------------------------------
$005000 constant (eng)          \ the jump table; entries every 3 bytes
$003020 constant (fa1)          \ operand 1 and the result
$003028 constant (fa2)          \ operand 2
$003040 constant (ferr)         \ 0 = ok, else the engine's error code

\ The ior is REPORTED, not discarded, and the two causes are named
\ separately. This used to print "missing from the card" for every
\ failure - and the failure it actually got was ior 3, KERR_NOSPACE:
\ no free file handle, because this open is the SIXTH in the suite's
\ chain - base, autorun, test, testfloa, float, then this. The file
\ was on the card the whole time, and the message sent the reader
\ looking for it. kfs.h says the same thing about NOSPACE being
\ mistaken for a full card; this is that mistake one layer up.
: (engload) ( -- )
  s" FPENGINE.BIN" r/o open-file            ( fileid ior )
  ?dup if
     nip cr ." FPENGINE.BIN not opened, ior " dup .
     3 = if ." -- no free file handle; too deep in nested includes"
          else ." -- missing from the card, see HELP FLOAT" then cr
     -38 throw then
  >r (eng) $1800 r@ read-file throw drop
  r> close-file throw ;
(engload)

\ The fifteen entries, thinnest possible: the operands are already in
\ the ABI block when these run, and the wrapper inside the blob saves
\ and restores everything it touches.
code (e:f+)    $005000 jsl, rtl, end-code
code (e:f-)    $005003 jsl, rtl, end-code
code (e:f*)    $005006 jsl, rtl, end-code
code (e:f/)    $005009 jsl, rtl, end-code
code (e:itof)  $00500c jsl, rtl, end-code
code (e:ftoi)  $00500f jsl, rtl, end-code
code (e:fsqrt) $005012 jsl, rtl, end-code
code (e:fln)   $005015 jsl, rtl, end-code
code (e:fexp)  $005018 jsl, rtl, end-code
code (e:fsin)  $00501b jsl, rtl, end-code
code (e:fcos)  $00501e jsl, rtl, end-code
code (e:ftan)  $005021 jsl, rtl, end-code
code (e:fatan) $005024 jsl, rtl, end-code
code (e:fasin) $005027 jsl, rtl, end-code
code (e:facos) $00502a jsl, rtl, end-code

: (fe?) ( -- )                  \ surface an engine error as a THROW
  (ferr) c@ ?dup if
    dup 15 = if drop -42 throw then
    dup 13 = if drop -43 throw then
    drop -46 throw then ;

\ --- float stack: 16 x 4 bytes, growing downward -------------------------------
64 buffer: fstk   fstk 64 + constant fstk0   variable fsp
: fclear ( -- ) fstk0 fsp ! ;  fclear
: fdepth ( -- n ) fstk0 fsp @ - 4 / ;
: fdrop ( F: r -- ) 4 fsp +! ;
: (fpush) ( -- addr ) fsp @ 4 - dup fsp ! ;

\ --- memory access, stack shuffles ---------------------------------------------
: f@ ( f-addr -- ) ( F: -- r )  @ (fpush) ! ;
: f! ( f-addr -- ) ( F: r -- )  fsp @ @ swap !  fdrop ;
: fdup  ( F: r -- r r )        fsp @ @ (fpush) ! ;
: fover ( F: a b -- a b a )    fsp @ 4 + @ (fpush) ! ;
: fswap ( F: a b -- b a )
  fsp @ @  fsp @ 4 + @  fsp @ !  fsp @ 4 + ! ;
: fnip  ( F: a b -- b )        fswap fdrop ;

\ --- marshalling ----------------------------------------------------------------
: (f1>) ( F: r -- r ) fsp @ @ (fa1) ! ;                 \ top -> A1
: (f2>) ( F: a b -- a b ) fsp @ @ (fa2) ! fsp @ 4 + @ (fa1) ! ;
: (f<)  ( F: a -- r ) (fa1) @ fsp @ ! (fe?) ;           \ A1 over the top
: (f2<) ( F: a b -- r ) fdrop (f<) ;

\ --- arithmetic ------------------------------------------------------------------
: f+ ( F: a b -- a+b ) (f2>) (e:f+) (f2<) ;
: f- ( F: a b -- a-b ) (f2>) (e:f-) (f2<) ;
: f* ( F: a b -- a*b ) (f2>) (e:f*) (f2<) ;
: f/ ( F: a b -- a/b ) (f2>) (e:f/) (f2<) ;

\ --- sign and tests: the cell IS the IEEE pattern --------------------------------
: fnegate ( F: r -- -r )
  fsp @ @ dup $7fffffff and if $80000000 xor then fsp @ ! ;
: fabs ( F: r -- |r| ) fsp @ @ $7fffffff and fsp @ ! ;
: f0= ( -- flag ) ( F: r -- ) fsp @ @ $7fffffff and 0=  fdrop ;
: f0< ( -- flag ) ( F: r -- )
  fsp @ @ dup 0< swap $7fffffff and 0<> and  fdrop ;
: f<  ( -- flag ) ( F: r1 r2 -- ) f- f0< ;
: f=  ( -- flag ) ( F: r1 r2 -- ) f- f0= ;
: f<> ( -- flag ) ( F: r1 r2 -- ) f= 0= ;
: f>  ( -- flag ) ( F: r1 r2 -- ) fswap f< ;
: f0<> ( -- flag ) ( F: r -- ) f0= 0= ;
: f0> ( -- flag ) ( F: r -- ) fnegate f0< ;
: fmax ( F: a b -- max ) fover fover f< if fnip else fdrop then ;
: fmin ( F: a b -- min ) fover fover f< if fdrop else fnip then ;

\ --- conversion --------------------------------------------------------------------
: s>f ( n -- ) ( F: -- r ) (fa1) ! (e:itof) (fpush) (fa1) @ swap ! (fe?) ;
: f>s ( -- n ) ( F: r -- ) (f1>) fdrop (e:ftoi) (fa1) @ (fe?) ;

\ --- transcendentals: one engine call each ------------------------------------------
: fsqrt ( F: r -- root )  (f1>) (e:fsqrt) (f<) ;
: fln   ( F: r -- ln r )  (f1>) (e:fln)   (f<) ;
: fexp  ( F: r -- e^r )   (f1>) (e:fexp)  (f<) ;
: fsin  ( F: r -- sin r ) (f1>) (e:fsin)  (f<) ;
: fcos  ( F: r -- cos r ) (f1>) (e:fcos)  (f<) ;
: ftan  ( F: r -- tan r ) (f1>) (e:ftan)  (f<) ;
: fatan ( F: r -- atan r) (f1>) (e:fatan) (f<) ;
: fasin ( F: r -- asin r) (f1>) (e:fasin) (f<) ;
: facos ( F: r -- acos r) (f1>) (e:facos) (f<) ;
\ AN INTEGRAL EXPONENT MULTIPLIES INSTEAD. BASIC's ^ has taken this
\ road since always (Q_FP_POW_INT: repeated multiply, exponent in a
\ 16-bit register), and it is why K^2 there is one engine call where
\ exp(2 ln K) is two series. The same test here -- y positive,
\ integral, under 65536 -- takes the same road, so the two languages'
\ power operators cost the same for the powers programs actually
\ write. Everything else still goes through fln/fexp, x > 0 as before.
: (fipow) ( n -- ) ( F: x -- x^n )     \ n >= 1
  fdup  1 ?do fover f* loop  fnip ;
: fpow ( F: x y -- x^y )
  fdup f0> if
    fdup 65536 s>f f< if
      fdup f>s                         ( n ) ( F: x y )
      dup s>f fover f= if fdrop (fipow) exit then
      drop
    then
  then
  fswap fln f* fexp ;
: f** fpow ;

\ --- defining words ------------------------------------------------------------------
: fvariable ( "name" -- ) create 4 allot ;
: fconstant ( "name" -- ) ( F: r -- ) create here f! 4 allot does> f@ ;

\ --- integer square root (no float stack use) -----------------------------------------
variable sq-n  variable sq-r  variable sq-b
: isqrt ( u -- root )
  sq-n !  0 sq-r !  16384 sq-b !
  begin sq-b @ while
    sq-r @ sq-b @ +  dup sq-n @ swap < 0= if
      sq-n @ swap - sq-n !  sq-r @ 2/ sq-b @ + sq-r !
    else drop sq-r @ 2/ sq-r ! then
    sq-b @ 2 rshift sq-b !
  repeat sq-r @ ;

\ --- string -> float --------------------------------------------------------------------
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

\ --- constants ---------------------------------------------------------------------------
\ Kept for compatibility with FLOATX and user code; the engine carries
\ its own copies of the ones its series need.
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

: (fround) ( -- n ) ( F: r -- )        \ nearest integer, halves away from zero
  fdup f0< if fhalfc f@ f- else fhalfc f@ f+ then f>s ;

\ --- output --------------------------------------------------------------------------------
\ Scaled into [1e8, 1e9) so every digit comes out of ONE integer
\ conversion, exactly as before; only the arithmetic underneath changed.
\ IEEE singles carry about seven significant digits against MFLPT's
\ nine, so the ninth printed digit is the format's noise floor.
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

\ --- float literals: hook the interpreter's not-found vector ----------------------------
\ Chains to the handler it replaced (the core's (dnum) double-literal
\ parser), and hands trailing-dot tokens straight back to it: 12. stays
\ a DOUBLE, 12.0 / 12.12 / 1e5 are floats.
'notfound @ constant (fnf)
: (flit) ( F: -- r )  r> 1+ dup 4 + 1- >r  f@ ;
: fliteral ( F: r -- ) ['] (flit) compile,  here 4 allot f! ;
: (fnum) ( c-addr u -- )
  2dup + 1- c@ '.' = if (fnf) execute exit then
  2dup >float if 2drop state @ if fliteral then exit then
  (fnf) execute ;
' (fnum) 'notfound !
