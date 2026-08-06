\ BMX - the image format, round-tripped through the card (forth/mod/bmx.fs).
\
\ The round trip is the point: a known pattern goes to VRAM, out to a file,
\ back into DIFFERENT VRAM, and every byte is compared. A mis-seeked
\ palette gap or an off-by-one at a 256-byte bounce-buffer seam is a wrong
\ pixel here, not a plausible-looking screen. The library's own
\ run-libbmx.sh proves the same property for the assembly implementation.
\
\ The four refusals are tested against files this test BUILDS, because a
\ malformed BMX is not something you can keep on the card by accident.
\ IF THE PALETTE ASSERTIONS FAIL ON HARDWARE, read them as a finding and
\ not as a broken module. They say that entries this test wrote through
\ the VERA port read back as what was written - true in the emulator,
\ and the board has not answered yet. If the MiSTer's VERA keeps its
\ palette in a RAM the CPU cannot read (an ordinary way to build one),
\ these three lines are where it says so. Nothing else here depends on
\ it: BMX-PAL exists exactly so a program never has to ask.

marker ---testbmx---

\ Standalone-safe: REQUIRE loads the Hayes tester only if it is not already
\ in, so `include testbmx` works on its own at the prompt and costs nothing
\ inside the suite, where test.fs loaded it first.
require tester

include bmx

decimal

\ Three windows into VRAM bank 0, all clear of the console: the source
\ stamp, the destination, and a second destination for the stride check.
: src!  ( val off -- ) $8000 + 0 swap rot vpoke ;
: dst!  ( val off -- ) $8300 + 0 swap rot vpoke ;
: dst@  ( off -- c )   $8300 + 0 swap vpeek ;
: dst2@ ( off -- c )   $8600 + 0 swap vpeek ;

\ An 8x2 stamp with every byte distinct, so a row landing at the wrong
\ address cannot pass by accident.
: mkstamp 8 0 do i 10 + i src!  i 20 + i 320 + src! loop ;
: wipedst 16 0 do 0 i dst!  0 i 320 + dst!  0 i $0300 + dst! loop ;

\ THE PALETTE IS ENTRIES 240-255, and both reasons matter.
\ The console owns 0-15, and a test that repaints those has changed the
\ machine out from under every file that runs after it. And VERA's palette
\ DOES NOT READ BACK on this hardware: entries this test writes come back
\ exactly, entries nobody wrote come back as something that is not the
\ palette in use at all. Saving those and loading them installs the
\ garbage for real - which is how the first version of this file blanked
\ the suite's screen for every test after it. So the palette this test
\ round-trips is a palette this test WROTE.
: mkpal   16 0 do i 16 * 1+ 240 i + pal! loop ;   \ 16 distinct colours
: wipepal 16 0 do $0999 240 i + pal! loop ;
: pv@ ( entry -- lo ) 240 + 2* $fa00 + 1 swap vpeek ;

\ A 16-byte header built by hand, for the files that must be REFUSED.
create hb 16 allot
variable tfd
: hbclr   16 0 do 0 hb i + c! loop ;
: hbmagic 'B' hb c!  'M' hb 1+ c!  'X' hb 2 + c!  1 hb 3 + c! ;
: mkfile ( c-addr u n -- )              \ n bytes of HB into the named file
  >r w/o create-file drop tfd !
  hb r> tfd @ write-file drop
  tfd @ close-file drop ;

cr .( testbmx: save an 8x2 stamp ) cr
mkstamp mkpal
8 bmx-width !  2 bmx-height !  8 bmx-bpp !
240 bmx-palstart !  16 bmx-palcount !  320 bmx-stride !  7 bmx-border !
T{ s" TBMX.BMX" 0 $8000 bmx-save -> 0 }T

cr .( testbmx: load it back somewhere else ) cr
wipedst
0 bmx-width !  0 bmx-height !  0 bmx-border !
wipepal
T{ 0 pv@ 15 pv@ -> $99 $99 }T         \ the palette really is clobbered
T{ s" TBMX.BMX" 0 $8300 bmx-load -> 0 }T
T{ bmx-width @ bmx-height @ bmx-bpp @ -> 8 2 8 }T
T{ bmx-palstart @ bmx-palcount @ bmx-border @ -> 240 16 7 }T
T{ 0 dst@ 7 dst@ -> 10 17 }T
T{ 320 dst@ 327 dst@ -> 20 27 }T
T{ 8 dst@ 328 dst@ -> 0 0 }T            \ a stamp leaves its surroundings
T{ 0 pv@ 1 pv@ 15 pv@ -> 1 17 241 }T \ ...and the palette came back

cr .( testbmx: BMX-PAL, the palette from memory ) cr
\ The supported path on this machine: the program keeps its own palette,
\ so nothing depends on reading VERA back. BMX-SAVE writes that buffer,
\ and BMX-LOAD fills it while it installs what it read.
create pbuf 32 allot
: pbuf! 32 0 do i 3 * 200 + 255 and pbuf i + c! loop ;
: pbuf0 32 0 do 0 pbuf i + c! loop ;
pbuf! pbuf to bmx-pal
T{ s" TBP.BMX" 0 $8000 bmx-save -> 0 }T
pbuf0 wipepal
T{ s" TBP.BMX" 0 $8300 bmx-load -> 0 }T
T{ pbuf c@ pbuf 1+ c@ pbuf 31 + c@ -> 200 203 37 }T   \ the buffer refilled
T{ 0 pv@ -> 200 }T                                   \ ...and VERA took it
0 to bmx-pal

cr .( testbmx: the stride decides where the rows land ) cr
\ Same file, rows packed instead of 320 apart. Nothing about the file
\ changes: the stride is the CALLER's description of the target.
8 bmx-stride !
T{ s" TBMX.BMX" 0 $8600 bmx-load -> 0 }T
T{ 0 dst2@ 7 dst2@ -> 10 17 }T
T{ 8 dst2@ 15 dst2@ -> 20 27 }T         \ row 1 immediately after row 0
320 bmx-stride !

cr .( testbmx: the four refusals ) cr
T{ s" NOSUCH.BMX" 0 $8300 bmx-load -> 1 }T      \ no such file

hbclr  s" TBAD.BMX" 16 mkfile
T{ s" TBAD.BMX" 0 $8300 bmx-load -> 2 }T        \ no BMX magic

hbclr hbmagic  1 hb 14 + c!  s" TCMP.BMX" 16 mkfile
T{ s" TCMP.BMX" 0 $8300 bmx-load -> 3 }T        \ compressed

\ 256 entries starting at 255 would run 512 bytes from $1FA00 straight
\ through the sprite attributes at $1FC00. The FILE is wrong, so it is
\ refused rather than clamped to something it never said.
hbclr hbmagic  0 hb 10 + c!  255 hb 11 + c!  s" TPAL.BMX" 16 mkfile
T{ s" TPAL.BMX" 0 $8300 bmx-load -> 2 }T        \ palette would not fit

\ A header promising one palette entry and a file that stops at the
\ header: the read that hits the end hands back fewer bytes than it was
\ asked for, and that short read is the only notice there will be.
hbclr hbmagic  8 hb 4 + c!  3 hb 5 + c!
1 hb 10 + c!  18 hb 12 + w!  8 hb 6 + w!  2 hb 8 + w!
s" TCUT.BMX" 16 mkfile
T{ s" TCUT.BMX" 0 $8300 bmx-load -> 1 }T        \ truncated

cr .( testbmx: a refused file publishes nothing ) cr
\ A program that checks the ior and then reports BMX-WIDTH must not be
\ shown a number from a file that was never loaded.
99 bmx-width !  99 bmx-height !
T{ s" TBAD.BMX" 0 $8300 bmx-load -> 2 }T
T{ bmx-width @ bmx-height @ -> 99 99 }T

cr .( testbmx: BMX-SAVE refuses the same impossible palette ) cr
\ Checked BEFORE the file is created: writing a header no loader will
\ accept is not a success, and the VRAM readback would gather sprite
\ attributes as though they were colours.
8 bmx-width !  2 bmx-height !
255 bmx-palstart !  256 bmx-palcount !
T{ s" TPAL2.BMX" 0 $8000 bmx-save -> 2 }T
240 bmx-palstart !  16 bmx-palcount !

cr .( testbmx ok ) cr

---testbmx---
