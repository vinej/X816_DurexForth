;;;
;;; fpengine -- SuperBasic's floating point, callable from durexForth.
;;;
;;; Copyright (C) 2026 Jean-Yves Vinet
;;; Dual-licensed: GPLv3 and MIT (the engine sources carry the same
;;; dual license precisely so they can flow into this MIT project).
;;;
;;; THE SAME IMPLEMENTATION, LITERALLY. This file assembles
;;; ../../X816_SuperBasic/basic816/src/X816/floats_x816.s and
;;; transcendentals_x816.s UNCHANGED (build.sh passes the include
;;; path), plus four routines build.sh extracts verbatim from the
;;; portable floats.s at build time (glue.gen.s) -- so there is no
;;; second copy of any algorithm to drift. What this file adds is only
;;; the crossing: a jump table, an ABI block, and a THROW that unwinds
;;; to the caller instead of jumping into BASIC's error handler.
;;;
;;; THE ABI (all in bank $00; the kernel's application window, which
;;; durexForth does not otherwise touch):
;;;
;;;   $003020  A1   dword  first operand and the result, IEEE-754 single
;;;   $003028  A2   dword  second operand
;;;   $003040  ERR  byte   0 = ok; else the SuperBasic error code
;;;                        (13 overflow, 15 div0, 23 domain)
;;;   $005000  the jump table, one JMP every 3 bytes, order below
;;;
;;; Call an entry with JSL to $00:5000+3n. Registers are clobbered, the
;;; caller's D, DBR and P are preserved. The engine's own state lives in
;;; the $3000 direct page and the $4B00 scratch page, exactly where
;;; SuperBasic keeps it -- neither program is resident when the other
;;; runs, and durexForth's bank-0 footprint stops at $07FF.
;;;
;;;   0 FADD   A1 := A1+A2        8 FEXP   A1 := e^A1
;;;   1 FSUB   A1 := A1-A2        9 FSIN   A1 := sin A1 (radians)
;;;   2 FMUL   A1 := A1*A2       10 FCOS
;;;   3 FDIV   A1 := A1/A2       11 FTAN
;;;   4 ITOF   A1 int32 -> float 12 FATAN
;;;   5 FTOI   A1 float -> int32 13 FASIN
;;;   6 FSQR   A1 := sqrt A1     14 FACOS
;;;   7 FLN    A1 := ln A1
;;;

; ---- the constants the engine sources expect --------------------------
SYSTEM_C256  = 2
SYSTEM_X816  = 3
SYSTEM       = SYSTEM_X816
TRACE_LEVEL  = 0

TYPE_INTEGER = 0
TYPE_FLOAT   = 1

ERR_OVERFLOW = 13
ERR_DIV0     = 15
ERR_DOMAIN   = 23

; ---- the engine's direct page (D = $3000 while it runs) ---------------
; FULL ADDRESSES, not offsets, with .dpage teaching the assembler that
; D = $3000: plain accesses then assemble direct-page and @w/@l-forced
; ones assemble absolute -- BOTH reaching the same byte, exactly as in
; BASIC. Defined as offsets, FTOI's "@w ARGUMENT1" stored to $00:0020
; and the conversion appeared to pass its argument through untouched.
SCRATCH      = $3010        ; dword: ITOF's exponent/negative locals
SCRATCH2     = $3014        ; dword: kept for symmetry with BASIC
MARG4        = $3018        ; word: FP_POW10's power
MARG6        = $301A        ; word: FP_POW10's negate flag
ARGUMENT1    = $3020        ; dword: operand/result (ABI: $003020)
ARGTYPE1     = $3024        ; byte
ARGUMENT2    = $3028        ; dword: second operand (ABI: $003028)
ARGTYPE2     = $302C        ; byte
             .dpage $3000

ENG_DP       = $3000
ENG_ERR      = $003040      ; byte, long-addressed so THROW works at any D
ENG_SPS      = $003042      ; word: the stack pointer the fail path restores

; ---- bank-0 scratch the engine sources address @l ----------------------
; floats_x816.s defines its own FP_S1..FP_T ($4B08-$4B19). These are the
; ones mmap_x816.s provides in BASIC, copied verbatim:
MATHR        = $004B00      ; 8 bytes: ints_x816.s accumulator
FP_SX        = $004B70      ; dword: FP_SQR's operand
FP_SY        = $004B74      ; dword: FP_SQR's estimate
FP_TX        = $004B80      ; dword: trig -- the original argument
FP_TR        = $004B84      ; dword: the reduced argument
FP_TU        = $004B88      ; dword: r*r
FP_TS        = $004B8C      ; dword: TAN's numerator
FP_TC        = $004B90      ; dword: TAN's denominator
FP_TQ        = $004B94      ; word: quadrant
FP_EX        = $004B98      ; dword: LN/EXP -- the argument
FP_EU        = $004B9C      ; dword: the polynomial's variable
FP_EN        = $004BA0      ; word: the power of two taken out
FP_AY        = $004BA4      ; dword: ATAN's reduction constant
FP_AV        = $004BA8      ; dword: ATAN's untouched argument
FP_ASGN      = $004BAC      ; word: ATAN's argument sign

; ---- the macros the engine sources use ---------------------------------
setas       .macro
            SEP #$20
            .as
            .endm

setal       .macro
            REP #$20
            .al
            .endm

setaxl      .macro
            REP #$30
            .al
            .xl
            .endm

setxl       .macro
            REP #$10
            .xl
            .endm

CALL        .macro
            JSR \1
            .endm

RETURN      .macro
            RTS
            .endm

; (BGE and BLT are 64tass built-in aliases for BCS/BCC -- the engine
;  sources use them directly and defining macros over them is an error.)

TRACE       .macro
            .endm

TRACE_A     .macro
            .endm

TRACE_L     .macro
            .endm

TRACE_X     .macro
            .endm

; THE THROW THAT COMES BACK. BASIC's THROW jumps into its error
; machinery and never returns; here an error must surface to Forth as a
; result. The shim stores the code, restores the stack pointer the
; wrapper saved at entry -- unwinding however deep the failure was --
; and returns through the wrapper's own epilogue.
THROW       .macro
            SEP #$20
            .as
            LDA #\1
            STA @l ENG_ERR
            JML ENG_FAIL
            .endm

; ---- the jump table -----------------------------------------------------
* = $5000

            JMP W_FADD              ;  0
            JMP W_FSUB              ;  1
            JMP W_FMUL              ;  2
            JMP W_FDIV              ;  3
            JMP W_ITOF              ;  4
            JMP W_FTOI              ;  5
            JMP W_FSQR              ;  6
            JMP W_FLN               ;  7
            JMP W_FEXP              ;  8
            JMP W_FSIN              ;  9
            JMP W_FCOS              ; 10
            JMP W_FTAN              ; 11
            JMP W_FATAN             ; 12
            JMP W_FASIN             ; 13
            JMP W_FACOS             ; 14

; Every wrapper: save the caller's world, give the engine its D and a
; DBR of 0 (its absolute addresses live in bank 0), remember where the
; stack stands so THROW can come home, run, restore. The pushes are
; P, B, D in that order; ENG_FAIL pulls them in reverse after resetting
; S, which is why the two must never diverge.
WRAP        .macro
            PHP
            PHB
            PHD
            setaxl
            PHX                     ; X is the CALLER'S data stack pointer.
            PHY                     ;  BASIC's OP_FP_* primitives preserve
                                    ;  X and Y by documented discipline; the
                                    ;  transcendentals do not (FP_SQR counts
                                    ;  its Newton steps in X), and a Forth
                                    ;  whose X is the stack dies at the
                                    ;  first FSQRT without these.
            LDA #ENG_DP
            TCD
            setas
            LDA #0
            PHA
            PLB
            STA @l ENG_ERR
            LDA #TYPE_FLOAT
            STA ARGTYPE1
            STA ARGTYPE2
            setal
            TSC
            STA @l ENG_SPS
            JSR \1
            setaxl                  ; The pulls must match the pushes: the
            PLY                     ;  engine returns at whatever width its
            PLX                     ;  last routine ended
            PLD
            PLB
            PLP
            RTL
            .endm

W_FADD      WRAP OP_FP_ADD
W_FSUB      WRAP OP_FP_SUB
W_FMUL      WRAP OP_FP_MUL
W_FDIV      WRAP OP_FP_DIV
W_ITOF      WRAP ITOF
W_FTOI      WRAP FTOI
W_FSQR      WRAP FP_SQR
W_FLN       WRAP FP_LN
W_FEXP      WRAP FP_EXP
W_FSIN      WRAP FP_SIN
W_FCOS      WRAP FP_COS
W_FTAN      WRAP FP_TAN
W_FATAN     WRAP FP_ATAN
W_FASIN     WRAP FP_ASIN
W_FACOS     WRAP FP_ACOS

ENG_FAIL    setaxl
            LDA @l ENG_SPS
            TCS
            PLY
            PLX
            PLD
            PLB
            PLP
            RTL

; ---- the engine itself ---------------------------------------------------
; glue.gen.s is EXTRACTED BY build.sh from SuperBasic's portable
; floats.s: ITOF, FTOI, FP_COMPARE, FARG1EQ0 and the 1.0/-1.0
; constants, between their .proc/.pend markers. Do not edit it; edit
; the original and rebuild.
; The engine sources assume their callers' widths, and each file's
; internal SEP/REP walk leaves the assembler in whatever state its last
; routine ended -- so the assumption is restated before every include,
; which is exactly what BASIC's build order happens to do for them.
            .al
            .xl
.include "glue.gen.s"
            .al
            .xl
.include "floats_x816.s"
            .al
            .xl
.include "transcendentals_x816.s"
