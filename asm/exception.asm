; CATCH THROW (ABORT")
;
; Stage B: the handler cell holds the FULL 16-bit CPU stack pointer - the
; stage-A code could only snapshot SL through an 8-bit tsx and rebuilt SH
; from the top-of-stack page, an assumption THROW no longer needs.

EXCEPTION_HANDLER
    +VALUE BANK1 + _EXCEPTION_HANDLER
_EXCEPTION_HANDLER
    !word 0, 0

+BACKLINK "catch", 5
CATCH
    ; save data stack pointer
    txa
    jsr PUSHA
    jsr TO_R
    ; save previous handler
    jsr EXCEPTION_HANDLER
    jsr FETCH
    jsr TO_R
    ; set current handler = the full 16-bit CPU stack pointer
    stx W3
    rep #$10
!rl
    tsx
    txa
    sep #$10
!rs
    ldx W3
    jsr PUSHA
    jsr EXCEPTION_HANDLER
    jsr STORE
    ; execute returns if no THROW
    jsr EXECUTE
    ; restore previous handler
    jsr R_TO
    jsr EXCEPTION_HANDLER
    jsr STORE
    ; discard saved stack pointer
    jsr R_TO
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
    rts
+   lda _EXCEPTION_HANDLER
    beq .print_error_and_abort

    ; restore the CPU stack: S := the handler's saved 16-bit value.
    jsr EXCEPTION_HANDLER
    jsr FETCH
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
    jsr R_TO
    jsr EXCEPTION_HANDLER
    jsr STORE

    ; exc# on return stack
    jsr R_TO
    jsr SWAP
    jsr TO_R

    ; restore stack
    lda LSB,x
    tax
    inx
    inx
    jsr R_TO
    rts

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
    jsr .get_abort_string
    jmp .type_and_abort
+   jsr .get_system_exception_string
    jsr COUNT
.type_and_abort
    jsr RVS
    jsr TYPE
.cr_and_abort
    jsr CR
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
    +VALUE BANK1 + .user_interrupt

.unknown_exception
    jsr RVS
    jsr DOT
    lda #'e'
    jsr PUTCHR
    lda #'r'
    jsr PUTCHR
    jsr PUTCHR
    jmp .cr_and_abort

.get_abort_string
.msg_addr = * + 1
    lda #0
.msg_bank = * + 1
    ldy #0
    jsr pushya
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
