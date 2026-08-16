\ The console font: CHARSET, FONT-COPY, GLYPH! / GLYPH@ (base.fs).
\
\ These assert the VRAM LAYOUT as much as the words - that the kernel's
\ font really is 256 glyphs of 8 bytes at FONT, that the tile index is
\ the character code with no bias, and that FONT2 is free space and not
\ the tilemap's tail. Every one of those is a claim base.fs's comment
\ makes, and a claim about somebody else's memory map is exactly the
\ kind that rots quietly. Requires tester.fs.
\
\ The live font is deliberately NOT modified: this file runs inside the
\ suite, and a suite that scrambles the glyphs it reports through has
\ no way to tell you it failed. Everything is done in FONT2.

marker ---testfont---

\ Standalone-safe: REQUIRE loads the Hayes tester only if it is not
\ already in, so `include test/testfont` works on its own at the prompt and
\ costs nothing inside the suite, where test.fs loaded it first.
require test/tester

decimal

cr .( testfont: the font is where base.fs says, and readable ) cr
create (g) 8 allot
create (g2) 8 allot
T{ font 65 glyph-addr -> font 520 + }T          \ 65*8, no $20 bias
T{ font2 font - -> 2048 }T                      \ exactly one font apart

\ "A" is $38 $6C $C6 $C6 $FE $C6 $C6 $00 in the kernel's font_cp437.s -
\ read from that file, not from memory of what an 8x8 "A" looks like:
\ the obvious $30 $78 $CC... is a DIFFERENT public-domain 8x8 font and
\ writing it here cost one run. Reading the glyph back proves the base
\ address, the 8-byte stride and the tile-index-is-the-code claim in one
\ assertion - a wrong base or a $20 bias lands on another glyph entirely.
font 65 (g) glyph@
T{ (g) c@ -> $38 }T
T{ (g) 1+ c@ -> $6c }T
T{ (g) 2 + c@ -> $c6 }T
T{ (g) 4 + c@ -> $fe }T
T{ (g) 7 + c@ -> 0 }T

\ ...and the space at 32 is blank, which a mis-scaled index would not be.
font 32 (g2) glyph@
T{ (g2) c@ (g2) 1+ c@ or (g2) 2 + c@ or (g2) 7 + c@ or -> 0 }T

cr .( testfont: font-copy duplicates all 2 KB ) cr
\ Dirty the destination first: a copy that silently did nothing would
\ otherwise pass against whatever VRAM happened to hold.
0 font2 0 vpoke  0 font2 1023 + 99 vpoke  0 font2 2047 + 99 vpoke
font font2 font-copy
font2 65 (g2) glyph@
T{ (g2) c@ -> $38 }T
T{ (g2) 4 + c@ -> $fe }T
\ The last byte of the 2 KB, which an off-by-one length never reaches.
\ Both source bytes happen to be 0, so these compare would pass against
\ untouched VRAM - dirtying with 99 first is what gives them teeth.
T{ 0 font2 2047 + vpeek -> 0 font 2047 + vpeek }T
T{ 0 font2 1023 + vpeek -> 0 font 1023 + vpeek }T

cr .( testfont: glyph! writes, glyph@ reads it back ) cr
$ff (g) c!  $81 (g) 1+ c!  $42 (g) 2 + c!  $24 (g) 3 + c!
$18 (g) 4 + c!  $24 (g) 5 + c!  $42 (g) 6 + c!  $81 (g) 7 + c!
(g) font2 200 glyph!
font2 200 (g2) glyph@
T{ (g2) c@ -> $ff }T
T{ (g2) 1+ c@ -> $81 }T
T{ (g2) 4 + c@ -> $18 }T
T{ (g2) 7 + c@ -> $81 }T
\ ...and it landed in VRAM where glyph-addr says, not merely in a buffer
T{ 0 font2 200 glyph-addr vpeek -> $ff }T
\ The NEIGHBOURS are untouched - an 8-byte write that ran long would
\ smear into glyph 201, and nothing else here would notice. Glyphs 199
\ and 201 are box-drawing pieces, so these are non-zero on purpose: a
\ zero expectation would also match a wiped font.
T{ 0 font2 201 glyph-addr 2 + vpeek -> $3f }T   \ glyph 201, byte 2
T{ 0 font2 199 glyph-addr 7 + vpeek -> $28 }T   \ glyph 199, last byte

cr .( testfont: charset points layer 0 at a font, and back ) cr
\ Tile base is addr>>11 in the high six bits of $9F2F (layer 0 - the X16
\ used layer 1, and $9F36 is that one; reading the wrong register here
\ would pass while the console never changed).
font2 charset
T{ $9f2f ioc@ 252 and -> font2 11 rshift 2* 2* }T
font charset
T{ $9f2f ioc@ 252 and -> font 11 rshift 2* 2* }T

cr .( testfont ok ) cr

---testfont---
