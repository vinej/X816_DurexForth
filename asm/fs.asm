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

; fs_getbyte - A = handle. Carry clear and A = byte, carry set at EOF.
; Preserves X and Y (the shims it calls do too).
fs_getbyte
    sty fs_ysave
    ldy fs_cpos
    cpy fs_ccnt
    bcc .serve
    jsr kern_fs_fill        ; A = handle -> A = fresh count
    sta fs_ccnt
    ldy #0
    sty fs_cpos
    cmp #0
    bne .serve
    ldy fs_ysave
    sec
    rts
.serve
    ldy fs_cpos
    lda fs_cache, y
    inc fs_cpos
    ldy fs_ysave
    clc
    rts
fs_ysave !byte 0

; fs_flush - if the CURRENT source is a file with unconsumed cached bytes,
; hand them back to the kernel (seek backwards) and empty the cache. The
; one call site that matters is PUSH_INPUT_SOURCE: every nested INCLUDE and
; EVALUATE passes through it before another source can touch the cache.
fs_flush
    lda SOURCE_ID_LSB
    beq .fs_drop            ; keyboard: nothing cached by contract
    bit SOURCE_ID_MSB
    bmi .fs_drop            ; evaluate
    lda fs_ccnt
    sec
    sbc fs_cpos
    beq .fs_drop            ; nothing unconsumed
    tay
    lda SOURCE_ID_LSB
    jsr kern_fs_seekback
.fs_drop
    lda #0
    sta fs_ccnt
    sta fs_cpos
    rts

    +BACKLINK "included", 8
INCLUDED ; ( addr u -- ) interpret a file as source
    ; Copy the name out of the parameter stack before anything moves it.
    lda LSB, x
    sta W2                  ; length
    lda LSB+1, x
    sta W
    lda MSB+1, x
    sta W+1                 ; W = name
    inx
    inx

    ldy W2
    cpy #65
    bcc +
    ldy #64                 ; clamp - kernel paths are shorter anyway
+   lda #0
    sta fs_name, y          ; terminator
-   dey
    bmi +
    lda (W), y
    sta fs_name, y
    jmp -
+
    jsr PUSH_INPUT_SOURCE

    ; TIB bookkeeping, upstream's shape: nested include lines stack upward
    ; in the TIB region so the parent's unconsumed text is not clobbered.
    lda TIB_PTR+1
    cmp #>TIB
    bne .reset_tib_ptr
    lda TO_IN_W
    cmp TIB_SIZE
    beq .open               ; current line fully consumed: reuse in place
    lda TIB_SIZE
    clc
    adc TIB_PTR
    sta TIB_PTR
    jmp .open
.reset_tib_ptr
    lda #<TIB
    sta TIB_PTR
    lda #>TIB
    sta TIB_PTR+1

.open
    jsr kern_fs_open
    bcc +
    ; Could not open: undo the source push and throw. No message here -
    ; the standard exception report carries the code, and the silence is
    ; what lets base.fs probe for an optional AUTORUN with CATCH.
    jsr POP_INPUT_SOURCE
    lda #-37                ; file i/o exception
    jmp throw_a
+
    sta SOURCE_ID_LSB
    lda #0
    sta SOURCE_ID_MSB
    sta fs_ccnt             ; a fresh file starts with an empty cache,
    sta fs_cpos             ; whatever an aborted include left behind

    jmp interpret_and_close
