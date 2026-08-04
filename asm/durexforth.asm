; PUSHYA 0 1 -1 START MSB LSB LATEST

; ACME assembler

!cpu 65816	; X816: 65816 native, 32-bit cells, LONG threading (stage C).
;
; STAGE C: ONE LONG WORLD. Every call in the image is jsl, every return
; rtl, every tail-transfer jml, every indirect dispatch jml [dp]. Mixed
; return widths across tail-jumps were the alternative, and that way lies
; a minefield of one-byte stack misalignments. Compiled code lives in
; banks $01-$04 (256 KB of single-cycle BRAM); a definition never
; straddles a bank, so in-word branch operands stay 16-bit and re-attach
; the bank carried by the return address.
;
; THE WIDTH CONVENTION - the one decision every primitive honours
; (X816_core doc/DUREXFORTH.md 4.2: get this wrong and the failures look
; random). At every word's ENTRY and EXIT, and at every point where
; compiled code runs:
;
;     M = 0   (16-bit accumulator/memory)
;     X = 1   (8-bit index registers)
;
; A word that needs byte operations goes `sep #$20` and MUST `rep #$20`
; before rtl. A word that needs a 16-bit index saves and restores the
; width. The kernel crossings in x816.asm keep their own discipline
; (rep #$30 around jsl) and re-enter this convention on the way out.
;
; Cells are 32 bits, held in two direct-page WORD planes indexed by X,
; which steps by TWO (dex/dex pushes, inx/inx pops). The historical names
; stay: LSB is the LOW WORD plane (bits 0-15), MSB the HIGH WORD plane
; (bits 16-31). `lda LSB, x` under M=0 reads a cell's low word in one
; instruction - the C64 split-byte-stack shape, widened, exactly as
; DUREXFORTH.md 2.2 planned. Neighbouring items are +2/-2, not +1.
;
; Addresses in cells are FLAT 24-bit (the whole point of stage B:
; DUREXFORTH.md 2.1) - the dictionary's cells carry $01xxxx, TIB carries
; $01x600, and @/! dereference through [W] long addressing. Compiled code
; still targets bank $01 with plain 16-bit jsl (2.3's one-bank ceiling).
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
; to its own D on entry and restores ours). $E0-$EF is the kernel-crossing
; staging (x816.asm).
;
; The split stack keeps its SIZE but neither the C64 bytes nor the C64
; indexing trick, for two hard-won reasons:
;
; 1. The 6502 original set LSB=$41/X_INIT=0 and relied on zp,x wrapping
;    within the page, so the first push (dex -> X=$FF) landed at
;    $41+$FF mod 256 = $40. A native-mode 65816 wraps direct-page indexing
;    within BANK 0, not the page (X816_core doc/MEMORY_MAP.md) - the same
;    push landed at $0140 while reads stayed at $78, and the REPL died on
;    its first line. So the planes' BASES are the bottom of their ranges
;    and X counts down from X_INIT with no wrap: empty X_INIT, full 0.
;
; 2. The C64 byte range $09-$40 is NOT available here. KERNEL.md 3.1
;    reserves $00-$21 for the C-runtime pseudo-registers and $22-$31 for
;    x16lib scratch - and that is not advisory: the kernel's VSYNC cursor
;    handler runs C code at interrupt time with D=$0000, using those very
;    bytes as its pseudo-registers and C stack pointer. A Forth stack at
;    $09-$40 and the handler corrupt each other once per frame, with the
;    victim chosen by interrupt phase - failures that look random and
;    move every run. The planes live in the free region at $32+.
; Stage B planes: 38 cells of 32 bits. LSB = LOW WORD plane, MSB = HIGH
; WORD plane (the names are historical, the width is not). X steps by 2.
; $CA-$D3 is a deliberate GUARD BAND: the underflow reporters run with X
; a cell or two ABOVE the empty mark, and those out-of-range plane writes
; must land in dead space - stage A had the same slack by accident, and
; without it the exception path clobbers W while reporting.
LSB = $32 ; low-word plane:  [$32 .. $7D]
MSB = $7e ; high-word plane: [$7E .. $C9]
; Temporary work areas for words, FOUR bytes each now: W holds a 24-bit
; pointer (byte 3 is padding kept zero) so @/! can go `lda [W],y` and
; reach the whole 16 MB. Some words use W..W2 as one 8-byte area - W2
; must stay at W+4.
W = $d4
W2 = $d8 ; must stay at W+4
W3 = $dc
TIB = $600 ; text input buffer (X16 golden RAM; $600-$7ff = 512 bytes)
; TIB grows upward as nested INCLUDEs stack their current lines, so it gets
; the top 512-byte run of golden RAM all to itself.  It must NOT sit at $400:
; FIND_BUFFER lives at $480 and the HOLD area at $500-$5fc, and a nested
; include whose accumulated lines crossed $480 had its still-unparsed text
; overwritten by every word lookup (garbled-token errors in long-lined,
; deeply-included files).
PROGRAM_BASE = $801
;HERE_POSITION = $801 + assembled program (defined below)
; The dictionary top. The C64/X16 stopped at $9EFF below the I/O page;
; bank $01 has no I/O, so the only ceiling is what EXEC can load -
; X816_EXEC_MAX is $FF00 bytes, putting the top at $FEFF and buying the
; dictionary 24 KB the original never had. The test suite needs it: the
; ANS core+ext runs compile ~30 KB of definitions.
WORDLIST_BASE = $feff
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

X_INIT = $4c ; 38 cells x 2 bytes of low word - X steps by 2 per cell.
             ; (The C64's 56 cells shrink: two word planes, the guard band
             ; and the wider W areas must all fit in the direct page at
             ; $32-$DF. ANS asks for 32; the suite's deepest test uses
             ; fewer than 20.)

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
; "jsl BANK1 + WORD + rtl" will not be replaced by "jmp WORD".

* = WORDLIST_BASE

!byte 0 ; zero name length = end of dictionary.

!set __LATEST = WORDLIST_BASE
; Stage C: the xt field is 3 bytes (24-bit flat address). Assembled words
; all live in bank $01; compiled words land wherever HERE walks.
!macro BACKLINK .name , .namesize {
    !set .xt = *
    * = __LATEST - len(.name) - 4
    !set __LATEST = *
    !byte .namesize
    !text .name
    !word .xt
    !byte 1
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
; Entered in native mode, M=0/X=0 - and that is ALL runtime/exec.s
; guarantees. Establish everything else: D is the SHELL's direct page at
; handover (the kernel's $2000 - using it would corrupt kernel state), the
; handover blob does sei and never cli, and S and DBR are wherever the
; shell left them. The return stack is set 16-bit (an 8-bit txs would zero
; SH and put it in the direct page).
    rep #$30
!al
!rl
    lda #0
    tcd
    ldx #RSTACK_TOP
    txs
    sep #$10
!rs
    rep #$20
!al
    ; The stage-B convention starts here: M=0, X=1, for every word and all
    ; compiled code (see the header). x816.asm crossings re-establish it.
    phk
    plb
    cli
    jmp COLD

* = PROGRAM_BASE

COLD
    ldx #X_INIT

    jsl BANK1 + quit_reset

    jsl BANK1 + PAGE

_START = * + 1
    jsl BANK1 + load_base

; Word Definitions
; ----------------

; The bank the image lives in. Cells hold FLAT addresses: pushing the
; address of anything assembled in this file means pushing BANK1 + label.
; (The label alone is still what absolute operands want - only the value
; that lands in a CELL carries the bank.)
BANK1 = $010000

; The VALUE shape: two 16-bit immediates, so any 32-bit cell fits.
; TO patches the low word at xt+3 and the high word at xt+8 - the
; Forth-level VALUE generator (base.fs) emits the identical layout.
!macro VALUE .val {
    dex
    dex
    lda	#.val & $ffff
    sta LSB, x
    lda	#(.val >> 16) & $ffff
    sta MSB, x
    rtl
}

; pushya - push the cell A:Y (A = low word, Y = high byte; bits 24-31
; zero). The historical name, one width up: every address on this machine
; fits 24 bits, so Y-as-bank covers all constant pushes.
    +BACKLINK "pushya", 6
pushya
    dex
    dex
    sta	LSB, x
    sep #$20
!as
    tya
    sta MSB, x
    stz MSB+1, x
    rep #$20
!al
    rtl

; PUSHA - push A as a cell, high word zero. The workhorse for 16-bit
; results computed in A.
PUSHA
    dex
    dex
    sta LSB, x
    stz MSB, x
    rtl

; pushbank1 - push A as a bank-$01 flat address: cell = BANK1 + A.
; Replaces stage A's `lda #< / ldy #> / jsl BANK1 + pushya` for in-image
; addresses.
pushbank1
    dex
    dex
    sta LSB, x
    lda #(BANK1 >> 16)
    sta MSB, x
    rtl

    +BACKLINK "0", 1
ZERO
    lda	#0
    jmp PUSHA

    +BACKLINK "1", 1
ONE
    +VALUE 1

    +BACKLINK "2", 1
    +VALUE 2

    +BACKLINK "-1", 2
MINUS_ONE
    ; NOT pushya: its Y is a bank BYTE, and $FF zero-extends to $00FF -
    ; a "-1" with a positive high word broke every POSTPONE of a
    ; non-immediate word before this was traced.
    lda	#$ffff
    dex
    dex
    sta LSB, x
    sta MSB, x
    rtl

; START - points to the code of the startup word (a flat address).
    +BACKLINK "start", 5
    +VALUE	BANK1 + _START

; The planes and scratch live in the direct page = bank $00: their flat
; addresses have no bank byte.
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
!src "fs.asm"
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
-   sep #$20
!as
    lda BOOT_STRING,x
    rep #$20
!al
    and #$ff
    jsl BANK1 + PUTCHR
    inx
    cpx #(PRINT_BOOT_MESSAGE - BOOT_STRING)
    bne -
    jsl BANK1 + CR
    ldx #X_INIT
    jmp QUIT

; LATEST - points to the most recently defined dictionary word.
; LATEST_PTR is the 16-bit in-bank pointer inside the VALUE's immediate.

    +BACKLINK "latest", 6
LATEST
LATEST_PTR = * + 3
    +VALUE	BANK1 + __LATEST

HERE_POSITION ; everything following this will be overwritten!

; Boot: INCLUDE the "base" sources off the card through the kernel FS_*
; API (asm/fs.asm). If BASE is missing the include throws -37, the error
; prints, and QUIT still delivers a working prompt with the assembled
; words only - a card problem must not cost the machine its REPL.
load_base
    lda #PRINT_BOOT_MESSAGE
    sta _START
    dex
    dex
    dex
    dex
    lda #basename
    sta LSB+2, x
    lda #(BANK1 >> 16)
    sta MSB+2, x
    lda #(basename_end - basename)
    sta LSB, x
    stz MSB, x
    ; the include chain's final rtl needs a 3-byte return: bank, then PC
    sep #$20
!as
    lda #1
    pha
    rep #$20
!al
    lda #QUIT-1
    pha
    jmp INCLUDED

basename
!text	"base"
basename_end
