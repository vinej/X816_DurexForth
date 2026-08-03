; X816 - kernel crossing and machine glue.
;
; durexForth code is 8-bit (M=1, X=1 native); the X816 kernel ABI is 16-bit
; (M=0, X=0), entered by jsl through the jump table at $00:FE00 (entry n at
; $FE00 + 4*n; numbers from X816_core tools/contract.py). This file is the
; ONLY place that switches register widths - the same one-crossing rule
; x16lib's system/x816kernel.asm follows, and for the same reason: a missed
; sep does not crash, it leaves 8-bit code running 16-bit and the symptom
; lands somewhere else entirely.
;
; Every shim preserves X and Y (KERNAL CHROUT/GETIN did, and the Forth
; stack pointer lives in X); the kernel itself preserves nothing but D/DBR.
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

; Direct-page staging for the width crossing. $E0-$EF is unclaimed by the
; Forth core (stacks $09-$78, W/W2/W3 $9C-$A1).
KTMP  = $e0                         ; 16-bit result/argument staging
KTMP2 = $e2                         ; second 16-bit staging
SPR_OFF = $ef                       ; sprite.asm attribute offset (in dp so
                                    ; it works whichever bank DBR selects)

; The CPU return stack. An 8-bit txs in native mode zeroes SH, which would
; move the return stack into the direct page - so S is only ever loaded
; 16-bit, with this value. NOT the C64's $01FF: kernel calls (jsl + KENTER
; frames + C bodies) and the VSYNC cursor IRQ land on this stack too, and
; a 16-bit S that sinks below $0100 dives into the direct page and smashes
; the Forth stacks at $41-$78. KERNEL.md 3.1 gives programs $0100-$1FFF of
; stack; take the top. CATCH snapshots only SL (8-bit tsx), which stays
; valid because Forth's own return depth keeps S inside the $1Fxx page -
; THROW rebuilds SH from >RSTACK_TOP.
RSTACK_TOP = $1fff

; Switch DBR to bank $00 for a run of I/O-page accesses. Clobbers A - use
; at word entry, before the first operand load.
!macro VIO {
    phb
    lda #0
    pha
    plb
}
!macro VIO_END {
    plb
}

; PUTCHR - print A. The KERNAL-CHROUT shape: preserves A, X and Y.
; CON_PUTC interprets $08 backspace, $0a newline, $0d return; everything
; else is a CP437 glyph. One translation: the Forth sends $0d meaning the
; PETSCII "next line", but CON_PUTC's $0d is return-only (column 0, same
; row) - so $0d becomes $0a here, or every REPL line would overprint row 0.
PUTCHR
    phx
    phy
    pha
    cmp #$0d
    bne +
    lda #$0a
+   rep #$30
!al
!rl
    and #$00ff
    jsl KERN_PUTC
    sep #$30
!as
!rs
    pla
    ply
    plx
    rts

; kern_getc - blocking key read, A = character. Polls CON_GETKEY rather
; than calling the blocking CON_GETC: the shell's own line reader polls
; (the path run-fwboot.sh proves on the real keyboard), and polling from
; here keeps the block on our side of the ABI. Keys without a character
; ($01xx) are swallowed: the 8-bit Forth REPL cannot express them yet.
kern_getc
    phx
    phy
-   rep #$30
!al
!rl
    jsl KERN_GETKEY
    sta KTMP
    sep #$30
!as
!rs
    lda KTMP
    ora KTMP+1
    beq -               ; no key yet
    lda KTMP+1
    bne -               ; non-character key
    ply
    plx
    lda KTMP
    rts

; kern_getin - the GETIN shape: A = character, 0 if none. Non-character
; keys read as "no key".
kern_getin
    phx
    phy
    rep #$30
!al
!rl
    jsl KERN_GETKEY
    sta KTMP
    sep #$30
!as
!rs
    ply
    plx
    lda KTMP+1
    bne +
    lda KTMP
    rts
+   lda #0
    rts

; kern_cls - clear the console.
kern_cls
    phx
    phy
    rep #$30
!al
!rl
    jsl KERN_CLS
    sep #$30
!as
!rs
    ply
    plx
    rts

; kern_gotoxy - A = column, Y = row.
kern_gotoxy
    sta KTMP
    sty KTMP2
    phx
    phy
    rep #$30
!al
!rl
    lda KTMP2
    and #$00ff
    tax
    lda KTMP
    and #$00ff
    jsl KERN_GOTOXY
    sep #$30
!as
!rs
    ply
    plx
    rts

; kern_getxy - KTMP = column, KTMP2 = row (16-bit each).
kern_getxy
    phx
    phy
    rep #$30
!al
!rl
    jsl KERN_GETXY
    sta KTMP
    stx KTMP2
    sep #$30
!as
!rs
    ply
    plx
    rts

; kern_frames - KTMP = VSYNC frame count (16-bit, wraps).
kern_frames
    phx
    phy
    rep #$30
!al
!rl
    jsl KERN_FRAMES
    sta KTMP
    sep #$30
!as
!rs
    ply
    plx
    rts

; kern_exit - back to the kernel prompt. Does not return.
kern_exit
    rep #$30
!al
!rl
    lda #0
    jsl KERN_EXIT
!as
!rs
-   bra -                           ; unreachable
