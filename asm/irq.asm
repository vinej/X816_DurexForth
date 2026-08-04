; IRQ - Forth words on VERA interrupt sources (SYSTEM.TXT).
;   ' W IRQ              run W once per frame (VSYNC);        0 IRQ disarms
;   ' W line LINE-IRQ    run W at a scanline (0-511) per frame; 0 0 LINE-IRQ off
;   ' W SPRCOL-IRQ       run W on sprite collisions;           0 SPRCOL-IRQ off
;   ' W AFLOW-IRQ        run W on PCM FIFO low - W MUST refill the FIFO
;                        (refilling IS the acknowledge) or the machine livelocks
;   COLLISIONS           ( -- mask ) collision groups seen since last read
;
; Chains onto CINV ($0314): the KERNAL stub has pushed A/X/Y before the
; vector and restores them after, and still scans the keyboard and acks
; VSYNC. LINE and SPRCOL are acknowledged HERE (nobody else will).
; Armed words run inside the interrupt on a private 16-cell stack window
; (the interrupted X cannot be trusted as the Forth SP - um* and the
; KERNAL wrappers borrow X); W/W2/W3, the RAM bank, VERA CTRL and both
; data-port addresses are saved around the whole pass; a busy flag skips
; over an overrunning pass.  Armed words must be stack-balanced, short,
; no disk I/O; restore any FX state (DCSEL=2) they touch; disarm before
; FORGETting them.

CINV      = $0314
VERA_IEN  = $9f26
VERA_ISR  = $9f27
VERA_LINE = $9f28

; xt slots: +0 frame(VSYNC)  +2 line  +4 sprcol  +6 aflow
irq_xts   !fill 8, 0
irq_old   !word 0
irq_on    !byte 0
irq_busy  !byte 0
irq_isr_s !byte 0
irq_colm  !byte 0               ; accumulated collision groups
irq_zp    !fill 6, 0            ; saved W/W2/W3
irq_bank  !byte 0               ; saved RAM bank
irq_vctl  !byte 0               ; saved VERA CTRL (ADDRSEL + DCSEL)
irq_vadr  !fill 6, 0            ; saved $9F20-22, both ports

X_IRQ = $d8                     ; private stack window (16 cells, deep end)

    +BACKLINK "irq", 3
    ; ( xt -- )  per-frame word; 0 disarms
    ldy #0
    bra irq_arm

    +BACKLINK "line-irq", 8
    ; ( xt line -- )  word at scanline 0-511; 0 0 disarms
    lda LSB, x                  ; scanline low byte
    sta VERA_LINE
    lda MSB, x                  ; bit 8 lives in IEN bit 7
    lsr
    lda #$80
    bcs il_b8set
    trb VERA_IEN
    bra il_b8done
il_b8set
    tsb VERA_IEN
il_b8done
    inx                         ; drop the line; xt on top
    ldy #2
    bra irq_arm

    +BACKLINK "sprcol-irq", 10
    ; ( xt -- )  word on sprite collision; 0 disarms
    ldy #4
    bra irq_arm

    +BACKLINK "aflow-irq", 9
    ; ( xt -- )  word on PCM FIFO low; MUST refill the FIFO; 0 disarms
    ldy #6
    bra irq_arm

irq_arm ; ( xt -- ) with Y = slot offset
    lda LSB, x
    sta irq_xts, y
    lda MSB, x
    sta irq_xts+1, y
    inx
    ; fall through: recompute IEN bits and the CINV hook

irq_update
    php
    sei                         ; php/plp: restore the caller's I flag
    ldy #2                      ; slots line/sprcol/aflow <-> IEN bits 1/2/3
    lda #2
iu_loop
    sta W2                      ; W2 = the IEN bit for this slot
    lda irq_xts, y
    ora irq_xts+1, y
    beq iu_off
    lda W2                      ; armed: enable the source
    tsb VERA_IEN
    bra iu_next
iu_off
    lda W2                      ; disarmed: disable + drop stale pending
    trb VERA_IEN
    cmp #8                      ; (AFLOW cannot be acked via ISR)
    beq iu_next
    sta VERA_ISR
iu_next
    iny
    iny
    lda W2
    asl
    cpy #8
    bne iu_loop

    ldy #7                      ; hook CINV iff any slot is armed
    lda #0
iu_scan
    ora irq_xts, y
    dey
    bpl iu_scan
    bne iu_hook
    lda irq_on
    beq iu_done
    lda irq_old
    sta CINV
    lda irq_old+1
    sta CINV+1
    stz irq_on
    bra iu_done
iu_hook
    lda irq_on
    bne iu_done
    lda CINV
    sta irq_old
    lda CINV+1
    sta irq_old+1
    lda #<irq_tramp
    sta CINV
    lda #>irq_tramp
    sta CINV+1
    lda #1
    sta irq_on
iu_done
    plp
    rtl

irq_tramp
    lda irq_busy
    beq it_go
    jmp (irq_old)               ; still inside the last pass: skip
it_go
    inc irq_busy
    lda VERA_ISR
    sta irq_isr_s
    ldy #5                      ; save W/W2/W3 (Y-indexed absolute: X is
it_sv                           ; not ours, and lda zp,y does not exist)
    lda+2 $9c, y
    sta irq_zp, y
    dey
    bpl it_sv
    lda $00
    sta irq_bank
    lda $9f25                   ; VERA state a handler may clobber:
    sta irq_vctl                ; CTRL, then both ports' addresses
    stz $9f25                   ; ADDRSEL 0 (DCSEL 0)
    ldy #2
it_sv0
    lda $9f20, y
    sta irq_vadr, y
    dey
    bpl it_sv0
    lda #1
    sta $9f25                   ; ADDRSEL 1
    ldy #2
it_sv1
    lda $9f20, y
    sta irq_vadr+3, y
    dey
    bpl it_sv1

    lda irq_isr_s
    and #1                      ; VSYNC (the KERNAL acks it)
    beq it_novs
    ldy #0
    jsl BANK1 + irq_call
it_novs
    lda irq_isr_s
    and #2                      ; LINE: ack first, nobody else will
    beq it_noln
    sta VERA_ISR
    ldy #2
    jsl BANK1 + irq_call
it_noln
    lda irq_isr_s
    and #4                      ; SPRCOL: ack, then accumulate the groups
    beq it_nosc
    sta VERA_ISR
    lda irq_isr_s
    lsr
    lsr
    lsr
    lsr
    ora irq_colm
    sta irq_colm
    ldy #4
    jsl BANK1 + irq_call
it_nosc
    lda irq_isr_s
    and #8                      ; AFLOW: the armed word refills = the ack
    beq it_noaf
    ldy #6
    jsl BANK1 + irq_call
it_noaf
    lda #1
    sta $9f25                   ; put the VERA state back: port 1...
    ldy #2
it_rs1
    lda irq_vadr+3, y
    sta $9f20, y
    dey
    bpl it_rs1
    stz $9f25                   ; ...port 0...
    ldy #2
it_rs0
    lda irq_vadr, y
    sta $9f20, y
    dey
    bpl it_rs0
    lda irq_vctl                ; ...and CTRL last
    sta $9f25
    lda irq_bank
    sta $00
    ldy #5
it_rs
    lda irq_zp, y
    sta+2 $9c, y
    dey
    bpl it_rs
    stz irq_busy
irq_chain
    jmp (irq_old)               ; previous CINV handler

irq_call ; Y = slot offset: run the xt (if any) on the private stack
    lda irq_xts, y
    ora irq_xts+1, y
    beq ic_none
    lda irq_xts, y
    sta W
    lda irq_xts+1, y
    sta W+1
    ldx #X_IRQ
    jmp (W)                     ; word rtl's back to the dispatch
ic_none
    rtl

    +BACKLINK "collisions", 10
    ; ( -- mask ) collision groups seen since the last read (then cleared)
    dex
    lda irq_colm
    sta LSB, x
    stz MSB, x
    stz irq_colm
    rtl
