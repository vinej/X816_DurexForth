; QUIT EXECUTE NOTFOUND ' FIND FIND-NAME >XT PARSE-NAME WORD EVALUATE /STRING
; DOWORDS
;
; Stage B: the input system's pointers (TIB_PTR, evaluate strings, W3 in
; the number parser) are 16-bit BANK-1-IMPLICIT - every input buffer lives
; in this bank (TIB, the dictionary, golden RAM), so (dp) reads through
; DBR do the right thing and SOURCE pushes BANK1+TIB_PTR. The dictionary
; walk in FIND_NAME is byte logic through and through, so it runs inside
; one sep #$20 window with the stage-A code shape intact.

brk_handler
    ; brk instructions (and NMI) abort back to the interpreter. On X816 this
    ; gets installed in the kernel's KIRQ_BRK/KIRQ_NMI slots via IRQ_SET in
    ; the platform-hooks phase; the X16 NMINV/CBINV vector stores are gone
    ; ($316-$319 is inside the X816 stack region, and there is no KERNAL).
    lda #-28 ; user interrupt
    jsr throw_a

quit_reset
    ; goes here from QUIT and program start
    txa             ; preserve Forth stack pointer across reset
    pha

    lda #0
    sta _EXCEPTION_HANDLER
    sta STATE
    sta TIB_SIZE
    sta TO_IN_W
    sta SOURCE_ID_LSB
    sep #$20
!as
    sta SOURCE_ID_MSB
    sta SAVE_INPUT_STACK_DEPTH
    rep #$20
!al
    lda #TIB
    sta TIB_PTR

; X816: close every kernel file handle. QUIT lands here after aborts that
; bypassed the include unwinding - and after a boot-time include chain
; that never returns (base.fs's `start @ execute`). Handles the kernel
; never opened refuse with KERR_BADARG, which kern_fs_close ignores.
close_all_logical_files:
    lda #8
-   pha
    jsr kern_fs_close
    pla
    dec
    bne -

    pla
    tax
    rts

    +BACKLINK "quit", 4
QUIT
    jsr quit_reset

    ; resets the return stack. 16-bit txs: an 8-bit txs in native mode
    ; zeroes SH and would put the return stack in the direct page.
    txa
    rep #$10
!rl
    ldx #RSTACK_TOP
    txs
    sep #$10
!rs
    tax

interpret_and_close
    lda #interpret_loop
    jsr pushbank1
    jsr CATCH
    jsr CLOSE_INPUT_SOURCE
    jmp THROW

interpret_loop
    jsr REFILL
    inx
    inx
    lda MSB-2,x
    beq .refill_failed
    jsr interpret_tib
    jmp interpret_loop
.refill_failed
    rts

interpret_tib
    jsr INTERPRET
    cpx #X_INIT+1
    bpl .throw_stack_underflow
    lda TO_IN_W
    cmp TIB_SIZE
    bne interpret_tib

    ; X816: the dictionary-overflow check runs for EVERY source, and throws
    ; both when the gap shrinks below a page and when the pointers have
    ; already crossed.
    lda LATEST_PTR
    sec
    sbc HERE_PTR
    bcc .throw_dictionary_overflow
    and #$ff00
    beq .throw_dictionary_overflow

    ; 0 - keyboard, -1 evaluate, else file
    lda SOURCE_ID_LSB
    beq +
    rts
+   lda STATE
    bne +
    lda #'o'
    jsr PUTCHR
    lda #'k'
    jsr PUTCHR
    lda #$d
    jmp PUTCHR
+   rts

.throw_stack_underflow
    lda #-4
    jmp throw_a
.throw_dictionary_overflow
    lda #-8
    ; fall through
throw_a
    ; sign-extend the (always negative) code to a full cell - a bank
    ; byte in Y cannot carry $FFFF
    dex
    dex
    sta LSB,x
    lda #$ffff
    sta MSB,x
    jmp THROW

    +BACKLINK "execute", 7
EXECUTE
    lda LSB, x
    sta W
    inx
    inx
    jmp (W)

INTERPRET
    jsr PARSE_NAME

    lda LSB,x
    bne +
    inx
    inx
    inx
    inx
    rts
+
    jsr TWODUP
    jsr FIND_NAME ; ( caddr u 0 | caddr u nt )
    lda MSB, x
    bne .found_word

    inx
    inx
    jsr READ_NUMBER
    beq .was_number

    ; Not a word, not a number: through the hookable not-found vector
    ; ( caddr u ) - default = the error below.  A handler (e.g. the FLOAT
    ; module's float-literal parser) either consumes caddr/u and returns
    ; like a word, or chains to NOTFOUND.  Get the cell with 'NOTFOUND.
    ; Like the number path above, suppress tail-call elimination when the
    ; handler compiles an inline literal (jsr + data bytes).
    lda STATE
    sta curr_word_no_tail_call_elimination

    ; "xxx" string literals: an undefined token starting with '"' goes
    ; through its own vector ( caddr u ), so it composes with 'NOTFOUND
    ; hooks (FLOAT literals). Default = the notfound error; base.fs
    ; installs the (quote) handler. Defined words still win: this path
    ; only runs after FIND_NAME failed.
    lda LSB+2, x
    sta W
    lda MSB+2, x
    sta W + 2
    sep #$20
!as
    lda [W]
    rep #$20
!al
    and #$ff
    cmp #$22 ; '"'
    ; X816: jmp (abs) fetches its pointer from bank $00, but these vector
    ; cells live in the program bank - copy through W2 (dp) and jmp (W2).
    bne +
    lda QUOTE_VEC
    sta W2
    jmp (W2)
+   lda NOTFOUND_VEC
    sta W2
    jmp (W2)

    ; yep, it's a number...
.was_number
    lda STATE ; are we compiling?
    bne +
    rts
+   ; yes, compile the number
    sta curr_word_no_tail_call_elimination
    jmp LITERAL

.found_word
    ; OK, we found a word... ( caddr u nt -- nt nt )
    lda MSB, x
    pha
    lda LSB, x
    inx
    inx
    sta LSB, x
    sta LSB+2, x
    pla
    sta MSB, x
    sta MSB+2, x
    jsr TO_XT
    jsr SWAP
    jsr GET_IMMED ; ( xt 1 | xt -1 )
    inx
    inx

    lda curr_word_no_tail_call_elimination
    sta last_word_no_tail_call_elimination
FOUND_WORD_WITH_NO_TCE = * + 1
    lda #0
    sta curr_word_no_tail_call_elimination

    ; Executes the word if it is immediate, or interpreting.
    lda MSB-2, x
    and STATE
    bne +
    jmp EXECUTE

    ; OK, this word should be compiled...
+   jmp COMPILE_COMMA

    +BACKLINK "notfound",8
print_word_not_found_error ; ( caddr u -- )
    jsr RVS
    jsr TYPE
    lda #'?'
    jsr PUTCHR
    lda #-13 ; undefined word
    jmp throw_a

NOTFOUND_VEC ; interpreter not-found hook (RAM cell, see INTERPRET)
    !word print_word_not_found_error

    +BACKLINK "'notfound", 9
    lda #NOTFOUND_VEC
    jmp pushbank1

QUOTE_VEC ; "xxx" string-literal hook (RAM cell, see INTERPRET)
    !word print_word_not_found_error

    +BACKLINK "'quote", 6
    lda #QUOTE_VEC
    jmp pushbank1

    +BACKLINK "'", 1
    jsr PARSE_NAME
    jsr TWODUP
    jsr FIND_NAME ; ( addr u nt|0 )
    inx
    inx
    lda MSB-2,x
    beq print_word_not_found_error
    inx
    inx
    sta MSB,x
    lda LSB-4,x
    sta LSB,x
    jmp TO_XT

    +BACKLINK "find", 4
FIND ; ( xt -1 | xt 1 | caddr 0 )
    jsr DUP
    jsr TO_R
    jsr COUNT
    jsr FIND_NAME
    lda MSB, x
    beq +
    jsr DUP
    jsr TO_XT
    jsr SWAP
    jsr GET_IMMED
    jsr R_TO
    inx
    inx
    rts
+   inx
    inx
    jsr R_TO
    jmp ZERO

FIND_BUFFER = $480 ; X16 golden RAM
FIND_BUFFER_SIZE = 31

    +BACKLINK "find-name", 9
FIND_NAME ; ( caddr u -- nt | 0 )
    inx
    inx
    lda LSB-2,x ; u
    beq .find_failed
    cmp #FIND_BUFFER_SIZE+1
    bcs .find_failed

    ; copy the (lowercased) name into FIND_BUFFER
    sep #$20
!as
    sta .findlen + 1
    rep #$20
!al
    lda LSB,x
    sta W2
    lda MSB,x
    sta W2 + 2

    sep #$20
!as
    lda LSB-2,x
    tay
    dey
-   lda [W2],y
    jsr CHAR_TO_LOWERCASE
    sta FIND_BUFFER,y
    dey
    bpl -

    ; The walk is stage-A byte logic verbatim: W is a 16-bit little-endian
    ; pointer in the direct page, so 8-bit adds with an inc W+1 carry work
    ; exactly as they did on the 6502.
    lda LATEST_PTR
    sta W
    lda LATEST_PTR+1
    sta W + 1
    ldy #0
    lda (W), y ; get string length of dictionary word
.examine_word
    and #STRLEN_MASK
.findlen
    cmp #$ff ; overwritten
    beq .string_compare

.string_compare_failed
    ; no match, advance the dp
    clc
    adc #3
    adc W
    sta W
    bcc +
    inc W + 1
+   lda (W), y
    ; Is word null? If not, examine it.
    bne .examine_word

    ; It is null - give up.
    rep #$20
!al
.find_failed
    inx
    inx
    jmp ZERO

; the compare paths below are entered FROM the 8-bit walk - the assembler
; must agree (a 16-bit immediate here runs its third byte as BRK)
!as
.string_compare
    ; equal strlen, now compare strings...
    tay
-   lda (W), y
    cmp FIND_BUFFER - 1, y
    bne .word_not_equal
    dey
    bne -

    ; word is equal!
    ; return address to dictionary word
    ldy #0
    lda (W), y
    ; Immediate words are exempt from TCE because custom compile-time behavior.
    ; (E.g. DROP compiles inx instead of jsr DROP.)
    and #F_NO_TAIL_CALL_ELIMINATION | F_IMMEDIATE
    sta FOUND_WORD_WITH_NO_TCE
    rep #$20
!al
    lda W
    sta LSB, x
    lda #(BANK1 >> 16)
    sta MSB, x
    rts

!as
.word_not_equal
    ldy #0
    lda (W), y
    and #STRLEN_MASK
    jmp .string_compare_failed
!al

GET_IMMED ; ( nt -- 1 | -1 )
    lda LSB, x
    sta W
    sep #$20
!as
    lda (W) ; a contains string length + mask
    rep #$20
!al
    and #F_IMMEDIATE
    beq .not_immed
    lda #1
    sta LSB, x
    stz MSB, x
    rts

.not_immed
    lda #$ffff
    sta LSB, x
    sta MSB, x
    rts

    +BACKLINK ">xt", 3
TO_XT
    lda LSB, x
    sta W
    sep #$20
!as
    lda (W) ; a contains string length + mask
    rep #$20
!al
    and #STRLEN_MASK
    sec ; the +1 for the length byte rides in on the carry
    adc W
    sta W
    lda (W) ; the 2-byte xt
    sta LSB, x
    lda #(BANK1 >> 16)
    sta MSB, x
    rts

IS_SPACE ; ( c -- f )
    lda LSB,x
    cmp #' ' | $80
    beq .is_space
    cmp #' ' + 1
    bcc .is_space
    lda #0
    bra +
.is_space
    lda #1
+   sta LSB,x
    stz MSB,x
    rts

IS_NOT_SPACE ; ( c -- f )
    jsr IS_SPACE
    jmp ZEQU

XT_SKIP ; ( addr n xt -- addr n )
    ; skip all chars satisfying xt
    jsr TO_R
-   jsr DUP
    jsr ZBRANCH
    !word .done
    jsr OVER
    jsr FETCHBYTE
    jsr R_FETCH
    jsr EXECUTE
    jsr ZBRANCH
    !word .done
    jsr ONE
    jsr SLASH_STRING
    jmp -
.done
    jsr R_TO
    inx
    inx
    rts

    +BACKLINK "parse-name", 10
PARSE_NAME ; ( name -- addr u )
    jsr SOURCE
    jsr TO_IN
    jsr FETCH
    jsr SLASH_STRING
    jsr LITXT
    !word IS_SPACE
    jsr XT_SKIP
    jsr OVER
    jsr TO_R
    jsr LITXT
    !word IS_NOT_SPACE
    jsr XT_SKIP
    jsr TWODUP
    jsr ONE
    jsr MIN
    jsr PLUS
    jsr SOURCE
    inx
    inx
    jsr MINUS
    jsr TO_IN
    jsr STORE
    inx
    inx
    jsr R_TO
    jsr TUCK
    jmp MINUS

; WORD ( delim -- strptr )
    +BACKLINK "word", 4
WORD
    ; reset transient string length
    jsr ZERO
    jsr HERE
    jsr STOREBYTE

.skip_delimiters
    jsr .get_char_from_tib
    beq .reached_word_end
    jsr .is_delim
    beq .skip_delimiters

.append_char
    ldy #0
    jsr pushya

    ; increment string length counter
    jsr HERE
    jsr FETCHBYTE
    jsr ONEPLUS
    jsr HERE
    jsr STOREBYTE

    ; write character to string
    jsr HERE
    jsr HERE
    jsr FETCHBYTE
    jsr PLUS
    jsr STOREBYTE

    ; get next character from TIB
    jsr .get_char_from_tib
    beq .reached_word_end
    jsr .is_delim
    bne .append_char

.reached_word_end
    inx
    inx
    jmp HERE

.is_delim
    ; a == delim?
    cmp LSB,x
    beq + ; yes

    ; delim == space?
    ldy LSB,x
    cpy #K_SPACE
    bne + ; no

    ; compare with nonbreaking space, too
    cmp #K_SPACE | $80
+   rts

.get_char_from_tib
    lda TO_IN_W
    cmp TIB_SIZE
    bne +
    lda #0
    rts
+   lda TIB_PTR
    clc
    adc TO_IN_W
    sta W
    sep #$20
!as
    lda (W)
    rep #$20
!al
    and #$ff
    inc TO_IN_W
    rts

    +BACKLINK "evaluate", 8
    jsr PUSH_INPUT_SOURCE
    lda LSB + 2, x
    sta EVALUATE_STRING_PTR
    lda LSB, x
    sta EVALUATE_STRING_SIZE
    inx
    inx
    inx
    inx

    lda #$ffff
    sta SOURCE_ID_LSB
    sep #$20
!as
    lda #$ff
    sta SOURCE_ID_MSB
    rep #$20
!al

    jmp interpret_and_close

    +BACKLINK "/string", 7
SLASH_STRING ; ( addr u n -- addr u )
    jsr DUP
    jsr TO_R
    jsr MINUS
    jsr SWAP
    jsr R_TO
    jsr PLUS
    jmp SWAP

; (entered and left in 8-bit A mode, part of READ_NUMBER's byte phase)
!as
apply_base
    sta _BASE
    dec .chars_to_process
    inc W3
    bne +
    inc W3+1
+   lda (W3)
    rts
!al

; Z = success, NZ = fail
; success: ( caddr u -- number )
; fail: ( caddr u -- caddr u )
READ_NUMBER
    sep #$20
!as
    lda LSB,x
    sta .chars_to_process
    rep #$20
!al
    lda LSB+2,x
    sta W3 ; string pointer, bank 1 implicit

    lda _BASE
    pha

    dex
    dex
    dex
    dex
    stz LSB+2,x ; the 32-bit accumulator
    stz MSB+2,x
    stz MSB,x

    sep #$20
!as
    stz .negate
    lda (W3)
    cmp #"'"
    bne +
    jmp .parse_char
+

    cmp #"#"
    bne .check_decimal
    lda #10
    jsr apply_base

.check_decimal
    cmp #"$"
    bne .check_binary
    lda #16
    jsr apply_base

.check_binary
    cmp #"%"
    bne .check_negate
    lda #2
    jsr apply_base

.check_negate
    cmp #"-"
    bne .loop_entry
    inc .negate
    jmp .prepare_next_char

.next_digit
    ; number *= _BASE (32-bit via UM*; overflow = nonzero high cell)
    rep #$20
!al
    lda _BASE
    sta LSB,x
    stz MSB,x
    jsr U_M_STAR
    lda LSB,x
    ora MSB,x
    bne .parse_failed
    sep #$20
!as
    inc W3
    bne +
    inc W3+1
+   lda (W3)

.loop_entry ; 8-bit A: char decode
    jsr CHAR_TO_LOWERCASE

    clc
    adc #-$30 ; ascii 0-9 -> 0-9

    cmp #10 ; within 0-9?
    bcc +

    clc
    adc #-$27 ; ascii a-f (a=$61) -> 10-15

    cmp #10
    bcc .parse_failed_sep

+   cmp _BASE
    bcs .parse_failed_sep

    ; accumulator += digit, 32-bit
    rep #$20
!al
    and #$ff
    clc
    adc LSB+2,x
    sta LSB+2,x
    bcc +
    inc MSB+2,x
    beq .parse_failed ; carried out of 32 bits
+   sep #$20
!as
.prepare_next_char
    dec .chars_to_process
    bne .next_digit

.parse_done
    rep #$20
!al
    pla
    sta _BASE

    ; the accumulator becomes the result, over caddr's slot
    lda LSB+2,x
    sta LSB+6,x
    lda MSB+2,x
    sta MSB+6,x
    inx
    inx
    inx
    inx
    inx
    inx
    sep #$20
!as
.negate = * + 1
    lda #0
    rep #$20
!al
    and #$ff
    beq +
    jsr NEGATE
    lda #0 ; Z set: success
+   rts

; 'c' character literal (8-bit A on entry)
!as
.parse_char
    lda .chars_to_process
    cmp #3
    bne .parse_failed_sep
    ldy #2
    lda (W3),y
    cmp #"'"
    bne .parse_failed_sep
    ldy #1
    lda (W3),y
    rep #$20
!al
    and #$ff
    sta LSB+2,x
    stz MSB+2,x
    jmp .parse_done

!as
.parse_failed_sep
    rep #$20
!al
.parse_failed
    pla
    sta _BASE
    inx
    inx
    inx
    inx ; X is never zero here, so Z is clear: fail
    rts

.chars_to_process
    !byte 0

+BACKLINK "dowords", 7 ; ( xt -- )
    ; to be useful, nothing must be left on stack before execute
    ; so that there is no distance between nt and the rest of the stack
    lda LSB,x
    sta .xt
    inx
    inx
    lda LATEST_PTR
    sta .dowords_nametoken

.dowords_lambda
    lda .dowords_nametoken
    sta W
    sep #$20
!as
    lda (W)
    rep #$20
!al
    and #$ff
    bne +
-   rts
+   and #STRLEN_MASK
    pha
    dex
    dex
    lda .dowords_nametoken
    sta LSB, x
    lda #(BANK1 >> 16)
    sta MSB, x
.xt = * + 1
    jsr PLACEHOLDER_ADDRESS
    inx
    inx
    lda MSB-2, x
    ora LSB-2, x
    beq .cancel
    pla
    clc
    adc #3 ; guaranteed carry clear
    adc .dowords_nametoken
    sta .dowords_nametoken
    jmp .dowords_lambda
; using a word here in case the lambda trashes Ws
.dowords_nametoken
    !word 0
.cancel
    pla
    rts
