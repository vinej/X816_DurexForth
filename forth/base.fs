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

  NOT the kernel heap. MEM_ALLOC's arena starts at $20:0000 -
  KERNEL.md 5.5 - and overlaps this space, including the block
  table in its first page. Nothing in this Forth calls MEM_ALLOC -
  no binding exists, and the kernel's own FS does not allocate -
  so the arena is dormant and this pointer owns the space.
  Whoever binds MEM_ALLOC has to carve the two apart first. )
$50000 constant sdram ( first data address in SDRAM, flat )
$e00000 sdram - constant sdram-size
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

( SYSCTL $9F80: bit 0 the boot overlay - dropped long before this
  runs - bit 1 the live E flag, bit 2 TURBO: 0 paces the CPU to an
  exact 8 MHz average, 1 releases the domain's full 14 MHz. The bit
  flips safely at any time. Reads return the EFFECTIVE speed - the
  MiSTer OSD's CPU Turbo option ORs over the software bit - so after
  0 turbo the machine may truthfully still say it is fast. )
: turbo? ( -- flag ) $9f80 ioc@ 4 and 0<> ;
: turbo ( flag -- ) 0<> 4 and $9f80 ioc! ;
: cpu-mhz ( -- u ) turbo? if 14 else 8 then ;

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

( HELP - the manual, on the card, in /HELP.

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

( Build "/HELP/TOPIC.TXT" - six characters, up to eight more, four
  more - and hand back the whole thing as a string. )
: (hpath!) ( c-addr u -- c-addr u )
  8 min dup (hlen) !
  s" /HELP/" (hpath) swap move
  0 ?do
    dup i + c@ (hupper) (hpath) 6 + i + c!
  loop drop
  s" .TXT" (hpath) 6 + (hlen) @ + swap move
  (hpath) (hlen) @ 10 + ;

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
  via HERE/ALLOT, data in SDRAM from bank $05 up to $DF via
  FAR-HERE/FAR-ALLOT - the top 2 MB, banks $E0-$FF, belong to the
  VERA2 window and the kernel firmware. )
cpu-mhz
0 u.r space .( MHz cpu.) cr
unused
0 u.r space .( bytes program, fast ram.) cr
far-unused
0 u.r space .( bytes data, sdram.) cr

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
