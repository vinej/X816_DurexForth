; C, W, , [ ] ; IMMEDIATE STATE LATESTXT : HEADER LIT LITW LITXT LITC
; COMPILE, LITERAL HERE W! DODOES
;
; Stage B: HERE and LATEST are 16-bit IN-BANK pointers (the dictionary
; stays in bank $01, DUREXFORTH.md 2.3) pushed as flat BANK1+ addresses.
; `,` moves a 4-byte cell; `w,` the 2-byte units the code generator deals
; in (jsr targets, branch operands); `c,` single opcodes. Inline literals
; come in three widths: LITC (byte), LITW (word, zero-extended), LIT
; (full cell) - LITERAL picks the smallest that fits. LITXT carries a
; 2-byte in-bank address and pushes it bank-extended.

curr_word_no_tail_call_elimination
    !word 1
last_word_no_tail_call_elimination
    !word 1

    +BACKLINK "c,", 2
CCOMMA
    lda HERE_PTR
    sta W
    sep #$20
!as
    lda LSB, x
    sta (W)
    rep #$20
!al
    inc HERE_PTR
    inx
    inx
    rts

    +BACKLINK "w,", 2
WCOMMA ; ( x -- ) compile the low 16 bits
    lda HERE_PTR
    sta W
    lda LSB, x
    sta (W)
    inc HERE_PTR
    inc HERE_PTR
    inx
    inx
    rts

    +BACKLINK ",", 1
COMMA ; ( x -- ) compile a 4-byte cell
    lda HERE_PTR
    sta W
    lda LSB, x
    sta (W)
    ldy #2
    lda MSB, x
    sta (W), y
    lda HERE_PTR
    clc
    adc #4
    sta HERE_PTR
    inx
    inx
    rts

    +BACKLINK "w!", 2
W_STORE ; ( x addr -- ) store the low 16 bits of x
    lda LSB, x
    sta W
    lda MSB, x
    sta W + 2
    lda LSB + 2, x
    sta [W]
    inx
    inx
    inx
    inx
    rts

; -----

    +BACKLINK "[", 1 | F_IMMEDIATE
LBRAC
    lda #0
    sta STATE
    rts

; -----

    ; Exempt from TCE because `: x [ ] ;` does not compile a jsr.
    +BACKLINK "]", 1 | F_NO_TAIL_CALL_ELIMINATION
RBRAC
    lda #1
    sta STATE
    rts

    +BACKLINK ";", 1 | F_IMMEDIATE
SEMICOLON
    jsr EXIT

    ; Unhides the word.
PENDING_LATEST = * + 1
    lda #0
    beq +
    sta LATEST_PTR
    lda #0
    sta PENDING_LATEST
+

    ; go back to IMMEDIATE mode.
    jmp LBRAC

    +BACKLINK "immediate", 9
    lda LATEST_PTR
    sta W
    sep #$20
!as
    lda (W)
    ora #F_IMMEDIATE
    sta (W)
    rep #$20
!al
    rts

; STATE - Is the interpreter executing code (0) or compiling a word (non-zero)?
    +BACKLINK "state", 5
    +VALUE	BANK1 + STATE
STATE
    !word 0, 0

    +BACKLINK "latestxt", 8
LATEST_XT = * + 3
    +VALUE	BANK1 + 0

    ; Exempt from TCE because `: x ;` does not compile a jsr.
    +BACKLINK ":", 1 | F_NO_TAIL_CALL_ELIMINATION
COLON
    lda LATEST_PTR
    pha

    jsr HEADER ; makes the dictionary entry / header

    ; defer the LATEST update to ;
    lda LATEST_PTR
    sta PENDING_LATEST

    pla
    sta LATEST_PTR

    lda HERE_PTR
    sta LATEST_XT

    jmp RBRAC ; enter compile mode

    +BACKLINK "header", 6
HEADER ; ( "name" -- )
    inc last_word_no_tail_call_elimination

    ; Parse: name address (flat) -> W2, length stays below the two
    ; popped cells at LSB-4.
    jsr PARSE_NAME
    inx
    inx
    lda LSB, x
    sta W2
    lda MSB, x
    sta W2 + 2
    inx
    inx

    ; Abort if empty string.
    lda LSB - 4, x ; length
    bne +
    lda #-16 ; attempt to use zero-length string as a name
    jmp throw_a
+   sep #$20
!as
    sta .putlen + 1
    rep #$20
!al

    ; Move LATEST back over the new entry: len + 1 flag/len byte + 2 xt.
    clc
    adc #3
    sta W3
    lda LATEST_PTR
    sec
    sbc W3
    sta LATEST_PTR
    sta W ; the entry address; (W) stores go through DBR = this bank

    ; Store name length, then the name (lowercased), byte by byte.
    sep #$20
!as
    lda LSB - 4, x
    sta (W)
    ldy #0
-   lda [W2], y
    jsr CHAR_TO_LOWERCASE
    iny
    sta (W), y
.putlen
    cpy #0
    bne -
    rep #$20
!al
    ; Store the xt: one 16-bit store at offset len+1.
    iny
    lda HERE_PTR
    sta (W), y
    rts

    +BACKLINK "lit", 3
LIT ; push the 4-byte inline cell
    dex
    dex
    pla
    sta W
    ldy #1
    lda (W), y
    sta LSB, x
    ldy #3
    lda (W), y
    sta MSB, x
    lda W
    clc
    adc #5
    sta W
    jmp (W)

    +BACKLINK "litw", 4
LITW ; push the 2-byte inline word, zero-extended
    dex
    dex
    pla
    sta W
    ldy #1
    lda (W), y
    sta LSB, x
    stz MSB, x
    lda W
    clc
    adc #3
    sta W
    jmp (W)

LITXT ; push the 2-byte inline in-bank address as a flat BANK1+ cell
    dex
    dex
    pla
    sta W
    ldy #1
    lda (W), y
    sta LSB, x
    lda #(BANK1 >> 16)
    sta MSB, x
    lda W
    clc
    adc #3
    sta W
    jmp (W)

    +BACKLINK "litc", 4
LITC ; push the 1-byte inline literal
    dex
    dex
    pla
    inc
    sta W
    sep #$20
!as
    lda (W)
    rep #$20
!al
    and #$ff
    sta LSB, x
    stz MSB, x
    inc W
    jmp (W)

    +BACKLINK "compile,", 8
COMPILE_COMMA
    lda #OP_JSR
    jsr compile_a
    jmp WCOMMA

    +BACKLINK "literal", 7 | F_IMMEDIATE
LITERAL
    dex
    dex
    lda MSB + 2, x
    bne .cell
    lda LSB + 2, x
    and #$ff00
    bne .word
    lda #LITC
    sta LSB, x
    lda #(BANK1 >> 16)
    sta MSB, x
    jsr COMPILE_COMMA
    jmp CCOMMA ; writes byte
.word
    lda #LITW
    sta LSB, x
    lda #(BANK1 >> 16)
    sta MSB, x
    jsr COMPILE_COMMA
    jmp WCOMMA ; writes word
.cell
    lda #LIT
    sta LSB, x
    lda #(BANK1 >> 16)
    sta MSB, x
    jsr COMPILE_COMMA
    jmp COMMA ; writes the whole cell

; HERE - points to the next free byte of memory. When compiling, compiled
; words go here. The low word of the pushed flat address IS the in-bank
; pointer, patched at HERE_PTR by everything that allots.
    +BACKLINK "here", 4
HERE
HERE_PTR = * + 3
    +VALUE  BANK1 + HERE_POSITION

    +BACKLINK "dodoes", 6

    ; behavior pointer address => W
    pla
    inc
    sta W

    ; push data pointer (flat) to param stack
    dex
    dex
    lda W
    clc
    adc #2
    sta LSB, x
    lda #(BANK1 >> 16)
    sta MSB, x

    lda (W) ; the behavior pointer itself
    sta W2
    jmp (W2)
