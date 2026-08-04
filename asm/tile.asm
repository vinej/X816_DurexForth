; TILE - VERA layer configuration (layer 0 or 1).
; LAYER-ON LAYER-OFF MAPBASE TILEBASE LAYER-MODE
; (TILE/TDATA/TATTR live in video.asm. VERA_* and VERA_DC_VIDEO come from
;  video.asm / sprite.asm, !src'd earlier.)
; Stage B: register bodies in sep #$20 windows; plane bytes at LSB+2n
; (low) / LSB+2n+1 (high).

VERA_L0_CONFIG   = $9f2d        ; +7 -> layer 1
VERA_L0_MAPBASE  = $9f2e
VERA_L0_TILEBASE = $9f2f

    +BACKLINK "layer-on", 8
LAYER_ON ; ( layer -- )
    +VIO
    sep #$20
!as
    stz VERA_CTRL
    lda #$10                    ; layer 0 enable = DC_VIDEO bit4
    ldy LSB, x
    beq +
    asl                         ; layer 1 -> bit5
+   ora VERA_DC_VIDEO
    sta VERA_DC_VIDEO
    rep #$20
!al
    inx
    inx
    +VIO_END
    rts

    +BACKLINK "layer-off", 9
LAYER_OFF ; ( layer -- )
    +VIO
    sep #$20
!as
    stz VERA_CTRL
    lda #$10
    ldy LSB, x
    beq +
    asl
+   eor #$ff
    and VERA_DC_VIDEO
    sta VERA_DC_VIDEO
    rep #$20
!al
    inx
    inx
    +VIO_END
    rts

    +BACKLINK "mapbase", 7
MAPBASE ; ( layer bank addr -- ) 512-aligned tile-map base
    +VIO
    sep #$20
!as
    stz VERA_CTRL
    lda LSB+1, x                ; addr hi
    lsr                         ; addr >> 9 (bits 16:9 of the map base)
    sta W
    lda LSB+2, x                ; bank
    and #1
    beq +
    lda W
    ora #$80                    ; bank -> bit 16 of the base (reg bit7)
    sta W
+   lda LSB+4, x                ; layer * 7
    beq ++
    lda #7
++  tay
    lda W
    sta VERA_L0_MAPBASE, y
    txa                         ; drop 3 cells
    clc
    adc #6
    tax
    rep #$20
!al
    +VIO_END
    rts

    +BACKLINK "tilebase", 8
TILEBASE ; ( layer bank addr -- ) 2 KB-aligned tile-data base, 8x8 tiles
    +VIO
    sep #$20
!as
    stz VERA_CTRL
    lda LSB+1, x                ; addr hi
    lsr
    lsr
    lsr                         ; addr >> 11
    sta W
    lda LSB+2, x                ; bank
    and #1
    beq +
    lda W
    ora #$20                    ; bank -> bit 16 (reg bit 5 before the <<2)
    sta W
+   lda W
    asl
    asl                         ; base bits occupy reg bits 7:2
    sta W
    lda LSB+4, x                ; layer * 7
    beq ++
    lda #7
++  tay
    lda W
    sta VERA_L0_TILEBASE, y
    txa                         ; drop 3 cells
    clc
    adc #6
    tax
    rep #$20
!al
    +VIO_END
    rts

    +BACKLINK "layer-mode", 10
LAYER_MODE ; ( layer cfg -- ) write the layer config byte
    +VIO
    sep #$20
!as
    stz VERA_CTRL
    lda LSB+2, x                ; layer * 7
    beq +
    lda #7
+   tay
    lda LSB, x                  ; cfg
    sta VERA_L0_CONFIG, y
    rep #$20
!al
    inx
    inx
    inx
    inx
    +VIO_END
    rts
