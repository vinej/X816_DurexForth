; INCLUDED - file source input on the X816 kernel's FS_* API.
;
; The successor to asm/disk.asm's CBM channel model (git history, deleted in
; the X816 port): no SETNAM/SETLFS/OPEN/CHKIN, no logical file numbers, no
; READST. SOURCE_ID holds the KERNEL HANDLE (1..n) directly, and REFILL's
; file arm (io.asm) reads that handle with no channel to select first -
; the same simplification the x16lib conversion found.
;
; The input-source discipline is upstream's, kept exactly:
;   - PUSH_INPUT_SOURCE saves the current source (io.asm)
;   - nested INCLUDE lines stack upward in the TIB region, so the parent's
;     half-consumed line survives underneath
;   - the file is interpreted by jumping into interpret_and_close, whose
;     CATCH closes this file and pops the source on BOTH the end-of-file
;     path and the THROW path, level by level
;
; A failed open THROWs -37 (file i/o exception) with the source popped and
; nothing printed - the standard error report names the code, and a caller
; probing for an optional file (base.fs's autorun hook) can CATCH it
; silently.
;
; Stage B: cache counters stay bytes (the cache is 128 bytes) and are
; touched in sep #$20 windows; the name copy reads the caller's string
; through [W] long, because a Forth string can now live anywhere in the
; 16 MB.

; NUL-terminated path staging for kern_fs_open (x816.asm). The kernel folds
; case itself (runtime/fat32.c does the 8.3 uppercase), so the name is
; copied verbatim.
fs_name !fill 65, 0

; FS_READ parameter block (X816_core doc/KERNEL.md 5.3): handle at +0 is
; patched per call by kern_fs_fill; destination and count are constants
; assembled here. +10 is the kernel's byte-count answer.
fs_blk
    !word 0                 ; +0  handle
    !word fs_cache          ; +2  destination, low 16
    !byte 1, 0              ; +4  destination bank, pad
    !word 128               ; +6  count = the cache size
    !word 0                 ; +8  count high
    !fill 4, 0              ; +10 bytes-read result

; FS_WRITE parameter block (X816_core doc/KERNEL.md 5.3), the mirror of
; fs_blk above: handle +0, flat SOURCE address +2 as 24 bits in a 32-bit
; field, byte count +6 as a full 32 bits, and the kernel's answer at +10.
;
; Source and count are patched per call rather than assembled, because the
; one caller - SAVE_IMAGE - computes both from HERE.
fs_wrblk
    !word 0                 ; +0  handle
    !word 0                 ; +2  source, low 16
    !byte 0, 0              ; +4  source bank, pad
    !word 0                 ; +6  count low
    !word 0                 ; +8  count high
    !fill 4, 0              ; +10 bytes-written result

; FS_SEEK parameter block: handle +0, whence +2 (1 = KFS_CUR), signed
; 32-bit offset +4. kern_fs_seekback patches the handle and the offset's
; low byte; the $FF fill is the sign extension of every -1..-128.
; +8 is the kernel's answer (the new absolute position) - it must be
; allocated or the kernel writes it over whatever follows this block.
fs_skblk
    !word 0                 ; +0  handle
    !byte 1, 0              ; +2  whence = current, pad
    !byte 0, $ff, $ff, $ff  ; +4  offset (low byte patched)
    !fill 4, 0              ; +8  position result, written by the kernel

; The read-ahead cache. ONE cache for whatever source is current: every
; input-source switch goes through fs_flush (PUSH_INPUT_SOURCE) or the
; discard in CLOSE_INPUT_SOURCE, so bytes cached for one file are either
; seeked back or thrown away before another file can read.
fs_cache !fill 128, 0
fs_ccnt !byte 0             ; bytes in the cache
fs_cpos !byte 0             ; next byte to serve

; fs_getbyte - A = handle (low byte used). Returns carry clear and the
; byte zero-extended in A, or carry set at EOF. Preserves X and Y.
fs_getbyte
    sty fs_ysave
    sep #$20
!as
    ldy fs_cpos
    cpy fs_ccnt
    bcc .serve
    rep #$20
!al
    jsl BANK1 + kern_fs_fill        ; A = handle -> A = fresh count (16-bit clean)
    sep #$20
!as
    sta fs_ccnt
    stz fs_cpos
    cmp #0
    bne .serve
    rep #$20
!al
    ldy fs_ysave
    sec
    rtl
!as
.serve
    ldy fs_cpos
    lda fs_cache, y
    inc fs_cpos
    rep #$20
!al
    and #$ff
    ldy fs_ysave
    clc
    rtl
fs_ysave !byte 0

; fs_flush - if the CURRENT source is a file with unconsumed cached bytes,
; hand them back to the kernel (seek backwards) and empty the cache. The
; one call site that matters is PUSH_INPUT_SOURCE: every nested INCLUDE and
; EVALUATE passes through it before another source can touch the cache.
fs_flush
    lda SOURCE_ID_LSB
    beq .fs_drop            ; keyboard: nothing cached by contract
    bmi .fs_drop            ; evaluate
    sep #$20
!as
    lda fs_ccnt
    sec
    sbc fs_cpos
    beq .fs_drop_sep        ; nothing unconsumed
    rep #$20
!al
    and #$ff
    tay                     ; hmm: Y is 8-bit; count 1..128 fits
    lda SOURCE_ID_LSB
    jsl BANK1 + kern_fs_seekback
    sep #$20
!as
.fs_drop_sep
    stz fs_ccnt
    stz fs_cpos
    rep #$20
!al
    rtl
.fs_drop
    sep #$20
!as
    stz fs_ccnt
    stz fs_cpos
    rep #$20
!al
    rtl

    ; (fs-flush) - hand the interpreter's read-ahead back to the kernel.
    ; CLOSE-SOURCE needs it: seeking the handle to EOF does not touch the
    ; bytes already cached here, so without this the line AFTER the one
    ; that called CLOSE-SOURCE still runs - which is exactly the bug this
    ; word was added to fix, caught by the test rather than by reading.
    +BACKLINK "(fs-flush)", 10
    jmp fs_flush

; SAVE-IMAGE ( addr u -- ior ) - write the LIVE program image to a card file.
;
; This is the turnkey save. Everything the interpreter has compiled lives in
; the program banks, and so does the state that describes it - HERE, LATEST
; and every VALUE are immediates inside the image itself - so writing the
; banks out writes the machine's whole Forth state, with nothing to serialise
; and nothing to keep in step.
;
; WHAT RANGE, and why it is not just "up to HERE". Code grows UP from
; $01:0000 and the dictionary HEADERS grow DOWN from the top of the assembled
; image, so the used memory is two regions with a hole between them. A length
; measured from HERE alone stops below the headers and saves a dictionary
; with no names in it. The whole of bank $01 covers both, so the count is
; 64 KB or HERE, whichever is larger - the second case being a session that
; has compiled its way up into bank $02 and beyond.
;
; ior is 0, or the kernel's KERR_* code. The CLOSE is checked and not just
; attempted: fat32_close is what writes the directory entry, so a file that
; was written but not closed has the length it had before - which for a
; create-truncate is zero, and would ship as a card with an empty FORTH.BIN
; on it.
    +BACKLINK "save-image", 10
SAVE_IMAGE ; ( addr u -- ior )
    ; Copy the name out of the parameter stack before anything moves it.
    lda LSB, x
    sta W2                  ; length
    lda LSB+2, x
    sta W
    lda MSB+2, x
    sta W+2                 ; W = name, flat
    inx
    inx
    inx
    inx

    ldy W2                  ; 8-bit Y: names are short
    cpy #65
    bcc +
    ldy #64                 ; clamp - kernel paths are shorter anyway
+   sep #$20
!as
    lda #0
    sta fs_name, y          ; terminator
-   dey
    bmi +
    lda [W], y
    sta fs_name, y
    bra -
+   rep #$20
!al

    ; Source: the base of the program banks.
    stz fs_wrblk+2          ; low 16 of $01:0000
    lda #1
    sta fs_wrblk+4          ; bank $01, and the pad byte above it

    ; Count: $FF00, which is X816_EXEC_MAX - the largest image the shell's
    ; `run` will load, because exec.s copies it with a 16-bit X. It is also
    ; exactly the span of the assembled image, $01:0000 up to the top where
    ; the dictionary headers live, so it covers BOTH growing ends: code up
    ; from the bottom and headers down from the top.
    ;
    ; A full 64 KB was the obvious first answer and it is wrong: 256 bytes
    ; over the ceiling, and `run` answers "TOO BIG" at boot rather than at
    ; save time, which is the worst place to find out.
    ;
    ; So if HERE has climbed out of bank $01 the session has compiled past
    ; what a loadable image can hold, and there is no file worth writing.
    ; Refuse with KERR_NOSPACE (3) instead of producing one `run` will reject.
    lda HERE_BANK
    cmp #1
    bne .too_big
    lda HERE_PTR
    cmp #$ff00
    bcs .too_big

    lda #$ff00
    sta fs_wrblk+6          ; count low
    stz fs_wrblk+8          ; count high
    bra .named
.too_big
    lda #3                  ; KERR_NOSPACE
    bra .push
.named

    jsl BANK1 + kern_fs_create
    bcs .push               ; A already holds KERR_*
    sta KTMP2               ; handle, needed again for the close

    jsl BANK1 + kern_fs_wr  ; A is still the handle
    bcc .wrote

    ; The write failed. Close anyway - a handle left open is one the kernel
    ; cannot hand out again - but report the WRITE's code, not the close's.
    pha
    lda KTMP2
    jsl BANK1 + kern_fs_close
    pla
    bra .push

.wrote
    lda KTMP2
    jsl BANK1 + kern_fs_close
    bcs .push               ; A = KERR_* from the close
    lda #0

.push
    dex
    dex
    sta LSB, x
    stz MSB, x
    rtl

    +BACKLINK "included", 8
INCLUDED ; ( addr u -- ) interpret a file as source
    ; Copy the name out of the parameter stack before anything moves it.
    lda LSB, x
    sta W2                  ; length
    lda LSB+2, x
    sta W
    lda MSB+2, x
    sta W+2                 ; W = name, flat
    inx
    inx
    inx
    inx

    ldy W2                  ; 8-bit Y: names are short
    cpy #65
    bcc +
    ldy #64                 ; clamp - kernel paths are shorter anyway
+   sep #$20
!as
    lda #0
    sta fs_name, y          ; terminator
-   dey
    bmi +
    lda [W], y
    sta fs_name, y
    bra -
+   rep #$20
!al

    jsl BANK1 + PUSH_INPUT_SOURCE

    ; TIB bookkeeping: this file's lines load at TIB_TOP - the first byte
    ; above every line a suspended file still owns (io.asm). Deriving the
    ; slot from TIB_PTR is WRONG under EVALUATE (it points into the
    ; evaluated string; the old page-check then "reset" to TIB base and
    ; loaded this file's lines over the OUTERMOST file's half-consumed
    ; line - the crashed-after-the-suite bug of 2026-08-04). Overflow is
    ; loud: past this ceiling a 255-byte line could leave the region.
    lda TIB_TOP
    cmp #TIB + $1c0
    bcc +
    jsl BANK1 + POP_INPUT_SOURCE
    lda #-8                 ; dictionary overflow, the input-stack code
    jmp throw_a
+   sta TIB_PTR

.open
    jsl BANK1 + kern_fs_open
    bcc +
    ; Could not open: undo the source push and throw. No message here -
    ; the standard exception report carries the code, and the silence is
    ; what lets base.fs probe for an optional AUTORUN with CATCH.
    jsl BANK1 + POP_INPUT_SOURCE
    lda #-37                ; file i/o exception
    jmp throw_a
+
    sta SOURCE_ID_LSB
    stz SOURCE_ID_MSB
    sep #$20
!as
    stz fs_ccnt             ; a fresh file starts with an empty cache,
    stz fs_cpos             ; whatever an aborted include left behind
    rep #$20
!al

    jmp interpret_and_close
