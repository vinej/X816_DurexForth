; SYSX - small KERNAL-wrapper primitives: I2C byte access + text charset.
; These jsl BANK1 + KERNAL routines that clobber .X (our Forth stack pointer), so each
; saves it with phx and restores with plx.  durexForth runs with ROM bank 0
; (KERNAL), so the $FExx / $FFxx entry points are callable directly.

    +BACKLINK "i2cpeek", 7
I2CPEEK ; ( dev reg -- byte )  i2c_read_byte $FEC6 (.X=dev .Y=reg -> .A, c=err)
    lda LSB, x                  ; reg
    tay
    lda LSB+1, x                ; dev
    phx                         ; save Forth SP
    tax                         ; .X = dev
    jsl $fec6
    plx                         ; restore Forth SP
    inx                         ; drop reg; result reuses the dev slot
    sta LSB, x
    stz MSB, x
    rtl

    +BACKLINK "i2cpoke", 7
I2CPOKE ; ( dev reg val -- )  i2c_write_byte $FEC9 (.X=dev .Y=reg .A=val)
    lda LSB+1, x                ; reg
    tay
    lda LSB+2, x                ; dev
    sta W                       ; stash (tax would clobber the Forth SP too early)
    lda LSB, x                  ; val -> .A
    phx                         ; save Forth SP
    ldx W                       ; .X = dev
    jsl $fec9
    plx                         ; restore Forth SP
    inx
    inx
    inx
    rtl

    +BACKLINK "charset", 7
CHARSET ; ( n -- )  screen_set_charset $FF62 (.A=charset: 1=ISO 2/3=PET .. 12=Katakana)
    lda LSB, x                  ; n
    phx                         ; save Forth SP (kernal clobbers .X/.Y)
    jsl $ff62
    plx
    inx                         ; drop n
    rtl

    +BACKLINK "keymap", 6
KEYMAP ; ( c-addr u -- )  set the keyboard layout by name:  s" de-de" keymap
    ; KERNAL keymap $FED2, carry clear = set, .X/.Y -> zero-terminated name.
    ; The ROM names are uppercase ("DE-DE", "EN-US/INT", "ABC/X16", ...) and
    ; matched exactly, so the copy is uppercased here.  An unknown name aborts
    ; with the usual reverse-video "name?" error.
    lda LSB, x                  ; u
    cmp #15
    bcs .km_fail                ; too long for any layout name
    tay
    lda #0
    sta .km_buf, y              ; zero-terminate the copy
    lda LSB+1, x
    sta W
    lda MSB+1, x
    sta W+1
-   dey
    bmi +
    lda (W), y
    cmp #'a'
    bcc ++
    cmp #'z'+1
    bcs ++
    and #$df                    ; a-z -> A-Z
++  sta .km_buf, y
    bra -
+   phx                         ; save Forth SP (kernal clobbers .X/.Y)
    ldx #<.km_buf
    ldy #>.km_buf
    clc                         ; carry clear = set layout
    jsl $fed2
    plx
    bcs .km_fail
    inx                         ; drop c-addr u
    inx
    rtl
.km_fail
    jmp print_word_not_found_error  ; ( c-addr u ) -> "name?" + throw -13
.km_buf !fill 16, 0

    +BACKLINK "monitor", 7
MONITOR ; ( -- )  enter the KERNAL machine-language monitor. Does NOT return.
    jmp $fecc
