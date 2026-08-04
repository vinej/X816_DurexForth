; X816 - kernel crossing and machine glue.
;
; Stage B: durexForth code runs M=0/X=1 (durexforth.asm header); the X816
; kernel ABI wants M=0/X=0, entered by jsl BANK1 + through the jump table at
; $00:FE00 (entry n at $FE00 + 4*n; numbers from X816_core
; tools/contract.py). Each shim goes rep #$30 for the crossing and comes
; back with sep #$10 - the accumulator stays wide throughout, so the
; convention holds at every boundary a word can see.
;
; Every shim preserves X and Y (the Forth stack pointer lives in X); the
; kernel itself preserves nothing but D/DBR.
;
; The program runs with PBR = DBR = its own bank ($01), so absolute data
; references reach the image. The two things that must NOT go through DBR:
;   - the CPU stack (pha/pla and n,s always address bank $00 - fine)
;   - the I/O page at $00:9F00 - VERA words switch DBR with +VIO/+VIO_END

; Kernel jump-table entries (bank $00, callable from anywhere via jsl).
KERN_PUTC   = $00fe00               ; C = character
KERN_GETC   = $00fe08               ; -> C = key, blocking
KERN_GETKEY = $00fe0c               ; -> C = key, 0 if none
KERN_CLS    = $00fe10
KERN_GOTOXY = $00fe14               ; C = column, X = row
KERN_GETXY  = $00fe18               ; -> C = column, X = row
KERN_EXIT   = $00fe84               ; C = status; does not return
KERN_FRAMES = $00fed0               ; -> C = VSYNC frames, 16-bit, wraps
KERN_FS_OPEN  = $00fe40             ; C:X = path, Y = mode -> C = handle
KERN_FS_CLOSE = $00fe44             ; C = handle
KERN_FS_READ  = $00fe48             ; C:X = parameter block -> C = bytes read
KERN_FS_SEEK  = $00fe50             ; C:X = parameter block

; Direct-page staging for the width crossing. $E0-$EF is unclaimed by the
; Forth core (planes $32-$D1, W/W2/W3 $D4-$DF).
KTMP  = $e0                         ; 16-bit result/argument staging
KTMP2 = $e2                         ; second 16-bit staging
SPR_OFF = $ef                       ; sprite.asm attribute offset (in dp so
                                    ; it works whichever bank DBR selects)

; The CPU return stack. An 8-bit txs in native mode zeroes SH, which would
; move the return stack into the direct page - so S is only ever loaded
; 16-bit, with this value. NOT the C64's $01FF: kernel calls (jsl + KENTER
; frames + C bodies) and the VSYNC cursor IRQ land on this stack too, and
; a 16-bit S that sinks below $0100 dives into the direct page and smashes
; the Forth stacks. KERNEL.md 3.1 gives programs $0100-$1FFF of stack;
; take the top. CATCH snapshots the full 16-bit S (exception.asm).
RSTACK_TOP = $1fff

; Switch DBR to bank $00 for a run of I/O-page accesses. Clobbers A - use
; at word entry, before the first operand load. (phb/plb are 8-bit always;
; the lda #0 rides in the 16-bit accumulator harmlessly.)
!macro VIO {
    phb
    lda #0
    pha
    plb
    plb ; drop the high byte of the 16-bit push, keep DBR = the low ($00)
}
!macro VIO_END {
    plb
}

; PUTCHR - print the character in A (low byte). The KERNAL-CHROUT shape:
; preserves A, X and Y. CON_PUTC interprets $08 backspace, $0a newline,
; $0d return; everything else is a CP437 glyph. One translation: the Forth
; sends $0d meaning the PETSCII "next line", but CON_PUTC's $0d is
; return-only (column 0, same row) - so $0d becomes $0a here, or every
; REPL line would overprint row 0.
PUTCHR
    phx
    phy
    pha
    and #$ff
    cmp #$0d
    bne +
    lda #$0a
+   rep #$30
!rl
    and #$00ff
    jsl KERN_PUTC
    sep #$10
!rs
    pla
    ply
    plx
    rtl

; kern_getc - blocking key read, A = character (zero-extended). Polls
; CON_GETKEY rather than calling the blocking CON_GETC: the shell's own
; line reader polls, and polling from here keeps the block on our side of
; the ABI. Keys without a character ($01xx) are swallowed: the Forth REPL
; cannot express them yet.
kern_getc
    phx
    phy
-   rep #$30
!rl
    jsl KERN_GETKEY
    sep #$10
!rs
    sta KTMP
    lda KTMP
    beq -               ; no key yet
    and #$ff00
    bne -               ; non-character key
    lda KTMP
    ply
    plx
    rtl

; kern_getin - the GETIN shape: A = character, 0 if none. Non-character
; keys read as "no key".
kern_getin
    phx
    phy
    rep #$30
!rl
    jsl KERN_GETKEY
    sep #$10
!rs
    sta KTMP
    lda KTMP
    and #$ff00
    beq +
    lda #0
    ply
    plx
    rtl
+   lda KTMP
    ply
    plx
    rtl

; kern_cls - clear the console.
kern_cls
    phx
    phy
    pha
    rep #$30
!rl
    jsl KERN_CLS
    sep #$10
!rs
    pla
    ply
    plx
    rtl

; kern_gotoxy - A = column, Y = row.
kern_gotoxy
    sta KTMP
    phx
    phy
    sty KTMP2
    stz KTMP2+1
    rep #$30
!rl
    lda KTMP2
    and #$00ff
    tax
    lda KTMP
    and #$00ff
    jsl KERN_GOTOXY
    sep #$10
!rs
    ply
    plx
    rtl

; kern_getxy - KTMP = column, KTMP2 = row (16-bit each).
kern_getxy
    phx
    phy
    rep #$30
!rl
    jsl KERN_GETXY
    sta KTMP
    stx KTMP2
    sep #$10
!rs
    ply
    plx
    rtl

; kern_frames - KTMP = VSYNC frame count (16-bit, wraps).
kern_frames
    phx
    phy
    rep #$30
!rl
    jsl KERN_FRAMES
    sta KTMP
    sep #$10
!rs
    ply
    plx
    rtl

; kern_fs_open - open the NUL-terminated path staged in fs_name (fs.asm),
; read-only. Out: carry clear and A = handle, or carry set and A = KERR_*.
; Pulls and sep do not touch carry, so the kernel's verdict survives.
kern_fs_open
    phx
    phy
    rep #$30
!rl
    ldx #1                          ; path bank = this bank
    lda #fs_name
    ldy #0                          ; mode 0 = read (KFS_READ)
    jsl KERN_FS_OPEN
    sta KTMP
    sep #$10
!rs
    ply
    plx
    lda KTMP
    rtl

; kern_fs_close - A = handle. Best-effort: a refused close (handle not
; open) is the common case for close_all_logical_files and is ignored.
kern_fs_close
    phx
    phy
    and #$ff
    sta KTMP
    rep #$30
!rl
    lda KTMP
    jsl KERN_FS_CLOSE
    sep #$10
!rs
    ply
    plx
    rtl

; kern_fs_fill - A = handle. Reads up to 128 bytes into fs_cache (fs.asm)
; with ONE kernel crossing and returns the count in A (0 = end of file, and
; a device error reads as end of file).
kern_fs_fill
    phx
    phy
    and #$ff
    sta fs_blk+0                    ; handle (block +0); dest and count are
                                    ; assembled constants in fs.asm
    rep #$30
!rl
    ldx #1                          ; block pointer bank
    lda #fs_blk
    jsl KERN_FS_READ
    sta KTMP                        ; bytes read
    sep #$10
!rs
    ply
    plx
    bcs +                           ; kernel error: report as EOF
    lda KTMP
    rtl
+   lda #0
    rtl

; kern_fs_seekback - A = handle, Y = bytes to step back (1..128). Rewinds
; the kernel's file position over cached-but-unconsumed bytes when a nested
; source takes over. Best-effort: a refused seek leaves the position wrong
; in a situation (broken handle) that is already failing louder elsewhere.
kern_fs_seekback
    phx
    phy
    and #$ff
    sta fs_skblk+0
    sty KTMP
    stz KTMP+1
    lda KTMP
    eor #$ff
    inc                             ; two's complement low byte
    sep #$20
!as
    sta fs_skblk+4                  ; offset = -Y, sign-extended $FF above
    rep #$20
!al
    rep #$30
!rl
    ldx #1
    lda #fs_skblk
    jsl KERN_FS_SEEK
    sep #$10
!rs
    ply
    plx
    rtl

; emu-exit ( status -- ) - stop the EMULATOR with an exit code, so a test
; harness gets its verdict the moment the suite finishes instead of waiting
; out a timeout. $9FBC is the emulator-only control page; on real hardware
; it is open bus, the store does nothing, and the word simply returns -
; callers put their hardware fallback (a halt loop) right after it.
    +BACKLINK "emu-exit", 8
EMU_EXIT
    phb
    sep #$20
!as
    lda #0
    pha
    plb                             ; the I/O page lives in bank 0
    lda LSB, x                      ; direct page: readable under any DBR
    sta $9fbc
    rep #$20
!al
    inx
    inx
    plb
    rtl

; kern_exit - back to the kernel prompt. Does not return.
kern_exit
    rep #$30
!rl
    lda #0
    jsl KERN_EXIT
!rs
-   bra -                           ; unreachable
