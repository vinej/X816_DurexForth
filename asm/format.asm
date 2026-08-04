; <# #> HOLD SIGN # #S U. . SPACE
;
; Stage B: a pictured number is a 64-bit double (two cells), and UD/MOD
; does the heavy lifting exactly as before - the digit extraction below is
; width-independent (digits < 36). The hold area is bytes in bank-1 golden
; RAM; the pointer stays a 16-bit self-modified immediate.

.hold_start = $5fc ; X16 golden RAM (grows down, stays within page $05)

; : <# holdp ! ;
+BACKLINK "<#", 2
LESS_NUMBER_SIGN
    lda #.hold_start
    sta .holdp
    rtl

; : #> 2drop holdp @ $5fc over - ;
+BACKLINK "#>", 2
NUMBER_SIGN_GREATER ; ( ud -- addr len )
    lda .holdp
    sta LSB+2,x
    lda #(BANK1 >> 16)
    sta MSB+2,x
    lda #.hold_start
    sec
    sbc .holdp
    sta LSB,x
    stz MSB,x
    rtl

; : hold -1 holdp +! holdp @ c! ;
+BACKLINK "hold", 4
HOLD
    dec .holdp
    inx
    inx
    sep #$20
!as
    lda LSB-2,x
.holdp = * + 1
    sta .hold_start
    rep #$20
!al
    rtl

; : sign 0< if '-' hold then ;
+BACKLINK "sign", 4
SIGN
    inx
    inx
    lda MSB-2,x
    and #$8000
    bne +
    rtl
+   jsl BANK1 + LITC
    !byte '-'
    jmp HOLD

; : # base @ ud/mod rot
; dup $a < if 7 - then $37 + hold ;
+BACKLINK "#", 1
NUMBER_SIGN
    jsl BANK1 + BASE
    jsl BANK1 + FETCH
    jsl BANK1 + UD_MOD
    jsl BANK1 + ROT
    lda LSB,x
    cmp #10
    bcs +
    sbc #6
+   clc
    adc #$37
    sta LSB,x
    jmp HOLD

; : #s # begin 2dup or while # repeat ;
+BACKLINK "#s", 2
NUMBER_SIGN_S
    jsl BANK1 + NUMBER_SIGN
    lda LSB,x
    ora MSB,x
    ora LSB+2,x
    ora MSB+2,x
    bne NUMBER_SIGN_S
    rtl

; : u. 0 <# #s #> type space ;
+BACKLINK "u.", 2
    jsl BANK1 + ZERO
    jsl BANK1 + LESS_NUMBER_SIGN
    jsl BANK1 + NUMBER_SIGN_S
    jsl BANK1 + NUMBER_SIGN_GREATER
    jsl BANK1 + TYPE
    jmp SPACE

; : . dup abs 0 <# #s rot sign #>
; type space ;
+BACKLINK ".", 1
DOT
    jsl BANK1 + DUP
    jsl BANK1 + ABS
    jsl BANK1 + ZERO
    jsl BANK1 + LESS_NUMBER_SIGN
    jsl BANK1 + NUMBER_SIGN_S
    jsl BANK1 + ROT
    jsl BANK1 + SIGN
    jsl BANK1 + NUMBER_SIGN_GREATER
    jsl BANK1 + TYPE
    jmp SPACE

+BACKLINK "space", 5
SPACE
    lda #' '
    jmp PUTCHR
