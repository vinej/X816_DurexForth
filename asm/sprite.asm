; SPRITE - X16 sprites (VERA sprite attribute RAM at VRAM $1fc00, 8 bytes each)
; SPRITES-ON SPRITES-OFF SPRITE-POS SPRITE-GET SPRITE-IMAGE SPRITE-SIZE
; SPRITE-Z SPRITE SPRITE-MOV SPRITE-MEM
; (VERA_* constants come from video.asm, !src'd earlier.)
; Stage B: register bodies in sep #$20 windows; plane bytes at LSB+2n
; (low) / LSB+2n+1 (high).

VERA_DC_VIDEO = $9f29           ; DCSEL 0: bit6 = sprite enable

; Point VERA ADDR0 at sprite attribute byte: $1fc00 + sprite*8 + SPR_OFF,
; bank 1, auto-increment 1.  A = sprite index. Preserves X (forth stack ptr).
; Entered and left in 8-bit A mode.
!as
set_sprite
    stz VERA_CTRL
    stz W+1
    asl
    rol W+1
    asl
    rol W+1
    asl
    rol W+1                     ; A / W+1 = sprite*8
    clc
    adc SPR_OFF
    sta VERA_ADDR_L             ; $fc00 low byte is 0
    lda W+1
    adc #$fc
    sta VERA_ADDR_M
    lda #$11                    ; bank 1 + auto-increment 1
    sta VERA_ADDR_H
    rts
!al

    +BACKLINK "sprites-on", 10
SPRITES_ON ; ( -- )
    +VIO
    sep #$20
!as
    stz VERA_CTRL
    lda VERA_DC_VIDEO
    ora #$40
    sta VERA_DC_VIDEO
    rep #$20
!al
    +VIO_END
    rts

    +BACKLINK "sprites-off", 11
SPRITES_OFF ; ( -- )
    +VIO
    sep #$20
!as
    stz VERA_CTRL
    lda VERA_DC_VIDEO
    and #$bf
    sta VERA_DC_VIDEO
    rep #$20
!al
    +VIO_END
    rts

    +BACKLINK "sprite-pos", 10
SPRITE_POS ; ( x y sprite -- )
    +VIO
    sep #$20
!as
    lda #2
    sta SPR_OFF
    lda LSB, x                  ; sprite
    jsr set_sprite
    lda LSB+4, x                ; x lo
    sta VERA_DATA0
    lda LSB+5, x                ; x hi
    and #3
    sta VERA_DATA0
    lda LSB+2, x                ; y lo
    sta VERA_DATA0
    lda LSB+3, x                ; y hi
    and #3
    sta VERA_DATA0
    rep #$20
!al
    inx
    inx
    inx
    inx
    inx
    inx
    +VIO_END
    rts

    +BACKLINK "sprite-get", 10
SPRITE_GET ; ( sprite -- x y )
    +VIO
    sep #$20
!as
    lda #2
    sta SPR_OFF
    lda LSB, x
    jsr set_sprite
    lda VERA_DATA0              ; x lo
    sta W
    lda VERA_DATA0              ; x hi
    and #3
    sta W+1
    lda VERA_DATA0              ; y lo
    sta W2
    lda VERA_DATA0              ; y hi
    and #3
    sta W2+1
    rep #$20
!al
    lda W                       ; x -> TOS slot
    sta LSB, x
    stz MSB, x
    dex                         ; push y
    dex
    lda W2
    sta LSB, x
    stz MSB, x
    +VIO_END
    rts

    +BACKLINK "sprite-image", 12
SPRITE_IMAGE ; ( graphaddr sprite -- ) 4bpp image, 32-aligned VRAM address
    +VIO
    sep #$20
!as
    stz SPR_OFF
    lda LSB, x                  ; sprite
    jsr set_sprite
    lda LSB+3, x                ; graphaddr hi
    sta W+1
    lda LSB+2, x                ; graphaddr lo
    sta W
    ldy #5                      ; address >> 5
-   lsr W+1
    ror W
    dey
    bne -
    lda W
    sta VERA_DATA0              ; byte0 = (addr>>5) low
    lda W+1
    and #$0f
    sta VERA_DATA0             ; byte1 = high nibble, 4bpp (mode bit7 = 0)
    rep #$20
!al
    inx
    inx
    inx
    inx
    +VIO_END
    rts

    +BACKLINK "sprite-size", 11
SPRITE_SIZE ; ( width height sprite -- ) size codes 0-3 = 8/16/32/64
    +VIO
    sep #$20
!as
    lda #7
    sta SPR_OFF
    lda LSB, x
    jsr set_sprite
    lda LSB+2, x                ; height
    and #3
    asl
    asl
    asl
    asl
    asl
    asl                         ; << 6
    sta W
    lda LSB+4, x                ; width
    and #3
    asl
    asl
    asl
    asl                         ; << 4
    ora W
    sta VERA_DATA0
    txa                         ; drop 3 cells
    clc
    adc #6
    tax
    rep #$20
!al
    +VIO_END
    rts

    +BACKLINK "sprite-z", 8
SPRITE_Z ; ( z sprite -- ) Z-depth 0=off 1=behind 2=between 3=front
    +VIO
    sep #$20
!as
    lda #6
    sta SPR_OFF
    lda LSB, x
    jsr set_sprite
    lda LSB+2, x                ; z
    and #3
    asl
    asl                         ; << 2
    sta VERA_DATA0
    rep #$20
!al
    inx
    inx
    inx
    inx
    +VIO_END
    rts

    +BACKLINK "sprite", 6
SPRITE ; ( num zdepth -- ) set Z-depth on sprite num and enable the layer
    +VIO
    sep #$20
!as
    lda #6
    sta SPR_OFF
    lda LSB+2, x                ; num
    jsr set_sprite
    lda LSB, x                  ; zdepth
    and #3
    asl
    asl
    sta VERA_DATA0
    rep #$20
!al
    inx
    inx
    inx
    inx
    +VIO_END
    jmp SPRITES_ON

    +BACKLINK "sprite-mov", 10
SPRITE_MOV ; ( num x y -- ) = BASIC MOVSPR num,x,y
    +VIO
    sep #$20
!as
    lda #2
    sta SPR_OFF
    lda LSB+4, x                ; num
    jsr set_sprite
    lda LSB+2, x                ; x lo
    sta VERA_DATA0
    lda LSB+3, x                ; x hi
    and #3
    sta VERA_DATA0
    lda LSB, x                  ; y lo
    sta VERA_DATA0
    lda LSB+1, x                ; y hi
    and #3
    sta VERA_DATA0
    txa                         ; drop 3 cells
    clc
    adc #6
    tax
    rep #$20
!al
    +VIO_END
    rts

    +BACKLINK "sprite-mem", 10
SPRITE_MEM ; ( num bank addr -- ) point image at VRAM bank:addr
    +VIO
    sep #$20
!as
    stz SPR_OFF
    lda LSB+4, x                ; num
    jsr set_sprite
    lda LSB+1, x                ; addr hi
    sta W+1
    lda LSB, x                  ; addr lo
    sta W
    ldy #5
-   lsr W+1
    ror W
    dey
    bne -
    lda LSB+2, x                ; bank
    and #1
    asl
    asl
    asl                         ; bank -> bit 3 of the high byte (>>5 of bit16)
    ora W+1
    and #$0f
    sta W+1
    lda W
    sta VERA_DATA0             ; byte0
    lda W+1
    sta VERA_DATA0             ; byte1
    txa                         ; drop 3 cells
    clc
    adc #6
    tax
    rep #$20
!al
    +VIO_END
    rts
