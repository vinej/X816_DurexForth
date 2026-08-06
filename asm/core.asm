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
    rtl
+   lda #OP_INX
    jsl BANK1 + compile_a
    lda #OP_INX
compile_a ; compile the byte in A (falls through on the tail call)
    jsl BANK1 + PUSHA
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
    rtl

    +BACKLINK "dup", 3
DUP
    dex
    dex
    lda MSB + 2, x
    sta MSB, x
    lda LSB + 2, x
    sta LSB, x
    rtl

    +BACKLINK "?dup", 4
QDUP
    lda MSB, x
    ora LSB, x
    bne DUP
    rtl

    +BACKLINK "nip", 3
NIP ; ( a b -- b )
    jsl BANK1 + SWAP
    inx
    inx
    rtl

    +BACKLINK "over", 4
OVER
    dex
    dex
    lda MSB + 4, x
    sta MSB, x
    lda LSB + 4, x
    sta LSB, x
    rtl

    +BACKLINK "2dup", 4
TWODUP
    jsl BANK1 + OVER
    jmp OVER

    +BACKLINK "1+", 2
ONEPLUS
    inc LSB, x
    bne +
    inc MSB, x
+   rtl

    +BACKLINK "1-", 2
ONEMINUS
    lda LSB, x
    bne +
    dec MSB, x
+   dec LSB, x
    rtl

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
    rtl

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
    rtl

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
    rtl

    +BACKLINK "and", 3
    lda MSB, x
    and MSB + 2, x
    sta MSB + 2, x

    lda LSB, x
    and LSB + 2, x
    sta LSB + 2, x

    inx
    inx
    rtl

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
    rtl

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
    rtl

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
    rtl

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
    rtl

    +BACKLINK "count", 5
COUNT
    jsl BANK1 + DUP
    jsl BANK1 + ONEPLUS
    jsl BANK1 + SWAP
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
    rtl

    +BACKLINK ">", 1
GREATER_THAN
    jsl BANK1 + SWAP
    jmp LESS_THAN

    +BACKLINK "max", 3
MAX
    jsl BANK1 + TWODUP
    jsl BANK1 + LESS_THAN
    jsl BANK1 + ZBRANCH
    !word +
    jsl BANK1 + SWAP
+   inx
    inx
    rtl

    +BACKLINK "min", 3
MIN
    jsl BANK1 + TWODUP
    jsl BANK1 + GREATER_THAN
    jsl BANK1 + ZBRANCH
    !word +
    jsl BANK1 + SWAP
+   inx
    inx
    rtl

    +BACKLINK "tuck", 4
TUCK ; ( x y -- y x y )
    jsl BANK1 + SWAP
    jmp OVER

    ; Exempt from TCE as top of return stack must contain a return address.
    ; Stage C: a return address is THREE bytes (jsl BANK1 + pushes PBR too); pull
    ; PC then bank, resume through jml [W]. A cell on the return stack is
    ; still TWO 16-bit words, high word pushed first.
    +BACKLINK ">r", 2 | F_NO_TAIL_CALL_ELIMINATION
TO_R
    pla
    inc
    sta W
    sep #$20
!as
    pla
    sta W+2
    rep #$20
!al
    lda MSB, x
    pha
    lda LSB, x
    pha
    inx
    inx
    jml [W]

    ; Exempt from TCE as top of return stack must contain a return address.
    +BACKLINK "r>", 2 | F_NO_TAIL_CALL_ELIMINATION
R_TO
    pla
    inc
    sta W
    sep #$20
!as
    pla
    sta W+2
    rep #$20
!al
    dex
    dex
    pla
    sta LSB, x
    pla
    sta MSB, x
    jml [W]

    ; Exempt from TCE as top of return stack must contain a return address.
    ; X816: stack-relative addressing - the cell sits above this word's own
    ; THREE-byte return address (1,s..3,s): low word at 4,s, high at 6,s.
    +BACKLINK "r@", 2 | F_NO_TAIL_CALL_ELIMINATION
R_FETCH
    dex
    dex
    lda 4, s
    sta LSB, x
    lda 6, s
    sta MSB, x
    rtl

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
    rtl

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

    +BACKLINK "sp@", 3
SP_FETCH ; ( -- n ) the data stack pointer
    ; An INDEX, not an address, and it cannot be anything else: a cell
    ; lives in TWO planes (LSB, x and MSB, x), so there is no single
    ; address holding one. X counts two bytes per cell, so
    ; (sp@ - sp0) / -2 is the depth, which is what DEPTH does above.
    sep #$20
!as
    txa
    rep #$20
!al
    and #$ff
    jmp PUSHA

    +BACKLINK "sp0", 3
SP_ZERO ; ( -- n ) the empty-stack value of SP@
    lda #X_INIT
    jmp PUSHA

    +BACKLINK "rp@", 3
RP_FETCH ; ( -- n ) the 65816 hardware stack pointer
    ; The return stack IS the CPU stack here - return addresses are three
    ; bytes and >R lives on it - so this is S, all sixteen bits of it.
    tsc
    jmp PUSHA

    +BACKLINK "within", 6
WITHIN ; ( test low high -- flag )
    jsl BANK1 + OVER
    jsl BANK1 + MINUS
    jsl BANK1 + TO_R
    jsl BANK1 + MINUS
    jsl BANK1 + R_TO
    jmp U_LESS

; ERASE ( start len -- )
    +BACKLINK "erase", 5
    jsl BANK1 + ZERO
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
+   rtl

    +BACKLINK "base", 4
BASE
    +VALUE	BANK1 + _BASE
_BASE
    !word 16, 0

    +BACKLINK "2*", 2
    asl LSB, x
    rol MSB, x
    rtl

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
    rtl

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
    rtl

    +BACKLINK "split", 5 ; ( n -- low16 high16 )
    lda MSB, x
    sta LSB - 2, x
    stz MSB, x
    stz MSB - 2, x
    dex
    dex
    rtl
