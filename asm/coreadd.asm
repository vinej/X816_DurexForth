; Core additions: small primitives requested for the X16 build.
; 2- SGN CATNIB SBIT CBIT FBIT ROLL 2ROT SLEEP MS REBOOT TICKS
; (TIME@ DATE@ SETTIME deferred: need the clock_get/set_date_time ABI.)
; Stage B: 32-bit cells; SBIT/CBIT/FBIT dereference [W] long (flat
; addresses); byte RMW inside sep #$20 windows.

    +BACKLINK "2-", 2
TWO_MINUS ; ( n -- n-2 )
    sec
    lda LSB, x
    sbc #2
    sta LSB, x
    lda MSB, x
    sbc #0
    sta MSB, x
    rtl

    +BACKLINK "sgn", 3
SGN ; ( n -- -1|0|1 )
    lda LSB, x
    ora MSB, x
    beq +                       ; zero -> leave 0
    lda MSB, x
    bmi ++                      ; negative
    lda #1                      ; positive -> 1
    sta LSB, x
    stz MSB, x
    rtl
++  lda #$ffff                  ; negative -> -1
    sta LSB, x
    sta MSB, x
+   rtl

    +BACKLINK "catnib", 6
CATNIB ; ( nh nl -- byte ) (nh<<4) | nl
    lda LSB, x                  ; nl
    and #$0f
    sta W
    lda LSB+2, x                ; nh
    asl
    asl
    asl
    asl
    ora W
    and #$ff
    inx
    inx
    sta LSB, x
    stz MSB, x
    rtl

    +BACKLINK "sbit", 4
SBIT ; ( addr mask -- ) set the masked bits at addr
    lda LSB+2, x
    sta W
    lda MSB+2, x
    sta W+2
    sep #$20
!as
    lda LSB, x                  ; mask
    ora [W]
    sta [W]
    rep #$20
!al
    inx
    inx
    inx
    inx
    rtl

    +BACKLINK "cbit", 4
CBIT ; ( addr mask -- ) clear the masked bits at addr
    lda LSB+2, x
    sta W
    lda MSB+2, x
    sta W+2
    sep #$20
!as
    lda LSB, x                  ; mask
    eor #$ff
    and [W]
    sta [W]
    rep #$20
!al
    inx
    inx
    inx
    inx
    rtl

    +BACKLINK "fbit", 4
FBIT ; ( flag addr mask -- ) set masked bits if flag, else clear
    lda LSB+2, x
    sta W
    lda MSB+2, x
    sta W+2
    lda LSB+4, x                ; flag
    ora MSB+4, x
    beq +                       ; false -> clear
    sep #$20
!as
    lda LSB, x                  ; mask
    ora [W]
    sta [W]
    rep #$20
!al
    bra ++
+   sep #$20
!as
    lda LSB, x
    eor #$ff
    and [W]
    sta [W]
    rep #$20
!al
++  inx
    inx
    inx
    inx
    inx
    inx
    rtl

; ROLL is deferred to a Forth definition: the split ZP stack is indexed with
; zp,X (which has no zp,Y counterpart for LDA), so variable-depth access is
; awkward in assembly.  ( : roll ?dup if swap >r 1- recurse r> swap then ; )

    +BACKLINK "2rot", 4
TWO_ROT ; ( a b c d e f -- c d e f a b )
    lda LSB+10, x
    sta W
    lda MSB+10, x
    sta W+2
    lda LSB+8, x
    sta W2
    lda MSB+8, x
    sta W3
    lda LSB+6, x
    sta LSB+10, x
    lda MSB+6, x
    sta MSB+10, x
    lda LSB+4, x
    sta LSB+8, x
    lda MSB+4, x
    sta MSB+8, x
    lda LSB+2, x
    sta LSB+6, x
    lda MSB+2, x
    sta MSB+6, x
    lda LSB, x
    sta LSB+4, x
    lda MSB, x
    sta MSB+4, x
    lda W
    sta LSB+2, x
    lda W+2
    sta MSB+2, x
    lda W2
    sta LSB, x
    lda W3
    sta MSB, x
    rtl

    +BACKLINK "sleep", 5
SLEEP ; ( jiffies -- ) wait n VSYNC frames (kernel IRQ_FRAMES; 60 Hz)
    jsl BANK1 + kern_frames             ; KTMP = frame count (16-bit, wraps)
    lda KTMP                    ; start
    sta W
-   jsl BANK1 + kern_frames
    lda KTMP
    sec
    sbc W                       ; elapsed (wrap-safe 16-bit subtract)
    cmp LSB, x                  ; elapsed vs jiffies (16-bit is plenty)
    bcc -
    inx
    inx
    rtl

    +BACKLINK "ms", 2
MS ; ( u -- ) wait u milliseconds (SYSCTL ms timer $9F90)
; The timer ticks 1 kHz in BOTH SYSCTL[2] CPU speeds, so this is exact at
; 8 and 14 MHz alike - the old calibrated busy loop was 8 MHz-only. The
; $9F90 byte must be read FIRST: it latches bits 31:8, and $9F91-$9F93
; return that latch (a 16-bit lda reads $9F90 then $9F91 - right order).
    +VIO                        ; DBR = $00; planes and W are dp, still fine
    lda $9f90                   ; snapshot start, low word (latches 31:8)
    sta W
    lda $9f92                   ; high word, from that latch
    sta W+2
-   lda $9f90
    sec
    sbc W                       ; elapsed = now - start, wrap-safe 32-bit
    tay
    lda $9f92
    sbc W+2
    cmp MSB, x                  ; elapsed vs u, high word first
    bcc -
    bne +
    tya
    cmp LSB, x
    bcc -
+   +VIO_END
    inx
    inx
    rtl

    +BACKLINK "reboot", 6
REBOOT ; ( -- ) leave Forth: back to the kernel prompt (EXIT, status 0)
    jmp kern_exit

    +BACKLINK "ticks", 5
TICKS ; ( -- ud ) VSYNC frame counter as an unsigned double (16-bit, wraps)
    jsl BANK1 + kern_frames             ; KTMP = frames
    dex
    dex
    dex
    dex
    lda KTMP
    sta LSB+2, x                ; low cell
    stz MSB+2, x
    stz LSB, x                  ; high cell = 0
    stz MSB, x
    rtl
