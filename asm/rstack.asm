; RSTACK - return-stack pair / drop words.
; durexForth is subroutine-threaded, so the "return stack" is the CPU stack.
; These are modeled on core.asm's >r / r> / r@: each word first pulls its own
; jsr return address off the CPU stack (fixing up the -1 that jsr leaves),
; does its work below that address, then jmp's back through it.  All are exempt
; from tail-call elimination for the same reason those are.
; Stage B: a cell on the return stack is TWO 16-bit pushes, high word first.

    +BACKLINK "rdrop", 5 | F_NO_TAIL_CALL_ELIMINATION
RDROP ; (R: x -- )
    pla
    inc
    sta W
    pla                 ; discard the return-stack cell (low then high word)
    pla
    jmp (W)

    +BACKLINK "2>r", 3 | F_NO_TAIL_CALL_ELIMINATION
TWO_TO_R ; ( x1 x2 -- ) (R: -- x1 x2)
    pla
    inc
    sta W
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
    jmp (W)

    +BACKLINK "2r>", 3 | F_NO_TAIL_CALL_ELIMINATION
TWO_R_TO ; ( -- x1 x2 ) (R: x1 x2 -- )
    pla
    inc
    sta W
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
    jmp (W)

    +BACKLINK "2r@", 3 | F_NO_TAIL_CALL_ELIMINATION
TWO_R_FETCH ; ( -- x1 x2 ) (R: x1 x2 -- x1 x2)
    ; X816: stack-relative addressing (always bank $00, DBR-independent).
    ; CPU stack top-down: ret(1,s) x2(3,s lo / 5,s hi) x1(7,s lo / 9,s hi).
    dex
    dex
    dex
    dex
    lda 3,s             ; x2 -> top data cell
    sta LSB,x
    lda 5,s
    sta MSB,x
    lda 7,s             ; x1 -> second data cell
    sta LSB+2,x
    lda 9,s
    sta MSB+2,x
    rts

; Return-ADDRESS juggling, 16-bit. >R/R> move whole 32-bit cells, but a
; real return address on the CPU stack is one 16-bit word - the stage-A
; idiom "r> ... >r ; " (LITS, DOES>, (?DO), (+LOOP)) needs these instead,
; or the rts after them consumes half a cell and executes the other half.
; RW> zero-... no: BANK1-extends (the address is in this bank, and its
; consumers do C@/+ arithmetic on it as a flat cell).

    +BACKLINK "rw>", 3 | F_NO_TAIL_CALL_ELIMINATION
RW_FROM ; ( -- w ) (R: w -- ) pull the caller's next R WORD
    pla
    inc
    sta W
    dex
    dex
    pla
    sta LSB,x
    lda #(BANK1 >> 16)
    sta MSB,x
    jmp (W)

    +BACKLINK "w>r", 3 | F_NO_TAIL_CALL_ELIMINATION
W_TO_R ; ( w -- ) (R: -- w ) push the cell's low word to R
    pla
    inc
    sta W
    lda LSB,x
    pha
    inx
    inx
    jmp (W)
