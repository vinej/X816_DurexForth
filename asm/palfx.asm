; PAL - VERA palette entry;  DCSEL - VERA FX DCSEL bank select
; (VERA_* constants come from video.asm, !src'd earlier.)
; Stage B: register bodies in sep #$20 windows; plane bytes at LSB+2n
; (low) / LSB+2n+1 (high). FX-MULT's 32-bit product now fits ONE cell,
; so it returns a single n instead of the stage-A lo/hi pair.

VERA_PALETTE = $fa00            ; VRAM $1fa00, bank 1, 2 bytes/entry

    +BACKLINK "pal!", 4
PAL ; ( rgb index -- ) set palette entry index (0-255) to 12-bit $0RGB
    +VIO
    sep #$20
!as
    stz VERA_CTRL
    lda LSB, x                  ; index
    asl                         ; index*2 (carry = high bit)
    sta VERA_ADDR_L
    lda #>VERA_PALETTE
    adc #0                      ; + carry
    sta VERA_ADDR_M
    lda #$11                    ; bank 1 + auto-increment 1
    sta VERA_ADDR_H
    lda LSB+2, x                ; rgb low  (G<<4 | B)
    sta VERA_DATA0
    lda LSB+3, x                ; rgb high (0 | R)
    and #$0f
    sta VERA_DATA0
    rep #$20
!al
    inx
    inx
    inx
    inx
    +VIO_END
    rts

    +BACKLINK "dcsel", 5
DCSEL ; ( n -- ) select DCSEL bank 0-63 (VERA FX)
    +VIO
    sep #$20
!as
    lda LSB, x
    asl                         ; DCSEL occupies CTRL bits 7:1, ADDRSEL = 0
    sta VERA_CTRL
    rep #$20
!al
    inx
    inx
    +VIO_END
    rts

    +BACKLINK "fx-mult", 7
FX_MULT ; ( a b -- n ) signed 16x16 -> 32-bit via the VERA FX multiplier.
        ; Stage B: the whole product fits one cell.
    +VIO
    sep #$20
!as
    lda #$04                    ; DCSEL=2
    sta VERA_CTRL
    stz $9f29                   ; FX_CTRL = 0
    lda #$10                    ; FX_MULT: Multiplier Enable
    sta $9f2c
    lda #$0c                    ; DCSEL=6
    sta VERA_CTRL
    lda LSB+2, x                ; multiplicand (FX_CACHE_L/M)
    sta $9f29
    lda LSB+3, x
    sta $9f2a
    lda LSB, x                  ; multiplier (FX_CACHE_H/U)
    sta $9f2b
    lda LSB+1, x
    sta $9f2c
    lda $9f29                   ; read FX_ACCUM_RESET (reset accumulator)
    lda #$04                    ; DCSEL=2
    sta VERA_CTRL
    lda #$40                    ; FX_CTRL: Cache Write Enable
    sta $9f29
    stz $9f20                   ; ADDR0 = 0, no increment
    stz $9f21
    stz $9f22
    stz $9f23                   ; multiply + write 32-bit result to VRAM 0
    ; read the 4 result bytes, each with a fresh address (avoid the VERA
    ; pre-fetch that latches stale data on auto-increment reads).
    stz $9f21
    stz $9f22
    stz $9f20
    lda $9f23
    sta W                       ; result byte 0
    lda #1
    sta $9f20
    lda $9f23
    sta W+1                     ; byte 1
    lda #2
    sta $9f20
    lda $9f23
    sta W+2                     ; byte 2
    lda #3
    sta $9f20
    lda $9f23
    sta W+3                     ; byte 3
    lda #$04                    ; restore normal VERA state
    sta VERA_CTRL               ; DCSEL=2
    stz $9f29                   ; FX_CTRL = 0 (cache-write off)
    stz $9f2c                   ; FX_MULT = 0 (multiplier off)
    stz VERA_CTRL               ; DCSEL=0, ADDRSEL=0
    rep #$20
!al
    inx
    inx
    lda W
    sta LSB, x
    lda W+2
    sta MSB, x
    +VIO_END
    rts

    +BACKLINK "fx*", 3
    jmp FX_MULT                 ; alias: signed 16x16 -> 32-bit

    +BACKLINK "fx-off", 6
FX_OFF ; ( -- ) turn the VERA FX helpers back off (so plain VPOKE works)
    +VIO
    sep #$20
!as
    lda #$04
    sta VERA_CTRL               ; DCSEL=2
    stz $9f29                   ; FX_CTRL = 0
    stz $9f2c                   ; FX_MULT = 0
    stz VERA_CTRL               ; DCSEL=0
    rep #$20
!al
    +VIO_END
    rts

    +BACKLINK "fx-fill", 7
FX_FILL ; ( byte vbank vaddr count -- ) fast VRAM fill via the 32-bit cache.
        ; count is a 16-bit quantity (one call fills up to 64 KB of VRAM).
    +VIO
    sep #$20
!as
    lda LSB+6, x                ; fill byte
    jsr fx_fill_core
    txa                         ; drop 4 cells
    clc
    adc #8
    tax
    rep #$20
!al
    +VIO_END
    rts

    +BACKLINK "fx-clear", 8
FX_CLEAR ; ( vbank vaddr count -- ) zero a VRAM region
    +VIO
    sep #$20
!as
    lda #0
    jsr fx_fill_core
    txa                         ; drop 3 cells
    clc
    adc #6
    tax
    rep #$20
!al
    +VIO_END
    rts

; A = fill byte; vbank=LSB+4,x vaddr=LSB+2/+3,x count=LSB/+1,x (bytes).
; Leaves the stack alone. 8-bit A throughout.
!as
fx_fill_core
    sta W3                      ; save the fill byte
    lda #$0c
    sta VERA_CTRL               ; DCSEL=6 (FX_CACHE_* at $9f29-$9f2c)
    lda W3
    sta $9f29                   ; load the 32-bit cache with 4x the fill byte
    sta $9f2a
    sta $9f2b
    sta $9f2c
    lda #$04
    sta VERA_CTRL               ; DCSEL=2, ADDRSEL=0
    stz $9f29                   ; FX off for the leading bytes
    lda LSB+2, x                ; vaddr low
    sta $9f20
    lda LSB+3, x                ; vaddr high
    sta $9f21
    lda LSB+4, x                ; vbank -> address bit 16
    and #1
    ora #$10                    ; +1 increment for the lead
    sta $9f22                   ; ADDR0_H
    ; VERA cache writes hit the ALIGNED quad (the 2 low address bits are
    ; ignored), so an unaligned start would clobber up to 3 bytes BEFORE
    ; the span. Write 0-3 leading bytes plainly until 4-byte aligned.
    lda LSB+2, x
    and #3
    beq fxf_lead_done           ; already aligned
    eor #3
    inc                         ; lead = (4 - (vaddr & 3)) & 3
    sta W2
    lda LSB+1, x                ; clamp lead to the count
    bne fxf_lead_ok
    lda LSB, x
    cmp W2
    bcs fxf_lead_ok
    sta W2
fxf_lead_ok
    sec                         ; count -= lead
    lda LSB, x
    sbc W2
    sta LSB, x
    lda LSB+1, x
    sbc #0
    sta LSB+1, x
    ldy W2
    beq fxf_lead_done
    lda W3
fxf_lead_lp
    sta $9f23                   ; plain byte writes, addr+=1
    dey
    bne fxf_lead_lp
fxf_lead_done
    lda LSB+4, x                ; now aligned: cache writes, +4
    and #1
    ora #$30                    ; auto-increment +4 (code 3, bits 7:4)
    sta $9f22
    lda #$40
    sta $9f29                   ; FX_CTRL = Cache Write Enable
    lda LSB, x                  ; rem = count & 3
    and #3
    sta W2
    lda LSB+1, x                ; W = count >> 2  (number of 4-byte flushes)
    lsr
    sta W+1
    lda LSB, x
    ror
    sta W
    lsr W+1
    ror W
fxf_loop
    lda W
    ora W+1
    beq fxf_rem
    stz $9f23                   ; DATA0=0 -> flush the whole cache (4 bytes), addr+=4
    lda W
    bne fxf_nb
    dec W+1
fxf_nb
    dec W
    bra fxf_loop
fxf_rem
    ldy W2                      ; 1..3 leftover bytes: plain byte writes at the
    beq fxf_end                 ; current (4-byte-aligned) address
    stz $9f29                   ; FX_CTRL = 0 (cache write off)
    lda LSB+4, x
    and #1
    ora #$10                    ; +1 auto-increment | bank  (ADDR L/M unchanged)
    sta $9f22
    lda W3                      ; fill byte
fxf_rloop
    sta $9f23                   ; DATA0 = byte, addr+=1
    dey
    bne fxf_rloop
fxf_end
    lda #$04
    sta VERA_CTRL               ; DCSEL=2
    stz $9f29                   ; FX_CTRL = 0
    stz VERA_CTRL               ; DCSEL=0, ADDRSEL=0
    rts
!al
