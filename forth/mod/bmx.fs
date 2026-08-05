\ BMX - the community X16 bitmap image format, version 1 (ADVANCED.TXT).
\ Cart: NEEDS BMX      SD card: INCLUDE BMX
\ The format Prog8 and the community tools write: 16-byte header, palette
\ (2 bytes/entry, VERA layout), then 8-bpp pixel rows.  Rows are BMX-STRIDE
\ bytes apart in VRAM (default 320 = the full bitmap; narrower images load
\ as stamps).  BMX-SAVE's palette comes from VRAM readback: only meaningful
\ if this program wrote those entries (PAL! or an earlier BMX-LOAD).
\ ior: 0 = ok, 1 = i/o error, 2 = not BMX v1, 3 = compressed (unsupported).

decimal

\ --- header fields (set by BMX-LOAD; set BEFORE calling BMX-SAVE) --------------
variable bmx-width    variable bmx-height
variable bmx-bpp      8 bmx-bpp !
variable bmx-palstart 0 bmx-palstart !
variable bmx-palcount 256 bmx-palcount !
variable bmx-border   0 bmx-border !
variable bmx-stride   320 bmx-stride !

\ --- channel words (self-contained; SP saved on the hardware stack) ------------
\ rtl, not rts,. Every word in this Forth is entered with jsl and must
\ leave with rtl: an rts ($60 against $6b) pops two bytes where three went
\ on, so the bank byte stays and the NEXT rtl returns into nowhere - at a
\ distance that depends on what ran in between. These were all rts.
code chkin ( file# -- ior )
txa, pha,
lsb lda,x tax,
$ffc6 jsr,
+branch bcs,
0 lda,#
:+
tay, pla, tax, tya,
lsb sta,x
0 lda,# msb sta,x
rtl, end-code

code chkout ( file# -- ior )
txa, pha,
lsb lda,x tax,
$ffc9 jsr,
+branch bcs,
0 lda,#
:+
tay, pla, tax, tya,
lsb sta,x
0 lda,# msb sta,x
rtl, end-code

code clrchn ( -- )
txa, pha,
$ffcc jsr,
pla, tax,
rtl, end-code

code readst ( -- status )
txa, pha,
$ffb7 jsr,
tay, pla, tax, dex, tya,
lsb sta,x
0 lda,# msb sta,x
rtl, end-code

code chrin ( -- chr )
txa, pha,
$ffcf jsr,
tay, pla, tax, dex, tya,
lsb sta,x
0 lda,# msb sta,x
rtl, end-code

: (la>) ( -- ) source-id dup 0 > if chkin drop else drop clrchn then ;

\ --- fast streaming: input channel <-> VERA DATA0 -------------------------------
variable bm-n
code (n>vram) ( n -- )                \ n >= 1 bytes: CHRIN -> DATA0
lsb lda,x bm-n sta,
msb lda,x bm-n 1+ sta,
inx,
txa, pha,
:-
$ffcf jsr,
$9f23 sta,
sec, bm-n lda, 1 sbc,# bm-n sta,
bm-n 1+ lda, 0 sbc,# bm-n 1+ sta,
bm-n lda, bm-n 1+ ora,
-branch bne,
pla, tax,
rtl, end-code

code (vram>n) ( n -- )                \ n >= 1 bytes: DATA0 -> CHROUT
lsb lda,x bm-n sta,
msb lda,x bm-n 1+ sta,
inx,
txa, pha,
:-
$9f23 lda,
$ffd2 jsr,
sec, bm-n lda, 1 sbc,# bm-n sta,
bm-n 1+ lda, 0 sbc,# bm-n 1+ sta,
bm-n lda, bm-n 1+ ora,
-branch bne,
pla, tax,
rtl, end-code

\ --- shared plumbing --------------------------------------------------------------
create bmhdr 16 allot
variable bmbank  variable bmaddr  variable bmrow
create bmnb 40 allot  variable bmnl        \ file name + ",S,x" suffix
: (bm-name) ( c-addr u c -- addr u' )      \ build [@:]name,S,c at bmnb
  >r 0 bmnl !
  r@ 'W' = if '@' bmnb c! ':' bmnb 1+ c! 2 bmnl ! then
  dup 32 > if 2drop bmnb 0 r> drop exit then
  dup >r bmnb bmnl @ + swap cmove
  bmnl @ r> + bmnl !
  ',' bmnb bmnl @ + c! 'S' bmnb bmnl @ 1+ + c! ',' bmnb bmnl @ 2 + + c!
  r> bmnb bmnl @ 3 + + c!
  bmnb bmnl @ 4 + ;
: (bm-row+) ( -- )                          \ advance bmaddr by the stride
  bmaddr @ bmx-stride @ +  dup bmaddr @ u< if 1 bmbank +! then  bmaddr ! ;
: (bm-close) ( -- ) clrchn 12 close (la>) ;

\ --- BMX-LOAD ------------------------------------------------------------------------
: (bm-hdr?) ( -- ior )                      \ read + validate the header
  16 0 do chrin bmhdr i + c! loop
  readst if 1 exit then                     \ missing file: junk + status set
  bmhdr c@ 'B' <>  bmhdr 1+ c@ 'M' <> or  bmhdr 2 + c@ 'X' <> or
  bmhdr 3 + c@ 1 <> or if 2 exit then
  bmhdr 14 + c@ if 3 exit then
  bmhdr 4 + c@ bmx-bpp !
  bmhdr 6 + @ bmx-width !   bmhdr 8 + @ bmx-height !
  bmhdr 10 + c@ ?dup 0= if 256 then bmx-palcount !
  bmhdr 11 + c@ bmx-palstart !
  bmhdr 15 + c@ bmx-border !  0 ;

: bmx-load ( c-addr u dev vbank vaddr -- ior )
  bmaddr ! bmbank ! device
  'R' (bm-name) 12 12 open ?dup if drop 1 exit then
  12 chkin ?dup if drop (bm-close) 1 exit then
  (bm-hdr?) ?dup if (bm-close) exit then
  1 $fa00 bmx-palstart @ 2* + vaddr         \ palette -> VERA $1FA00+
  bmx-palcount @ 2* (n>vram)
  bmhdr 12 + @ 16 bmx-palcount @ 2* + -     \ gap up to the pixel offset
  ?dup if 0 max 0 ?do chrin drop loop then
  bmx-width @ bmx-bpp @ * 8 /               \ bytes per row
  bmx-height @ 0 ?do
    bmbank @ bmaddr @ vaddr
    dup ?dup if (n>vram) then
    readst dup $40 and 0= swap 0<> and if   \ error mid-file (EOF on the
      drop (bm-close) 1 unloop exit then    \ last row is expected)
    (bm-row+)
  loop drop
  (bm-close) 0 ;

\ --- BMX-SAVE ------------------------------------------------------------------------
: (bm-h!) ( -- )                            \ build the header from the vars
  'B' bmhdr c!  'M' bmhdr 1+ c!  'X' bmhdr 2 + c!  1 bmhdr 3 + c!
  bmx-bpp @ bmhdr 4 + c!
  bmx-bpp @ dup 8 = if drop 3 else dup 4 = if drop 2 else 2 = if 1 else 0 then then then
  bmhdr 5 + c!
  bmx-width @ bmhdr 6 + !  bmx-height @ bmhdr 8 + !
  bmx-palcount @ dup 256 = if drop 0 then bmhdr 10 + c!
  bmx-palstart @ bmhdr 11 + c!
  16 bmx-palcount @ 2* + bmhdr 12 + !
  0 bmhdr 14 + c!  bmx-border @ bmhdr 15 + c! ;

: bmx-save ( c-addr u dev vbank vaddr -- ior )
  bmaddr ! bmbank ! device
  'W' (bm-name) 12 12 open ?dup if drop 1 exit then
  12 chkout ?dup if drop (bm-close) 1 exit then
  (bm-h!)
  16 0 do bmhdr i + c@ emit loop
  1 $fa00 bmx-palstart @ 2* + vaddr
  bmx-palcount @ 2* (vram>n)
  bmx-width @ bmx-bpp @ * 8 /
  bmx-height @ 0 ?do
    bmbank @ bmaddr @ vaddr
    dup ?dup if (vram>n) then
    (bm-row+)
  loop drop
  (bm-close) 0 ;
