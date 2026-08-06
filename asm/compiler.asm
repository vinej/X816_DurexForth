; C, W, , [ ] ; IMMEDIATE STATE LATESTXT : HEADER LIT LITW LITXT LITC
; COMPILE, LITERAL HERE W! DODOES
;
; Stage B: HERE and LATEST are 16-bit IN-BANK pointers (the dictionary
; stays in bank $01, DUREXFORTH.md 2.3) pushed as flat BANK1+ addresses.
; `,` moves a 4-byte cell; `w,` the 2-byte units the code generator deals
; in (jsl BANK1 + targets, branch operands); `c,` single opcodes. Inline literals
; come in three widths: LITC (byte), LITW (word, zero-extended), LIT
; (full cell) - LITERAL picks the smallest that fits. LITXT carries a
; 2-byte in-bank address and pushes it bank-extended.

curr_word_no_tail_call_elimination
    !word 1
last_word_no_tail_call_elimination
    !word 1

; Stage C: HERE is a 24-bit pointer (low word in HERE_PTR, bank word in
; HERE_BANK - the two immediates of the HERE value). The comma family
; writes through [W] so compilation lands in whatever bank HERE walks.
here_to_w
    lda HERE_PTR
    sta W
    lda HERE_BANK
    sta W+2
    rtl

    +BACKLINK "c,", 2
CCOMMA
    jsl BANK1 + here_to_w
    sep #$20
!as
    lda LSB, x
    sta [W]
    rep #$20
!al
    inc HERE_PTR
    inx
    inx
    rtl

    +BACKLINK "w,", 2
WCOMMA ; ( x -- ) compile the low 16 bits
    jsl BANK1 + here_to_w
    lda LSB, x
    sta [W]
    inc HERE_PTR
    inc HERE_PTR
    inx
    inx
    rtl

    +BACKLINK ",", 1
COMMA ; ( x -- ) compile a 4-byte cell
    jsl BANK1 + here_to_w
    lda LSB, x
    sta [W]
    ldy #2
    lda MSB, x
    sta [W], y
    lda HERE_PTR
    clc
    adc #4
    sta HERE_PTR
    inx
    inx
    rtl

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
    rtl

; -----

    ; The fetch half of W! , and the reason it exists: a CELL IS 32 BITS
    ; here, so `addr @` on a two-byte field takes four bytes and calls
    ; the next field's low half part of this one. That has been the most
    ; expensive class of bug in the whole port - stale 16-bit-cell code
    ; in extras' FIELD:, advanced's ring buffer, advgfx's seed stack,
    ; advsnd's ADPCM tables - and a 16-bit fetch is the missing tool.
    ; C@ zero-extends, so W@ does; SW@ is for a field that was signed
    ; when it was written.
    +BACKLINK "w@", 2
W_FETCH ; ( addr -- u ) fetch 16 bits, zero-extended
    lda LSB, x
    sta W
    lda MSB, x
    sta W + 2
    lda [W]
    sta LSB, x
    stz MSB, x
    rtl

    +BACKLINK "sw@", 3
SW_FETCH ; ( addr -- n ) fetch 16 bits, sign-extended
    lda LSB, x
    sta W
    lda MSB, x
    sta W + 2
    lda [W]
    sta LSB, x         ; STA leaves N from the LDA above
    bpl +
    lda #$ffff
    sta MSB, x
    rtl
+   stz MSB, x
    rtl

    +BACKLINK "[", 1 | F_IMMEDIATE
LBRAC
    lda #0
    sta STATE
    rtl

; -----

    ; Exempt from TCE because `: x [ ] ;` does not compile a jsr.
    +BACKLINK "]", 1 | F_NO_TAIL_CALL_ELIMINATION
RBRAC
    lda #1
    sta STATE
    rtl

    +BACKLINK ";", 1 | F_IMMEDIATE
SEMICOLON
    jsl BANK1 + EXIT

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
    rtl

; STATE - Is the interpreter executing code (0) or compiling a word (non-zero)?
    +BACKLINK "state", 5
    +VALUE	BANK1 + STATE
STATE
    !word 0, 0

    +BACKLINK "latestxt", 8
LATEST_XT = * + 3
LATEST_XT_BANK = * + 8
    +VALUE	BANK1 + 0

    ; Exempt from TCE because `: x ;` does not compile a jsr.
    +BACKLINK ":", 1 | F_NO_TAIL_CALL_ELIMINATION
COLON
    lda LATEST_PTR
    pha

    jsl BANK1 + HEADER ; makes the dictionary entry / header

    ; defer the LATEST update to ;
    lda LATEST_PTR
    sta PENDING_LATEST

    pla
    sta LATEST_PTR

    ; a definition must fit its bank: bump HERE to the next bank when
    ; fewer than 1 KB remain (documented ceiling per definition)
    jsl BANK1 + bank_headroom

    lda HERE_PTR
    sta LATEST_XT
    lda HERE_BANK
    sta LATEST_XT_BANK

    jmp RBRAC ; enter compile mode

; If HERE is within 1 KB of its bank's ceiling, advance to the next
; bank. Bank $01's ceiling is LATEST - the headers keep growing down
; there even after code moves on - and banks $02-$04 run to $FFFF.
; Beyond $04: -8 throw.
bank_headroom
    lda HERE_BANK
    and #$ff
    cmp #1
    bne .bh_high
    lda LATEST_PTR
    sec
    sbc HERE_PTR
    bcc .bh_bump ; crossed (a huge allot): bump rather than wedge
    cmp #$0400
    bcs .bh_done
    bra .bh_bump
.bh_high
    lda HERE_PTR
    cmp #$fc00
    bcc .bh_done
.bh_bump
    lda HERE_BANK
    inc
    and #$ff
    cmp #5
    bcc +
    lda #-8
    jmp throw_a
+   sta HERE_BANK
    stz HERE_PTR
.bh_done
    rtl

    +BACKLINK "header", 6
HEADER ; ( "name" -- )
    inc last_word_no_tail_call_elimination

    ; Parse: name address (flat) -> W2, length stays below the two
    ; popped cells at LSB-4.
    jsl BANK1 + PARSE_NAME
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

    ; Move LATEST back over the new entry: len + 1 flag/len byte + 3 xt.
    clc
    adc #4
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
    jsl BANK1 + CHAR_TO_LOWERCASE
    iny
    sta (W), y
.putlen
    cpy #0
    bne -
    rep #$20
!al
    ; Store the xt: 16-bit low word at len+1, bank byte at len+3.
    iny
    lda HERE_PTR
    sta (W), y
    iny
    iny
    sep #$20
!as
    lda HERE_BANK
    sta (W), y
    rep #$20
!al
    rtl

    +BACKLINK "lit", 3
LIT ; push the 4-byte inline cell
    dex
    dex
    pla
    sta W
    sep #$20
!as
    pla
    sta W+2
    rep #$20
!al
    ldy #1
    lda [W], y
    sta LSB, x
    ldy #3
    lda [W], y
    sta MSB, x
    lda W
    clc
    adc #5
    sta W
    jml [W]

    +BACKLINK "litw", 4
LITW ; push the 2-byte inline word, zero-extended
    dex
    dex
    pla
    sta W
    sep #$20
!as
    pla
    sta W+2
    rep #$20
!al
    ldy #1
    lda [W], y
    sta LSB, x
    stz MSB, x
    lda W
    clc
    adc #3
    sta W
    jml [W]

LITXT ; push the 2-byte inline in-bank address as a flat BANK1+ cell
    dex
    dex
    pla
    sta W
    sep #$20
!as
    pla
    sta W+2
    rep #$20
!al
    ldy #1
    lda [W], y
    sta LSB, x
    lda #(BANK1 >> 16)
    sta MSB, x
    lda W
    clc
    adc #3
    sta W
    jml [W]

    +BACKLINK "litc", 4
LITC ; push the 1-byte inline literal
    dex
    dex
    pla
    inc
    sta W
    sep #$20
!as
    pla
    sta W+2
    lda [W]
    rep #$20
!al
    and #$ff
    sta LSB, x
    stz MSB, x
    inc W
    jml [W]

    +BACKLINK "compile,", 8
COMPILE_COMMA ; ( xt -- ) compile a 4-byte `jsl xt`
    lda #$22 ; jsl
    jsl BANK1 + compile_a
    jsl BANK1 + DUP
    jsl BANK1 + WCOMMA          ; low word
    lda MSB, x          ; bank byte
    and #$ff
    sta LSB, x
    stz MSB, x
    jmp CCOMMA

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
    jsl BANK1 + COMPILE_COMMA
    jmp CCOMMA ; writes byte
.word
    lda #LITW
    sta LSB, x
    lda #(BANK1 >> 16)
    sta MSB, x
    jsl BANK1 + COMPILE_COMMA
    jmp WCOMMA ; writes word
.cell
    lda #LIT
    sta LSB, x
    lda #(BANK1 >> 16)
    sta MSB, x
    jsl BANK1 + COMPILE_COMMA
    jmp COMMA ; writes the whole cell

; HERE - points to the next free byte of memory. When compiling, compiled
; words go here. The low word of the pushed flat address IS the in-bank
; pointer, patched at HERE_PTR by everything that allots.
    +BACKLINK "here", 4
HERE
HERE_PTR = * + 3
HERE_BANK = * + 8
    +VALUE  BANK1 + HERE_POSITION

    +BACKLINK "dodoes", 6

    ; behavior pointer address => W (24-bit: the created word may live in
    ; any code bank)
    pla
    inc
    sta W
    sep #$20
!as
    pla
    sta W+2
    rep #$20
!al

    ; push data pointer (flat: behavior field + 3) to param stack
    dex
    dex
    lda W
    clc
    adc #3
    sta LSB, x
    lda W+2
    and #$ff
    sta MSB, x

    lda [W] ; the behavior pointer, low word
    sta W2
    ldy #2
    sep #$20
!as
    lda [W], y ; its bank
    sta W2+2
    rep #$20
!al
    jml [W2]
