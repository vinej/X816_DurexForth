; PUSHYA 0 1 -1 START MSB LSB LATEST

; ACME assembler

!cpu 65816	; X816: 65816 native. The core is still 8-bit code (M=1, X=1);
		; asm/x816.asm is the only file that switches widths.
!to "build/forth.bin", plain	; X816 image: file offset = offset in bank $01
; No !ct: text/char literals stay ASCII. The X816 console is CP437, which
; agrees with ASCII in $20-$7E; the X16 control codes ($93 clear, $12 rvs)
; are gone - CON_PUTC interprets only $08, $0a and $0d.

; Opcodes.
OP_JMP = $4c
OP_JSR = $20
OP_RTS = $60
OP_INX = $e8

; CHROUT keys.
K_RETURN = $d
K_SPACE = ' '

; Addresses.
; X816 zeropage: the whole direct page is the program's (the kernel switches
; to its own D on entry and restores ours). The split parameter stack keeps
; its historical place; $E0-$EF is the kernel-crossing staging (x816.asm).
LSB = $41 ; low-byte stack placed in [$09 .. $40]
MSB = $79 ; high-byte stack placed in [$41 .. $78]
; Temporary work areas for words, two bytes each.  These MUST stay outside
; the X16 ROM's own zeropage segments (ZPKERNAL $80-$90, ZPDOS $91-$9B,
; ZPAUDIO $A7-$A8, ZPMATH $A9-$D3, ZPBASIC $D4-$FE): the C64 port had W at
; $8B, and any code word that parked the Forth stack pointer in W across a
; KERNAL call (open, chkin, ...) got it clobbered by CBDOS and crashed with
; a "stack" error.  $9C-$A6 is claimed by no ROM bank.
W = $9c
W2 = $9e ; must stay at W+2: some words use W..W2+1 as one 4-byte area
W3 = $a0
TIB = $600 ; text input buffer (X16 golden RAM; $600-$7ff = 512 bytes)
; TIB grows upward as nested INCLUDEs stack their current lines, so it gets
; the top 512-byte run of golden RAM all to itself.  It must NOT sit at $400:
; FIND_BUFFER lives at $480 and the HOLD area at $500-$5fc, and a nested
; include whose accumulated lines crossed $480 had its still-unparsed text
; overwritten by every word lookup (garbled-token errors in long-lined,
; deeply-included files).
PROGRAM_BASE = $801
;HERE_POSITION = $801 + assembled program (defined below)
WORDLIST_BASE = $9eff ; historical top; bank $01 has no I/O, could rise later
; PUTCHR is a routine in x816.asm now (CON_PUTC via the kernel), not a ROM
; address - callers are unchanged.

; Parameter Stack
; ---------------

; The x register contains the current stack depth.
; It is initially 0 and decrements when items are pushed.
; The parameter stack is placed in zeropage to save space.
; (E.g. lda $FF,x takes less space than lda $FFFF,x)
; We use a split stack that store low-byte and high-byte
; in separate ranges on the zeropage, so that popping and
; pushing gets faster (only one inx/dex operation).

X_INIT = 0

; Dictionary
; ----------

; Grows backwards from WORDLIST_BASE. Each entry has one
; byte of flag bits + name length, followed by the bytes of
; the word's name, and a two-byte "execution token," the
; address of its code. The address of a dictionary entry is
; called the word's "name token."

STRLEN_MASK = $1f
F_IMMEDIATE = $80 ; interpret the word even in compiler STATE
F_NO_TAIL_CALL_ELIMINATION = $40
; Exempt this word from tail call elimination i.e.
; "jsr WORD + rts" will not be replaced by "jmp WORD".

* = WORDLIST_BASE

!byte 0 ; zero name length = end of dictionary.

!set __LATEST = WORDLIST_BASE
!macro BACKLINK .name , .namesize {
    !set .xt = *
    * = __LATEST - len(.name) - 3
    !set __LATEST = *
    !byte .namesize
    !text .name
    !word .xt
    * = .xt
}

; Program Space
; -------------

; Main assembly starts at PROGRAM_BASE, then the assembled
; compiler begins writing at HERE_POSITION, to which we
; assemble a startup routine that we're okay with being
; overwritten.

; PLACEHOLDER_ADDRESSes are assembled into the instruction
; stream then self-modified by the running program. Low
; byte must be 0 for situations where the Y register is
; used instead.
PLACEHOLDER_ADDRESS = $1200

; X816 image header: the "X816" magic at $01:0000, entry point at $01:0004 -
; the format boot/boot.s and the kernel's EXEC both recognise. The loader
; drops the file at $01:0000, so file offsets are bank-$01 offsets.
* = $0000
!text "X816"
; Entered in native mode, M=0/X=0, D=$0000. Set the return stack 16-bit
; (an 8-bit txs would zero SH), drop to 8-bit registers, and make this
; bank the data bank so absolute references reach the image.
    rep #$10
!rl
    ldx #RSTACK_TOP
    txs
    sep #$30
!rs
    phk
    plb
    jmp COLD

* = PROGRAM_BASE

COLD
    ldx #X_INIT

    jsr quit_reset

    jsr PAGE

_START = * + 1
    jsr load_base

; Word Definitions
; ----------------

!macro VALUE .word {
    lda	#<.word
    ldy	#>.word
    jmp pushya
}

    +BACKLINK "pushya", 6
pushya
    dex
    sta	LSB, x
    sty	MSB, x
    rts

    +BACKLINK "0", 1
ZERO
    lda	#0
    tay
    jmp pushya

    +BACKLINK "1", 1
ONE
    +VALUE 1

    +BACKLINK "2", 1
    +VALUE 2

    +BACKLINK "-1", 2
MINUS_ONE
    lda	#-1
    tay
    jmp pushya

; START - points to the code of the startup word.
    +BACKLINK "start", 5
    +VALUE	_START

    +BACKLINK "msb", 3
    +VALUE	MSB

    +BACKLINK "lsb", 3
    +VALUE	LSB

!src "x816.asm"
!src "core.asm"
!src "math.asm"
!src "move.asm"
!src "interpreter.asm"
!src "compiler.asm"
!src "control.asm"
!src "io.asm"
!src "lowercase.asm"
!src "exception.asm"
!src "format.asm"
!src "video.asm"
!src "sprite.asm"
!src "tile.asm"
!src "palfx.asm"
; X816: input.asm (KERNAL joystick/mouse), clock.asm (X16 RTC), irq.asm
; (chains the KERNAL CINV vector) and sysx.asm (ROM I2C/charset/keymap/
; monitor) are parked - they come back on the kernel APIs (IRQ_SET slots,
; CON_*) in the platform-hooks phase. See X816_core doc/DUREXFORTH.md.
!src "coreadd.asm"
!src "rstack.asm"

BOOT_STRING
!src "../build/version.asm"
PRINT_BOOT_MESSAGE
    ldx #0
-   lda BOOT_STRING,x
    jsr PUTCHR
    inx
    cpx #(PRINT_BOOT_MESSAGE - BOOT_STRING)
    bne -
    jsr CR
    ldx #X_INIT
    jmp QUIT

; LATEST - points to the most recently defined dictionary word.

    +BACKLINK "latest", 6
LATEST
LATEST_LSB = * + 1
LATEST_MSB = * + 3
    +VALUE	__LATEST

HERE_POSITION ; everything following this will be overwritten!

; X816: the disk INCLUDED of the "base" sources is gone with disk.asm.
; Until the kernel FS_* file words exist (PORTING plan phase 7), boot goes
; straight to the prompt with the assembled words only; base.fs and friends
; will be embedded in the image first, then loaded from FAT32.
load_base
    lda #<PRINT_BOOT_MESSAGE
    sta _START
    lda #>PRINT_BOOT_MESSAGE
    sta _START+1
    jmp PRINT_BOOT_MESSAGE
