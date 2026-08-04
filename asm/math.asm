; U< - UM* UM/MOD M+ INVERT NEGATE ABS * DNEGATE M* 0< S>D FM/MOD /MOD UD/MOD
;
; Stage B: cells are 32 bits, so a "double" (ud/d) is 64 - two cells, the
; more significant on top, exactly the ANS shape. UM* is a 32x32->64 shift
; multiply and UM/MOD a 64/32 shift divide: Garth Wilson's algorithms
; (http://6502.org/source/integers/ummodfix/ummodfix.htm), one register
; width up - 33-step loop instead of 17, product in W..W2 (8 contiguous
; bytes) instead of 4.

    +BACKLINK "u<", 2
U_LESS ; ( a b -- flag )
    lda MSB + 2, x
    cmp MSB, x
    bcc .true
    bne .false
    lda LSB + 2, x
    cmp LSB, x
    bcc .true
.false
    lda #0
    bra +
.true
    lda #$ffff
+   inx
    inx
    sta MSB, x
    sta LSB, x
    rts

    +BACKLINK "-", 1
MINUS
    lda LSB + 2, x
    sec
    sbc LSB, x
    sta LSB + 2, x

    lda MSB + 2, x
    sbc MSB, x
    sta MSB + 2, x

    inx
    inx
    rts

product = W ; W and W2: 8 contiguous bytes of 64-bit product

    +BACKLINK "um*", 3
; ( u1 u2 -- ud )  wastes W, W2, y
U_M_STAR
    lda #0
    sta product + 4 ; clear upper half of product; the A=0 also
    sta product + 6 ; establishes the register alias below
    ldy #32
.shift_r
    lsr MSB + 2, x ; divide multiplier by 2
    ror LSB + 2, x
    bcc .rotate_r
    lda product + 4 ; upper half of product += multiplicand
    clc
    adc LSB, x
    sta product + 4
    lda product + 6
    adc MSB, x
.rotate_r
    ror ; rotate partial product (A aliases product+6 between rounds)
    sta product + 6
    ror product + 4
    ror product + 2
    ror product
    dey
    bne .shift_r

    lda product
    sta LSB + 2, x
    lda product + 2
    sta MSB + 2, x
    lda product + 4
    sta LSB, x
    lda product + 6
    sta MSB, x
    rts

    +BACKLINK "um/mod", 6
UM_DIV_MOD
; ( udlo udhi divisor -- rem quot )
; Wastes W (loop counter + carry bit), W2 (subtract staging)
        N = W
        sec
        lda LSB + 2, x  ; Subtract hi cell of dividend by
        sbc LSB, x      ; divisor to see if there's an overflow condition.
        lda MSB + 2, x
        sbc MSB, x
        bcs .oflo       ; Branch if /0 or overflow.

        lda #33         ; Loop 33x.
        sta N
.loop   rol LSB + 4, x  ; Rotate dividend lo cell left one bit.
        rol MSB + 4, x
        dec N
        beq .end
        rol LSB + 2, x  ; Otherwise rotate dividend hi cell left one bit.
        rol MSB + 2, x
        stz N + 2
        rol N + 2       ; Rotate the bit carried out of above into N+2.

        sec
        lda LSB + 2, x  ; Subtract dividend hi cell minus divisor.
        sbc LSB, x
        sta W2          ; Result temporarily in W2.
        lda MSB + 2, x
        sbc MSB, x
        sta W2 + 2
        lda N + 2       ; Bring in the bit carried out above.
        sbc #0
        bcc .loop

        lda W2          ; If that didn't cause a borrow,
        sta LSB + 2, x  ; make the result from above the
        lda W2 + 2      ; new dividend hi cell
        sta MSB + 2, x
        bcs .loop       ; and then branch up.

.oflo   ; overflow or /0: throw division by zero.
        lda #-10
        jmp throw_a

.end    inx
        inx
        jmp SWAP

    +BACKLINK "m+", 2
M_PLUS ; ( d n -- d )
    stz W3
    lda MSB, x
    bpl +
    dec W3 ; $FFFF: the sign extension of n
+   clc
    lda LSB, x
    adc LSB + 4, x
    sta LSB + 4, x
    lda MSB, x
    adc MSB + 4, x
    sta MSB + 4, x
    lda W3
    adc LSB + 2, x
    sta LSB + 2, x
    lda W3
    adc MSB + 2, x
    sta MSB + 2, x
    inx
    inx
    rts

    +BACKLINK "invert", 6
INVERT
    lda MSB, x
    eor #$ffff
    sta MSB, x
    lda LSB, x
    eor #$ffff
    sta LSB, x
    rts

    +BACKLINK "negate", 6
NEGATE
    jsr INVERT
    jmp ONEPLUS

    +BACKLINK "abs", 3
ABS
    lda MSB, x
    bmi NEGATE
    rts

DABS_STAR           ; ( n1 n2 -- ud1 )
    lda MSB, x      ;   ud1 = abs(n1) * abs(n2)
    eor MSB + 2, x  ;   with the final sign in A's bit 15 (and the N flag)
    pha
    jsr ABS
    inx
    inx
    jsr ABS
    dex
    dex
    jsr U_M_STAR
    pla
    rts

    +BACKLINK "*", 1
    jsr DABS_STAR
    inx
    inx
    and #$8000
    bne NEGATE
    rts

    +BACKLINK "dnegate", 7
DNEGATE
    jsr INVERT
    inx
    inx
    jsr INVERT
    dex
    dex
    inc LSB + 2, x
    bne +
    inc MSB + 2, x
    bne +
    inc LSB, x
    bne +
    inc MSB, x
+   rts

    +BACKLINK "m*", 2
    jsr DABS_STAR
    bmi DNEGATE
    rts

    +BACKLINK "0<", 2
ZERO_LESS
    lda MSB, x
    and #$8000
    beq +
    lda #$ffff
+   sta MSB, x
    sta LSB, x
    rts

    +BACKLINK "s>d", 3
S_TO_D
    jsr DUP
    jmp ZERO_LESS

    +BACKLINK "fm/mod", 6
FM_DIV_MOD
    lda MSB, x
    sta DIVISOR_SIGN
    bpl +
    jsr NEGATE
    inx
    inx
    jsr DNEGATE
    dex
    dex
+   lda MSB + 2, x
    bpl +
    jsr TUCK
    jsr PLUS
    jsr SWAP
+   jsr UM_DIV_MOD
DIVISOR_SIGN = * + 1
    lda #$ffff      ; placeholder, patched with the divisor's sign word
    bpl +
    inx
    inx
    jsr NEGATE
    dex
    dex
+   rts

    +BACKLINK "/mod", 4
    lda MSB, x
    sta MSB - 2, x
    lda LSB, x
    sta LSB - 2, x
    inx
    inx
    jsr S_TO_D
    dex
    dex
    jmp FM_DIV_MOD

    ; (ud1 u2 -- urem udquot)
    +BACKLINK "ud/mod", 6
UD_MOD
    lda LSB, x
    sta LSB - 2, x
    sta W3
    lda MSB, x
    sta MSB - 2, x
    sta W3 + 2      ; cache the divisor
    stz LSB, x
    stz MSB, x
    dex
    dex
    jsr UM_DIV_MOD  ; divide the high cell
    lda LSB, x
    pha
    lda MSB, x
    pha             ; cache the high cell of the quotient
    lda W3          ; uncache the divisor
    sta LSB, x
    lda W3 + 2
    sta MSB, x
    jsr UM_DIV_MOD  ; divide the low cell
    dex
    dex
    pla             ; push the high cell of the quotient
    sta MSB, x
    pla
    sta LSB, x
    rts
