; QUIT EXECUTE NOTFOUND ' FIND FIND-NAME >XT PARSE-NAME WORD EVALUATE /STRING
; DOWORDS

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

    ldx #0
    stx _EXCEPTION_HANDLER
    stx _EXCEPTION_HANDLER+1

    lda #>TIB
    sta TIB_PTR + 1

    stx     STATE
    stx     TIB_SIZE
    stx     TIB_SIZE + 1
    stx     TIB_PTR
    stx     TO_IN_W
    stx     TO_IN_W + 1
    stx     SOURCE_ID_LSB
    stx     SOURCE_ID_MSB
    stx     SAVE_INPUT_STACK_DEPTH

; X816: the CLRCHN/CLOSE-all loop went with the CBM channel model; open
; kernel file handles will be closed here once the FS_* words exist.
close_all_logical_files:
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
    ldy #>interpret_loop
    lda #<interpret_loop
    jsr pushya
    jsr CATCH
    jsr CLOSE_INPUT_SOURCE
    jmp THROW

interpret_loop
    jsr REFILL
    inx
    lda MSB-1,x
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
    lda TO_IN_W + 1
    cmp TIB_SIZE + 1
    bne interpret_tib

    ; 0 - keyboard, -1 evaluate, else file
    lda SOURCE_ID_LSB
    beq +
    rts
+   lda LATEST_LSB
    sec
    sbc HERE_LSB
    lda LATEST_MSB
    sbc HERE_MSB
    beq .throw_dictionary_overflow
    lda STATE
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
    ldy #$ff
    jsr pushya
    jmp THROW

    +BACKLINK "execute", 7
EXECUTE
    lda LSB, x
    sta W
    lda MSB, x
    sta W + 1
    inx
    jmp (W)

INTERPRET
    jsr PARSE_NAME

    lda LSB,x
    bne +
    inx
    inx
    rts
+
    jsr TWODUP
    jsr FIND_NAME ; ( caddr u 0 | caddr u nt )
    lda MSB, x
    bne .found_word

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
    ; ROLLBACK: replace this block with "jmp (NOTFOUND_VEC)" and delete
    ; QUOTE_VEC + 'quote below (plus the (quote) block in base.fs).
    lda LSB+1, x
    sta W
    lda MSB+1, x
    sta W + 1
    ldy #0
    lda (W), y
    cmp #$22 ; '"'
    ; X816: jmp (abs) fetches its pointer from bank $00, but these vector
    ; cells live in the program bank - copy through W2 (dp) and jmp (W2).
    bne +
    lda QUOTE_VEC
    sta W2
    lda QUOTE_VEC+1
    sta W2+1
    jmp (W2)
+   lda NOTFOUND_VEC
    sta W2
    lda NOTFOUND_VEC+1
    sta W2+1
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
    ; OK, we found a word...

    lda MSB, x
    ldy LSB, x
    inx
    sta MSB, x
    sty LSB, x
    sta MSB+1, x
    sty LSB+1, x
    jsr TO_XT
    jsr SWAP
    jsr GET_IMMED ; ( xt 1 | xt -1 )
    inx

    lda curr_word_no_tail_call_elimination
    sta last_word_no_tail_call_elimination
FOUND_WORD_WITH_NO_TCE = * + 1
    lda #0
    sta curr_word_no_tail_call_elimination

    ; Executes the word if it is immediate, or interpreting.
    lda MSB-1, x
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
    dex
    lda #<NOTFOUND_VEC
    sta LSB, x
    lda #>NOTFOUND_VEC
    sta MSB, x
    rts

QUOTE_VEC ; "xxx" string-literal hook (RAM cell, see INTERPRET)
    !word print_word_not_found_error

    +BACKLINK "'quote", 6
    dex
    lda #<QUOTE_VEC
    sta LSB, x
    lda #>QUOTE_VEC
    sta MSB, x
    rts

    +BACKLINK "'", 1
    jsr PARSE_NAME
    jsr TWODUP
    jsr FIND_NAME ; ( addr u nt|0 )
    inx
    lda MSB-1,x
    beq print_word_not_found_error
    inx
    sta MSB,x
    lda LSB-2,x
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
    rts
+   inx
    jsr R_TO
    jmp ZERO

FIND_BUFFER = $480 ; X16 golden RAM
FIND_BUFFER_SIZE = 31

    +BACKLINK "find-name", 9
FIND_NAME ; ( caddr u -- nt | 0 )
    inx
    lda LSB-1,x
    tay
    beq .find_failed
    cmp #FIND_BUFFER_SIZE+1
    bcs .find_failed
    sta .findlen + 1

    lda MSB,x
    sta W2+1
    lda LSB,x
    sta W2

    dey
-   lda (W2),y
    jsr CHAR_TO_LOWERCASE
    sta FIND_BUFFER,y
    dey
    bpl -

    lda LATEST_LSB
    sta W
    lda LATEST_MSB
    sta W + 1
    ; W now contains new dictionary pointer.
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
.find_failed
    inx
    jmp ZERO

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
    lda W
    sta LSB, x
    lda W + 1
    sta MSB, x
    rts

.word_not_equal
    ldy #0
    lda (W), y
    and #STRLEN_MASK
    jmp .string_compare_failed

GET_IMMED ; ( nt -- 1 | -1 )
    lda MSB, x
    sta W + 1
    lda LSB, x
    sta W
    ldy #0

    lda (W), y ; a contains string length + mask
    and #F_IMMEDIATE
    beq .not_immed
    sty MSB, x ; 0
    iny
    sty LSB, x ; 1
    rts

.not_immed
    lda #$ff
    sta LSB, x
    sta MSB, x
    rts

    +BACKLINK ">xt", 3
TO_XT
    lda MSB, x
    sta W + 1
    lda LSB, x
    sta W
    ; W contains pointer to word
    ldy #0
    lda (W), y ; a contains string length + mask
    and #STRLEN_MASK
    clc
    adc #1 ; offset for char + string length
    adc LSB, x
    sta LSB, x
    bcc +
    inc MSB, x
+   jsr FETCH
    rts

IS_SPACE ; ( c -- f )
    ldy #1
    lda LSB,x
    cmp #' ' | 0x80
    beq .is_space
    lda #' '
    cmp LSB,x
    bcs .is_space
    dey
.is_space:
    sty LSB,x
    sty MSB,x
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
    rts

    +BACKLINK "parse-name", 10
PARSE_NAME ; ( name -- addr u )
    jsr SOURCE
    jsr TO_IN
    jsr FETCH
    jsr SLASH_STRING
    jsr LIT
    !word IS_SPACE
    jsr XT_SKIP
    jsr OVER
    jsr TO_R
    jsr LIT
    !word IS_NOT_SPACE
    jsr XT_SKIP
    jsr TWODUP
    jsr ONE
    jsr MIN
    jsr PLUS
    jsr SOURCE
    inx
    jsr MINUS
    jsr TO_IN
    jsr STORE
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
    lda TO_IN_W + 1
    cmp TIB_SIZE + 1
    bne +
    lda #0
    rts
+
    clc
    lda TIB_PTR
    adc TO_IN_W
    sta W
    lda TIB_PTR + 1
    adc TO_IN_W + 1
    sta W + 1
    ldy #0
    lda (W),y

    inc TO_IN_W
    bne +
    inc TO_IN_W + 1
+   rts

    +BACKLINK "evaluate", 8
    jsr PUSH_INPUT_SOURCE
    lda LSB + 1, x
    sta EVALUATE_STRING_PTR_LSB
    lda MSB + 1, x
    sta EVALUATE_STRING_PTR_MSB
    lda LSB, x
    sta EVALUATE_STRING_SIZE_LSB
    lda MSB, x
    sta EVALUATE_STRING_SIZE_MSB
    inx
    inx

    ldy #-1
    sty SOURCE_ID_MSB
    sty SOURCE_ID_LSB

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

apply_base
    sta _BASE
    dec .chars_to_process
    inc W3
    bne +
    inc W3+1
+   lda (W3),y
    rts

; Z = success, NZ = fail
; success: ( caddr u -- number )
; fail: ( caddr u -- caddr u )
READ_NUMBER
    lda LSB,x
    sta .chars_to_process
    lda MSB+1,x
    sta W3+1
    lda LSB+1,x
    sta W3

    lda _BASE
    pha

    ldy #0
    sty .negate
    dex
    dex
    sty LSB+1,x
    sty MSB+1,x
    sty MSB,x

    lda (W3), y
    cmp #"'"
    beq .parse_char

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
    ; number *= _BASE
    lda _BASE
    sta LSB,x
    jsr U_M_STAR
    lda LSB,x
    bne .parse_failed ; overflow!

    inc W3
    bne +
    inc W3+1
+   lda (W3), y

.loop_entry
    jsr CHAR_TO_LOWERCASE

    clc
    adc #-$30 ; ascii 0-9 -> 0-9

    cmp #10 ; within 0-9?
    bcc +

    clc
    adc #-$27 ; ascii a-f (a=$61) -> 10-15

    cmp #10
    bcc .parse_failed

+   cmp _BASE
    bcs .parse_failed

    adc LSB+1,x
    sta LSB+1,x
    bcc .prepare_next_char
    inc MSB+1,x
    beq .parse_failed
.prepare_next_char
    dec .chars_to_process
    bne .next_digit

.parse_done
    pla
    sta _BASE

    lda LSB+1,x
    sta LSB+3,x
    lda MSB+1,x
    sta MSB+3,x
    inx
    inx
    inx
.negate = * + 1
    lda #0
    beq +
    jsr NEGATE
    tya ; clear Z flag
+   rts

.parse_char
    lda .chars_to_process
    cmp #3
    bne .parse_failed
    ldy #2
    lda (W3),y
    cmp #"'"
    bne .parse_failed
    dey
    lda (W3),y
    sta LSB+1,x
    lda #0
    sta MSB+1,x
    jmp .parse_done

.parse_failed
    pla
    sta _BASE
    inx
    inx ; Z flag set
    rts

.chars_to_process
    !byte 0

+BACKLINK "dowords", 7 ; ( xt -- )
    ; to be useful, nothing must be left on stack before execute
    ; so that there is no distance between nt and the rest of the stack
    lda LSB,x
    sta .xt
    lda MSB, x
    sta .xt + 1
    inx
    lda LATEST_LSB
    sta .dowords_nametoken
    lda LATEST_MSB
    sta .dowords_nametoken + 1

.dowords_lambda
    lda .dowords_nametoken
    sta W
    lda .dowords_nametoken + 1
    sta W + 1
    ldy #0
    lda (W), y
    bne +
-   rts
+   and #STRLEN_MASK
    pha
    dex
    lda .dowords_nametoken
    sta LSB, x
    lda .dowords_nametoken + 1
    sta MSB, x
.xt = * + 1
    jsr PLACEHOLDER_ADDRESS
    inx
    lda MSB-1, x
    ora LSB-1, x
    beq .cancel
    pla
    clc
    adc #3 ; guaranteed carry clear
    adc .dowords_nametoken
    sta .dowords_nametoken
    lda .dowords_nametoken + 1
    adc #0
    sta .dowords_nametoken + 1
    jmp .dowords_lambda
; using a word here in case the lambda trashes Ws
.dowords_nametoken
    !word 0
.cancel
    pla
    rts
