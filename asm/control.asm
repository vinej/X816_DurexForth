; IF THEN BEGIN WHILE REPEAT BRANCH 0BRANCH UNLOOP EXIT
;
; Stage B: compiled control flow keeps its stage-A shape - branch operands
; in the instruction stream are 16-bit IN-BANK addresses (the dictionary
; stays inside bank $01, DUREXFORTH.md 2.3). So forward references compile
; a 2-byte placeholder with WCOMMA and are patched with W_STORE, while the
; general , and ! move whole 32-bit cells. HERE pushes a flat address; its
; low word is the in-bank pointer these operands want.

    +BACKLINK "if", 2 | F_IMMEDIATE
    jsr LITXT
    !word ZBRANCH
    jsr COMPILE_COMMA
    jsr HERE
    jsr ZERO
    jmp WCOMMA

    +BACKLINK "then", 4 | F_IMMEDIATE
    jsr HERE
    jsr SWAP
    jmp W_STORE

    +BACKLINK "begin", 5 | F_IMMEDIATE
    jmp HERE

    +BACKLINK "while", 5 | F_IMMEDIATE
    jsr LITXT
    !word ZBRANCH
    jsr COMPILE_COMMA
    jsr HERE
    jsr ZERO
    jsr WCOMMA
    jmp SWAP

COMPILE_JMP
    jsr LITC
    !byte OP_JMP
    jmp CCOMMA

    +BACKLINK "repeat", 6 | F_IMMEDIATE
    jsr COMPILE_JMP
    jsr WCOMMA
    jsr HERE
    jsr SWAP
    jmp W_STORE

    +BACKLINK "branch", 6
BRANCH
    pla
    inc
    sta W ; the 2-byte operand's own address (in this bank)
    lda (W)
    sta + + 1
+   jmp PLACEHOLDER_ADDRESS ; replaced with branch destination

    +BACKLINK "0branch", 7
ZBRANCH
    inx
    inx
    lda LSB - 2, x
    ora MSB - 2, x
    beq BRANCH

    ; skip the 2-byte operand
    pla
    clc
    adc #2
    pha
    rts

    ; Exempt from TCE as top of return stack must contain a return address.
    +BACKLINK "unloop",	6 | F_NO_TAIL_CALL_ELIMINATION
    jsr R_TO
    jsr R_TO
    jsr R_TO
    inx
    inx
    inx
    inx
    jsr TO_R
    rts

    +BACKLINK "exit", 4 | F_IMMEDIATE
EXIT
    lda last_word_no_tail_call_elimination
    bne +
    ; do tail call elimination: instead of adding a final rts,
    ; replace the last jsr with a jmp.
    lda HERE_PTR
    sec
    sbc #3
    sta .instr_ptr
    sep #$20
!as
    lda #OP_JMP
.instr_ptr = * + 1
    sta PLACEHOLDER_ADDRESS ; operand replaced with the jsr's address
    rep #$20
!al
    rts
+
    lda #OP_RTS
    jmp compile_a
