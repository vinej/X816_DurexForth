; EMIT PAGE RVS CR TYPE KEY? KEY REFILL SOURCE SOURCE-ID >IN CHAR IOABORT
;
; Stage B: TIB_PTR / TIB_SIZE / TO_IN_W / the evaluate-string cursor are
; 16-bit words, bank-1-implicit (interpreter.asm's note: every input
; buffer lives in this bank). Byte work - reading characters, filling the
; TIB - runs in sep #$20 windows; everything at a word boundary is M=0.

    +BACKLINK "emit", 4
EMIT
    lda	LSB, x
    inx
    inx
    jmp	PUTCHR

    +BACKLINK "page", 4
PAGE
    jmp kern_cls

    +BACKLINK "rvs", 3
RVS ; ( -- ) invert text output
    ; X816: CON_PUTC has no reverse-video control code; the console cursor
    ; owns the attribute byte. No-op until an attribute API exists.
    rtl

    +BACKLINK "cr", 2
CR ; ( -- )
    lda #$d
    jmp PUTCHR

    +BACKLINK "type", 4
TYPE ; ( caddr u -- )
    ; Guard: TYPE needs two cells. A bare TYPE, or "12 TYPE", would spray
    ; garbage-length memory to the screen before the interpreter's
    ; post-word underflow check could fire; refuse up front instead.
    ; (X = X_INIT is empty, X_INIT-2 holds one cell.)
    cpx #X_INIT-2
    bcc +
    lda #-4 ; throws "stack" (stack underflow)
    jmp throw_a
-   lda LSB,x
    ora MSB,x
    bne +
    inx
    inx
    inx
    inx
    rtl
+   jsl BANK1 + OVER
    jsl BANK1 + FETCHBYTE
    jsl BANK1 + EMIT
    jsl BANK1 + ONE
    jsl BANK1 + SLASH_STRING
    jmp -

    +BACKLINK "key?", 4
    lda .key_pending
    bne .pushtrue
    jsl BANK1 + kern_getin ; preserves X/Y
    sta .key_pending
    beq +
.pushtrue
    lda #$ffff
    dex
    dex
    sta LSB, x
    sta MSB, x
    rtl
+   jmp PUSHA ; A = 0: false

    +BACKLINK "key", 3
    lda .key_pending
    bne +
-   jsl BANK1 + kern_getin
    cmp #0
    beq -
+   stz .key_pending
    jmp PUSHA

.key_pending
    !word 0

CLOSE_INPUT_SOURCE
    ; X816: SOURCE_ID above zero is a kernel file handle - close it, then
    ; pop the input state. There is no channel to re-select afterwards:
    ; REFILL reads whatever handle SOURCE_ID holds. The read-ahead cache
    ; belongs to the closing file, so it is discarded, not seeked back.
    stx W3
    sep #$20
!as
    stz fs_ccnt
    stz fs_cpos
    rep #$20
!al
    lda SOURCE_ID_LSB
    beq +                   ; 0 = keyboard
    bmi +                   ; -1 = evaluate
    jsl BANK1 + kern_fs_close
+   jsl BANK1 + POP_INPUT_SOURCE
    ldx W3
    rtl

    +BACKLINK "refill", 6
REFILL ; ( -- flag )

    stz TO_IN_W
    stz TIB_SIZE

    lda SOURCE_ID_LSB
    bpl +                   ; (the eval arm outgrew a bmi's reach)
    jmp .getLineFromEvaluateString
+   bne .getLineFromDisk

    ; getLineFromConsole
    ; X816: the KERNAL BASIN screen editor is gone; read keys from the
    ; kernel console and edit the line here. CON_PUTC interprets $08 as
    ; backspace, so echoing the key IS the screen edit.

    ldy #0         ; TIB index (kern_getc/PUTCHR preserve X and Y)
-   jsl BANK1 + kern_getc
    cmp #$d
    beq .gotReturn
    cmp #$08
    beq .backspace
    cpy #$58       ; line-length limit
    beq -
    sep #$20
!as
    sta TIB,y
    rep #$20
!al
    jsl BANK1 + PUTCHR     ; echo
    iny
    bra -
.backspace
    cpy #0
    beq -
    dey
    jsl BANK1 + PUTCHR     ; echo the $08: the console steps back and blanks
    bra -
.gotReturn
    ; Set TIB_SIZE to number of chars fetched.
    sep #$20
!as
    sty TIB_SIZE
    rep #$20
!al
    jsl BANK1 + PUTCHR
.return_true
    ; A fresh line is in the TIB region (keyboard and file sources; an
    ; evaluate line lives in its string) - raise TIB_TOP over it, so a
    ; nested INCLUDED stacks its own lines above, never on top of it.
    lda SOURCE_ID_LSB
    bmi +
    lda TIB_PTR
    clc
    adc TIB_SIZE
    sta TIB_TOP
+   dex
    dex
    lda #$ffff
    sta LSB,x
    sta MSB,x
    rtl

.getLineFromDisk
    ; X816: SOURCE_ID is a kernel file handle; read it a byte at a time
    ; through fs_getbyte (which preserves X and Y). NUL, $0D and $0A
    ; all end a line - a CR-LF file just yields a harmless empty line.
    lda TIB_PTR
    sta W
-   lda SOURCE_ID_LSB
    jsl BANK1 + fs_getbyte ; byte in A (16-bit clean), carry set at EOF
    bcs .disk_eof
    ora #0
    beq .return_true
    cmp #K_RETURN
    beq .return_true
    cmp #$0a
    beq .return_true
    ldy TIB_SIZE
    sep #$20
!as
    sta (W),y
    rep #$20
!al
    inc TIB_SIZE
    bra -
.disk_eof
    ; End of file: deliver a final unterminated line if one accumulated,
    ; else report the source exhausted.
    lda TIB_SIZE
    bne .return_true

.return_false
    dex
    dex
    stz MSB,x
    stz LSB,x
    rtl

.getLineFromEvaluateString
    lda EVALUATE_STRING_SIZE
    beq .return_false

    lda EVALUATE_STRING_PTR
    sta TIB_PTR
    sta W
-   lda EVALUATE_STRING_SIZE
    beq .eval_line_done
    sep #$20
!as
    lda (W)
    rep #$20
!al
    and #$ff
    cmp #$d
    beq .eval_cr
    inc W
    inc TIB_SIZE
    dec EVALUATE_STRING_SIZE
    bra -
.eval_cr
    inc W ; consume the CR; it is not part of the line
    dec EVALUATE_STRING_SIZE
.eval_line_done
    lda W
    sta EVALUATE_STRING_PTR
    jmp .return_true

EVALUATE_STRING_PTR
    !word 0
EVALUATE_STRING_SIZE
    !word 0

    +BACKLINK "source", 6
SOURCE
    dex
    dex
    dex
    dex
    lda TIB_PTR
    sta LSB+2, x
    lda #(BANK1 >> 16)
    sta MSB+2, x
    lda TIB_SIZE
    sta LSB, x
    stz MSB, x
    rtl

TIB_PTR
    !word 0
; First TIB byte above every line a suspended (or the current) file
; source still owns. INCLUDED stacks the next nested file's lines here;
; REFILL raises it per line; the input-source frames save and restore it,
; so closing a nest hands the space back. TIB_PTR cannot serve this role:
; under EVALUATE it points into the evaluated string, and deriving the
; slot from it once made INCLUDED "reset" onto the outermost file's
; half-consumed line (the post-suite crash of 2026-08-04).
TIB_TOP
    !word TIB
TIB_SIZE
    !word 0, 0 ; padded to a cell: #tib hands this address to @

    +BACKLINK "#tib", 4
    ; ( -- addr ) the cell holding the number of characters in TIB
    lda #TIB_SIZE
    jmp pushbank1

    +BACKLINK "source-id", 9
SOURCE_ID_LSB = * + 3
SOURCE_ID_MSB = * + 8
    ; -1 : string (via evaluate)
    ; 0 : keyboard
    ; 1+ : file id
    +VALUE	0

    +BACKLINK ">in", 3
TO_IN
    +VALUE BANK1 + TO_IN_W
TO_IN_W
    !word 0, 0 ; padded to a cell: >IN is fetched and stored with @/!

    +BACKLINK "char", 4
CHAR ; ( name -- char )
    jsl BANK1 + PARSE_NAME
    inx
    inx
    jmp FETCHBYTE

SAVE_INPUT_STACK
    ; Forth standard 11.3.3 "Input Source":
    ; "Input [...] shall be nestable in any order to at least eight levels."
    ; X816: FIFTEEN levels (16 would push the 8-bit depth index to 256),
    ; and the overflow is CHECKED (the ninth push on the C64 corrupted the
    ; depth byte; see stage A's history). Eight 16-bit words per level.
    !fill 15*16
SAVE_INPUT_STACK_DEPTH
    !byte 0

push_input_stack ; A(16) -> the save stack
    ldy SAVE_INPUT_STACK_DEPTH
    cpy #15*16
    bcs .input_stack_overflow
    sta SAVE_INPUT_STACK, y
    iny
    iny
    sty SAVE_INPUT_STACK_DEPTH
    rtl
.input_stack_overflow
    lda #-8 ; dictionary/structure overflow - loud, never silent corruption
    jmp throw_a

pop_input_stack ; -> A(16)
    ldy SAVE_INPUT_STACK_DEPTH
    dey
    dey
    sty SAVE_INPUT_STACK_DEPTH
    lda SAVE_INPUT_STACK, y
    rtl

PUSH_INPUT_SOURCE
    ; X816: hand cached read-ahead back to the kernel before another
    ; source becomes current (fs.asm).
    jsl BANK1 + fs_flush
    lda TO_IN_W
    jsl BANK1 + push_input_stack
    lda SOURCE_ID_LSB
    jsl BANK1 + push_input_stack
    lda SOURCE_ID_MSB ; only the low byte is meaningful
    jsl BANK1 + push_input_stack
    lda TIB_PTR
    jsl BANK1 + push_input_stack
    lda TIB_SIZE
    jsl BANK1 + push_input_stack
    lda EVALUATE_STRING_PTR
    jsl BANK1 + push_input_stack
    lda EVALUATE_STRING_SIZE
    jsl BANK1 + push_input_stack
    lda TIB_TOP
    jmp push_input_stack

POP_INPUT_SOURCE
    jsl BANK1 + pop_input_stack
    sta TIB_TOP
    jsl BANK1 + pop_input_stack
    sta EVALUATE_STRING_SIZE
    jsl BANK1 + pop_input_stack
    sta EVALUATE_STRING_PTR
    jsl BANK1 + pop_input_stack
    sta TIB_SIZE
    jsl BANK1 + pop_input_stack
    sta TIB_PTR
    jsl BANK1 + pop_input_stack
    sta SOURCE_ID_MSB
    jsl BANK1 + pop_input_stack
    sta SOURCE_ID_LSB
    jsl BANK1 + pop_input_stack
    sta TO_IN_W
    rtl

; handle errors returned by open,
; close, and chkin. If ioresult is
; nonzero, print error message and
; throw -37.
    +BACKLINK "ioabort", 7
IOABORT ; ( ioresult -- )
    inx
    inx
    lda MSB-2,x
    bne .print_ioerr
    lda LSB-2,x
    bne .print_ioerr
    rtl

.print_ioerr
    lda #.ioerr
    sta W

    jsl BANK1 + RVS

    ldy #0
-   sep #$20
!as
    lda (W),y
    pha
    and #$7f
    rep #$20
!al
    and #$ff
    jsl BANK1 + PUTCHR
    iny
    sep #$20
!as
    pla
    rep #$20
!al
    bpl -

    lda #-37 ; file i/o exception
    jmp throw_a

.ioerr
    !text "ioerro"
    !byte 'r'|$80
