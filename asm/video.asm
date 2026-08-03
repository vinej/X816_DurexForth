; VIDEO - X16 video / screen / cursor (VERA + KERNAL)
; VPOKE VPEEK VADDR V! V@ V!W SCREEN COLOR BORDER CLS
; LOCATE CURSOR POS SCROLLX SCROLLY TILE TDATA TATTR

; VERA registers
VERA_ADDR_L = $9f20
VERA_ADDR_M = $9f21
VERA_ADDR_H = $9f22
VERA_DATA0  = $9f23
VERA_CTRL   = $9f25
VERA_DC_BORDER = $9f2c
VERA_L1_HSCROLL_L = $9f37
VERA_L1_HSCROLL_H = $9f38
VERA_L1_VSCROLL_L = $9f39
VERA_L1_VSCROLL_H = $9f3a
; X816: the I/O page is in bank $00 and the program's DBR is its own bank,
; so every word touching VERA registers runs between +VIO / +VIO_END
; (x816.asm). Word bodies use only VERA registers and the direct page, so
; the whole body can run with DBR = $00.

    +BACKLINK "vpoke", 5
VPOKE ; ( bank addr value -- )
    +VIO
    stz VERA_CTRL       ; ADDRSEL 0
    lda LSB+1, x        ; addr lo
    sta VERA_ADDR_L
    lda MSB+1, x        ; addr hi
    sta VERA_ADDR_M
    lda LSB+2, x        ; bank
    and #1
    sta VERA_ADDR_H     ; no auto-increment
    lda LSB, x          ; value
    sta VERA_DATA0
    inx
    inx
    inx
    +VIO_END
    rts

    +BACKLINK "vpeek", 5
VPEEK ; ( bank addr -- value )
    +VIO
    stz VERA_CTRL
    lda LSB, x          ; addr lo
    sta VERA_ADDR_L
    lda MSB, x          ; addr hi
    sta VERA_ADDR_M
    lda LSB+1, x        ; bank
    and #1
    sta VERA_ADDR_H
    inx                 ; drop addr
    lda VERA_DATA0
    sta LSB, x
    stz MSB, x
    +VIO_END
    rts

    +BACKLINK "vaddr", 5
VADDR ; ( bank addr -- ) point data port at VRAM, auto-increment 1
    +VIO
    stz VERA_CTRL
    lda LSB, x
    sta VERA_ADDR_L
    lda MSB, x
    sta VERA_ADDR_M
    lda LSB+1, x        ; bank
    and #1
    ora #$10            ; auto-increment 1
    sta VERA_ADDR_H
    inx
    inx
    +VIO_END
    rts

    +BACKLINK "v!", 2
V_STORE ; ( byte -- )
    +VIO
    lda LSB, x
    sta VERA_DATA0
    inx
    +VIO_END
    rts

    +BACKLINK "v@", 2
V_FETCH ; ( -- byte )
    +VIO
    dex
    lda VERA_DATA0
    sta LSB, x
    stz MSB, x
    +VIO_END
    rts

    +BACKLINK "v!w", 3
V_STOREW ; ( w -- ) low byte first
    +VIO
    lda LSB, x
    sta VERA_DATA0
    lda MSB, x
    sta VERA_DATA0
    inx
    +VIO_END
    rts

; X816: SCREEN (KERNAL screen_mode) and COLOR (the KERNAL's $0376 shadow)
; are gone - the kernel console owns the text mode and its attributes.

    +BACKLINK "border", 6
BORDER ; ( color -- )
    +VIO
    stz VERA_CTRL       ; DCSEL 0
    lda LSB, x
    sta VERA_DC_BORDER
    inx
    +VIO_END
    rts

    +BACKLINK "cls", 3
CLS ; ( -- )
    jmp kern_cls

    +BACKLINK "locate", 6
LOCATE ; ( row col -- )
    lda LSB+1, x        ; row
    tay
    lda LSB, x          ; col
    jsr kern_gotoxy     ; A = column, Y = row
    inx
    inx
    rts

    +BACKLINK "cursor", 6
CURSOR ; ( -- row col )
    jsr kern_getxy      ; KTMP = column, KTMP2 = row
    dex
    dex
    lda KTMP2
    sta LSB+1, x        ; row (deeper)
    stz MSB+1, x
    lda KTMP
    sta LSB, x          ; col on top
    stz MSB, x
    rts

    +BACKLINK "pos", 3
POS ; ( -- col )
    jsr kern_getxy
    dex
    lda KTMP
    sta LSB, x
    stz MSB, x
    rts

    +BACKLINK "scrollx", 7
SCROLLX ; ( n -- )
    +VIO
    lda LSB, x
    sta VERA_L1_HSCROLL_L
    lda MSB, x
    and #$0f
    sta VERA_L1_HSCROLL_H
    inx
    +VIO_END
    rts

    +BACKLINK "scrolly", 7
SCROLLY ; ( n -- )
    +VIO
    lda LSB, x
    sta VERA_L1_VSCROLL_L
    lda MSB, x
    and #$0f
    sta VERA_L1_VSCROLL_H
    inx
    +VIO_END
    rts

; Text tilemap helpers. Default 80x60 mode: 128-wide map at VRAM $1b000,
; two bytes/cell (code, attribute). Address = $b000 + y*256 + x*2, bank 1.
    +BACKLINK "tile", 4
TILE ; ( x y code attr -- )
    +VIO
    lda LSB+3, x        ; x
    asl                 ; x*2
    sta VERA_ADDR_L
    lda LSB+2, x        ; y
    clc
    adc #$b0
    sta VERA_ADDR_M
    lda #$11            ; bank 1 + auto-increment 1
    sta VERA_ADDR_H
    lda LSB+1, x        ; code
    sta VERA_DATA0
    lda LSB, x          ; attr
    sta VERA_DATA0
    inx
    inx
    inx
    inx
    +VIO_END
    rts

    +BACKLINK "tdata", 5
TDATA ; ( x y -- code )
    +VIO
    lda LSB+1, x        ; x
    asl
    sta VERA_ADDR_L
    lda LSB, x          ; y
    clc
    adc #$b0
    sta VERA_ADDR_M
    lda #$01            ; bank 1, no increment
    sta VERA_ADDR_H
    inx
    lda VERA_DATA0
    sta LSB, x
    stz MSB, x
    +VIO_END
    rts

    +BACKLINK "tattr", 5
TATTR ; ( x y -- attr )
    +VIO
    lda LSB+1, x        ; x
    asl
    ora #1              ; attr byte at x*2+1
    sta VERA_ADDR_L
    lda LSB, x          ; y
    clc
    adc #$b0
    sta VERA_ADDR_M
    lda #$01
    sta VERA_ADDR_H
    inx
    lda VERA_DATA0
    sta LSB, x
    stz MSB, x
    +VIO_END
    rts
