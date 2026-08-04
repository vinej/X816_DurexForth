# Stage C - jsl/rtl threading over banks $01-$04

Goal (X816_core doc/DUREXFORTH.md 2.3): program space = the full 256 KB of
single-cycle BRAM. Dictionary words are jsl-called and rtl-terminated;
HERE is a 24-bit pointer walking banks $01..$04; headers stay in bank $01.

The suite (run-tests.sh, green on stage B at a371a7c) is the gate for
every step.

## The convention deltas from stage B

- A compiled call is `jsl xt24` (4 bytes); TCE patches $22 -> $5C (jml),
  same length, same operand.
- Every BACKLINK word ends `rtl`. Internal helpers (PUTCHR, pushya,
  compile_a, .locals) keep jsr/rts - they are only ever called from
  bank-$01 asm.
- Asm words calling OTHER WORDS use `jsl WORD` (the callee rtl's).
  Mechanical sweep: every `jsr X` where X is a BACKLINK label.
- Return addresses on the CPU stack are 3 bytes ([lo][hi][bank], lo on
  top after the pushes). The self-juggling entry pattern becomes:
      pla / inc / sta W / sep #$20 / pla / sta W+2 / rep #$20
      ... body ...
      jml [W]
  (jml [dp] = $DC reads the full 3-byte target.)
- rw>/w>r become rl>/l>r moving 3 bytes; the cell naturally carries the
  bank in bits 16-23. LITS, DOES>, (?DO), (+LOOP), UNLOOP just rename.
- >R / R> / R@ / 2R@ / RDROP still move 4-byte cells (two pha/pla) - only
  their OWN entry/exit juggling changes per above. R@ offsets shift:
  return address is 3 bytes, so the cell sits at 4,s/6,s.
- BRANCH/ZBRANCH/LIT/LITW/LITC/LITXT: pull 3-byte ret; inline operands
  stay 16-bit; the target bank = the pulled bank (a definition never
  crosses a bank). Resume via jml [W].
- EXECUTE: cell -> W (3 bytes used), jml [W].
- DODOES: behavior pointer widens to 3 bytes; data field at xt+7; the
  created word is `jsl dodoes` + ptr24. CREATE/DOES>/`to` offsets follow.
- BACKLINK xt field: 3 bytes (!word xt + !byte ^xt... in stage C all asm
  words are bank $01 so ^xt = 1 - still store it, TO_XT reads 3).
  HEADER stores 3-byte xts for colon words; TO_XT returns the full flat
  xt; FIND/'/latestxt carry it in cells unchanged.
- HERE: a 24-bit pointer (keep HERE_PTR as lo16 + HERE_BANK byte, or one
  cell variable + [W]-based CCOMMA/COMMA/WCOMMA). COLON checks headroom:
  fewer than 1 KB left in the bank -> bump HERE to the next bank start
  (definitions never straddle; 1 KB max per definition, documented).
  The overflow check becomes: code capped at $04:FFFF; headers-down vs
  code-up check only applies inside bank $01.
- The runtime assembler (asm.fs): jsl,/jml,/rtl, opcodes; `code` words
  end `rtl,` (base.fs code words: 2/ or xor lshift rshift 2over 2swap d+
  value-generated bodies, doloop (do)/(loop)/j). `;`-EXIT's TCE and the
  `latest >xt jmp,` recursion sites become jml with the bank.
- VALUE shape gains nothing (it is data, patched by TO) but its generated
  body must end rtl,.
- Compiled variables/created words all produce flat cells already - no
  change to their VALUES, only to their code endings.

## Order of work

1. rstack.asm: rl>/l>r + re-derive R@/2R@ offsets. core.asm TO_R/R_TO/
   R_FETCH entry juggling to 3-byte form.
2. control.asm BRANCH/ZBRANCH/EXIT(TCE $22->$5C), compiler.asm LIT*/
   COMPILE_COMMA(jsl)/HERE 24-bit/CCOMMA-COMMA-WCOMMA via [W]/DODOES.
3. The BACKLINK rtl sweep + inter-word jsl sweep across all asm files
   (scripted, then hand-reviewed: the width-flow audit script from stage
   B extends to check call/exit pairing).
4. interpreter.asm EXECUTE/dowords/tick/TO_XT/HEADER 3-byte xts.
5. forth/: asm.fs new opcodes, base.fs code-word endings + does>/lits via
   rl>/l>r (rename only), doloop.fs endings.
6. Boot smoke -> loop battery -> suite -> negative control.
7. Free-space report: show per-bank; `unused` counts $01 code headroom +
   full banks $02-$04.

## Costs accepted

+1 byte per compiled call (~20% code size), ~2 cycles per call. Hot
paths (asm primitives' internals) are unchanged jsr/rts.
