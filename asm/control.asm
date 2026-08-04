; IF THEN BEGIN WHILE REPEAT BRANCH 0BRANCH UNLOOP EXIT
;
; Stage B: compiled control flow keeps its stage-A shape - branch operands
; in the instruction stream are 16-bit IN-BANK addresses (the dictionary
; stays inside bank $01, DUREXFORTH.md 2.3). So forward references compile
; a 2-byte placeholder with WCOMMA and are patched with W_STORE, while the
; general , and ! move whole 32-bit cells. HERE pushes a flat address; its
; low word is the in-bank pointer these operands want.

    +BACKLINK "if", 2 | F_IMMEDIATE
    jsl BANK1 + LITXT
    !word ZBRANCH
    jsl BANK1 + COMPILE_COMMA
    jsl BANK1 + HERE
    jsl BANK1 + ZERO
    jmp WCOMMA

    +BACKLINK "then", 4 | F_IMMEDIATE
    jsl BANK1 + HERE
    jsl BANK1 + SWAP
    jmp W_STORE

    +BACKLINK "begin", 5 | F_IMMEDIATE
    jmp HERE

    +BACKLINK "while", 5 | F_IMMEDIATE
    jsl BANK1 + LITXT
    !word ZBRANCH
    jsl BANK1 + COMPILE_COMMA
    jsl BANK1 + HERE
    jsl BANK1 + ZERO
    jsl BANK1 + WCOMMA
    jmp SWAP

COMPILE_JMP
    jsl BANK1 + LITC
    !byte OP_JMP
    jmp CCOMMA

    +BACKLINK "repeat", 6 | F_IMMEDIATE
    jsl BANK1 + COMPILE_JMP
    jsl BANK1 + WCOMMA
    jsl BANK1 + HERE
    jsl BANK1 + SWAP
    jmp W_STORE

    +BACKLINK "branch", 6
BRANCH
    ; The operand is 16-bit; the target bank is the one the return
    ; address carries (a definition never straddles a bank).
    pla
    inc
    sta W
    sep #$20
!as
    pla
    sta W+2
    rep #$20
!al
    lda [W] ; long read: the operand lives in the word's own bank
    sta W
    jml [W]

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
    rtl

    ; Exempt from TCE as top of return stack must contain a return address.
    ; The return address is a 3-byte word (rl>/l>r); the loop's i and
    ; limit are full cells.
    +BACKLINK "unloop",	6 | F_NO_TAIL_CALL_ELIMINATION
    jsl BANK1 + RL_FROM
    jsl BANK1 + R_TO
    jsl BANK1 + R_TO
    inx
    inx
    inx
    inx
    jsl BANK1 + L_TO_R
    rtl

    +BACKLINK "exit", 4 | F_IMMEDIATE
EXIT
    lda last_word_no_tail_call_elimination
    bne +
    ; tail call elimination: the last call is a 4-byte `jsl BANK1 + xt`; patch
    ; its opcode to jml ($5C) - same length, same 24-bit operand.
    jsl BANK1 + here_to_w
    lda W
    sec
    sbc #4
    sta W
    sep #$20
!as
    lda #$5c ; jml
    sta [W]
    rep #$20
!al
    rtl
+
    lda #$6b ; rtl
    jmp compile_a
