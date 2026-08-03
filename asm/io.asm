; EMIT PAGE RVS CR TYPE KEY? KEY REFILL SOURCE SOURCE-ID >IN CHAR IOABORT

    +BACKLINK "emit", 4
EMIT
    lda	LSB, x
    inx
    jmp	PUTCHR

    +BACKLINK "page", 4
PAGE
    lda #K_CLRSCR
    jmp PUTCHR

    +BACKLINK "rvs", 3
RVS ; ( -- ) invert text output
    lda #$12
    jmp PUTCHR

    +BACKLINK "cr", 2
CR ; ( -- )
    lda #$d
    jmp PUTCHR

    +BACKLINK "type", 4
TYPE ; ( caddr u -- )
    ; Guard: TYPE needs two cells. A bare TYPE, or "12 TYPE", would spray
    ; garbage-length memory to the screen before the interpreter's
    ; post-word underflow check could fire; refuse up front instead.
    ; (Content can't be validated - Forth is untyped - only the depth.
    ; X = X_INIT is empty, X_INIT-1 has one item; deeper wraps below.)
    cpx #X_INIT
    beq .type_underflow
    cpx #X_INIT-1
    bne +
.type_underflow
    lda #-4 ; throws "stack" (stack underflow)
    jmp throw_a
+   lda #0 ; quote mode off
    sta $381 ; X16 qtsw
-   lda LSB,x
    ora MSB,x
    bne +
    inx
    inx
    rts
+   jsr OVER
    jsr FETCHBYTE
    jsr EMIT
    jsr ONE
    jsr SLASH_STRING
    jmp -

    +BACKLINK "key?", 4
    lda .key_pending
    bne .pushtrue
    stx W
    jsr $ffe4 ; GETIN
    ldx W
    sta .key_pending
    beq +
.pushtrue
    lda #$ff
+   tay
    jmp pushya

    +BACKLINK "key", 3
    lda .key_pending
    bne +
-   stx W
    jsr $ffe4 ; GETIN
    ldx W
    cmp #0
    beq -
+   ldy #0
    sty .key_pending
    ldy #0
    jmp pushya

.key_pending
    !byte 0

CLOSE_INPUT_SOURCE
    ; X816: no file input sources until the kernel FS_* words exist, so
    ; SOURCE_ID is only ever 0 (keyboard) or -1 (evaluate) - there is no
    ; channel to close or re-select, just the input state to pop.
    stx W
    jsr POP_INPUT_SOURCE
    ldx W
    rts

    +BACKLINK "refill", 6
REFILL ; ( -- flag )

    ldy #0
    sty TO_IN_W
    sty TO_IN_W + 1
    sty TIB_SIZE
    sty TIB_SIZE + 1

    lda SOURCE_ID_LSB
    bmi .getLineFromEvaluateString
    bne .return_false ; X816: file sources return with the FS_* words

    ; getLineFromConsole

    stx W          ; save forth stack pointer
    ldy #0         ; TIB index
-   sty W2         ; BASIN clobbers X/Y - keep index in W2
    jsr $ffcf      ; BASIN (reads a screen-edited line, char by char)
    ldy W2
    cmp #$d
    beq .gotReturn
    sta TIB,y
    cpy #$58 ; TIB area is $400-$458
    beq -
    iny
    jmp -
.gotReturn
    ; Set TIB_SIZE to number of chars fetched.
    sty TIB_SIZE
    jsr PUTCHR
    ldx W
.return_true
    dex
    lda #$ff
    sta LSB,x
    sta MSB,x
    rts

; X816: .getLineFromDisk (READST/CHRIN over a CBM channel) was deleted with
; disk.asm; its successor reads through FS_READ when the file words return.

.return_false
    dex
    lda #0
    sta MSB,x
    sta LSB,x
    rts

.getLineFromEvaluateString
    lda EVALUATE_STRING_SIZE_LSB
    ora EVALUATE_STRING_SIZE_MSB
    beq .return_false

EVALUATE_STRING_PTR_LSB = * + 1
    lda #0
    sta TIB_PTR
EVALUATE_STRING_PTR_MSB = * + 1
    lda #0
    sta TIB_PTR + 1

.grow_tib_to_end_of_line
    lda EVALUATE_STRING_PTR_LSB
    sta + + 1
    lda EVALUATE_STRING_PTR_MSB
    sta + + 2
+   lda PLACEHOLDER_ADDRESS
    tay

    inc EVALUATE_STRING_PTR_LSB
    bne +
    inc EVALUATE_STRING_PTR_MSB
+
    lda EVALUATE_STRING_SIZE_LSB
    bne +
    dec EVALUATE_STRING_SIZE_MSB
+   dec EVALUATE_STRING_SIZE_LSB

    tya
    cmp #$d
    beq .return_true

    inc TIB_SIZE
    bne +
    inc TIB_SIZE + 1
+
EVALUATE_STRING_SIZE_LSB = * + 1
    lda #0
EVALUATE_STRING_SIZE_MSB = * + 1
    ora #0
    bne .grow_tib_to_end_of_line
    jmp .return_true

    +BACKLINK "source", 6
SOURCE
    dex
    dex
    lda TIB_PTR
    sta LSB+1, x
    lda TIB_PTR + 1
    sta MSB+1, x
    lda TIB_SIZE
    sta LSB, x
    lda TIB_SIZE + 1
    sta MSB, x
    rts

TIB_PTR
    !word 0
TIB_SIZE
    !word 0

    +BACKLINK "#tib", 4
    ; ( -- addr ) the cell holding the number of characters in TIB
    dex
    lda #<TIB_SIZE
    sta LSB, x
    lda #>TIB_SIZE
    sta MSB, x
    rts

    +BACKLINK "source-id", 9
SOURCE_ID_LSB = * + 1
SOURCE_ID_MSB = * + 3
    ; -1 : string (via evaluate)
    ; 0 : keyboard
    ; 1+ : file id
    +VALUE	0

    +BACKLINK ">in", 3
TO_IN
    +VALUE TO_IN_W
TO_IN_W
    !word 0

    +BACKLINK "char", 4
CHAR ; ( name -- char )
    jsr PARSE_NAME
    inx
    jmp FETCHBYTE

SAVE_INPUT_STACK
    ; Forth standard 11.3.3 "Input Source":
    ; "Input [...] shall be nestable in any order to at least eight levels."
    ; Eight levels is overkill for INCLUDED, since opening more than four DOS
    ; channels gives a "no channel" error message on C64.
    ; It is anyway nice to keep some extra levels for EVALUATE and LOAD.
    !fill 8*12
SAVE_INPUT_STACK_DEPTH
    !byte 0

push_input_stack
    ; Stack overflow check could be added, but does not seem needed in practice.
    ldy SAVE_INPUT_STACK_DEPTH
    sta SAVE_INPUT_STACK, y
    inc SAVE_INPUT_STACK_DEPTH
    rts

pop_input_stack
    dec SAVE_INPUT_STACK_DEPTH
    ldy SAVE_INPUT_STACK_DEPTH
    lda SAVE_INPUT_STACK, y
    rts

PUSH_INPUT_SOURCE
    lda TO_IN_W
    jsr push_input_stack
    lda TO_IN_W+1
    jsr push_input_stack
    lda SOURCE_ID_LSB
    jsr push_input_stack
    lda SOURCE_ID_MSB
    jsr push_input_stack
    lda TIB_PTR
    jsr push_input_stack
    lda TIB_PTR+1
    jsr push_input_stack
    lda TIB_SIZE
    jsr push_input_stack
    lda TIB_SIZE+1
    jsr push_input_stack
    lda EVALUATE_STRING_PTR_LSB
    jsr push_input_stack
    lda EVALUATE_STRING_PTR_MSB
    jsr push_input_stack
    lda EVALUATE_STRING_SIZE_LSB
    jsr push_input_stack
    lda EVALUATE_STRING_SIZE_MSB
    jmp push_input_stack

POP_INPUT_SOURCE
    jsr pop_input_stack
    sta EVALUATE_STRING_SIZE_MSB
    jsr pop_input_stack
    sta EVALUATE_STRING_SIZE_LSB
    jsr pop_input_stack
    sta EVALUATE_STRING_PTR_MSB
    jsr pop_input_stack
    sta EVALUATE_STRING_PTR_LSB
    jsr pop_input_stack
    sta TIB_SIZE+1
    jsr pop_input_stack
    sta TIB_SIZE
    jsr pop_input_stack
    sta TIB_PTR+1
    jsr pop_input_stack
    sta TIB_PTR
    jsr pop_input_stack
    sta SOURCE_ID_MSB
    jsr pop_input_stack
    sta SOURCE_ID_LSB
    jsr pop_input_stack
    sta TO_IN_W+1
    jsr pop_input_stack
    sta TO_IN_W
    rts

; handle errors returned by open,
; close, and chkin. If ioresult is
; nonzero, print error message and
; throw -37.
    +BACKLINK "ioabort", 7
IOABORT ; ( ioresult -- )
    inx
    lda MSB-1,x
    bne .print_ioerr
    lda LSB-1,x
    bne .print_ioerr
    rts

.print_ioerr
    lda #<.ioerr
    sta W
    lda #>.ioerr
    sta W+1

    jsr RVS

    ldy #0
-   lda (W),y
    pha
    and #$7f
    jsr PUTCHR
    iny
    pla
    bpl -

    lda #-37 ; file i/o exception
    jmp throw_a

.ioerr
    !text "ioerro"
    !byte 'r'|$80
