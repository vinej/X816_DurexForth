; MOVE
;
; Stage B: pointers are flat 24-bit and lengths 32-bit, so the cc65
; page-stepping byte loops are replaced by [W]-long copies. Byte-at-a-time
; with a sep/rep pair per byte - correct first; an MVN fast path for
; in-bank copies is a later optimization, and MVN's own four silent traps
; are already on record (X816_core, x816-mvn-block-move).

SRC = W
DST = W2
LEN = W3

cmove_getparams: ; pop ( src dst u ) into SRC/DST/LEN, X dropped by 6
	lda LSB, x
	sta LEN
	lda MSB, x
	sta LEN + 2
	lda LSB + 2, x
	sta DST
	lda MSB + 2, x
	sta DST + 2
	lda LSB + 4, x
	sta SRC
	lda MSB + 4, x
	sta SRC + 2
	sep #$20
!as
	txa
	clc
	adc #6
	tax
	rep #$20
!al
	rtl

    +BACKLINK "cmove>", 6
CMOVE_BACK ; ( src dst u -- ) copy u bytes, high addresses first (overlap-safe up)
	jsl BANK1 + cmove_getparams
	; point SRC/DST one past their last byte
	lda SRC
	clc
	adc LEN
	sta SRC
	lda SRC + 2
	adc LEN + 2
	sta SRC + 2
	lda DST
	clc
	adc LEN
	sta DST
	lda DST + 2
	adc LEN + 2
	sta DST + 2
-	lda LEN
	ora LEN + 2
	beq cmove_done
	; step both pointers down, then copy
	lda SRC
	bne +
	dec SRC + 2
+	dec SRC
	lda DST
	bne +
	dec DST + 2
+	dec DST
	sep #$20
!as
	lda [SRC]
	sta [DST]
	rep #$20
!al
	lda LEN
	bne +
	dec LEN + 2
+	dec LEN
	bra -

    +BACKLINK "cmove", 5
CMOVE ; ( src dst u -- ) copy u bytes, low addresses first
	jsl BANK1 + cmove_getparams
-	lda LEN
	ora LEN + 2
	beq cmove_done
	sep #$20
!as
	lda [SRC]
	sta [DST]
	rep #$20
!al
	inc SRC
	bne +
	inc SRC + 2
+	inc DST
	bne +
	inc DST + 2
+	lda LEN
	bne +
	dec LEN + 2
+	dec LEN
	bra -

cmove_done
	rtl

    +BACKLINK "move", 4
MOVE
    jsl BANK1 + TO_R
    jsl BANK1 + TWODUP
    jsl BANK1 + U_LESS
    jsl BANK1 + R_TO
    jsl BANK1 + SWAP
    jsl BANK1 + ZBRANCH
    !word .br
    jmp CMOVE_BACK
.br = *
    jmp CMOVE
