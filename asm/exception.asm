; CATCH THROW (ABORT")
;
; Stage B: the handler cell holds the FULL 16-bit CPU stack pointer - the
; stage-A code could only snapshot SL through an 8-bit tsx and rebuilt SH
; from the top-of-stack page, an assumption THROW no longer needs.

    ; HANDLER is the ordinary Forth name for this cell (Forth-83, gforth,
    ; and FLOW.TXT). It holds the CPU stack pointer of the innermost
    ; CATCH frame, or 0 when nothing is catching - which is what a
    ; debugger wants to see and what THROW unwinds to.
    +BACKLINK "handler", 7
EXCEPTION_HANDLER
    +VALUE BANK1 + _EXCEPTION_HANDLER
_EXCEPTION_HANDLER
    !word 0, 0

+BACKLINK "catch", 5
CATCH
    ; save data stack pointer
    txa
    jsl BANK1 + PUSHA
    jsl BANK1 + TO_R
    ; save previous handler
    jsl BANK1 + EXCEPTION_HANDLER
    jsl BANK1 + FETCH
    jsl BANK1 + TO_R
    ; set current handler = the full 16-bit CPU stack pointer
    stx W3
    rep #$10
!rl
    tsx
    txa
    sep #$10
!rs
    ldx W3
    jsl BANK1 + PUSHA
    jsl BANK1 + EXCEPTION_HANDLER
    jsl BANK1 + STORE
    ; execute returns if no THROW
    jsl BANK1 + EXECUTE
    ; restore previous handler
    jsl BANK1 + R_TO
    jsl BANK1 + EXCEPTION_HANDLER
    jsl BANK1 + STORE
    ; discard saved stack pointer
    jsl BANK1 + R_TO
    inx
    inx
    ; normal completion
    jmp ZERO

+BACKLINK "throw", 5
THROW
    lda LSB,x
    ora MSB,x
    bne +
    ; 0 throw is no-op
    inx
    inx
    rtl
+   lda _EXCEPTION_HANDLER
    beq .print_error_and_abort

    ; restore the CPU stack: S := the handler's saved 16-bit value.
    jsl BANK1 + EXCEPTION_HANDLER
    jsl BANK1 + FETCH
    stx W3
    lda LSB,x
    rep #$10
!rl
    tax
    txs
    sep #$10
!rs
    ldx W3
    inx
    inx

    ; restore previous handler
    jsl BANK1 + R_TO
    jsl BANK1 + EXCEPTION_HANDLER
    jsl BANK1 + STORE

    ; exc# on return stack
    jsl BANK1 + R_TO
    jsl BANK1 + SWAP
    jsl BANK1 + TO_R

    ; restore stack
    lda LSB,x
    tax
    inx
    inx
    jsl BANK1 + R_TO
    rtl

.print_error_and_abort
    lda MSB,x
    cmp #$ffff
    beq +
    jmp .unknown_exception
+
    lda LSB,x
    cmp #-13 ; Undefined word is printed before THROW.
    bne +
    jmp .cr_and_abort
+   cmp #-37 ; File I/O errors are printed before THROW.
    bne +
    jmp .cr_and_abort
+
    cmp #-2 ; abort"
    bne +
    jsl BANK1 + .get_abort_string
    jmp .type_and_abort
+   jsl BANK1 + .get_system_exception_string
    jsl BANK1 + COUNT
.type_and_abort
    jsl BANK1 + RVS
    jsl BANK1 + TYPE
.cr_and_abort
    jsl BANK1 + CR
    ; X816: an uncaught error stops the EMULATOR right here with the
    ; message on screen (status 1), so a test harness never waits out a
    ; timeout on a machine that has already said what went wrong. $9FBC
    ; is open bus on hardware - there this falls through to the prompt.
    phb
    sep #$20
!as
    lda #0
    pha
    plb
    lda #1
    sta $9fbc
    rep #$20
!al
    plb
    ldx #X_INIT
    jmp QUIT

; It is a bit cheesy to use a hardcoded list, but it works.
; A linked list would be more flexible.
.get_system_exception_string
    cmp #-1
    bne +
    +VALUE BANK1 + .abort_string
+   cmp #-4
    bne +
    +VALUE BANK1 + .stack_underflow
+   cmp #-8
    bne +
    +VALUE BANK1 + .mem_full
+   cmp #-10
    bne +
    +VALUE BANK1 + .div_error
+   cmp #-16
    bne +
    +VALUE BANK1 + .no_word
+   cmp #-28
    bne .unknown_exception
    ; -28 is ANS "user interrupt" and BOTH the break key and a BRK opcode
    ; raise it - CATCH cannot tell them apart, and should not. The word on
    ; screen must: "break" is the machine obeying the key, "brk" is an
    ; instruction nobody meant to execute. The flag is set by whichever
    ; handler ran (asm/interpreter.asm, and kern_getc for a parked one).
    lda brk_from_key
    beq +
    +VALUE BANK1 + .user_break
+   +VALUE BANK1 + .user_interrupt

.unknown_exception
    jsl BANK1 + RVS
    jsl BANK1 + DOT
    lda #'e'
    jsl BANK1 + PUTCHR
    lda #'r'
    jsl BANK1 + PUTCHR
    jsl BANK1 + PUTCHR
    jmp .cr_and_abort

.get_abort_string
.msg_addr = * + 1
    lda #0
.msg_bank = * + 1
    ldy #0
    jsl BANK1 + pushya
.msg_len = * + 1
    lda #0
    ldy #0
    jmp pushya

.abort_string
    !byte 5
    !text "abort"
.stack_underflow
    !byte 5
    !text "stack"
.mem_full
    !byte 4
    !text "full"
.no_word
    !byte 7
    !text "no name"
.div_error
    !byte 2
    !text "/0" ; division by zero
.user_interrupt
    !byte 3
    !text "brk"
.user_break
    !byte 5
    !text "break"

+BACKLINK "(abort\")", 8 ; ( addr u -- )
    lda LSB,x
    sta .msg_len
    inx
    inx
    lda LSB,x
    sta .msg_addr
    sep #$20
!as
    lda MSB,x
    sta .msg_bank
    rep #$20
!al
    inx
    inx
    lda #-2
    jmp throw_a
