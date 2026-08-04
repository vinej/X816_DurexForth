; EMIT PAGE RVS CR TYPE KEY? KEY REFILL SOURCE SOURCE-ID >IN CHAR IOABORT

    +BACKLINK "emit", 4
EMIT
    lda	LSB, x
    inx
    jmp	PUTCHR

    +BACKLINK "page", 4
PAGE
    jmp kern_cls

    +BACKLINK "rvs", 3
RVS ; ( -- ) invert text output
    ; X816: CON_PUTC has no reverse-video control code; the console cursor
    ; owns the attribute byte. No-op until an attribute API exists.
    rts

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
    jsr kern_getin ; preserves X/Y
    sta .key_pending
    beq +
.pushtrue
    lda #$ff
+   tay
    jmp pushya

    +BACKLINK "key", 3
    lda .key_pending
    bne +
-   jsr kern_getin
    cmp #0
    beq -
+   ldy #0
    sty .key_pending
    ldy #0
    jmp pushya

.key_pending
    !byte 0

CLOSE_INPUT_SOURCE
    ; X816: SOURCE_ID above zero is a kernel file handle - close it, then
    ; pop the input state. There is no channel to re-select afterwards:
    ; REFILL reads whatever handle SOURCE_ID holds. The read-ahead cache
    ; belongs to the closing file, so it is discarded, not seeked back.
    stx W
    lda #0
    sta fs_ccnt
    sta fs_cpos
    lda SOURCE_ID_LSB
    beq +                   ; 0 = keyboard
    bit SOURCE_ID_MSB
    bmi +                   ; -1 = evaluate
    jsr kern_fs_close
+   jsr POP_INPUT_SOURCE
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
    bne .getLineFromDisk

    ; getLineFromConsole
    ; X816: the KERNAL BASIN screen editor is gone; read keys from the
    ; kernel console and edit the line here. CON_PUTC interprets $08 as
    ; backspace, so echoing the key IS the screen edit.

    stx W          ; save forth stack pointer
    ldy #0         ; TIB index (kern_getc/PUTCHR preserve X and Y)
-   jsr kern_getc
    cmp #$d
    beq .gotReturn
    cmp #$08
    beq .backspace
    cpy #$58       ; line-length limit
    beq -
    sta TIB,y
    jsr PUTCHR     ; echo
    iny
    jmp -
.backspace
    cpy #0
    beq -
    dey
    jsr PUTCHR     ; echo the $08: the console steps back and blanks
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

.getLineFromDisk
    ; X816: SOURCE_ID is a kernel file handle; read it a byte at a time
    ; through kern_fs_getbyte (which preserves X and Y). NUL, $0D and $0A
    ; all end a line - a CR-LF file just yields a harmless empty line, and
    ; mkcard-era $00 $00 headers read as blanks, exactly as upstream.
    lda TIB_PTR
    sta W
    lda TIB_PTR + 1
    sta W+1
-   lda SOURCE_ID_LSB
    jsr fs_getbyte
    bcs .disk_eof
    ora #0
    beq .return_true
    cmp #K_RETURN
    beq .return_true
    cmp #$0a
    beq .return_true
    ldy TIB_SIZE
    sta (W),y
    inc TIB_SIZE
    jmp -
.disk_eof
    ; End of file: deliver a final unterminated line if one accumulated,
    ; else report the source exhausted.
    lda TIB_SIZE
    bne .return_true

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
    ; X816: SIXTEEN levels, not the C64's eight, and the overflow is
    ; CHECKED. The suite runs five levels deep before a single test runs
    ; (keyboard > base > autorun > test > suite), and testcore's nested
    ; EVALUATE tests stack more - upstream's ninth push landed on the
    ; depth byte just past the array, and every pop after that was
    ; misaligned: SOURCE_ID came back garbage and the interpreter fell
    ; silently back to the keyboard mid-suite.
    !fill 16*12
SAVE_INPUT_STACK_DEPTH
    !byte 0

push_input_stack
    ldy SAVE_INPUT_STACK_DEPTH
    cpy #16*12
    bcs .input_stack_overflow
    sta SAVE_INPUT_STACK, y
    inc SAVE_INPUT_STACK_DEPTH
    rts
.input_stack_overflow
    lda #-8 ; dictionary/structure overflow - loud, never silent corruption
    jmp throw_a

pop_input_stack
    dec SAVE_INPUT_STACK_DEPTH
    ldy SAVE_INPUT_STACK_DEPTH
    lda SAVE_INPUT_STACK, y
    rts

PUSH_INPUT_SOURCE
    ; X816: hand cached read-ahead back to the kernel before another
    ; source becomes current (fs.asm).
    jsr fs_flush
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
