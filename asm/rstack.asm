; RSTACK - return-stack pair / drop words.
; durexForth is subroutine-threaded, so the "return stack" is the CPU stack.
; These are modeled on core.asm's >r / r> / r@: each word first pulls its own
; jsr return address off the CPU stack (fixing up the -1 that jsr leaves),
; does its work below that address, then jmp's back through it.  All are exempt
; from tail-call elimination for the same reason those are.

    +BACKLINK "rdrop", 5 | F_NO_TAIL_CALL_ELIMINATION
RDROP ; (R: x -- )
    pla
    sta W
    pla
    sta W+1
    inc W
    bne +
    inc W+1
+   pla                 ; discard the return-stack cell (low then high)
    pla
    jmp (W)

    +BACKLINK "2>r", 3 | F_NO_TAIL_CALL_ELIMINATION
TWO_TO_R ; ( x1 x2 -- ) (R: -- x1 x2)
    pla
    sta W
    pla
    sta W+1
    inc W
    bne +
    inc W+1
+   lda MSB+1,x         ; push x1 (ends up deeper on the return stack)
    pha
    lda LSB+1,x
    pha
    lda MSB,x           ; push x2 (on top)
    pha
    lda LSB,x
    pha
    inx
    inx
    jmp (W)

    +BACKLINK "2r>", 3 | F_NO_TAIL_CALL_ELIMINATION
TWO_R_TO ; ( -- x1 x2 ) (R: x1 x2 -- )
    pla
    sta W
    pla
    sta W+1
    inc W
    bne +
    inc W+1
+   dex
    dex
    pla                 ; x2 low  (top of return stack)
    sta LSB,x
    pla                 ; x2 high
    sta MSB,x
    pla                 ; x1 low
    sta LSB+1,x
    pla                 ; x1 high
    sta MSB+1,x
    jmp (W)

    +BACKLINK "2r@", 3 | F_NO_TAIL_CALL_ELIMINATION
TWO_R_FETCH ; ( -- x1 x2 ) (R: x1 x2 -- x1 x2)
    ; X816: stack-relative addressing (always bank $00, DBR-independent).
    ; CPU stack top-down: ret(1,s/2,s) x2(3,s/4,s) x1(5,s/6,s).
    dex
    dex
    lda 3,s             ; x2 -> top data cell
    sta LSB,x
    lda 4,s
    sta MSB,x
    lda 5,s             ; x1 -> second data cell
    sta LSB+1,x
    lda 6,s
    sta MSB+1,x
    rts
