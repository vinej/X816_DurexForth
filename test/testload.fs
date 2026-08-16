\ BLOAD / BSAVE / VLOAD / VSAVE - raw bytes to and from memory and VRAM
\ (base.fs, over the file words). WRITES TO THE CARD and cleans up after
\ itself, like testfile and testdir.
\
\ There is no device number and no PRG header here: no IEC bus to address
\ and no CBM load-address convention. What you save is what you get back,
\ which is exactly what these assert.

marker ---testload---

\ Standalone-safe: REQUIRE loads the Hayes tester only if it is not
\ already in, so `include test/testload` works on its own at the prompt and
\ costs nothing inside the suite, where test.fs loaded it first.
require test/tester

decimal

\ 300 bytes, not 256: the VRAM pair bounces through a 256-byte buffer, so
\ a length that is exactly one chunk would never exercise the second
\ iteration or the partial tail. Every size here is deliberately awkward.
300 constant #b
create src #b allot
create dst #b allot
: (fill) ( addr -- ) #b 0 do i 255 and over i + c! loop drop ;
: (wipe) ( addr -- ) #b 0 do 0 over i + c! loop drop ;
src (fill)

cr .( testload: bsave then bload, byte for byte ) cr
T{ s" BLTEST.BIN" src #b bsave -> 0 }T
T{ s" BLTEST.BIN" file-status nip -> 0 }T
dst (wipe)
T{ s" BLTEST.BIN" dst bload -> #b 0 }T          \ all 300 bytes, ior 0
T{ src #b dst #b compare -> 0 }T
\ The file is exactly the length asked for - no header snuck in front.
variable fd
T{ s" BLTEST.BIN" r/o open-file swap fd ! -> 0 }T
T{ fd @ file-size -> #b 0 0 }T
T{ fd @ close-file -> 0 }T

cr .( testload: bload straight into SDRAM ) cr
\ READ-FILE hands the kernel a full 24-bit address, so far space needs no
\ bounce buffer. This is the assertion that proves it: a shim that only
\ passed 16 bits would land the data in bank 1 and this would not match.
\ FAR-BUFFER: because FAR-ALLOT takes a size and returns nothing.
#b far-buffer: fbuf
T{ s" BLTEST.BIN" fbuf bload -> #b 0 }T
T{ src #b fbuf #b compare -> 0 }T
T{ fbuf $50000 u< -> false }T                   \ really above the fast banks

cr .( testload: vsave then vload, through VRAM ) cr
\ Colon definitions, because DO ... LOOP is COMPILE-ONLY: typed straight
\ at the interpreter it compiles into HERE and never runs, leaving the
\ loop's operands on the stack to surface as a WRONG NUMBER later.
: (paint) ( bank addr -- ) vaddr #b 0 do i 255 and v! loop ;
: (scrub) ( bank addr -- ) vaddr #b 0 do 0 v! loop ;
0 $8000 (paint)
T{ s" VLTEST.BIN" 0 $8000 #b vsave -> 0 }T
0 $9000 (scrub)
T{ s" VLTEST.BIN" 0 $9000 vload -> #b 0 }T
0 value (bad)
: (vcheck) 0 $9000 vaddr #b 0 do v@ i 255 and <> if -1 to (bad) leave then loop ;
(vcheck)
T{ (bad) -> 0 }T
\ ...and the source is still there: VSAVE reads VRAM, it does not consume it.
0 to (bad)
: (scheck) 0 $8000 vaddr #b 0 do v@ i 255 and <> if -1 to (bad) leave then loop ;
(scheck)
T{ (bad) -> 0 }T

cr .( testload: tilesets, tilemaps, sprites and palettes ) cr
\ The wrappers, and what they are for: every one is VLOAD or VSAVE with
\ the address and the length read out of VERA instead of typed by hand.
\ So the assertions are about the ARITHMETIC - does a layer's map base,
\ map size, tile base, and a sprite's image address and pixel count come
\ back the way the registers say - and then that the bytes survive.
\ Layer 1 throughout. Layer 0 is the console on this machine, and a test
\ that saved and reloaded the text screen would be writing over the thing
\ every later file prints on.

\ 64x32 cells at $10000 (VRAM bank 1), tiles at $12000, so nothing here
\ touches the console's map at $00000 or its font at $04000.
1 1 $0000 mapbase
1 1 $2000 tilebase
1 $10 layer-mode                        \ %0001_0000: width code 1 = 64
                                        \ cells, height code 0 = 32

T{ 1 layer-map -> 1 $0000 }T
T{ 1 layer-tiles -> 1 $2000 }T
T{ 1 layer-map-size -> 4096 }T          \ 64 * 32 cells * 2 bytes

: (mpaint) 1 $0000 vaddr 4096 0 do i 255 and v! loop ;
: (mscrub) 1 $0000 vaddr 4096 0 do 0 v! loop ;
(mpaint)
T{ s" TMTEST.BIN" 1 tmapsave -> 0 }T
(mscrub)
T{ s" TMTEST.BIN" 1 tmapload -> 4096 0 }T
0 to (bad)
: (mcheck) 1 $0000 vaddr 4096 0 do v@ i 255 and <> if -1 to (bad) leave then loop ;
(mcheck)
T{ (bad) -> 0 }T

\ Tile data has no inherent length - a tileset is as long as it is - so
\ TILESAVE takes one and TILELOAD does not need one.
: (tpaint) 1 $2000 vaddr 512 0 do i 255 and v! loop ;
: (tscrub) 1 $2000 vaddr 512 0 do 0 v! loop ;
(tpaint)
T{ s" TSTEST.BIN" 1 512 tilesave -> 0 }T
(tscrub)
T{ s" TSTEST.BIN" 1 tileload -> 512 0 }T
0 to (bad)
: (tcheck) 1 $2000 vaddr 512 0 do v@ i 255 and <> if -1 to (bad) leave then loop ;
(tcheck)
T{ (bad) -> 0 }T

\ Sprite 5: image at VRAM $4000 of bank 1, 16x16. SPRITE-MEM leaves the
\ mode bit clear (both it and SPRITE-IMAGE write 4bpp), so the pixel
\ count is 16*16/2 = 128 bytes - and that halving is the assertion worth
\ having, because a save that believed 8bpp would put 128 bytes of
\ somebody else's VRAM in the file.
5 1 $4000 sprite-mem
1 1 5 sprite-size                       \ size codes 1,1 = 16 x 16
T{ 5 sprite-addr -> 1 $4000 }T
T{ 5 sprite-bytes -> 128 }T

: (spaint) 1 $4000 vaddr 128 0 do i 255 and v! loop ;
: (sscrub) 1 $4000 vaddr 128 0 do 0 v! loop ;
(spaint)
T{ s" SPTEST.BIN" 5 sprite-save -> 0 }T
(sscrub)
T{ s" SPTEST.BIN" 5 sprite-load -> 128 0 }T
0 to (bad)
: (scheck2) 1 $4000 vaddr 128 0 do v@ i 255 and <> if -1 to (bad) leave then loop ;
(scheck2)
T{ (bad) -> 0 }T

\ Palette entries 240-255, never 0-15: the console owns the low sixteen,
\ and PAL-SAVE can only be trusted on entries this program wrote - the
\ readback does not answer for anything else. That is why the word takes
\ a range instead of assuming all 256.
: (ppaint) 16 0 do i 16 * 1+ 240 i + pal! loop ;
: (pscrub) 16 0 do $0999 240 i + pal! loop ;
: (pv@) ( entry -- lo ) 240 + 2* $fa00 + 1 swap vpeek ;
(ppaint)
T{ s" PLTEST.BIN" 240 16 pal-save -> 0 }T
(pscrub)
T{ 0 (pv@) -> $99 }T
T{ s" PLTEST.BIN" 240 pal-load -> 32 0 }T
T{ 0 (pv@) 1 (pv@) 15 (pv@) -> 1 17 241 }T

cr .( testload: a missing file is reported, not guessed at ) cr
dst (wipe)
T{ s" NOSUCH.BIN" dst bload nip 0<> -> true }T  \ nonzero ior
T{ dst c@ -> 0 }T                               \ and nothing was written
T{ s" NOSUCH.BIN" 0 $9000 vload nip 0<> -> true }T

cr .( testload: clean up ) cr
T{ s" BLTEST.BIN" delete-file -> 0 }T
T{ s" VLTEST.BIN" delete-file -> 0 }T
T{ s" TMTEST.BIN" delete-file -> 0 }T
T{ s" TSTEST.BIN" delete-file -> 0 }T
T{ s" SPTEST.BIN" delete-file -> 0 }T
T{ s" PLTEST.BIN" delete-file -> 0 }T
\ No FAR-EMPTY here: that would reset the far pointer for everyone, and
\ unwinding ---testload--- puts it back on its own (MARKER saves it).

cr .( testload ok ) cr

---testload---
