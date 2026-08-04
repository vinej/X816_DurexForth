; RSTACK - return-stack pair / drop words.
; durexForth is subroutine-threaded, so the "return stack" is the CPU stack.
; These are modeled on core.asm's >r / r> / r@: each word first pulls its own
; 3-byte jsl return address (PC then bank) off the CPU stack, does its work below
; that address, then resumes through jml [W]. All are exempt from tail-call
; elimination for the same reason those are.
; Stage C: a return address is THREE bytes; a cell on the return stack is
; still TWO 16-bit pushes, high word first.

; Pull the caller's 3-byte return into W (PC+1, bank) - shared prologue.
; Entered by plain same-bank jsr from the words below: that 2-byte frame
; sits ABOVE the 3-byte address being pulled, so this cannot be a macro
; user, it must run inline. ACME macro instead:
!macro PULL_RET {
    pla
    inc
    sta W
    sep #$20
!as
    pla
    sta W+2
    rep #$20
!al
}

    +BACKLINK "rdrop", 5 | F_NO_TAIL_CALL_ELIMINATION
RDROP ; (R: x -- )
    +PULL_RET
    pla                 ; discard the return-stack cell (low then high word)
    pla
    jml [W]

    +BACKLINK "2>r", 3 | F_NO_TAIL_CALL_ELIMINATION
TWO_TO_R ; ( x1 x2 -- ) (R: -- x1 x2)
    +PULL_RET
    lda MSB+2,x         ; push x1 (ends up deeper on the return stack)
    pha
    lda LSB+2,x
    pha
    lda MSB,x           ; push x2 (on top)
    pha
    lda LSB,x
    pha
    inx
    inx
    inx
    inx
    jml [W]

    +BACKLINK "2r>", 3 | F_NO_TAIL_CALL_ELIMINATION
TWO_R_TO ; ( -- x1 x2 ) (R: x1 x2 -- )
    +PULL_RET
    dex
    dex
    dex
    dex
    pla                 ; x2 low word (top of return stack)
    sta LSB,x
    pla                 ; x2 high word
    sta MSB,x
    pla                 ; x1 low word
    sta LSB+2,x
    pla                 ; x1 high word
    sta MSB+2,x
    jml [W]

    +BACKLINK "2r@", 3 | F_NO_TAIL_CALL_ELIMINATION
TWO_R_FETCH ; ( -- x1 x2 ) (R: x1 x2 -- x1 x2)
    ; X816: stack-relative addressing (always bank $00, DBR-independent).
    ; CPU stack top-down: ret(1,s..3,s) x2(4,s lo / 6,s hi) x1(8,s / 10,s).
    dex
    dex
    dex
    dex
    lda 4,s             ; x2 -> top data cell
    sta LSB,x
    lda 6,s
    sta MSB,x
    lda 8,s             ; x1 -> second data cell
    sta LSB+2,x
    lda 10,s
    sta MSB+2,x
    rtl

; Return-ADDRESS juggling, 24-bit. >R/R> move whole 32-bit cells, but a
; real return address on the CPU stack is three bytes - the "r> ... >r ;"
; idiom (LITS, DOES>, (?DO), (+LOOP), UNLOOP) uses these instead. RL>
; delivers the address as a flat cell (the bank rides in bits 16-23).

    +BACKLINK "rl>", 3 | F_NO_TAIL_CALL_ELIMINATION
RL_FROM ; ( -- addr ) (R: ret24 -- ) pull the caller's next return address
    +PULL_RET
    dex
    dex
    pla
    sta LSB,x
    sep #$20
!as
    pla
    sta MSB,x
    stz MSB+1,x
    rep #$20
!al
    jml [W]

    +BACKLINK "l>r", 3 | F_NO_TAIL_CALL_ELIMINATION
L_TO_R ; ( addr -- ) (R: -- ret24 ) push a flat address as a return address
    +PULL_RET
    sep #$20
!as
    lda MSB,x           ; bank byte
    pha
    rep #$20
!al
    lda LSB,x
    pha
    inx
    inx
    jml [W]
