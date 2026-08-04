; INPUT - X16 input devices (KERNAL joystick / mouse)
; JOY MOUSE
; (MX/MY/MB/MWHEEL deferred: need the mouse_get buffer ABI confirmed and
;  live input to verify.)

K_JOYSTICK_GET = $ff56
K_MOUSE_CONFIG = $ff68

    +BACKLINK "joy", 3
JOY ; ( n -- buttons ) 0 = keyboard, 1-4 = gamepads; active-high, 0 if absent
    lda LSB, x
    stx W                       ; save forth stack pointer
    jsl BANK1 + K_JOYSTICK_GET          ; A=byte0 X=byte1 Y=$00 present / $ff absent
    cpy #0
    bne +                       ; absent
    eor #$ff                    ; active-high low byte
    sta W2
    txa
    eor #$ff                    ; active-high high byte
    sta W3
    bra ++
+   stz W2                      ; absent -> 0
    stz W3
++  ldx W                       ; restore forth stack pointer
    lda W2
    sta LSB, x
    lda W3
    sta MSB, x
    rtl

    +BACKLINK "mouse", 5
MOUSE ; ( mode -- ) 0 = off, 1 = on, -1 = auto-scale
    lda LSB, x
    stx W
    ldx #0
    ldy #0
    jsl BANK1 + K_MOUSE_CONFIG
    ldx W
    inx
    rtl

; mouse_get ($ff6b, .A=0): returns .A=buttons, r0 ($02/$03)=X, r1 ($04/$05)=Y.
K_MOUSE_GET = $ff6b

    +BACKLINK "mx", 2
MX ; ( -- x )
    stx W
    lda #0
    jsl BANK1 + K_MOUSE_GET
    ldx W
    dex
    lda $02
    sta LSB, x
    lda $03
    sta MSB, x
    rtl

    +BACKLINK "my", 2
MY ; ( -- y )
    stx W
    lda #0
    jsl BANK1 + K_MOUSE_GET
    ldx W
    dex
    lda $04
    sta LSB, x
    lda $05
    sta MSB, x
    rtl

    +BACKLINK "mb", 2
MB ; ( -- buttons ) bit0 left, bit1 right, bit2 middle
    stx W
    lda #0
    jsl BANK1 + K_MOUSE_GET             ; .A = buttons
    ldx W
    dex
    sta LSB, x
    stz MSB, x
    rtl

    +BACKLINK "mwheel", 6
MWHEEL ; ( -- delta ) signed wheel movement (best-effort: r2 low byte)
    stx W
    lda #0
    jsl BANK1 + K_MOUSE_GET
    ldx W
    dex
    lda $06
    sta LSB, x
    stz MSB, x
    bpl +
    lda #$ff
    sta MSB, x
+   rtl
