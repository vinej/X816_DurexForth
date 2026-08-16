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

( Return-ADDRESS juggling uses rl>/l>r - a >R cell is 32 bits but a
  return address is a 3-byte word. )
: lits ( -- addr len )
rl> 1+ count 2dup + 1- l>r ;

( "0 to foo" sets value foo to 0 )
: (to) >r split r@ 8 + w! r> 3 + w! ;
\ TO on a VALUE - code word, first byte $a9 - patches its immediates.
\ TO on a 2VALUE - create/does> word, first byte $22 (jsl dodoes) -
\ stores the double at the data field xt+7 with 2!.
: to ' dup c@ $22 = if
7 + state c@ if postpone literal postpone 2! exit then 2!
else
state c@ if postpone literal postpone (to) exit then (to)
then ; immediate

: allot ( n -- ) here + to here ;

: s" ( -- addr len )
'"' parse state @ if postpone lits
dup c, tuck here swap move allot
then ; immediate

: ." postpone s" postpone type
; immediate
: .( ')' parse type ; immediate

( SYSCTL $9F80: bit 0 the boot overlay - dropped long before this
  runs - bit 1 the live E flag, bit 2 TURBO: 0 paces the CPU to an
  exact 8 MHz average, 1 releases the domain's full 14 MHz. The bit
  flips safely at any time. Reads return the EFFECTIVE speed - the
  MiSTer OSD's CPU Turbo option ORs over the software bit - so after
  0 turbo the machine may truthfully still say it is fast.

  THESE LIVE UP HERE, above everything else this file compiles, for
  one reason: the banner below has to be the FIRST line on screen and
  the include chatter starts on the very next one. Everything they
  need is already present - IOC@ and IF/ELSE/THEN come from the
  assembled image and 0<> is defined above. )
: turbo? ( -- flag ) $9f80 ioc@ 4 and 0<> ;
: turbo ( flag -- ) 0<> 4 and $9f80 ioc! ;
: cpu-mhz ( -- u ) turbo? if 14 else 8 then ;

( THE BANNER IS NOT PRINTED HERE. It used to be, and it was wrong for the
  pre-compiled image: this file runs while COMPILING, which a turnkey image
  does once at build time, so the speed it printed would have been the build
  machine's for ever. PRINT_BANNER in asm/durexforth.asm prints it from COLD
  instead, on every start, reading $9F80 live. )

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
( w, not comma - the operand is a 16-bit branch target and THEN patches
  exactly two bytes. With a 4-byte cell here, two spare bytes stayed ZERO
  in the instruction stream, and the runtime helper's matched path returns
  right onto them: $00 $00 is BRK plus its signature byte. The kernel's
  default for an unhandled brk is to resume at PC+2, which skipped
  precisely those two bytes - so CASE worked, the suite was green, and
  every OF that matched was taking a trap and coming back. Installing a
  real brk handler is what finally made it audible. A stage-B leftover:
  comma compiled two bytes back when cells were 16-bit. )
: of postpone (of) here 0 w, ; immediate
: endof postpone else ; immediate
: endcase postpone drop
begin ?dup while postpone then
repeat ; immediate

( dodoes words contain:
 1. jsr dodoes
 2. two-byte code pointer. default: rts
 3. variable length data )
here 6b c, ( rtl - the long world's empty DOES> behavior )
( split, not rshift: rshift is defined later in this file )
: create
header postpone dodoes literal dup w, split nip c, ;
: does> rl> 1+ dup latest >xt 4 + w!
split nip latest >xt 6 + c! ;

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
( TO relies on this exact layout: low-word imm at xt+3, high at xt+8 )
code dex, dex, split swap lda,# lsb sta,x lda,# msb sta,x rtl, ;
: constant value ;
( PAD FOLLOWS HERE, which is what ANS says it is: a transient region
  whose contents nothing may rely on across an ALLOT or a comma. It was
  a FIXED address, $10500, chosen when the dictionary was small - and
  the dictionary grew past it. HERE is $157CF at boot now, so every
  write to the old pad landed on COMPILED CODE. Nothing in the boot
  chain wrote there, which is the only reason it was survivable: it
  waited for the first user to type `65 pad c!` and wonder why a word
  defined ten minutes ago had stopped working. The file's own comment
  proposed this fix before the collision happened. )
: pad ( -- addr ) here 68 + ;
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

( program space left, ALL of it: while HERE is in bank 1, the gap to
  the headers plus the three empty banks above; afterwards, everything
  to the end of bank 4. )
: unused ( -- u ) here $10000 < if latest here - $20 - $30000 +
else $50000 here - then ;
: blank ( addr u -- ) bl fill ;
\ X816: reset/poweroff (SMC i2cpoke) and save-forth (saveb) are gone with
\ the parked sysx/disk modules; they return with the platform-hooks phase.

code 2/
msb lda,x 8000 cmp,# msb ror,x lsb ror,x
rtl, end-code
code or
msb lda,x msb 2+ ora,x msb 2+ sta,x
lsb lda,x lsb 2+ ora,x lsb 2+ sta,x
inx, inx, rtl, end-code
code xor
msb lda,x msb 2+ eor,x msb 2+ sta,x
lsb lda,x lsb 2+ eor,x lsb 2+ sta,x
inx, inx, rtl, end-code

:- dup inx, inx, rtl, end-code
code lshift ( x1 u -- x2 )
lsb dec,x -branch bmi,
lsb 2+ asl,x msb 2+ rol,x
latest >xt jml,
code rshift ( x1 u -- x2 )
lsb dec,x -branch bmi,
msb 2+ lsr,x lsb 2+ ror,x
latest >xt jml,

: variable
0 value
here latest >xt (to)
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
lsb 8 + lda,x lsb sta,x rtl, end-code
code 2swap ( a b c d -- c d a b )
lsb lda,x pha, lsb 4 + lda,x lsb sta,x pla, lsb 4 + sta,x
msb lda,x pha, msb 4 + lda,x msb sta,x pla, msb 4 + sta,x
lsb 2+ lda,x pha, lsb 6 + lda,x lsb 2+ sta,x pla, lsb 6 + sta,x
msb 2+ lda,x pha, msb 6 + lda,x msb 2+ sta,x pla, msb 6 + sta,x
rtl, end-code
code d+ ( d1 d2 -- d3 )
clc,
lsb 2+ lda,x lsb 6 + adc,x lsb 6 + sta,x
msb 2+ lda,x msb 6 + adc,x msb 6 + sta,x
lsb lda,x lsb 4 + adc,x lsb 4 + sta,x
msb lda,x msb 4 + adc,x msb 4 + sta,x
inx, inx, inx, inx, rtl, end-code
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

( ANS >NUMBER over the same digit converter the dot-literal parser
  uses: accumulate ud*base+digit until the first char that is no
  digit in BASE, and hand back where it stopped. NUMBER.TXT promised
  it; the loop is the dot-literal one, minus dot-and-sign dressing. )
: >number ( ud1 c-addr1 u1 -- ud2 c-addr2 u2 )
begin dup while
over c@ (dig) 0= if exit then
>r 2swap base @ ud* r> m+ 2swap
1 /string repeat ;

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

( ANS core words the helpdoc pages promised - each was ticked [x] with
  no definition behind it, found by probing every documented word
  against the live dictionary. NOTE: this part of the file is still in
  HEX - only single-digit literals below. )
-1 constant true
0 constant false
: 0> 0 swap < ;
: cell+ 4 + ;
: cells 4 * ;
: char+ 1+ ;
: chars ;
: align ;
: aligned ;
( CREATE shape: jsl dodoes = 4 bytes, then the DOES> pointer as lo16 +
  bank byte - so the data field starts at xt+7, same offset the VALUE
  shape uses. DOES> patches +4 and +6; keep the three in step. )
: >body 7 + ;
: defer ( "name" -- ) create ['] abort , does> @ execute ;
: defer! ( xt2 xt1 -- ) >body ! ;
: defer@ ( xt1 -- xt2 ) >body @ ;
: is ( xt "name" -- ) state @
if postpone ['] postpone defer! else ' defer! then ; immediate
: action-of ( "name" -- xt ) state @
if postpone ['] postpone defer@ else ' defer@ then ; immediate
( no queries answered - false for everything is ANS-conformant )
: environment? ( c-addr u -- false ) 2drop false ;
( the rest of LOGIC.TXT's comparisons - each is the negation of one
  that already exists, because on a total order "a <= b" is exactly
  "not a > b", and the operand is already reduced to a flag. )
: 0<= 0> 0= ;
: 0>= 0< 0= ;
: <= > 0= ;
: >= < 0= ;
: u<= u> 0= ;
: u>= u< 0= ;
( equality does not care about signedness - the bits are equal or they
  are not - so these two are honest aliases, kept because the page
  names them and someone porting code will type them. )
: u<> <> ;
: u= = ;

( symmetric division: remainder takes the dividend's sign, the
  quotient truncates toward zero - FM/MOD's floor is in math.asm )
: sm/rem ( d1 n1 -- n2 n3 )
2dup xor >r over >r abs >r dabs r> um/mod
swap r> 0< if negate then
swap r> 0< if negate then ;

( FAR DATA SPACE - the SDRAM behind the four fast banks.

  The dictionary - HERE and ALLOT - stays in single-cycle BRAM, banks
  $01-$04, because every instruction fetch there pays SDRAM's ~6
  cycles. Bulk DATA has no such reason to live in the expensive
  banks, so it gets its own bump pointer, FAR-HERE, walking
  $05:0000-$DF:FFFF. Cells already carry flat 24-bit addresses and
  @ ! c@ c! move fill erase are all long-addressed, so a far block
  is an ordinary address everywhere a near one is.

  The idiom is CREATE and ALLOT's - far-here 1000 far-allot - or name
  the block with far-buffer:. There is no far FREE: the pointer
  only goes up, MARKER puts it back where it was, and FAR-EMPTY
  drops the lot.

  THE CEILING IS THE KERNEL'S TO SAY, AND IT MOVES. The top of
  what used to be data space, $C0:0000-$DF:FFFF, is the kernel
  writable-data region - the resident editor's page pool. It is
  reserved at boot and MEM-RELEASE hands it to the kernel heap for
  the rest of the session, so there is no one number to compile in.
  sdram-size is therefore a VALUE set from MEM-TOP, not a constant:
  a compile-time copy would be wrong on one side of a release and
  would say nothing about it, which is the whole failure this
  arrangement exists to prevent. FAR-INIT re-asks; base.fs is
  compiled off the card at every COLD so the ordinary boot has
  already asked, but a frozen or cartridge image must call it.

  NOT the kernel heap, and no longer a hand-carve. MEM_ALLOC's
  arena starts at $20:0000 - KERNEL.md 5.5 - and ends at the same
  ceiling MEM-TOP reports, so the two allocators take their bound
  from one place and cannot drift apart. This is what retires the
  note that used to stand here: whoever bound MEM_ALLOC would have
  had to separate it from far-here by hand. )
$50000 constant sdram ( first data address in SDRAM, flat )
0 value sdram-size
: far-init ( -- ) mem-top 1+ sdram - to sdram-size ;
far-init
sdram value far-here
: far-unused ( -- u ) sdram sdram-size + far-here - ;
( -8, dictionary overflow: far space IS data space. Refusing
  loudly matters more here than in the dictionary - the addresses
  just above are the VERA2 window and the kernel firmware, and a
  silent bump into them writes to the screen or worse. )
: far-allot ( u -- )
dup far-unused u> if -8 throw then
far-here + to far-here ;
: far-buffer: ( u "name" -- ) far-here swap far-allot constant ;
: far-empty ( -- ) sdram to far-here ;

( linked list. each element contains
  backlink + hashed file name )
0 value (includes)

( unwinding a marker returns the far pointer too - a module that
  claims SDRAM gives it back when it is forgotten, or every
  reload of it would leak a block. )
: marker ( -- )
far-here (includes) latest here create , , , ,
does> dup @ to here
   4 + dup @ to latest
   4 + dup @ to (includes)
   4 +     @ to far-here ;

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

( TURBO?, TURBO and CPU-MHZ are defined at the TOP of this file, next to
  the boot banner that has to print the speed before anything else. )

( A WALL CLOCK, IN SOFTWARE.

  There is no battery-backed RTC on this machine and the kernel's clock
  call answers milliseconds since boot, so the date has to come from
  somewhere: you tell it once with SETTIME and everything after that is
  arithmetic on the free-running millisecond timer. A power cycle
  forgets it and DATE@ reads 1970-01-01 again - which is the honest
  behaviour for a machine with no clock chip, and better than a plausible
  wrong date. If an I2C RTC ever appears on the SMC bus, SETTIME from it
  at boot and everything here keeps working.

  MS@ is the same timer MS runs on: $9F90 IS READ FIRST because reading
  it latches bits 31:8, and reading the high bytes without it hands back
  whatever the last latch held. It wraps after 49.7 days.

  Two honest limits. The seconds count is one 32-bit cell, so this is a
  1970..2038 clock. And SECONDS divides the millisecond count with the
  signed /, which is exact until the timer passes 2^31 ms - 24.8 days of
  uptime - after which the wall clock jumps. Neither is worth a double
  on a machine you power off. )
variable (clk0)                       \ seconds at millisecond-timer zero
: ms@ ( -- u )                        \ milliseconds since boot
  $9f90 ioc@  $9f91 ioc@ 8 lshift or
  $9f92 ioc@ 16 lshift or  $9f93 ioc@ 24 lshift or ;
: seconds ( -- u ) ms@ 1000 / (clk0) @ + ;

( Howard Hinnant's civil-from-days / days-from-civil, which is exact for
  every proleptic Gregorian date and needs no month table - the 153/5
  business is the length of a five-month cycle in a year that starts in
  March, which is why March is month 0 here. Positive years only: the
  algorithm wants FLOOR division and Forth's is symmetric, and they only
  disagree below year 0. )
variable (cy) variable (cm) variable (cd)
variable (era) variable (yoe) variable (doy) variable (doe) variable (mp)
: civil>days ( y mo d -- days )       \ days since 1970-01-01
  (cd) ! (cm) ! (cy) !
  (cm) @ 3 < if (cy) @ 1- (cy) ! then
  (cy) @ 400 / (era) !
  (cy) @ (era) @ 400 * - (yoe) !
  (cm) @ dup 2 > if 3 - else 9 + then 153 * 2 + 5 / (cd) @ + 1- (doy) !
  (yoe) @ 365 * (yoe) @ 4 / + (yoe) @ 100 / - (doy) @ + (doe) !
  (era) @ 146097 * (doe) @ + 719468 - ;
: days>civil ( days -- y mo d )
  719468 +  dup 146097 / (era) !
  (era) @ 146097 * - (doe) !
  (doe) @ (doe) @ 1460 / - (doe) @ 36524 / + (doe) @ 146096 / - 365 / (yoe) !
  (doe) @ (yoe) @ 365 * (yoe) @ 4 / + (yoe) @ 100 / - - (doy) !
  (doy) @ 5 * 2 + 153 / (mp) !
  (doy) @ (mp) @ 153 * 2 + 5 / - 1+ (cd) !
  (mp) @ dup 10 < if 3 + else 9 - then (cm) !
  (yoe) @ (era) @ 400 * + (cm) @ 3 < if 1+ then (cy) !
  (cy) @ (cm) @ (cd) @ ;

: settime ( y mo d h m s -- )
  >r >r >r civil>days 86400 *
  r> 3600 * +  r> 60 * +  r> +
  ms@ 1000 / - (clk0) ! ;
: time@ ( -- h m s )
  seconds 86400 mod
  dup 3600 / swap 3600 mod
  dup 60 / swap 60 mod ;
: date@ ( -- y mo d ) seconds 86400 / days>civil ;
: .time ( -- ) time@ >r >r 0 <# # # #> type ':' emit
  r> 0 <# # # #> type ':' emit  r> 0 <# # # #> type ;
: .date ( -- ) date@ >r >r 0 <# # # # # #> type '-' emit
  r> 0 <# # # #> type '-' emit  r> 0 <# # # #> type ;

( TI and STOP, named after BASIC's, over the VSYNC frame counter that
  TICKS reads. Jiffies, not milliseconds: a frame is the unit anything
  drawing to the screen is actually paced by. )
: ti ( -- clk ) ticks drop ;
: stop ( clk -- ) ti swap - 65535 and
  dup . ." jiffies (" 1000 60 */ . ." ms)" cr ;

( THE CONSOLE FONT - the X816's answer to the X16's CHARSET.

  There is no charset ROM to select from, so the X16 word cannot port:
  its n picked one of a dozen banked ROM fonts. Here the console is
  VERA layer 0 and its font is ordinary VRAM the kernel filled at boot
  from runtime/font_cp437.s, so "select a charset" is "point the layer
  at 2 KB of tile data" - and anything you can write is a charset.

  VRAM layout the kernel establishes: the tilemap is 128x64 cells of
  two bytes at $00000-$03FFF, and the font is 256 glyphs of 8 bytes at
  FONT. A font must start on a 2 KB boundary - the tile-base register
  holds addr>>11 - so FONT2 is simply the next slot up, and the first
  free VRAM after the console's own.

  A glyph is 8 bytes, one per scanline, top first, bit 7 leftmost.
  Codes are CP437 and the tile index IS the character code: no $20
  bias, so 65 GLYPH-ADDR is the "A" the kernel prints. )
$4000 constant font   ( the kernel's CP437, live at boot )
$4800 constant font2  ( the next 2 KB slot - free VRAM, yours )

: charset ( vaddr -- ) 0 0 rot tilebase ;
: glyph-addr ( base n -- vaddr ) 8 * + ;

( FONT-COPY moves 2 KB VRAM to VRAM a byte at a time, through the CPU.
  VERA has two data ports and a copy could stream both, but VADDR here
  only drives port 0 - and this runs once, so re-addressing per byte
  costs milliseconds nobody waits for. Copy before you edit: writing
  over FONT itself edits the font the kernel is drawing with, which is
  legal and immediate but leaves no way back short of a reboot.

  No >R inside the loop, and that is not a style preference: I is
  literally R@ at a fixed stack offset, so ANY cell parked on the
  return stack makes I read the wrong one. It looks like a copy that
  runs but moves the wrong bytes. Juggle on the data stack instead. )
: font-copy ( src dst -- )
  2048 0 do
    over i + 0 swap vpeek       ( src dst byte )
    over i + swap 0 -rot vpoke  ( src dst )
  loop 2drop ;

: glyph! ( c-addr base n -- ) glyph-addr 0 swap vaddr
  8 0 do dup i + c@ v! loop drop ;
: glyph@ ( base n c-addr -- ) >r glyph-addr 0 swap vaddr r>
  8 0 do dup i + v@ swap c! loop drop ;

( ANS COMPARE - STRING.TXT promised it, and file code needs it: the
  honest way to check what came back off the card is to compare the
  bytes, not just the count. Returns -1, 0 or 1: the shorter string
  loses only when it is a prefix of the longer one, so "ab" sorts
  before "abc" but after "aa". )
variable (cmp-a1) variable (cmp-u1)
variable (cmp-a2) variable (cmp-u2)
: compare ( c-addr1 u1 c-addr2 u2 -- n )
  (cmp-u2) ! (cmp-a2) ! (cmp-u1) ! (cmp-a1) !
  (cmp-u1) @ (cmp-u2) @ min 0 ?do
    (cmp-a1) @ i + c@ (cmp-a2) @ i + c@
    2dup < if 2drop unloop -1 exit then
    > if unloop 1 exit then
  loop
  (cmp-u1) @ (cmp-u2) @
  2dup < if 2drop -1 exit then
  > if 1 exit then 0 ;

( ANS FILE ACCESS over the primitives in asm/file.asm.

  The primitives speak the kernel's language: one 32-bit CELL for an
  offset or a size - a cell holds a whole FAT32 file position, so no
  double is needed - and the kernel's own KERR_* number as the ior,
  0 meaning success. These words put the ANS shapes on top, which
  mostly means widening those cells to the doubles ANS specifies.

  ior values, from X816_core doc/KERNEL.md: 1 no such call, 2 not
  found, 3 no space, 4 bad argument, 5 I/O error, 6 exists, 7 not
  empty. They are NOT the Forth THROW codes; -37 is what the include
  path throws, and these words return rather than throw, per ANS. )
0 constant r/o
1 constant w/o
2 constant r/w

( OPEN-FILE OPENS: it never destroys. This kernel has exactly two
  modes - read an existing file, or CREATE one, truncating whatever
  was there - runtime/kfs.c calls fat32_create when mode is
  KFS_WRITE.
  There is no "open for writing, keep the contents" and no append, so
  W/O OPEN-FILE cannot be honoured and is REFUSED rather than quietly
  emptying the caller's file. Truncation is a thing you have to ask
  for by name, and CREATE-FILE is that name. R/W is refused for the
  same reason plus a second: passing any unknown mode to FS_OPEN gets
  a READ-ONLY handle back, so a program that ignored the ior would
  write nowhere and never learn. )
: open-file ( c-addr u fam -- fileid ior )
  r/o = if r/o fs-open exit then
  2drop 0 1 ;
( fam is accepted and ignored - there is one kind of created file. )
: create-file ( c-addr u fam -- fileid ior ) drop w/o fs-open ;
: close-file ( fileid -- ior ) fs-close ;
: read-file ( c-addr u1 fileid -- u2 ior ) fs-read ;
: write-file ( c-addr u fileid -- ior ) fs-write nip ;
: delete-file ( c-addr u -- ior ) fs-delete ;
: rename-file ( c-addr1 u1 c-addr2 u2 -- ior ) fs-rename ;
: file-size ( fileid -- ud ior ) fs-size >r 0 r> ;
: file-position ( fileid -- ud ior ) >r 0 1 r> fs-seek >r 0 r> ;
: reposition-file ( ud fileid -- ior ) >r drop 0 r> fs-seek nip ;
( No flush call exists, so this reports "no such call" rather than
  returning 0 and leaving you to believe something was flushed.
  CLOSE-FILE is the sync point on this machine. RESIZE-FILE has no
  kernel call behind it either, and says so the same way. )
: flush-file ( fileid -- ior ) drop 1 ;
: resize-file ( ud fileid -- ior ) drop 2drop 1 ;
( Does it exist? Open it read-only and put it straight back. x is the
  ANS implementation-defined extra, and 0 here says "nothing to add". )
: file-status ( c-addr u -- x ior )
  r/o fs-open dup 0= if
    drop close-file drop 0 0
  else swap drop 0 swap then ;

variable (rl-fid) variable (rl-ior) variable (rl-n)
variable (rl-a) variable (rl-max)
create (rl-c) 1 allot
create (rl-nl) 1 allot  10 (rl-nl) c!
( One kernel crossing per BYTE, which is slow and deliberate: the
  read-ahead cache in fs.asm belongs to the interpreter's source
  file, and borrowing it here would corrupt an INCLUDE that happens
  to be running. Buy speed with your own buffer and READ-FILE. )
: read-line ( c-addr u1 fileid -- u2 flag ior )
  (rl-fid) ! (rl-max) ! (rl-a) !
  0 (rl-n) ! 0 (rl-ior) !
  begin (rl-n) @ (rl-max) @ < while
    (rl-c) 1 (rl-fid) @ fs-read     ( got ior )
    ?dup if (rl-ior) ! drop (rl-n) @ -1 (rl-ior) @ exit then
    0= if (rl-n) @ dup 0<> 0 exit then    ( end of file )
    (rl-c) c@
    dup 10 = if drop (rl-n) @ -1 0 exit then
    dup 13 = if drop else
      (rl-a) @ (rl-n) @ + c!  1 (rl-n) +!
    then
  repeat
  (rl-n) @ -1 0 ;
: write-line ( c-addr u fileid -- ior )
  dup >r write-file ?dup if r> drop exit then
  (rl-nl) 1 r> write-file ;

( THE INPUT SOURCE - INCLUDE-FILE, and the position words.

  INCLUDE-FILE interprets a file somebody else opened, and it does it
  LINE BY LINE rather than reading the lot into a buffer and calling
  EVALUATE on that. The X16 version read 8 KB and evaluated it in one
  go, which quietly changes the language: a `\` comment eats to the end
  of the BUFFER rather than the end of the line, because a newline is
  just a character to the parser. A colon definition still spans lines
  here - compilation state survives EVALUATE - so nothing is lost by
  going a line at a time except the surprise.

  ANS says INCLUDE-FILE closes the file when it reaches the end, and
  this does. )
create (if-buf) 200 allot
: include-file ( fileid -- )
  >r
  begin
    (if-buf) 200 r@ read-line        ( u2 flag ior )
    ?dup if drop 2drop r> close-file drop exit then
  while                              ( u2 )
    (if-buf) swap evaluate
  repeat
  drop
  r> close-file drop ;

( SAVE-INPUT hands back the parse offset and which source it belonged
  to, and RESTORE-INPUT refuses - the FALSE/TRUE flag ANS asks for -
  when the source has changed underneath it. Within one line that is
  the whole job; across a REFILL it is not, and saying so beats
  restoring an offset into a line that is no longer there. )
: save-input ( -- x1 x2 n ) >in @ source-id 2 ;
: restore-input ( xn..x1 n -- flag )
  \ A spec this word did not write is DROPPED, not left lying: ANS
  \ consumes xn..x1 n whether the restore worked or not, and a refusal
  \ that also unbalanced the stack would be two problems.
  dup 2 <> if 0 ?do drop loop true exit then
  drop
  source-id <> if drop true exit then
  >in ! false ;

( CLOSE-SOURCE ends the current file source now instead of at its last
  line. It seeks to the end rather than closing the handle: the handle
  belongs to the interpreter, which closes it and pops back to the
  parent source when the read comes up empty - doing that here would
  pull a file out from under the machinery still holding it. )
: close-source ( -- )
  source-id dup 0> if
    (fs-flush)                          \ the read-ahead first: see fs.asm
    dup >r file-size drop r> reposition-file drop
  else drop then ;

( DIRECTORIES. The X16's CD/DIR talked to a CBM device over the IEC
  bus and took DOS command strings; here they are kernel calls, and a
  path is ordinary ASCII with / separators, absolute or relative.

  A directory entry is 18 bytes: name at +0, 13 bytes NUL-terminated;
  attributes at +13 with bit 0 set for a directory; size at +14 as 32
  bits, 0 for a directory. )
create dirent 18 allot
create (cwdbuf) 80 allot        ( KFS_PATH, what FS_GETCWD needs )
: dirent-name ( -- c-addr u ) dirent dup 13 0 do
    dup c@ 0= if leave then 1+ loop over - ;
: dirent-dir? ( -- flag ) dirent 13 + c@ 1 and 0<> ;
: dirent-size ( -- u ) dirent 14 + @ ;

: cd ( c-addr u -- ior ) fs-chdir ;
: mkdir ( c-addr u -- ior ) fs-mkdir ;
: rmdir ( c-addr u -- ior ) fs-rmdir ;
: cwd ( -- c-addr u ior ) (cwdbuf) dup fs-getcwd ( addr len ior ) ;
: pwd ( -- ) cwd if ." ?" 2drop else type then cr ;

: dir-open ( c-addr u -- handle ior ) fs-diropen ;
: dir-close ( handle -- ior ) fs-dirclose ;
( ior 2 from the kernel is END OF DIRECTORY, not a fault - it uses
  BADARG for a handle that was never a directory, and that shows up on
  the FIRST call rather than the last. So 2 becomes a false flag here
  and anything else stays an error worth reporting. )
: dir-next ( handle -- flag ior )
  dirent swap fs-dirnext
  dup 0= if drop -1 0 exit then
  dup 2 = if drop 0 0 exit then
  0 swap ;

( DIR lists the working directory. Directories are marked <DIR> where a
  size would go, because a FAT32 directory entry stores 0 there and a
  bare 0 in a size column tells you nothing. )
: dir ( -- )
  cr pwd
  s" ." dir-open ( handle ior )
  if drop ." cannot open ." cr exit then
  begin dup dir-next ( handle flag ior )
    ?dup if ." dir error " . drop cr exit then
  while
    dirent-name type
    dirent-name nip 14 swap - 0 max spaces
    dirent-dir? if ." <DIR>" else dirent-size u. then cr
  repeat dir-close drop ;

( LS - DIR with a pattern, which is what CONTROL/FILE.TXT call it. The
  pattern is a plain SUBSTRING, not a glob: the kernel hands back 8.3 names
  and matching "*.TXT" would be pretending to a shell this machine has not
  got. LS on its own lists everything, exactly as DIR does. )
variable (ssn)  variable (ssnu)
: (substr?) ( hay hu needle nu -- flag )
  dup 0= if 2drop 2drop true exit then
  (ssnu) ! (ssn) !
  begin dup (ssnu) @ >= while
    2dup drop (ssnu) @ (ssn) @ (ssnu) @ compare 0= if 2drop true exit then
    1 /string
  repeat 2drop false ;

: ls ( "pattern" -- )
  parse-name (ssnu) ! (ssn) !           \ address first, then length
  cr pwd
  s" ." dir-open
  if drop ." cannot open ." cr exit then
  begin dup dir-next
    ?dup if ." dir error " . drop cr exit then
  while
    dirent-name (ssn) @ (ssnu) @ (substr?) if
      dirent-name type
      dirent-name nip 14 swap - 0 max spaces
      dirent-dir? if ." <DIR>" else dirent-size u. then cr
    then
  repeat dir-close drop ;

( AUDIO - the parts that are HARDWARE.

  VERA's PSG is 64 bytes of VRAM at $1F9C0, four per voice: frequency
  low, frequency high, then panning and volume, then waveform and pulse
  width. VERA's PCM FIFO is three I/O registers. The YM2151 is an
  address/data port pair at $9F40. All of that is on this machine and
  needs nothing from anybody's ROM.

  What is NOT here: the note-playing API - PSGNOTE, FMINIT, FMINST and
  the rest - because those are the X16 ROM's audio driver, 163 built-in
  instrument patches and a note table included. Porting them is a
  separate job, not a binding, and pretending otherwise would leave
  words that exist and do nothing. AUDIOFM says so on the page. )

$f9c0 constant psgbase          ( VRAM $1F9C0 = bank 1, offset $F9C0 )
: psg! ( value n -- ) psgbase + 1 swap rot vpoke ;
: psg@ ( n -- value ) psgbase + 1 swap vpeek ;
: psginit ( -- ) 64 0 do 0 i psg! loop ;
: psgfreq ( freq voice -- )
  4 * >r dup 255 and r@ psg! 8 rshift r> 1+ psg! ;
( Volume sets BOTH channels, which is what the page promises; PSGPAN
  keeps the volume and PSGWAV keeps the pulse width, so the two halves
  of a shared byte can be set in either order. )
: psgvol ( vol voice -- ) 4 * 2 + >r 63 and $c0 or r> psg! ;
: psgpan ( pan voice -- )
  4 * 2 + >r 3 and 6 lshift r@ psg@ 63 and or r> psg! ;
: psgwav ( wave voice -- )
  4 * 3 + >r 3 and 6 lshift r@ psg@ 63 and or r> psg! ;
( PULSE WIDTH, the other half of that byte, and NOT optional for the
  pulse waveform: the renderer emits a high sample only while
  phase>>10 <= pw, so the width PSGINIT leaves behind - zero - is a
  1-in-64 duty cycle. That is a thin click, not a tone, and it is why a
  square-wave beep needs 32 here. Saw and triangle use pw as an XOR on
  the phase instead, so they are loud at any width. )
: psgpw ( pw voice -- )
  4 * 3 + >r 63 and r@ psg@ $c0 and or r> psg! ;

: pcmctrl ( n -- ) $9f3b ioc! ;
: pcmrate ( n -- ) $9f3c ioc! ;
: pcm! ( byte -- ) $9f3d ioc! ;
( AUDIO_CTRL reads back TWO status bits the write side does not have:
  bit 7 full, bit 6 EMPTY. Empty is the one a feeder wants - it means
  the sound has already stopped, not that it is about to. )
: pcmfull? ( -- flag ) $9f3b ioc@ $80 and 0<> ;
: pcmempty? ( -- flag ) $9f3b ioc@ $40 and 0<> ;
: pcm-write ( addr count -- ) 0 ?do dup i + c@ pcm! loop drop ;

( The YM2151 answers writes only - there is no register readback in the
  chip - so YM@ reports a SHADOW that YM! keeps. It is what the chip was
  last told, which is the only honest thing available.
  The wait for the busy flag is BOUNDED: a chip that never clears it
  would otherwise hang the machine, and a hang is a worse failure than a
  write that went out slightly early. )
create ymshadow 256 allot
: (ym-wait) ( -- ) 1000 0 do $9f40 ioc@ $80 and 0= if leave then loop ;
: ym! ( value reg -- )
  2dup ymshadow + c!
  (ym-wait) dup $9f40 ioc! drop
  (ym-wait) $9f41 ioc! ;
: ym@ ( reg -- value ) 255 and ymshadow + c@ ;

( SCREEN - the bitmap mode the GRAPHIC module draws into.

  There is no KERNAL SCREEN call here, so this is built from the layer
  words this Forth already has. Mode 128 is 320x240x256 on layer 0, which
  is what GINIT asks for; mode 0 puts the console back.

  THE BITMAP AND THE CONSOLE SHARE VRAM. The kernel's text map starts at
  $00000 and a 320x240 bitmap is 76,800 bytes from the same place, so
  entering graphics scribbles over the characters. Mode 0 restores the
  registers it saved and then CLS, which is what redraws them. )
variable (scr-saved)
variable (scr-cfg) variable (scr-hs) variable (scr-vs) variable (scr-tb)

( THE BITMAP LANDS ON THE FONT. A 320x240 8-bit bitmap is 76,800 bytes
  from VRAM $00000, and the console's characters are at $04000 - so
  entering graphics eats them, and coming back gives a screen of
  well-formed rubbish. CLS repairs the character MAP and can do nothing
  about the glyphs, so the 2 KB is kept in far memory across the trip. )
2048 far-buffer: (scr-font)
: (font-save) ( -- ) 0 $4000 vaddr 2048 0 do v@ (scr-font) i + c! loop ;
: (font-load) ( -- ) 0 $4000 vaddr 2048 0 do (scr-font) i + c@ v! loop ;
: screen ( mode -- )
  128 = if
    (scr-saved) @ 0= if                 ( first time in: remember the text setup )
      $9f2d ioc@ (scr-cfg) !  $9f2a ioc@ (scr-hs) !
      $9f2b ioc@ (scr-vs) !  $9f2f ioc@ (scr-tb) !
      (font-save)
      1 (scr-saved) !
    then
    64 $9f2a ioc!  64 $9f2b ioc!        ( 640x480 halved to 320x240 )
    0 7 layer-mode                      ( bitmap, 8 bits a pixel )
    0 0 0 tilebase                      ( pixels from VRAM $00000, 320 wide )
    0 layer-on
  else
    (scr-saved) @ if
      (scr-cfg) @ $9f2d ioc!  (scr-hs) @ $9f2a ioc!
      (scr-vs) @ $9f2b ioc!  (scr-tb) @ $9f2f ioc!
      (font-load)
      0 (scr-saved) !
    then
    0 layer-on cls
  then ;

( INTERRUPTS - arming a Forth word on a VERA source.

  The kernel dispatches one slot per SOURCE and acknowledges it; firq.asm
  rebuilds this Forth's conventions around the call. What is left here is
  the part VERA owns: a source that is not ENABLED never interrupts, and
  the enable bits are ours to set.

  A handler runs with interrupts masked and must not enable them, must not
  THROW - there is no CATCH between it and the kernel - and must put back
  VERA's CTRL and address ports if it touches them. Keep it short: it runs
  inside somebody else's word.

  Only IRQ is proven. See the note below the example.

    : tick  1 frames +! ;   ' tick irq        \ every vertical blank
    0 irq                                     \ and off again )
\ $9F26 IS the interrupt-enable register, and $9F60 is where VERA2's own
\ registers live - a detail worth writing down because looking at the wrong
\ one makes IEN appear not to work at all.
\ READING $9F26 DOES NOT GIVE BACK WHAT YOU WROTE. Bits 0-3 are the enables,
\ but bit 6 reads as the CURRENT SCANLINE's ninth bit and bit 7 as
\ IRQLINE's, so a value read back is usually 64 or 65 higher than the
\ enables alone. Read-modify-write is still safe - a write uses only bits
\ 0-3 and 7 - but anything COMPARING a read to an expected value has to
\ mask, and a test that did not was the thing that made this look broken.
\ AFLOW'S ENABLE DOES NOT STICK. Writing 8 here reads back as 0 enables;
\ writing 4 or 1 reads back faithfully. kirq.s says the same thing from the
\ other end - it excludes AFLOW from its acknowledge because "writing its
\ bit does nothing". So AFLOW-IRQ arms the kernel slot and does not pretend
\ to enable a source that will not enable, which leaves ADVSND's PCM
\ streaming waiting on the hardware rather than on this file.
$9f26 constant vera-ien
$9f27 constant vera-isr
: (ien+) ( mask -- ) vera-ien ioc@ or vera-ien ioc! ;
: (ien-) ( mask -- ) invert vera-ien ioc@ and vera-ien ioc! ;

: irq ( xt -- )                 ( vertical blank; 0 disarms )
  dup if 1 (ien+) else 1 (ien-) then 0 (irq!) ;

\ The raster line is nine bits: the low eight are written to $9F27 - a write
\ there is IRQLINE, not the status a read gives - and bit 8 rides in bit 7
\ of IEN, which the read-modify-write above carries along for free.
: line-irq ( xt line -- )
  dup 255 and vera-isr ioc!
  256 and if $80 (ien+) else $80 (ien-) then
  dup if 2 (ien+) else 2 (ien-) then 1 (irq!) ;

: sprcol-irq ( xt -- ) dup if 4 (ien+) else 4 (ien-) then 2 (irq!) ;
: aflow-irq  ( xt -- ) 3 (irq!) ;      ( arming only - see above )

( Which sprites collided, from the top nibble of the status register. )
: collisions ( -- n ) vera-isr ioc@ 4 rshift 15 and ;

( INPUT - SNES pads and the SMC mouse, both on VIA1 port A.

  The pads are a shift register, exactly as the X16 KERNAL drives them:
  PA2 latches, PA3 clocks, and each pad presents one bit on a line of
  its own - PA7 for pad 1 down to PA4 for pad 4. Twenty-four bits come
  out MSB first and every one of them is ACTIVE LOW:

    byte 0   B Y SELECT START UP DOWN LEFT RIGHT
    byte 1   A X L R  then four 1 bits, the pad's ID
    byte 2   $00 if a pad answered, $FF if the line just floated high

  PA0 and PA1 are the I2C bus to the SMC, which is where the mouse
  comes from, so the direction register is read-modify-written and
  those two bits are left exactly as they were. )
$9f01 constant via1-pa
$9f03 constant via1-ddr

variable joy1 variable joy2 variable joy3 variable joy4
: (joy-clock) ( -- bits )       ( one clock, sampling all four lines )
  0 via1-pa ioc! via1-pa ioc@ 8 via1-pa ioc! ;
: (joy-roll) ( bits mask var -- bits )
  >r over and 0<> 1 and r@ @ 2* or r> ! ;
: joy-scan ( -- )               ( sample every pad; JOY does this for you )
  via1-ddr ioc@ $f0 invert and $0c or via1-ddr ioc!
  8 via1-pa ioc!                ( latch low, clock high )
  $0c via1-pa ioc!              ( latch high: the pads load )
  0 via1-pa ioc!
  0 joy1 ! 0 joy2 ! 0 joy3 ! 0 joy4 !
  24 0 do
    (joy-clock)
    $80 joy1 (joy-roll)  $40 joy2 (joy-roll)
    $20 joy3 (joy-roll)  $10 joy4 (joy-roll)
    drop
  loop ;

( JOY returns the buttons ACTIVE HIGH, which is the other way up from
  the wire: bits 0-7 are B Y SELECT START UP DOWN LEFT RIGHT and bits
  8-11 are A X L R. An absent pad reads as all ones, so its third byte
  is $FF and JOY answers 0 - the same "nothing pressed" a present pad
  gives, but tell them apart with JOY? if you need to. Joystick 0 is
  the X16's keyboard-as-joystick and has nothing behind it here. )
: (joy@) ( n -- raw )
  dup 1 = if drop joy1 @ exit then
  dup 2 = if drop joy2 @ exit then
  dup 3 = if drop joy3 @ exit then
      4 = if joy4 @ exit then 0 ;
: joy? ( n -- flag )            ( is a pad actually attached? )
  joy-scan (joy@) dup 0= if drop false exit then 255 and 0= ;
: joy ( n -- buttons )
  joy-scan (joy@)
  dup 0= if exit then           ( no such pad: not "every button at once" )
  dup 255 and if drop 0 exit then
  dup 16 rshift 255 and 255 xor          ( raw byte0' )
  swap 8 rshift 255 and 255 xor 4 rshift 8 lshift or ;

( The mouse is an SMC register read over the bit-banged I2C bus - the
  same bus, the same two pins. Open drain: a 1 in the DIRECTION
  register drives the line low, a 0 releases it to the pull-up, and the
  output register stays 0 throughout. )
: (i2c-idle) 0 via1-ddr ioc! ;
: (i2c-sda)  1 via1-ddr ioc! ;
: (i2c-scl)  2 via1-ddr ioc! ;
: (i2c-both) 3 via1-ddr ioc! ;
: (i2c-start) 0 via1-pa ioc! (i2c-idle) (i2c-sda) (i2c-both) ;
: (i2c-stop)  (i2c-both) (i2c-sda) (i2c-idle) ;
: (i2c-bit) ( f -- )
  if (i2c-scl) (i2c-idle) (i2c-scl) else (i2c-both) (i2c-sda) (i2c-both) then ;
: (i2c>) ( b -- )               ( send MSB first, then clock the ACK slot )
  8 0 do dup $80 and 0<> (i2c-bit) 2* loop drop
  (i2c-scl) (i2c-idle) (i2c-scl) ;
: (i2c-rbit) ( -- f )           ( release SDA, clock once, sample )
  (i2c-scl) (i2c-idle) via1-pa ioc@ 1 and 0<> (i2c-scl) ;
variable (i2cack)
: (i2c<) ( ackf -- b )
  (i2cack) ! 0
  8 0 do 2* (i2c-rbit) if 1 or then loop
  (i2cack) @ if (i2c-both) (i2c-sda) (i2c-both)
  else (i2c-scl) (i2c-idle) (i2c-scl) then ;

variable mouse-on  variable mouse-x  variable mouse-y
variable mouse-b   variable mouse-w
create (mpkt) 4 allot

( MOUSE turns the data path on and off. It does NOT draw a pointer:
  the X16 KERNAL drew one with a hardware sprite, and on this machine
  the sprite words are yours to call - which is better than a pointer
  you cannot move out of the way. Mode -1 is accepted and behaves as 1;
  there is no scaling to choose between with one screen mode. )
: mouse ( mode -- )
  dup 0= if drop 0 mouse-on ! exit then
  drop 1 mouse-on !
  0 mouse-x ! 0 mouse-y ! 0 mouse-b ! 0 mouse-w !
  (i2c-start) $84 (i2c>) $20 (i2c>) 3 (i2c>) (i2c-stop) ;

( One SMC packet: status, dx, dy, wheel. The X and Y signs live in the
  status byte, PS/2 fashion, and Y counts UP the screen there and down
  here - so it is subtracted. An empty mailbox answers with a zero
  status, which is also what "no buttons, no movement" looks like, and
  costs nothing to apply. )
: (mouse-poll) ( -- )
  mouse-on @ 0= if exit then
  (i2c-start) $84 (i2c>) $21 (i2c>) (i2c-stop)
  (i2c-start) $85 (i2c>)
  true (i2c<) (mpkt) c!
  true (i2c<) (mpkt) 1+ c!
  true (i2c<) (mpkt) 2 + c!
  false (i2c<) (mpkt) 3 + c!
  (i2c-stop)
  (mpkt) c@ dup 7 and mouse-b !
  dup 16 and if (mpkt) 1+ c@ 256 - else (mpkt) 1+ c@ then
  mouse-x @ + 0 max 639 min mouse-x !
  32 and if (mpkt) 2 + c@ 256 - else (mpkt) 2 + c@ then
  mouse-y @ swap - 0 max 479 min mouse-y !
  (mpkt) 3 + c@ dup 8 and if 16 - then mouse-w @ + mouse-w ! ;

: mx ( -- x ) (mouse-poll) mouse-x @ ;
: my ( -- y ) (mouse-poll) mouse-y @ ;
: mb ( -- buttons ) (mouse-poll) mouse-b @ ;
: mwheel ( -- delta )           ( signed, and cleared by reading it )
  (mouse-poll) mouse-w @ 0 mouse-w ! ;

( CONTROL words that the SMC and the I2C bus make possible. RESET is the
  command Ctrl+Alt+Del raises; POWEROFF is the one the OSD would. Both are
  writes to the SMC at $42, the same device the mouse answers on. )
: i2cpoke ( dev reg val -- )
  >r swap 2* (i2c-start) (i2c>) (i2c>) r> (i2c>) (i2c-stop) ;
: i2cpeek ( dev reg -- byte )
  over 2* (i2c-start) (i2c>) (i2c>) (i2c-stop)
  (i2c-start) 2* 1 or (i2c>) false (i2c<) (i2c-stop) ;
: reset    ( -- ) $42 2 0 i2cpoke ;
: poweroff ( -- ) $42 1 0 i2cpoke ;

\ FILE.TXT's short names for the ANS pair. Not the C64's OPEN, which took a
\ logical file, a device and a secondary address: there is no IEC bus here,
\ so a file is a name and a mode and nothing else.
: open  ( c-addr u fam -- fileid ior ) open-file ;
: close ( fileid -- ior ) close-file ;


( ANS STRUCTURES - STRUCTURE.TXT, which was 0/5.
    begin-structure point
      field: p.x
      field: p.y
    end-structure
  POINT then pushes the total size and P.X / P.Y turn a base address
  into a field address. A field is an OFFSET added to whatever you give
  it, so the same names work on a near buffer or a far one. )
: begin-structure ( "name" -- addr 0 ) create here 0 0 , does> @ ;
: end-structure ( addr n -- ) swap ! ;
: +field ( n1 n2 "name" -- n3 ) create over , + does> @ + ;
: field: ( n1 "name" -- n2 ) 4 +field ;
: cfield: ( n1 "name" -- n2 ) 1 +field ;

( STRINGS - the rest of STRING.TXT. The BASIC-flavoured ones - LEFT,
  RIGHT, MID, STR, VAL - are here because the page promises them; MID
  counts from 1 like the BASIC it is named after, and everything else
  counts from 0 like the rest of Forth. Anything returning a fresh
  string builds it in PAD, which moves with HERE - copy it if you need
  it to outlive the next definition. )
: place ( addr len dst -- ) 2dup c! 1+ swap move ;
variable (pdst) variable (plen)
: +place ( addr len dst -- )
  (pdst) ! (plen) !
  (pdst) @ count + (plen) @ move
  (pdst) @ dup c@ (plen) @ + swap c! ;
: len ( c-addr u -- u ) nip ;
: asc ( c-addr u -- code ) drop c@ ;
: chr ( code -- c-addr 1 ) pad c! pad 1 ;
: left ( c-addr u n -- c-addr n2 ) min ;
: right ( c-addr u n -- c-addr2 n2 ) over min >r + r@ - r> ;
: mid ( c-addr u start len -- c-addr2 len2 ) >r 1- /string r> min ;
: rpt ( char n -- c-addr u )
  200 min dup >r 0 ?do dup pad i + c! loop drop pad r> ;
: str ( n -- c-addr u ) s>d tuck dabs <# #s rot sign #> ;
( SIGN EXTENSION, for a narrow value that is already in a cell.

  W@ and SW@ settle this when the value comes out of MEMORY. These are
  for when it does not: a pair of hardware registers read with IOC@, a
  field picked apart with AND and RSHIFT, a byte off a VERA port. The
  bits say nothing about their own signedness - $FFFF is 65535 and -1
  at the same time - so the word that knows which one it is has to say
  so, and this is how it says it. )
: w>n ( u -- n ) 65535 and dup 32768 and if 65536 - then ;
: c>n ( c -- n ) 255 and dup 128 and if 256 - then ;

: nhex ( u -- c-addr u ) base @ >r hex 0 <# #s #> r> base ! ;
: nbin ( u -- c-addr u ) base @ >r 2 base ! 0 <# #s #> r> base ! ;
: val ( c-addr u -- n )
  over c@ '-' = dup >r if 1 /string then
  0. 2swap >number 2drop drop r> if negate then ;
: sliteral ( addr len -- )
  postpone lits dup c, tuck here swap move allot ; immediate
: linput ( c-addr +n -- +n2 ) accept ;

( LOAD AND SAVE - raw bytes to and from memory or VRAM.

  No device number: there is no IEC bus, so there is nothing to
  address. No PRG header either - the X16's two-byte load address is a
  CBM convention, and an X816 program image is a different shape
  entirely - magic plus an entry at $01:0004, per X816_core
  doc/KERNEL.md. These move BYTES: what you save is what you get back.

  Memory addresses are full 24-bit cells, so BLOAD straight into
  far-allot space works with no bounce buffer - READ-FILE hands the
  kernel the address and the data lands there. VRAM is a separate
  address space reached through a port, so the VRAM pair does bounce
  through a 256-byte buffer. )
variable (bfd) variable (baddr) variable (blen)
variable (vb) variable (va)
create (vbuf) 256 allot

: bload ( c-addr u addr -- u ior )
  (baddr) !
  r/o open-file ?dup if nip 0 swap exit then (bfd) !
  (bfd) @ file-size
  ?dup if >r 2drop 0 r> (bfd) @ close-file drop exit then
  drop                                  ( the low cell is the whole size )
  (baddr) @ swap (bfd) @ read-file
  (bfd) @ close-file drop ;

: bsave ( c-addr u addr len -- ior )
  (blen) ! (baddr) !
  r/o create-file ?dup if nip exit then (bfd) !
  (baddr) @ (blen) @ (bfd) @ write-file
  (bfd) @ close-file drop ;

: (vpush) ( u -- ) 0 ?do (vbuf) i + c@ v! loop ;
: (vpull) ( u -- ) 0 ?do v@ (vbuf) i + c! loop ;

: vload ( c-addr u bank vaddr -- u ior )
  (va) ! (vb) !
  r/o open-file ?dup if nip 0 swap exit then (bfd) !
  (vb) @ (va) @ vaddr
  0 (blen) !
  begin
    (vbuf) 256 (bfd) @ read-file        ( got ior )
    ?dup if (bfd) @ close-file drop nip (blen) @ swap exit then
    dup 0= if drop true else dup (blen) +! (vpush) false then
  until
  (bfd) @ close-file drop
  (blen) @ 0 ;

: vsave ( c-addr u bank vaddr len -- ior )
  (blen) ! (va) ! (vb) !
  r/o create-file ?dup if nip exit then (bfd) !
  (vb) @ (va) @ vaddr
  begin (blen) @ 0 > while
    (blen) @ 256 min
    dup (vpull)
    dup negate (blen) +!
    (vbuf) swap (bfd) @ write-file
    ?dup if (bfd) @ close-file drop exit then
  repeat
  (bfd) @ close-file drop 0 ;

( TILESETS, TILEMAPS, SPRITES AND PALETTES - VLOAD and VSAVE with the
  address and the length filled in.

  Every one of these is a wrapper and nothing more, and that is the
  point: `s" LEVEL.MAP" 1 tmapload` says what it does, where
  `s" LEVEL.MAP" 1 $b000 vload` says it to somebody who already knows
  where layer 1's map is and how big it is. The hardware knows both -
  the map base, the tile base and the map's size are in VERA's own
  registers - so asking the caller to repeat them is asking for the
  chance to get them wrong.

  A LAYER, not an address: the X16's TILESAVE/TILELOAD took a VRAM
  address and its TMAPSAVE was hardwired to layer 1. Here you name the
  layer and the words read where it points. Layer 0 is the console on
  this machine, so `0 tmapsave` saves the text screen and `1 tmapsave`
  saves your game's.

  PAL-SAVE takes a RANGE, and that is about hardware, not taste. This
  machine cannot read back palette entries nobody wrote - see the head
  of mod/bmx.fs, which found it the hard way - so saving all 256 and
  loading them back installs garbage over the console's own colours.
  Save the entries you set. If you keep your palette in memory, BSAVE
  that instead and nothing depends on a readback at all. )

: (lreg) ( layer -- reg )       \ VERA's layer registers, 7 apart
  if $9f34 else $9f2d then ;
: (split17) ( u -- vbank vaddr ) dup 16 rshift swap 65535 and ;
: (mapdim) ( code -- n ) 32 swap lshift ;      \ 0-3 = 32/64/128/256 cells

: layer-map ( layer -- vbank vaddr )           \ MAPBASE holds addr 16:9
  (lreg) 1+ ioc@ 9 lshift (split17) ;
: layer-tiles ( layer -- vbank vaddr )         \ TILEBASE holds addr 16:11
  (lreg) 2 + ioc@ 252 and 9 lshift (split17) ;
: layer-map-size ( layer -- u )                \ cells * 2 bytes
  (lreg) ioc@ dup 4 rshift 3 and (mapdim)
  swap 6 rshift 3 and (mapdim) * 2* ;

: tileload ( c-addr u layer -- u ior ) layer-tiles vload ;
: tilesave ( c-addr u layer len -- ior ) >r layer-tiles r> vsave ;
: tmapload ( c-addr u layer -- u ior ) layer-map vload ;
: tmapsave ( c-addr u layer -- ior )
  dup >r layer-map r> layer-map-size vsave ;

\ Sprite attributes are eight bytes each from VRAM $1FC00. Byte 0 and the
\ low nibble of byte 1 are the image address >> 5; bit 7 of byte 1 picks
\ 8bpp over 4bpp; byte 7 carries the two size codes.
: (sprb) ( n i -- b ) swap 8 * $fc00 + + 1 swap vpeek ;
: (sprdim) ( code -- px ) 8 swap lshift ;      \ 0-3 = 8/16/32/64 pixels
: sprite-addr ( n -- vbank vaddr )
  dup 0 (sprb) swap 1 (sprb) 15 and 8 lshift or 5 lshift (split17) ;
: sprite-bytes ( n -- u )
  dup 7 (sprb) dup 4 rshift 3 and (sprdim)
  swap 6 rshift 3 and (sprdim) *
  swap 1 (sprb) 128 and 0= if 1 rshift then ;  \ 4bpp: half a byte a pixel
: sprite-load ( c-addr u n -- u ior ) sprite-addr vload ;
: sprite-save ( c-addr u n -- ior )
  dup >r sprite-addr r> sprite-bytes vsave ;

: pal-load ( c-addr u start -- u ior ) 2* $fa00 + 1 swap vload ;
: pal-save ( c-addr u start count -- ior )
  2* >r 2* $fa00 + 1 swap r> vsave ;

( HELP - the manual, on the card, in /FORTH/HELP.

  Card names are the topic TRUNCATED TO EIGHT characters and
  uppercased: the kernel's FAT32 reader skips long filenames on
  purpose, so ARITHMETIC.TXT would be invisible and travels as
  ARITHMET.TXT instead. All forty topics truncate uniquely, and both
  card builders check that - run-tests.sh and X816_core's mksdcard.py.

  A page is longer than a screen, so output pauses. Any key continues;
  ESC or Q stops. HELP on its own shows the index. )
create (hpath) 24 allot
create (hline) 100 allot
variable (hlen)
0 value (hfd)
0 value (hrow)

: (hupper) ( c -- c ) dup 'a' 'z' 1+ within if $20 - then ;

( Build "/FORTH/HELP/TOPIC.TXT" - twelve characters, up to eight more,
  four more - and hand back the whole thing as a string.

  ABSOLUTE, so HELP works from whatever directory the reader is in.
  The pages moved from /HELP to /FORTH/HELP when the card grew a folder
  per language. The 12 below is that prefix, and the hpath buffer's 24
  bytes is exactly 12 + 8 + 4 with nothing spare. )
: (hpath!) ( c-addr u -- c-addr u )
  8 min dup (hlen) !
  s" /FORTH/HELP/" (hpath) swap move
  0 ?do
    dup i + c@ (hupper) (hpath) 12 + i + c!
  loop drop
  s" .TXT" (hpath) 12 + (hlen) @ + swap move
  (hpath) (hlen) @ 16 + ;

( true = the reader asked to stop. ESC or Q; anything else carries on. )
: (hpause) ( -- flag )
  ." -- more --" key dup $1b = swap
  dup 'q' = swap 'Q' = or or cr ;

: (hshow) ( -- )
  0 to (hrow)
  begin
    (hline) 100 (hfd) read-line      ( u2 flag ior )
    if 2drop true                    ( an ior: stop )
    else
      if                             ( a line )
        (hline) swap type cr
        (hrow) 1+ to (hrow)
        (hrow) 22 = if 0 to (hrow) (hpause) else false then
      else drop true                 ( end of file: stop )
      then
    then
  until ;

: help ( "topic" -- )
  parse-name dup 0= if 2drop s" INDEX" then
  (hpath!) r/o open-file ( fileid ior )
  if drop cr ." no help for that - try HELP INDEX" cr exit then
  to (hfd)
  cr (hshow)
  (hfd) close-file drop ;

cr
( the machine's two spaces: program in the four single-cycle banks
  via HERE/ALLOT, data in SDRAM from bank $05 up via FAR-HERE and
  FAR-ALLOT, stopping wherever MEM-TOP says. The top 4 MB, banks
  $C0-$FF, are the kernel writable-data region, the VERA2 window
  and the kernel firmware - and the first of those three is
  RELEASABLE, so the data figure below is 2 MB larger after a
  MEM-RELEASE. That is why it is printed from the queried value
  and not from a constant: a stale boundary is this arrangement's
  failure mode, and the number being on screen is the standing
  check against it. )
( K and M, not bytes. The two figures are four and seven digits wide, and
  at that length nobody reads them as sizes - "224210" and "12582912" are
  a wall of digits that hide the very thing they are here to show, which
  is whether the boundary moved. 219 K and 12 M are read at a glance, and
  a MEM-RELEASE turning 12 M into 14 M is visible from across the room.

  The MHz line is gone from here: it now leads the banner at the top of
  this file, where it is the first thing on screen. )
unused 1024 /
0 u.r .( K program, fast ram.) cr
far-unused 1048576 /
0 u.r .( M data, sdram.) cr

\ SYSTEM IS INCLUDED HERE, AT THE END, NOT UP WITH THE OTHER MODULES.
\ It is in the boot chain at all so BYE exists at the prompt without anyone
\ typing INCLUDE SYSTEM -- Forth is launched from the desktop's FORTH tile,
\ and BYE is how a person expects to leave a Forth.
\ It has to be DOWN HERE because BYE resets the machine through the SMC and
\ the (I2C-*) words it needs are defined at the i2c block above, ~570 lines
\ below the module list. Included up there it compiles against words that do
\ not exist yet, COLD aborts, and the only symptom is make-turnkey.sh saying
\ "produced no image" long afterwards.
.( system..) include system

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
