; FILE - the ANS file-access primitives over the X816 kernel's FS_* calls.
;
; asm/fs.asm already drives FS_OPEN/READ/SEEK for ONE purpose: INCLUDED, the
; interpreter's source input. Its buffers are shaped for that job - a fixed
; 128-byte read-ahead cache, a read-only open, a seek that only steps
; backwards - and they belong to whichever file the interpreter is reading.
; Nothing here touches them. A program that opens a data file while an
; INCLUDE is running must not disturb the include, and sharing fs_name or
; fs_blk would do exactly that at the first nested use.
;
; These are PRIMITIVES, not the ANS words: they hand back the kernel's own
; KERR_* code as an ior (0 = success) and take counts as full cells. The ANS
; layer - OPEN-FILE, READ-LINE and the rest - is Forth, in base.fs, because
; it is stack shuffling and loop control with no width crossings in it.
;
; Every call crosses to the kernel M=0/X=0 and comes back M=0/X=1, the same
; shim discipline as x816.asm; X (the Forth stack pointer) is saved around
; each crossing because the kernel preserves nothing but D and DBR.

; Path staging. The kernel wants NUL-terminated ASCII and folds case itself
; (runtime/fat32.c does the 8.3 uppercasing), so names are copied verbatim.
; TWO buffers because RENAME needs both paths live at once.
file_name1 !fill 65, 0
file_name2 !fill 65, 0

; The read/write parameter block (X816_core doc/KERNEL.md 5.2/5.3, and
; runtime/kfs.c for the offsets that doc does not spell out):
;   +0  handle          +2  24-bit buffer address
;   +6  32-bit count    +10 32-bit result, written by the kernel
; +10 MUST exist even though the count also comes back in C: the kernel
; writes it unconditionally, so a block that stops at +10 gets whatever
; follows it in memory overwritten.
file_blk
    !word 0                 ; +0  handle
    !word 0                 ; +2  buffer, low 16
    !byte 0, 0              ; +4  buffer bank, pad
    !word 0                 ; +6  count, low
    !word 0                 ; +8  count, high
    !fill 4, 0              ; +10 bytes transferred, from the kernel

; The seek block: handle +0, whence +2, signed 32-bit offset +4, and the
; new absolute position at +8 - again written by the kernel whether or not
; the caller wants it.
file_skblk
    !word 0                 ; +0  handle
    !byte 0, 0              ; +2  whence, pad
    !fill 4, 0              ; +4  offset, signed
    !fill 4, 0              ; +8  new position, from the kernel

; The rename block: two 24-bit path pointers.
file_rnblk
    !word 0                 ; +0  old path, low 16
    !byte 0, 0              ; +2  old path bank, pad
    !word 0                 ; +4  new path, low 16
    !byte 0, 0              ; +6  new path bank, pad

; file_copy_name - copy the Forth string at W (flat, 24-bit) of length W2
; into the buffer whose LOW BYTE is in A, NUL-terminated and clamped to 64
; characters. Entered and left with 16-bit A; Y is used 8-bit.
; The clamp is not politeness: the buffers are 65 bytes and a longer path
; would walk into the next one.
file_copy_name
    sta W3                  ; destination, low 16 (both buffers are bank 1)
    ldy W2                  ; 8-bit Y - paths are short
    cpy #65
    bcc +
    ldy #64
+   sep #$20
!as
    lda #0
    sta (W3), y             ; terminator (dp-indirect: DBR is $01 here)
-   dey
    bmi +
    lda [W], y
    sta (W3), y
    bra -
+   rep #$20
!al
    rtl

; file_pop_name - pull ( addr u ) off the Forth stack into W/W2 and copy it
; to file_name1. Leaves the stack two cells lighter.
file_pop_name
    lda LSB, x
    sta W2                  ; length
    lda LSB+2, x
    sta W
    lda MSB+2, x
    sta W+2                 ; W = flat address of the text
    inx
    inx
    inx
    inx
    lda #file_name1
    jml BANK1 + file_copy_name   ; its rtl returns to OUR caller

; ---------------------------------------------------------------------------
; fs-open ( c-addr u mode -- handle ior )
;
; mode is the kernel's: 0 = KFS_READ (the file must exist), 1 = KFS_WRITE
; (created, and TRUNCATED if it was there). There is no read-write mode in
; this kernel and none is invented here - base.fs's OPEN-FILE refuses R/W
; rather than quietly opening for reading, which is what passing any other
; value to FS_OPEN would do (kfs.c tests `mode == KFS_WRITE` and treats
; everything else as read).
; ---------------------------------------------------------------------------
    +BACKLINK "fs-open", 7
FS_OPEN_W
    lda LSB, x              ; mode
    sta W3+2
    inx
    inx
    jsl BANK1 + file_pop_name

    phx
    phy
    rep #$30
!rl
    ldx #1                  ; path bank
    lda #file_name1
    ldy W3+2                ; mode
    jsl KERN_FS_OPEN
    sta KTMP                ; handle, or KERR_* if carry set
    sep #$10
!rs
    ply
    plx
    bcs .open_failed
    dex
    dex
    dex
    dex
    lda KTMP
    sta LSB+2, x            ; handle
    stz MSB+2, x
    stz LSB, x              ; ior = 0
    stz MSB, x
    rtl
.open_failed
    dex
    dex
    dex
    dex
    stz LSB+2, x            ; handle = 0, meaningless on failure
    stz MSB+2, x
    lda KTMP
    sta LSB, x              ; ior = the kernel's KERR_*
    stz MSB, x
    rtl

; ---------------------------------------------------------------------------
; fs-close ( handle -- ior )
; ---------------------------------------------------------------------------
    +BACKLINK "fs-close", 8
FS_CLOSE_W
    lda LSB, x
    sta KTMP
    phx
    phy
    rep #$30
!rl
    lda KTMP
    jsl KERN_FS_CLOSE
    sta KTMP
    sep #$10
!rs
    ply
    plx
    bcs +
    stz LSB, x              ; ior = 0
    stz MSB, x
    rtl
+   lda KTMP
    sta LSB, x
    stz MSB, x
    rtl

; file_rw_setup - fill file_blk from ( addr u handle ) on the Forth stack
; and drop all three cells. The buffer address is a full flat cell, so a
; read straight into SDRAM (far-allot space) works with no bounce buffer.
file_rw_setup
    lda LSB, x
    sta file_blk+0          ; handle
    lda LSB+2, x
    sta file_blk+6          ; count, low
    lda MSB+2, x
    sta file_blk+8          ; count, high
    lda LSB+4, x
    sta file_blk+2          ; buffer, low 16
    lda MSB+4, x
    sta file_blk+4          ; buffer bank (high byte of the cell is the pad)
    ; Drop three cells = SIX, not twelve. X indexes the LSB plane and a
    ; cell is two bytes THERE plus two in the MSB plane, so one cell is
    ; `inx inx`. Twelve walked the stack pointer three cells past the top
    ; and the interpreter reported -4 on the next word - a stack underflow
    ; nowhere near the word that caused it.
    txa
    clc
    adc #6
    tax
    stz file_blk+10         ; clear the kernel's result field, so a call
    stz file_blk+12         ; that fails loudly cannot look like a success
    rtl

; file_rw_result - push ( count ior ) from file_blk+10 and the carry state
; captured in KTMP2 (0 = ok, else KERR_*).
file_rw_result
    dex
    dex
    dex
    dex
    lda file_blk+10
    sta LSB+2, x
    lda file_blk+12
    sta MSB+2, x
    lda KTMP2
    sta LSB, x
    stz MSB, x
    rtl

; ---------------------------------------------------------------------------
; fs-read ( addr u handle -- u-read ior )
;
; Short reads are NOT an error: u-read below u is how end of file arrives,
; and the ANS layer turns that into READ-FILE's count or READ-LINE's flag.
; ---------------------------------------------------------------------------
    +BACKLINK "fs-read", 7
FS_READ_W
    jsl BANK1 + file_rw_setup
    phx
    phy
    rep #$30
!rl
    ldx #1                  ; block bank
    lda #file_blk
    jsl KERN_FS_READ
    sep #$10
!rs
    ply
    plx
    lda #0
    bcc +
    lda KTMP                ; carry set: A held KERR_* across the pulls
+   sta KTMP2
    jml BANK1 + file_rw_result

; ---------------------------------------------------------------------------
; fs-write ( addr u handle -- u-written ior )
; ---------------------------------------------------------------------------
    +BACKLINK "fs-write", 8
FS_WRITE_W
    jsl BANK1 + file_rw_setup
    phx
    phy
    rep #$30
!rl
    ldx #1
    lda #file_blk
    jsl KERN_FS_WRITE
    sta KTMP
    sep #$10
!rs
    ply
    plx
    lda #0
    bcc +
    lda KTMP
+   sta KTMP2
    jml BANK1 + file_rw_result

; ---------------------------------------------------------------------------
; fs-seek ( d whence handle -- d-position ior )
;
; whence: 0 = from the start, 1 = from the current position, 2 = from the
; end (KFS_SET/CUR/END). The offset is a SIGNED 32-bit double, so seeking
; backwards from the end is -100. 2 fs-seek with 0. is "go to the end".
; ---------------------------------------------------------------------------
    +BACKLINK "fs-seek", 7
FS_SEEK_W
    lda LSB, x
    sta file_skblk+0        ; handle
    lda LSB+2, x
    sep #$20
!as
    sta file_skblk+2        ; whence (byte)
    rep #$20
!al
    lda LSB+4, x
    sta file_skblk+4        ; offset, low 16
    lda MSB+4, x
    sta file_skblk+6        ; offset, high 16
    txa
    clc
    adc #6                  ; drop handle, whence and offset - three cells,
    tax                     ; two bytes of X each (see file_rw_setup)
    stz file_skblk+8
    stz file_skblk+10

    phx
    phy
    rep #$30
!rl
    ldx #1
    lda #file_skblk
    jsl KERN_FS_SEEK
    sta KTMP
    sep #$10
!rs
    ply
    plx
    lda #0
    bcc +
    lda KTMP
+   sta KTMP2

    dex
    dex
    dex
    dex
    lda file_skblk+8
    sta LSB+2, x            ; new position, low
    lda file_skblk+10
    sta MSB+2, x            ; ...and high
    lda KTMP2
    sta LSB, x
    stz MSB, x
    rtl

; ---------------------------------------------------------------------------
; fs-size ( handle -- d ior )
;
; The one FS call that answers in registers rather than a block: C is the
; low half and X the high half (kfs.c calls a block for a single number
; "ceremony"). So the shim must read X back out of the crossing, which is
; why the Forth stack pointer goes on the CPU stack and not into X here.
; ---------------------------------------------------------------------------
    +BACKLINK "fs-size", 7
FS_SIZE_W
    lda LSB, x
    sta KTMP
    phx
    phy
    rep #$30
!rl
    lda KTMP
    jsl KERN_FS_SIZE
    sta KTMP                ; size, low 16
    stx KTMP2               ; size, high 16 - the kernel's second result
    sep #$10
!rs
    ply
    plx
    bcs .size_failed
    dex
    dex
    lda KTMP
    sta LSB+2, x            ; low half of the double
    lda KTMP2
    sta MSB+2, x
    stz LSB, x              ; ior = 0
    stz MSB, x
    rtl
.size_failed
    dex
    dex
    stz LSB+2, x
    stz MSB+2, x
    lda KTMP                ; KERR_* came back in C
    sta LSB, x
    stz MSB, x
    rtl

; ---------------------------------------------------------------------------
; fs-delete ( c-addr u -- ior )
; ---------------------------------------------------------------------------
    +BACKLINK "fs-delete", 9
FS_DELETE_W
    jsl BANK1 + file_pop_name
    phx
    phy
    rep #$30
!rl
    ldx #1
    lda #file_name1
    jsl KERN_FS_DELETE
    sta KTMP
    sep #$10
!rs
    ply
    plx
    dex
    dex
    lda #0
    bcc +
    lda KTMP
+   sta LSB, x
    stz MSB, x
    rtl

; ---------------------------------------------------------------------------
; fs-rename ( c-addr1 u1 c-addr2 u2 -- ior )   old -> new
; ---------------------------------------------------------------------------
    +BACKLINK "fs-rename", 9
FS_RENAME_W
    ; The NEW name is on top; stage it in buffer 2 first, then the old one
    ; in buffer 1 - file_pop_name always uses buffer 1, so the second name
    ; is copied by hand rather than by adding a parameter nobody else wants.
    lda LSB, x
    sta W2
    lda LSB+2, x
    sta W
    lda MSB+2, x
    sta W+2
    inx
    inx
    inx
    inx
    lda #file_name2
    jsl BANK1 + file_copy_name
    jsl BANK1 + file_pop_name       ; the old name, into file_name1

    lda #file_name1
    sta file_rnblk+0
    lda #1
    sta file_rnblk+2                ; bank, pad byte above it is zero
    lda #file_name2
    sta file_rnblk+4
    lda #1
    sta file_rnblk+6

    phx
    phy
    rep #$30
!rl
    ldx #1
    lda #file_rnblk
    jsl KERN_FS_RENAME
    sta KTMP
    sep #$10
!rs
    ply
    plx
    dex
    dex
    lda #0
    bcc +
    lda KTMP
+   sta LSB, x
    stz MSB, x
    rtl

; ---------------------------------------------------------------------------
; DIRECTORIES.
;
; Directory handles are 129 up, a range DISJOINT from file handles 1..5
; (runtime/kfs.h), so passing one to fs-read is a clean KERR_BADARG rather
; than a walk through directory records as if they were file bytes. They
; come from a separate pool of two, so a listing must be closed before
; another can start.
; ---------------------------------------------------------------------------

; CHDIR, MKDIR and RMDIR are the same shape as fs-delete above -
; ( c-addr u -- ior ) with the path staged in file_name1 - and each is
; written out rather than shared through one body with a patched jsl
; operand. That was tried: the patch site was computed as an offset from
; the routine's start, the count was wrong by nine bytes, and it would
; have called into the middle of an instruction. A shared body needs the
; site named by LABEL, and at that point three plain copies of twenty
; obvious lines are the cheaper thing to be sure of.
!macro PATHCALL .entry {
    jsl BANK1 + file_pop_name
    phx
    phy
    rep #$30
!rl
    ldx #1                          ; path bank
    lda #file_name1
    jsl .entry
    sta KTMP
    sep #$10
!rs
    ply
    plx
    dex
    dex
    lda #0
    bcc +
    lda KTMP
+   sta LSB, x
    stz MSB, x
    rtl
}

    +BACKLINK "fs-chdir", 8
FS_CHDIR_W ; ( c-addr u -- ior )
    +PATHCALL KERN_FS_CHDIR

    +BACKLINK "fs-mkdir", 8
FS_MKDIR_W ; ( c-addr u -- ior )
    +PATHCALL KERN_FS_MKDIR

    +BACKLINK "fs-rmdir", 8
FS_RMDIR_W ; ( c-addr u -- ior )
    +PATHCALL KERN_FS_RMDIR

; ---------------------------------------------------------------------------
; fs-getcwd ( addr -- len ior ) - write the working directory, NUL
; terminated, into the caller's buffer. It needs 80 bytes (KFS_PATH); the
; kernel writes as many as the path takes and never asks how big it is, so
; a short buffer is silent corruption and not this word's to catch.
; ---------------------------------------------------------------------------
    +BACKLINK "fs-getcwd", 9
FS_GETCWD_W
    lda LSB, x
    sta KTMP                        ; buffer, low 16
    lda MSB, x
    sta KTMP2                       ; buffer bank
    phx
    phy
    rep #$30
!rl
    ldx KTMP2
    lda KTMP
    jsl KERN_FS_GETCWD
    sta KTMP                        ; length written
    sep #$10
!rs
    ply
    plx
    dex
    dex
    lda #0
    bcc +
    lda KTMP
    stz KTMP                        ; failed: report length 0, not garbage
+   sta LSB, x                      ; ior
    stz MSB, x
    lda KTMP
    sta LSB+2, x                    ; length, over the buffer address
    stz MSB+2, x
    rtl

; ---------------------------------------------------------------------------
; fs-diropen ( c-addr u -- handle ior )
; ---------------------------------------------------------------------------
    +BACKLINK "fs-diropen", 10
FS_DIROPEN_W
    jsl BANK1 + file_pop_name
    phx
    phy
    rep #$30
!rl
    ldx #1
    lda #file_name1
    jsl KERN_DIR_OPEN
    sta KTMP
    sep #$10
!rs
    ply
    plx
    dex
    dex
    dex
    dex
    lda #0
    bcc +
    lda KTMP
    stz KTMP                        ; no handle on failure
+   sta LSB, x                      ; ior
    stz MSB, x
    lda KTMP
    sta LSB+2, x                    ; handle
    stz MSB+2, x
    rtl

; ---------------------------------------------------------------------------
; fs-dirnext ( addr handle -- ior ) - fill an 18-byte entry buffer:
;   +0  name, 13 bytes, NUL terminated    +13 attributes, bit 0 = directory
;   +14 size, 32 bits (0 for a directory)
;
; END OF DIRECTORY IS ior 2 (KERR_NOTFOUND), not an error to report: the
; kernel uses BADARG for "that was never a directory handle", which happens
; on the FIRST call rather than the last, so the two cannot be confused.
; base.fs's DIR-NEXT turns 2 into a false flag.
;
; The buffer goes in X:Y here and not C:X - C is spent on the handle. Same
; 24-bit shape, shifted along one register.
; ---------------------------------------------------------------------------
    +BACKLINK "fs-dirnext", 10
FS_DIRNEXT_W
    lda LSB, x
    sta KTMP                        ; handle
    lda LSB+2, x
    sta KTMP2                       ; buffer, low 16
    lda MSB+2, x
    sta W3                          ; buffer bank
    inx
    inx                             ; drop the handle cell
    phx
    phy
    rep #$30
!rl
    lda KTMP
    ldx KTMP2
    ldy W3
    jsl KERN_DIR_NEXT
    sta KTMP
    sep #$10
!rs
    ply
    plx
    lda #0
    bcc +
    lda KTMP
+   sta LSB, x                      ; ior, over the buffer address
    stz MSB, x
    rtl

; ---------------------------------------------------------------------------
; fs-dirclose ( handle -- ior )
; ---------------------------------------------------------------------------
    +BACKLINK "fs-dirclose", 11
FS_DIRCLOSE_W
    lda LSB, x
    sta KTMP
    phx
    phy
    rep #$30
!rl
    lda KTMP
    jsl KERN_DIR_CLOSE
    sta KTMP
    sep #$10
!rs
    ply
    plx
    lda #0
    bcc +
    lda KTMP
+   sta LSB, x
    stz MSB, x
    rtl
