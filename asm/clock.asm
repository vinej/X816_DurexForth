; CLOCK - X16 real-time clock (KERNAL clock_get/set_date_time, bank 0).
; TIME@ DATE@ SETTIME
; clock_get_date_time ($ff50) writes: $02=year(0-99) $03=month $04=day
;   $05=hours $06=minutes $07=seconds  (all below the $09+ parameter stack)

    +BACKLINK "time@", 5
TIME_FETCH ; ( -- h m s )
    stx W3
    jsl $ff50
    ldx W3
    dex
    dex
    dex
    lda $05
    sta LSB+2, x
    stz MSB+2, x
    lda $06
    sta LSB+1, x
    stz MSB+1, x
    lda $07
    sta LSB, x
    stz MSB, x
    rtl

    +BACKLINK "date@", 5
DATE_FETCH ; ( -- year month day )  year is the full 4-digit year
    stx W3
    jsl $ff50
    ldx W3
    dex
    dex
    dex
    lda $02
    clc
    adc #<2000
    sta LSB+2, x
    lda #>2000
    adc #0
    sta MSB+2, x
    lda $03
    sta LSB+1, x
    stz MSB+1, x
    lda $04
    sta LSB, x
    stz MSB, x
    rtl

    +BACKLINK "settime", 7
SETTIME ; ( year month day h m s -- )
    lda LSB, x                  ; seconds
    sta $07
    lda LSB+1, x                ; minutes
    sta $06
    lda LSB+2, x                ; hours
    sta $05
    lda LSB+3, x                ; day
    sta $04
    lda LSB+4, x                ; month
    sta $03
    lda LSB+5, x                ; year - 2000 (low byte, valid 2000-2099)
    sec
    sbc #<2000
    sta $02
    stx W3
    jsl $ff4d
    ldx W3
    inx
    inx
    inx
    inx
    inx
    inx
    rtl
