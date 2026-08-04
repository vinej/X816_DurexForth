; VIDEO - X816 video / screen / cursor (VERA + kernel console)
; VPOKE VPEEK VADDR V! V@ V!W IOC@ BORDER CLS
; LOCATE CURSOR POS SCROLLX SCROLLY TILE TDATA TATTR
;
; Stage B: VERA registers are BYTE registers, so every register body runs
; in a sep #$20 window between +VIO / +VIO_END. Plane access under sep
; reads single bytes: a 16-bit value's low byte is LSB+2n and its high
; byte LSB+2n+1 (the old MSB+n of the byte-plane era).

; VERA registers
VERA_ADDR_L = $9f20
VERA_ADDR_M = $9f21
VERA_ADDR_H = $9f22
VERA_DATA0  = $9f23
VERA_CTRL   = $9f25
VERA_DC_BORDER = $9f2c
; X816: the kernel console's text is LAYER 0 (runtime/console.c sets
; DC_VIDEO = $11); the X16 put its text on layer 1. Scroll the layer the
; text is actually on.
VERA_L0_HSCROLL_L = $9f30
VERA_L0_HSCROLL_H = $9f31
VERA_L0_VSCROLL_L = $9f32
VERA_L0_VSCROLL_H = $9f33

    +BACKLINK "vpoke", 5
VPOKE ; ( bank addr value -- )
    +VIO
    sep #$20
!as
    stz VERA_CTRL       ; ADDRSEL 0
    lda LSB+2, x        ; addr lo
    sta VERA_ADDR_L
    lda LSB+3, x        ; addr hi
    sta VERA_ADDR_M
    lda LSB+4, x        ; bank
    and #1
    sta VERA_ADDR_H     ; no auto-increment
    lda LSB, x          ; value
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
    rtl

    +BACKLINK "vpeek", 5
VPEEK ; ( bank addr -- value )
    +VIO
    sep #$20
!as
    stz VERA_CTRL
    lda LSB, x          ; addr lo
    sta VERA_ADDR_L
    lda LSB+1, x        ; addr hi
    sta VERA_ADDR_M
    lda LSB+2, x        ; bank
    and #1
    sta VERA_ADDR_H
    lda VERA_DATA0
    rep #$20
!al
    inx
    inx
    and #$ff
    sta LSB, x
    stz MSB, x
    +VIO_END
    rtl

    +BACKLINK "vaddr", 5
VADDR ; ( bank addr -- ) point data port at VRAM, auto-increment 1
    +VIO
    sep #$20
!as
    stz VERA_CTRL
    lda LSB, x
    sta VERA_ADDR_L
    lda LSB+1, x
    sta VERA_ADDR_M
    lda LSB+2, x        ; bank
    and #1
    ora #$10            ; auto-increment 1
    sta VERA_ADDR_H
    rep #$20
!al
    inx
    inx
    inx
    inx
    +VIO_END
    rtl

    +BACKLINK "v!", 2
V_STORE ; ( byte -- )
    +VIO
    sep #$20
!as
    lda LSB, x
    sta VERA_DATA0
    rep #$20
!al
    inx
    inx
    +VIO_END
    rtl

    +BACKLINK "v@", 2
V_FETCH ; ( -- byte )
    +VIO
    dex
    dex
    sep #$20
!as
    lda VERA_DATA0
    rep #$20
!al
    and #$ff
    sta LSB, x
    stz MSB, x
    +VIO_END
    rtl

    +BACKLINK "v!w", 3
V_STOREW ; ( w -- ) low byte first
    +VIO
    sep #$20
!as
    lda LSB, x
    sta VERA_DATA0
    lda LSB+1, x
    sta VERA_DATA0
    rep #$20
!al
    inx
    inx
    +VIO_END
    rtl

; X816: SCREEN (KERNAL screen_mode) and COLOR (the KERNAL's $0376 shadow)
; are gone - the kernel console owns the text mode and its attributes.

; ioc@ - fetch a byte from bank $00, where the I/O page lives. A plain C@
; goes through the cell's own bank byte; this word forces bank $00, which
; is where VERA register readbacks live.
    +BACKLINK "ioc@", 4
IOFETCHBYTE ; ( addr -- byte )
    lda LSB, x
    sta KTMP
    +VIO
    sep #$20
!as
    lda (KTMP)          ; DBR = $00 under +VIO
    rep #$20
!al
    +VIO_END
    and #$ff
    sta LSB, x
    stz MSB, x
    rtl

    +BACKLINK "border", 6
BORDER ; ( color -- )
    +VIO
    sep #$20
!as
    stz VERA_CTRL       ; DCSEL 0
    lda LSB, x
    sta VERA_DC_BORDER
    rep #$20
!al
    inx
    inx
    +VIO_END
    rtl

    +BACKLINK "cls", 3
CLS ; ( -- )
    jmp kern_cls

    +BACKLINK "locate", 6
LOCATE ; ( row col -- )
    lda LSB+2, x        ; row
    tay
    lda LSB, x          ; col
    jsl BANK1 + kern_gotoxy     ; A = column, Y = row
    inx
    inx
    inx
    inx
    rtl

    +BACKLINK "cursor", 6
CURSOR ; ( -- row col )
    jsl BANK1 + kern_getxy      ; KTMP = column, KTMP2 = row
    dex
    dex
    dex
    dex
    lda KTMP2
    sta LSB+2, x        ; row (deeper)
    stz MSB+2, x
    lda KTMP
    sta LSB, x          ; col on top
    stz MSB, x
    rtl

    +BACKLINK "pos", 3
POS ; ( -- col )
    jsl BANK1 + kern_getxy
    lda KTMP
    jmp PUSHA

    +BACKLINK "scrollx", 7
SCROLLX ; ( n -- )
    +VIO
    sep #$20
!as
    lda LSB, x
    sta VERA_L0_HSCROLL_L
    lda LSB+1, x
    and #$0f
    sta VERA_L0_HSCROLL_H
    rep #$20
!al
    inx
    inx
    +VIO_END
    rtl

    +BACKLINK "scrolly", 7
SCROLLY ; ( n -- )
    +VIO
    sep #$20
!as
    lda LSB, x
    sta VERA_L0_VSCROLL_L
    lda LSB+1, x
    and #$0f
    sta VERA_L0_VSCROLL_H
    rep #$20
!al
    inx
    inx
    +VIO_END
    rtl

; Text tilemap helpers. X816 console: 128-wide map at VRAM $00000 bank 0
; (runtime/console.c, MAPBASE 0), two bytes/cell (code, attribute).
; Address = y*256 + x*2 - the X16's $1b000 base is an X16-ism.
    +BACKLINK "tile", 4
TILE ; ( x y code attr -- )
    +VIO
    sep #$20
!as
    stz VERA_CTRL
    lda LSB+6, x        ; x
    asl                 ; x*2
    sta VERA_ADDR_L
    lda LSB+4, x        ; y
    sta VERA_ADDR_M
    lda #$10            ; bank 0 + auto-increment 1
    sta VERA_ADDR_H
    lda LSB+2, x        ; code
    sta VERA_DATA0
    lda LSB, x          ; attr
    sta VERA_DATA0
    txa                 ; drop 4 cells
    clc
    adc #8
    tax
    rep #$20
!al
    +VIO_END
    rtl

    +BACKLINK "tdata", 5
TDATA ; ( x y -- code )
    +VIO
    sep #$20
!as
    stz VERA_CTRL
    lda LSB+2, x        ; x
    asl
    sta VERA_ADDR_L
    lda LSB, x          ; y
    sta VERA_ADDR_M
    stz VERA_ADDR_H     ; bank 0, no increment
    lda VERA_DATA0
    rep #$20
!al
    inx
    inx
    and #$ff
    sta LSB, x
    stz MSB, x
    +VIO_END
    rtl

    +BACKLINK "tattr", 5
TATTR ; ( x y -- attr )
    +VIO
    sep #$20
!as
    stz VERA_CTRL
    lda LSB+2, x        ; x
    asl
    ora #1              ; attr byte at x*2+1
    sta VERA_ADDR_L
    lda LSB, x          ; y
    sta VERA_ADDR_M
    stz VERA_ADDR_H
    lda VERA_DATA0
    rep #$20
!al
    inx
    inx
    and #$ff
    sta LSB, x
    stz MSB, x
    +VIO_END
    rtl
