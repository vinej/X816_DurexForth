; DROP SWAP DUP ?DUP NIP OVER 2DUP 1+ 1- + = 0= AND ! @ C! C@ COUNT < > MAX MIN
; TUCK >R R> R@ BL PICK DEPTH WITHIN ERASE FILL BASE 2* ROT +! SPLIT
;
; Stage B: 32-bit cells in two WORD planes (durexforth.asm header). Every
; word here is entered and left with M=0/X=1. The stage-A byte tricks that
; used Y as a temporary are gone - Y is 8-bit, so word swaps go through
; pha/pla instead. @/! and friends dereference [W] long: cells carry flat
; 24-bit addresses, and a cell in memory is 4 bytes little-endian.

    +BACKLINK "drop", 4 | F_IMMEDIATE
DROP
    lda STATE
    bne +
    inx
    inx
    rts
+   lda #OP_INX
    jsr compile_a
    lda #OP_INX
compile_a ; compile the byte in A (falls through on the tail call)
    jsr PUSHA
    jmp CCOMMA

    +BACKLINK "swap", 4
SWAP
    lda MSB, x
    pha
    lda MSB + 2, x
    sta MSB, x
    pla
    sta MSB + 2, x

    lda LSB, x
    pha
    lda LSB + 2, x
    sta LSB, x
    pla
    sta LSB + 2, x
    rts

    +BACKLINK "dup", 3
DUP
    dex
    dex
    lda MSB + 2, x
    sta MSB, x
    lda LSB + 2, x
    sta LSB, x
    rts

    +BACKLINK "?dup", 4
QDUP
    lda MSB, x
    ora LSB, x
    bne DUP
    rts

    +BACKLINK "nip", 3
NIP ; ( a b -- b )
    jsr SWAP
    inx
    inx
    rts

    +BACKLINK "over", 4
OVER
    dex
    dex
    lda MSB + 4, x
    sta MSB, x
    lda LSB + 4, x
    sta LSB, x
    rts

    +BACKLINK "2dup", 4
TWODUP
    jsr OVER
    jmp OVER

    +BACKLINK "1+", 2
ONEPLUS
    inc LSB, x
    bne +
    inc MSB, x
+   rts

    +BACKLINK "1-", 2
ONEMINUS
    lda LSB, x
    bne +
    dec MSB, x
+   dec LSB, x
    rts

    +BACKLINK "+", 1
PLUS
    lda LSB, x
    clc
    adc LSB + 2, x
    sta LSB + 2, x

    lda MSB, x
    adc MSB + 2, x
    sta MSB + 2, x

    inx
    inx
    rts

    +BACKLINK "=", 1
EQUAL
    lda LSB, x
    cmp LSB + 2, x
    bne +
    lda MSB, x
    cmp MSB + 2, x
    bne +
    lda #$ffff
    bra ++
+   lda #0
++  inx
    inx
    sta MSB, x
    sta LSB, x
    rts

; 0=
    +BACKLINK "0=", 2
ZEQU
    lda LSB, x
    ora MSB, x
    beq +
    lda #0
    bra ++
+   lda #$ffff
++  sta MSB, x
    sta LSB, x
    rts

    +BACKLINK "and", 3
    lda MSB, x
    and MSB + 2, x
    sta MSB + 2, x

    lda LSB, x
    and LSB + 2, x
    sta LSB + 2, x

    inx
    inx
    rts

    +BACKLINK "!", 1
STORE
    lda LSB, x
    sta W
    lda MSB, x
    sta W + 2 ; bank byte (bits 24-31 land in the pad, [W] ignores them)

    lda LSB + 2, x
    sta [W]
    ldy #2
    lda MSB + 2, x
    sta [W], y

    inx
    inx
    inx
    inx
    rts

    +BACKLINK "@", 1
FETCH
    lda LSB, x
    sta W
    lda MSB, x
    sta W + 2

    lda [W]
    sta LSB, x
    ldy #2
    lda [W], y
    sta MSB, x
    rts

    +BACKLINK "c!", 2
STOREBYTE
    lda LSB, x
    sta W
    lda MSB, x
    sta W + 2
    sep #$20
!as
    lda LSB + 2, x
    sta [W]
    rep #$20
!al
    inx
    inx
    inx
    inx
    rts

    +BACKLINK "c@", 2
FETCHBYTE
    lda LSB, x
    sta W
    lda MSB, x
    sta W + 2
    sep #$20
!as
    lda [W]
    rep #$20
!al
    and #$ff
    sta LSB, x
    stz MSB, x
    rts

    +BACKLINK "count", 5
COUNT
    jsr DUP
    jsr ONEPLUS
    jsr SWAP
    jmp FETCHBYTE

    +BACKLINK "<", 1
LESS_THAN
    sec
    lda LSB + 2, x
    sbc LSB, x
    lda MSB + 2, x
    sbc MSB, x
    bvc +
    eor #$8000
+   bpl +
    lda #$ffff
    bra ++
+   lda #0
++  inx
    inx
    sta LSB, x
    sta MSB, x
    rts

    +BACKLINK ">", 1
GREATER_THAN
    jsr SWAP
    jmp LESS_THAN

    +BACKLINK "max", 3
MAX
    jsr TWODUP
    jsr LESS_THAN
    jsr ZBRANCH
    !word +
    jsr SWAP
+   inx
    inx
    rts

    +BACKLINK "min", 3
MIN
    jsr TWODUP
    jsr GREATER_THAN
    jsr ZBRANCH
    !word +
    jsr SWAP
+   inx
    inx
    rts

    +BACKLINK "tuck", 4
TUCK ; ( x y -- y x y )
    jsr SWAP
    jmp OVER

    ; Exempt from TCE as top of return stack must contain a return address.
    ; M=0 makes the return-address juggling one pull: pla is 16-bit. A cell
    ; on the return stack is TWO words, high word pushed first (so the low
    ; word is at the lower address, like a cell in memory).
    +BACKLINK ">r", 2 | F_NO_TAIL_CALL_ELIMINATION
TO_R
    pla
    inc
    sta W
    lda MSB, x
    pha
    lda LSB, x
    pha
    inx
    inx
    jmp (W)

    ; Exempt from TCE as top of return stack must contain a return address.
    +BACKLINK "r>", 2 | F_NO_TAIL_CALL_ELIMINATION
R_TO
    pla
    inc
    sta W
    dex
    dex
    pla
    sta LSB, x
    pla
    sta MSB, x
    jmp (W)

    ; Exempt from TCE as top of return stack must contain a return address.
    ; X816: stack-relative addressing - the cell sits above this word's own
    ; return address (1,s..2,s): low word at 3,s, high word at 5,s.
    +BACKLINK "r@", 2 | F_NO_TAIL_CALL_ELIMINATION
R_FETCH
    dex
    dex
    lda 3, s
    sta LSB, x
    lda 5, s
    sta MSB, x
    rts

    +BACKLINK "bl", 2
BL
    +VALUE	K_SPACE

    +BACKLINK "pick", 4
    ; ( xu .. x0 u -- xu .. x0 xu ) - replace u (the top slot) with xu.
    stx W3
    lda LSB, x
    asl ; u * 2 = plane offset
    sep #$20
!as
    sta W
    txa
    clc
    adc W
    tax ; X indexes u's slot as if the stack topped at xu's cell
    rep #$20
!al
    lda LSB + 2, x
    pha
    lda MSB + 2, x
    ldx W3
    sta MSB, x
    pla
    sta LSB, x
    rts

    +BACKLINK "depth", 5
    ; depth = (X_INIT - X) / 2 - X steps by two per cell now
    sep #$20
!as
    txa
    eor #$ff
    clc
    adc #X_INIT + 1
    lsr
    rep #$20
!al
    and #$ff
    jmp PUSHA

    +BACKLINK "within", 6
WITHIN ; ( test low high -- flag )
    jsr OVER
    jsr MINUS
    jsr TO_R
    jsr MINUS
    jsr R_TO
    jmp U_LESS

; ERASE ( start len -- )
    +BACKLINK "erase", 5
    jsr ZERO
    ; falls into FILL

; FILL ( start len char -- )
    +BACKLINK "fill", 4
FILL
    sep #$20
!as
    lda LSB, x ; char
    sta W3
    rep #$20
!al
    lda LSB + 2, x ; len, 32-bit
    sta W2
    lda MSB + 2, x
    sta W2 + 2
    lda LSB + 4, x ; start, flat
    sta W
    lda MSB + 4, x
    sta W + 2
    sep #$20
!as
    txa ; drop all three cells
    clc
    adc #6
    tax
    rep #$20
!al
-   lda W2
    ora W2 + 2
    beq +
    sep #$20
!as
    lda W3
    sta [W]
    rep #$20
!al
    inc W
    bne ++
    inc W + 2
++  lda W2
    bne +++
    dec W2 + 2
+++ dec W2
    bra -
+   rts

    +BACKLINK "base", 4
BASE
    +VALUE	BANK1 + _BASE
_BASE
    !word 16, 0

    +BACKLINK "2*", 2
    asl LSB, x
    rol MSB, x
    rts

    +BACKLINK "rot", 3 ; ( a b c -- b c a )
ROT
    lda MSB + 4, x
    pha
    lda MSB + 2, x
    sta MSB + 4, x
    lda MSB, x
    sta MSB + 2, x
    pla
    sta MSB, x
    lda LSB + 4, x
    pha
    lda LSB + 2, x
    sta LSB + 4, x
    lda LSB, x
    sta LSB + 2, x
    pla
    sta LSB, x
    rts

    +BACKLINK "+!", 2 ; ( num addr -- )
PLUS_STORE
    lda LSB, x
    sta W
    lda MSB, x
    sta W + 2
    clc
    lda [W]
    adc LSB + 2, x
    sta [W]
    ldy #2
    lda [W], y
    adc MSB + 2, x
    sta [W], y
    inx
    inx
    inx
    inx
    rts

    +BACKLINK "split", 5 ; ( n -- low16 high16 )
    lda MSB, x
    sta LSB - 2, x
    stz MSB, x
    stz MSB - 2, x
    dex
    dex
    rts
