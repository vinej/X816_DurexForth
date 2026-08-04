; Entered from sep #$20 (8-bit A) windows only - HEADER, FIND-NAME and
; READ_NUMBER all fold case while walking bytes. Assembled 8-bit to match.
!as
CHAR_TO_LOWERCASE ; ( a -- a )
    cmp #'A'
    bcc +
    cmp #'Z' + 1
    bcs +
    sbc #'A' - 'a' - 1
+   rtl
!al
