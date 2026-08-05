\ BLOAD / BSAVE / VLOAD / VSAVE - raw bytes to and from memory and VRAM
\ (base.fs, over the file words). WRITES TO THE CARD and cleans up after
\ itself, like testfile and testdir.
\
\ There is no device number and no PRG header here: no IEC bus to address
\ and no CBM load-address convention. What you save is what you get back,
\ which is exactly what these assert.

marker ---testload---

\ Standalone-safe: REQUIRE loads the Hayes tester only if it is not
\ already in, so `include testload` works on its own at the prompt and
\ costs nothing inside the suite, where test.fs loaded it first.
require tester

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

cr .( testload: a missing file is reported, not guessed at ) cr
dst (wipe)
T{ s" NOSUCH.BIN" dst bload nip 0<> -> true }T  \ nonzero ior
T{ dst c@ -> 0 }T                               \ and nothing was written
T{ s" NOSUCH.BIN" 0 $9000 vload nip 0<> -> true }T

cr .( testload: clean up ) cr
T{ s" BLTEST.BIN" delete-file -> 0 }T
T{ s" VLTEST.BIN" delete-file -> 0 }T
\ No FAR-EMPTY here: that would reset the far pointer for everyone, and
\ unwinding ---testload--- puts it back on its own (MARKER saves it).

cr .( testload ok ) cr

---testload---
