\ BMX - the community X16 bitmap image format, version 1 (ADVANCED.TXT).
\ Cart: NEEDS BMX      SD card: INCLUDE BMX
\
\ A 16-byte header, then the palette (2 bytes an entry, in VERA's own
\ layout), then the pixel rows. Rows land BMX-STRIDE bytes apart in VRAM -
\ 320 is the full-screen bitmap, so a 320-wide image is a plain contiguous
\ load and a narrower one arrives as a STAMP with the pixels around it
\ untouched. X816_Library's storage/bmx.asm is the authority for the format
\ and this follows its shape; regenerate nothing, but read that file before
\ changing what a byte means here.
\
\ THE CHANNEL MODEL IS GONE, and it was most of the old file. This was
\ durexForth's heaviest KERNAL user: OPEN a logical file on device 8 with a
\ ",S,R" name, CHKIN to make it the input channel, CHRIN one byte at a time
\ into the VERA port, and READST after every stage to ask whether any of it
\ had worked. None of that exists on this machine - no IEC bus, no channel
\ table, no ST byte. OPEN-FILE hands back a handle, READ-FILE moves a
\ counted block and REPORTS ITS OWN failure at the call that suffered it,
\ and REPOSITION-FILE steps over the gap between palette and pixels that
\ the old code drained one CHRIN at a time. Five CODE words went with it.
\
\ THE DEVICE ARGUMENT IS GONE TOO, as it is from BLOAD and VLOAD: with one
\ card there is nothing to address, and an argument that is accepted and
\ then ignored is a promise the machine cannot keep. The stack comment is
\ the documentation people actually read.
\
\ Bytes move through base.fs's (VBUF) with its (VPUSH)/(VPULL) - the same
\ 256-byte bounce buffer VLOAD and VSAVE use, for the same reason: the
\ kernel delivers a read to ascending memory and cannot be aimed at a
\ fixed I/O port. Nothing here re-enters those words, so one buffer does.
\
\ THE PALETTE CANNOT BE READ BACK ON THIS MACHINE, and finding that out is
\ what this module cost. VERA's palette lives at VRAM $1FA00 and the X16
\ reads it like any other VRAM; here the read answers with something that
\ is NOT the palette in use - at boot it comes back as bytes that would
\ make the console invisible while the console is plainly readable, and
\ the same 16 bytes read twice with nothing written in between do not
\ agree. Entries this program wrote itself DO read back exactly, every
\ time, which is the only reason the round trip below works at all.
\
\ So BMX-SAVE reading the palette out of VRAM is a trap with a plausible
\ face: it succeeds, the file looks right, and BMX-LOAD then installs
\ those bytes as the real palette and the screen goes to pieces. That is
\ not a hypothesis - it is what killed the test suite's display the first
\ time this file ran after the graphics tests.
\
\ BMX-PAL is the way out: point it at PALCOUNT*2 bytes of memory holding
\ the palette you set, and BMX-SAVE writes those instead of guessing, and
\ BMX-LOAD fills the same buffer as it installs what it read. Programs
\ that set a palette have it in memory already. Leave BMX-PAL at 0 and
\ the VRAM path is used, which is right only for entries this program
\ wrote - PAL!, or an earlier BMX-LOAD.
\
\ ior: 0 = ok, 1 = i/o error (including a file shorter than its own header
\ claims), 2 = not a BMX v1, or a palette that would not fit, 3 = the data
\ is compressed, which is not supported.

decimal

\ --- header fields (set by BMX-LOAD; set BEFORE calling BMX-SAVE) --------------
variable bmx-width    variable bmx-height
variable bmx-bpp      8 bmx-bpp !
variable bmx-palstart 0 bmx-palstart !
variable bmx-palcount 256 bmx-palcount !
variable bmx-border   0 bmx-border !
variable bmx-stride   320 bmx-stride !
0 value bmx-pal       \ PALCOUNT*2 bytes of the caller's own palette, or 0

create bmhdr 16 allot
variable (bmfd)  variable (bmv)  variable (bmc)

\ A 16-bit fetch, spelled out because a CELL IS 32 BITS: `bmhdr 6 + @`
\ would take four bytes of the header and call it the width, and the two
\ it stole are the height. W! is a primitive (the compiler patches
\ two-byte operands with it); its counterpart never needed to exist.
: (w@) ( addr -- u ) dup c@ swap 1+ c@ 8 lshift or ;

\ VERA's depth code is log2(bpp): 1 bpp = 0, 2 = 1, 4 = 2, 8 = 3.
: (bm-code) ( bpp -- n ) 0 swap begin dup 1 > while 1 rshift swap 1+ swap repeat drop ;

\ The VRAM cursor is ONE 17-bit number - bit 16 is the bank. It was a bank
\ variable and an address variable with a carry between them when a cell
\ held 16 bits; four-byte cells make the whole thing an ordinary +! .
: (bm-aim)  ( -- ) (bmv) @ 16 rshift 1 and  (bmv) @ 65535 and  vaddr ;
: (bm-row+) ( -- ) bmx-stride @ (bmv) +! ;
: (bm-close) ( -- ) (bmfd) @ close-file drop ;

\ --- the two pumps ---------------------------------------------------------------
\ A SHORT READ IS THE ERROR. There is no status byte to consult afterwards:
\ if the file ends in the middle of a row, the read that hit the end says so
\ by handing back fewer bytes than it was asked for, and that is the only
\ notice there will be. Ignore it and the rest of the image is whatever the
\ bounce buffer happened to be holding.
: (bm>v) ( u -- ior )                   \ u bytes: file -> the aimed VERA port
  begin dup while
    dup 256 min (bmc) !
    (vbuf) (bmc) @ (bmfd) @ read-file   ( u got ior )
    ?dup if drop 2drop 1 exit then      ( u got )
    (bmc) @ <> if drop 1 exit then
    (bmc) @ (vpush)
    (bmc) @ -
  repeat drop 0 ;

: (v>bm) ( u -- ior )                   \ u bytes: the aimed VERA port -> file
  begin dup while
    dup 256 min (bmc) !
    (bmc) @ (vpull)
    (vbuf) (bmc) @ (bmfd) @ write-file  ( u ior )
    ?dup if 2drop 1 exit then
    (bmc) @ -
  repeat drop 0 ;

\ --- the palette, from wherever the caller keeps it -------------------------------
: (bm-aimpal) ( -- ) 1 $fa00 bmx-palstart @ 2* + vaddr ;

: (pal>bm) ( -- ior )                   \ palette -> file
  bmx-pal if
    bmx-pal bmx-palcount @ 2* (bmfd) @ write-file if 1 else 0 then exit then
  (bm-aimpal) bmx-palcount @ 2* (v>bm) ;

: (bm>pal) ( -- ior )                   \ file -> palette (+ the caller's copy)
  (bm-aimpal)
  bmx-pal 0= if bmx-palcount @ 2* (bm>v) exit then
  \ Read it into the caller's buffer and push THAT to VERA, rather than
  \ installing it and reading it back: reading it back is the thing this
  \ machine cannot do.
  bmx-pal bmx-palcount @ 2* (bmfd) @ read-file ?dup if drop drop 1 exit then
  bmx-palcount @ 2* <> if 1 exit then
  bmx-palcount @ 2* 0 ?do bmx-pal i + c@ v! loop 0 ;

\ --- BMX-LOAD ------------------------------------------------------------------------
: (bm-hdr?) ( -- ior )                \ read and validate; publish the fields
  bmhdr 16 (bmfd) @ read-file ?dup if drop drop 1 exit then
  16 <> if 1 exit then
  bmhdr c@ 'B' <>  bmhdr 1+ c@ 'M' <> or  bmhdr 2 + c@ 'X' <> or
  bmhdr 3 + c@ 1 <> or if 2 exit then
  \ The palette goes to $1FA00 + palstart*2 and runs for palcount*2 bytes,
  \ and the SPRITE ATTRIBUTES begin at $1FC00: a header claiming 256 entries
  \ from index 255 would write 512 bytes straight through them. A palette
  \ that does not fit makes the FILE wrong, not the write, so refuse it here
  \ rather than clamping it to something it never was.
  bmhdr 10 + c@ ?dup 0= if 256 then     ( count )
  dup 1- bmhdr 11 + c@ + 255 > if drop 2 exit then
  bmhdr 14 + c@ if drop 3 exit then     \ compressed
  \ Nothing above here has published a field. A file this word REFUSES
  \ must leave the caller's variables as it found them, or a program that
  \ checks the ior and reports BMX-WIDTH prints a number from a file that
  \ was never loaded.
  bmx-palcount !
  bmhdr 4 + c@ bmx-bpp !
  bmhdr 6 + (w@) bmx-width !   bmhdr 8 + (w@) bmx-height !
  bmhdr 11 + c@ bmx-palstart !
  bmhdr 15 + c@ bmx-border !  0 ;

: bmx-load ( c-addr u vbank vaddr -- ior )
  swap 1 and 16 lshift or (bmv) !
  r/o open-file ?dup if 2drop 1 exit then (bmfd) !
  (bm-hdr?) ?dup if (bm-close) exit then
  (bm>pal) ?dup if (bm-close) exit then
  \ The header says where the pixels begin; SEEK there. The old code read
  \ the gap away a byte at a time because a channel could only go forwards.
  bmhdr 12 + (w@) 0 (bmfd) @ reposition-file if (bm-close) 1 exit then
  bmx-width @ bmx-bpp @ * 8 /           ( bytes in one row )
  bmx-height @ 0 ?do
    (bm-aim)
    dup (bm>v) ?dup if nip (bm-close) unloop exit then
    (bm-row+)
  loop drop
  (bm-close) 0 ;

\ --- BMX-SAVE ------------------------------------------------------------------------
: (bm-h!) ( -- )                        \ build the header from the variables
  'B' bmhdr c!  'M' bmhdr 1+ c!  'X' bmhdr 2 + c!  1 bmhdr 3 + c!
  bmx-bpp @ bmhdr 4 + c!
  bmx-bpp @ (bm-code) bmhdr 5 + c!
  bmx-width @ bmhdr 6 + w!   bmx-height @ bmhdr 8 + w!
  bmx-palcount @ 255 and bmhdr 10 + c!  \ 256 entries store as 0
  bmx-palstart @ bmhdr 11 + c!
  16 bmx-palcount @ 2* + bmhdr 12 + w!  \ where the pixels begin
  0 bmhdr 14 + c!  bmx-border @ bmhdr 15 + c! ;

: bmx-save ( c-addr u vbank vaddr -- ior )
  swap 1 and 16 lshift or (bmv) !
  \ The same palette-fit rule as the load side, checked BEFORE the file is
  \ created: writing a header no loader will accept is not a success, and
  \ the readback would gather sprite attributes as if they were colours.
  bmx-palcount @ 1- bmx-palstart @ + 255 > if 2drop 2 exit then
  w/o create-file ?dup if 2drop 1 exit then (bmfd) !
  (bm-h!)
  bmhdr 16 (bmfd) @ write-file if (bm-close) 1 exit then
  (pal>bm) ?dup if (bm-close) exit then
  bmx-width @ bmx-bpp @ * 8 /
  bmx-height @ 0 ?do
    (bm-aim)
    dup (v>bm) ?dup if nip (bm-close) unloop exit then
    (bm-row+)
  loop drop
  \ Closing a half-written file still matters: until the handle is closed
  \ the directory entry is whatever it was, and what did reach the card
  \ would be lost with it.
  (bm-close) 0 ;
