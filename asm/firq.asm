; =====================================================================
; firq.asm -- running Forth from an interrupt.
;
; The kernel dispatches one slot per SOURCE (X816_Calypsi runtime/kirq.s,
; doc/KERNEL.md 5.6). This file puts a Forth execution token behind four
; of them - VSYNC, raster line, sprite collision and audio FIFO low - so
; that `' tick irq` arms a colon word on the vertical blank.
;
; THE ENVIRONMENT THE KERNEL HANDS US, and what has to change:
;
;   given                       Forth wants
;   M=0, X=0 (16-bit A/X/Y)     M=0, X=1  -- the data stack pointer is X
;                               and every plane access is `LSB, x`
;   D   = $0000                 the same; the planes are absolute dp
;   DBR = $00                   DBR = $01 -- what every absolute data
;                               reference in this Forth assumes
;   reached by jsl              so this must finish with rtl
;
; brk_handler abandons its frame instead of returning, and says in place
; that the shape must not be copied here: the IRQ path ACKNOWLEDGES, and
; a handler that walked out of the dispatcher would leave the source
; unacknowledged and the machine wedged. Everything below returns.
;
; THE DATA STACK POINTER IS THE HARD PART, and the answer here differs
; from brk_handler's on purpose. An abort can take the depth from its own
; CATCH frame; an interrupt has no such frame and must run on the stack
; it landed on. The interrupted X is in the dispatcher's frame at 11,s.
; Reading it COUPLES this file to kirq.s's KPROLOGUE + kirq_call shape,
; which is the same coupling nmi_handler documents a few pages up - and
; the same layout, so if one breaks the other breaks with it:
;
;   1-3,s rtl return, 4-5,s jsr return, 6-7,s D, 8,s B, 9-10,s Y,
;   11-12,s X, 13-14,s A, then the CPU frame: 15,s P, 16-17,s PC, 18,s PBR
;
; Nothing here writes that copy back. The dispatcher's KEPILOGUE pulls X
; for us, so a handler that leaves rubbish on the stack loses it at the
; rti and the interrupted word gets its own pointer back untouched. What
; the handler pushed lands ABOVE the interrupted stack pointer, in space
; that word had already finished with.
;
; A HANDLER MUST NOT THROW. There is no CATCH between here and the
; dispatcher, so an abort would unwind straight through kernel state that
; is half torn down. Wrap anything doubtful in CATCH inside the handler.
;
; A HANDLER MUST NOT ENABLE INTERRUPTS, and must save VERA's CTRL and
; address ports if it touches them - kirq.s deliberately leaves those
; alone so an interrupt cannot corrupt a console write in progress, and
; a handler that reprograms them without restoring undoes that guarantee.
; =====================================================================

FIRQ_SLOTS = 4

; One 32-bit cell per slot: the low word is the xt's address, the high
; word its bank. Zero means disarmed, and zero is safe to mean that -
; nothing can execute at $00:0000, which is the direct page.
firq_xt
    !fill FIRQ_SLOTS * 4, 0

; Scratch saved across a handler. ONE static copy is enough and does not
; need a stack: the dispatcher runs with interrupts masked and a handler
; is forbidden from enabling them, so this cannot be re-entered.
firq_w
    !fill 12, 0

; --- the four entry points the kernel calls --------------------------
; Each only names its slot; the frame must be identical at firq_enter for
; every one of them, so these are branches and not calls.
firq_vsync
    lda #0 * 4
    bra firq_enter
firq_line
    lda #1 * 4
    bra firq_enter
firq_sprcol
    lda #2 * 4
    bra firq_enter
firq_aflow
    lda #3 * 4

firq_enter
    ; A = the slot's offset into firq_xt. Still M=0/X=0 here.
    pha                         ; park it; the frame below accounts for this
    lda 13, s                   ; 11,s + our 2 = the interrupted X
    sep #$10
!rs
    phk
    plb                         ; DBR = $01: absolute references are legal now
    tax                         ; ...and X is Forth's data stack pointer again

    ; Save what a Forth word will trample. W/W2/W3 are the primitives'
    ; working pointers, and the interrupted word is very likely holding a
    ; value in one of them: `@` is `lda [W],y`, and an interrupt between
    ; the store to W and the load through it would come back to a pointer
    ; belonging to whatever the handler did instead.
    lda W
    sta firq_w
    lda W + 2
    sta firq_w + 2
    lda W2
    sta firq_w + 4
    lda W2 + 2
    sta firq_w + 6
    lda W3
    sta firq_w + 8
    lda W3 + 2
    sta firq_w + 10

    pla                         ; the slot offset, 16 bits as pushed
    tay                         ; 8-bit Y: offsets are 0, 4, 8, 12
    lda firq_xt, y
    ora firq_xt + 2, y
    beq firq_done               ; disarmed between the IRQ and here

    ; Push the xt as an ordinary cell and let EXECUTE do the jumping: it
    ; already knows how to reach a 24-bit target and how to come back.
    dex
    dex                         ; a cell is TWO bytes of X, one per plane
    lda firq_xt, y
    sta LSB, x
    lda firq_xt + 2, y
    sta MSB, x
    jsl BANK1 + EXECUTE

firq_done
    lda firq_w
    sta W
    lda firq_w + 2
    sta W + 2
    lda firq_w + 4
    sta W2
    lda firq_w + 6
    sta W2 + 2
    lda firq_w + 8
    sta W3
    lda firq_w + 10
    sta W3 + 2
    rep #$30                    ; back to the width the dispatcher expects
!rl
    rtl

; ---------------------------------------------------------------------
; (irq!) ( xt slot -- ) - arm slot with xt, or disarm it with xt = 0.
;
; Installs this file's trampoline in the kernel slot the first time that
; slot is used, and leaves it there: the trampoline costs nothing when
; the cell is zero, and taking it out again would race an interrupt
; already on its way in.
; ---------------------------------------------------------------------
    +BACKLINK "(irq!)", 6
FIRQ_SET
    lda LSB, x                  ; the slot number
    and #$ff
    cmp #FIRQ_SLOTS
    bcc +
    lda #-9                     ; invalid memory address: no such slot
    jmp throw_a
+   asl                         ; a cell each
    asl
    sta W3                      ; the table offset, kept across the install

    ; store the xt
    inx
    inx                         ; drop the slot number
    phy
    ldy W3
    lda LSB, x
    sta firq_xt, y
    lda MSB, x
    sta firq_xt + 2, y
    ply
    inx
    inx                         ; drop the xt

    ; point the kernel at our trampoline for this slot
    lda W3
    lsr                         ; W3 is slot*4; the table below is words,
    tay                         ; so slot*2 is the index we want
    lda firq_entries, y
    sta KTMP
    lda #^(BANK1 + firq_vsync)  ; every trampoline is in bank $01
    sta KTMP2
    tya
    lsr                         ; Y is slot*2; the kernel wants the slot
    jsl BANK1 + kern_irq_set
    rtl

; The trampoline addresses, indexed by slot. A table rather than four
; compares: the slot number is already an index by the time we get here.
firq_entries
    !word firq_vsync
    !word firq_line
    !word firq_sprcol
    !word firq_aflow
