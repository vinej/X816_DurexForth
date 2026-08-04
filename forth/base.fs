: 2+ 1+ 1+ ;
: 2! swap over ! 4 + ! ;
: 2@ dup 4 + @ swap @ ;
: jmp, 4c c, ;
: postpone bl word dup find ?dup 0= if
count notfound then
rot drop -1 = if [ ' literal compile,
' compile, literal ] then compile,
; immediate
: ['] ' postpone literal ; immediate
: [char] char postpone literal
; immediate
: else jmp, here 0 w,
swap here swap w! ; immediate
: until postpone 0branch w, ; immediate
: again jmp, w, ; immediate
: recurse
latestxt compile, ; immediate

: \ source >in ! drop ; immediate
: <> = 0= ;
: u> swap u< ;
: 0<> 0= 0= ;

: parse >r source >in @ /string
over swap begin dup while over c@ r@ <>
while 1 /string repeat then r> drop >r
over - dup r> if 1+ then >in +! ;

: ( source-id 0= if ')' parse drop drop
else begin >in @ ')' parse nip >in @ rot
- = while refill drop repeat then ;
immediate

( X816 stage B: return-ADDRESS juggling uses rw>/w>r - a >R cell is 32
  bits but an rts consumes 16. )
: lits ( -- addr len )
rw> 1+ count 2dup + 1- w>r ;

( "0 to foo" sets value foo to 0 )
: (to) >r split $ff and r@ 3 + c! r> w! ;
\ TO on a VALUE - code word, first byte $a9 - patches its immediates.
\ TO on a 2VALUE - create/does> word, first byte $20 - stores the double
\ at the data field xt+5 with 2!.
: to ' dup c@ $20 = if
5 + state c@ if postpone literal postpone 2! exit then 2!
else
1+ state c@ if postpone literal postpone (to) exit then (to)
then ; immediate

: allot ( n -- ) here + to here ;

: s" ( -- addr len )
'"' parse state @ if postpone lits
dup c, tuck here swap move allot
then ; immediate

: ." postpone s" postpone type
; immediate
: .( ')' parse type ; immediate

( "xxx" string literals, same semantics
  as S". The kernel routes undefined
  tokens starting with " through the
  'QUOTE vector - defined words win, so
  S" ." ABORT" etc. are unaffected. An
  unterminated string errors like an
  unknown word. ROLLBACK: delete this
  block + the QUOTE_VEC check in
  interpreter.asm. )
: (qlit) ( ca u -- ca u | )
state @ if postpone lits
dup c, tuck here swap move allot then ;
: (quote) ( ca u -- ca u | )
dup 1 > if 2dup + 1- c@ '"' = if
1- 1- swap 1+ swap (qlit) exit
then then
'"' parse + source + over = if
drop notfound then
rot 1+ tuck - rot drop (qlit) ;
' (quote) 'quote !
.( compile base..)

: case 0 ; immediate
: (of) over = if drop r> 2+ >r exit
then branch ;
: of postpone (of) here 0 , ; immediate
: endof postpone else ; immediate
: endcase postpone drop
begin ?dup while postpone then
repeat ; immediate

( dodoes words contain:
 1. jsr dodoes
 2. two-byte code pointer. default: rts
 3. variable length data )
here 60 c, ( rts )
: create
header postpone dodoes literal w, ;
: does> rw> 1+ latest >xt 3 + w! ;

.( asm..)
parse-name asm included

: -rot rot rot ;
: roll ( xu..x0 u -- x{u-1}..x0 xu )
?dup if swap >r 1- recurse r> swap then ;

( creates value that is fast to read
  but can only be rewritten by "to".
   0 value foo
   foo . \ prints 0
   1 to foo
   foo . \ prints 1 )
: value ( n -- )
( TO relies on this lda/ldy order )
code split swap lda,# $ff and ldy,#
['] pushya jmp, ;
: constant value ;
( to free up space, pad could be
  e.g. HERE+34 instead )
$500 constant pad ( X16 golden RAM )
: spaces ( n -- )
begin ?dup while space 1- repeat ;

( X816: W moved out of the relocated MSB stack plane - asm/durexforth.asm )
d4 value w
d8 value w2
dc value w3

: hex 10 base ! ;
: decimal a base ! ;

: 2drop ( a b -- )
postpone drop postpone drop ; immediate

: unused ( -- u ) latest here - $20 - ;
: blank ( addr u -- ) bl fill ;
\ X816: reset/poweroff (SMC i2cpoke) and save-forth (saveb) are gone with
\ the parked sysx/disk modules; they return with the platform-hooks phase.

code 2/
msb lda,x 8000 cmp,# msb ror,x lsb ror,x
rts, end-code
code or
msb lda,x msb 2+ ora,x msb 2+ sta,x
lsb lda,x lsb 2+ ora,x lsb 2+ sta,x
inx, inx, rts, end-code
code xor
msb lda,x msb 2+ eor,x msb 2+ sta,x
lsb lda,x lsb 2+ eor,x lsb 2+ sta,x
inx, inx, rts, end-code

:- dup inx, inx, rts, end-code
code lshift ( x1 u -- x2 )
lsb dec,x -branch bmi,
lsb 2+ asl,x msb 2+ rol,x
latest >xt jmp,
code rshift ( x1 u -- x2 )
lsb dec,x -branch bmi,
msb 2+ lsr,x lsb 2+ ror,x
latest >xt jmp,

: variable
0 value
here latest >xt 1+ (to)
4 allot ;

( true alias: a new header whose xt
  points at the old word's code, with
  the flag bits copied, so immediacy
  and compile semantics carry over. )
: synonym ( "newname" "oldname" -- )
header parse-name 2dup find-name
?dup 0= if notfound then nip nip
dup >xt latest dup c@ $1f and + 1+ w!
c@ $c0 and latest dup c@ rot or swap c! ;

( double / buffer defining words - DEFINING.TXT )
: 2variable ( "name" -- ) variable 4 allot ;
: buffer: ( n "name" -- ) create allot ;
: 2constant ( d "name" -- ) create , , does> 2@ ;
: 2value ( d "name" -- ) create , , does> 2@ ;
: 2literal ( d -- ) swap postpone literal postpone literal ; immediate

( from FIG UK... )
: / /mod nip ;
: mod /mod drop ;
: */mod >r m* r> fm/mod ;
: */ */mod nip ;
( ...from FIG UK )

( double-cell numbers - DOUBLE.TXT. core so DOUBLE works without compat. )
code 2over ( a b c d -- a b c d a b )
dex, dex,
msb 8 + lda,x msb sta,x
lsb 8 + lda,x lsb sta,x
dex, dex,
msb 8 + lda,x msb sta,x
lsb 8 + lda,x lsb sta,x rts, end-code
code 2swap ( a b c d -- c d a b )
lsb lda,x pha, lsb 4 + lda,x lsb sta,x pla, lsb 4 + sta,x
msb lda,x pha, msb 4 + lda,x msb sta,x pla, msb 4 + sta,x
lsb 2+ lda,x pha, lsb 6 + lda,x lsb 2+ sta,x pla, lsb 6 + sta,x
msb 2+ lda,x pha, msb 6 + lda,x msb 2+ sta,x pla, msb 6 + sta,x
rts, end-code
code d+ ( d1 d2 -- d3 )
clc,
lsb 2+ lda,x lsb 6 + adc,x lsb 6 + sta,x
msb 2+ lda,x msb 6 + adc,x msb 6 + sta,x
lsb lda,x lsb 4 + adc,x lsb 4 + sta,x
msb lda,x msb 4 + adc,x msb 4 + sta,x
inx, inx, inx, inx, rts, end-code
: ?dnegate 0< if dnegate then ;
: dabs dup ?dnegate ;
: d>s ( d -- n ) drop ;
: d- ( d1 d2 -- d3 ) dnegate d+ ;
: d2* ( d -- 2d ) 2dup d+ ;
: d2/ ( d -- d/2 ) dup >r 2/ swap 1 rshift r> 1 and $1f lshift or swap ;
: d0= ( d -- flag ) or 0= ;
: d0< ( d -- flag ) nip 0< ;
: d= ( d1 d2 -- flag ) d- d0= ;
: d< ( d1 d2 -- flag ) rot 2dup = if 2drop u< else 2swap 2drop swap < then ;
: du< ( ud1 ud2 -- flag ) rot 2dup = if 2drop u< else 2swap 2drop swap u< then ;
: d<> ( d1 d2 -- flag ) d= 0= ;
: d> ( d1 d2 -- flag ) 2swap d< ;
: du> ( ud1 ud2 -- flag ) 2swap du< ;
: d0<> ( d -- flag ) or 0<> ;
: d0> ( d -- flag ) 0 0 2swap d< ;
: dmax ( d1 d2 -- d ) 2over 2over d< if 2swap then 2drop ;
: dmin ( d1 d2 -- d ) 2over 2over d< 0= if 2swap then 2drop ;
: d. ( d -- ) tuck dabs <# #s rot sign #> type space ;

( mixed / triple-precision multiply-divide - ARITHMETIC.TXT )
: ud* ( ud u -- ud ) tuck * >r um* r> + ;
: ut* ( lo hi u -- t0 t1 t2 )            \ unsigned double * single -> triple
swap over um* 2swap um* >r -rot r> 0 2swap d+ ;
: ut/ ( t0 t1 t2 u -- q0 q1 )            \ unsigned triple / single -> double
>r r@ um/mod -rot r> um/mod nip swap ;
: m*/ ( d n1 n2 -- d )                   \ d*n1/n2, triple intermediate (truncates)
dup 2 pick xor 3 pick xor >r             \ combined sign -> R
abs >r abs >r dabs                       \ |n2| |n1| on R, |d| on stack
r> ut* r> ut/ r> ?dnegate ;

( "12." double literals - DOUBLE.TXT:
  digits with a trailing dot push a
  double, in the current BASE, "-" ok.
  Installed in 'NOTFOUND; FLOAT chains
  trailing-dot tokens back here, so 12.
  stays a double and 12.12 a float.
  ROLLBACK: delete this block and make
  the float.fs literal hook fall back
  to plain notfound again. )
: (dig) ( c -- n -1 | 0 )
dup '0' '9' 1+ within if '0' -
else dup 'a' 'z' 1+ within if 'a' - $a +
else dup 'A' 'Z' 1+ within if 'A' - $a +
else drop 0 exit then then then
dup base @ u< dup 0= if nip then ;
: (dnum) ( ca u -- d | ca u <throws> )
dup 2 < if notfound then
2dup + 1- c@ '.' <> if notfound then
2dup 1- over c@ '-' = dup >r if 1 /string then
dup 0= if 2drop r> drop notfound then
0 0 2swap ( ca u ud ca' u' )
begin dup while
over c@ (dig) 0= if
2drop 2drop r> drop notfound then
>r 2swap base @ ud* r> m+ 2swap
1 /string repeat 2drop
r> if dnegate then 2swap 2drop
state @ if swap
postpone literal postpone literal then ;
' (dnum) 'notfound !

( number output: right-justified fields + helpers - NUMERIC.TXT )
: holds ( addr u -- ) begin dup while 1- 2dup + c@ hold repeat 2drop ;
: d.r ( d w -- ) >r tuck dabs <# #s rot sign #> r> over - 0 max spaces type ;
: .r ( n w -- ) >r s>d r> d.r ;
: u.r ( u w -- ) >r 0 <# #s #> r> over - 0 max spaces type ;
: ? ( addr -- ) @ . ;

: .s depth begin ?dup while
dup pick . 1- repeat ;

: abort -1 throw ;
: abort" postpone if
postpone s" postpone (abort")
postpone then ; immediate

( linked list. each element contains
  backlink + hashed file name )
0 value (includes)

: marker ( -- )
(includes) latest here create , , ,
does> dup @ to here
   4 + dup @ to latest
   4 +     @ to (includes) ;

: include parse-name included ;

: :noname here here to latestxt ] ;

marker ---modules---

.( wordlist..) include wordlist

\ hides private words
hide 1mi hide 2mi hide 23mi hide 3mi
hide latestxt
hide dodoes hide (abort")

.( labels..) include labels
.( doloop..) include doloop
.( debug..) include debug
.( require..) include require
.( accept..) include accept
\ X816: ls (CBM directory channel), open (KERNAL device variable), help
\ (loadb) and turnkey (saveb) wait on their kernel replacements.

decimal

cr
( free RAM = gap between here, growing up,
  and the dictionary, growing down. )
latest here - $20 -
0 u.r space .( bytes free.) cr

( boot hook: if an AUTORUN file exists on the card, include it before the
  prompt. INCLUDED throws -37 silently for a missing file, so that case is
  quiet - but any OTHER code escaping the script is REPORTED, because a
  swallowed error is a suite that "just stops" with no evidence. )
: (autorun) s" autorun" included ;
: (autorun-report) ( n -- )
  ?dup if dup -37 = if drop else
    cr ." autorun threw " . cr 1 emu-exit
  then then ;
' (autorun) catch (autorun-report)
