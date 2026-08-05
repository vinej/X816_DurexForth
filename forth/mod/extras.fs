\ EXTRAS - the legacy dictionary and compiler words that base.fs does not
\ carry (DICTIONARY.TXT, FLOW.TXT AHEAD).
\ Cart: NEEDS EXTRAS      SD card: INCLUDE EXTRAS
\
\ THIS FILE USED TO REDEFINE ELEVEN WORDS BASE.FS ALREADY HAS, and every
\ one of its copies was the 16-bit-cell version from before stage B:
\
\   >body   was  5 +      the CREATE shape here is jsl dodoes (4 bytes)
\                         plus a 3-byte DOES> pointer, so the body is at
\                         xt+7. Writing at +5 lands INSIDE that pointer.
\   field:  was  2 +field a cell is FOUR bytes now
\   begin-structure, end-structure, +field, cfield:, defer, defer!,
\   defer@, is, action-of - all duplicated, all stale.
\
\ The damage was not subtle once it ran: DEFER! stored an execution token
\ two bytes early, over the DOES> pointer, so the first call to a deferred
\ word jumped into the middle of its own header and hung the machine.
\ testextras found it the first time it was ever run.
\
\ This is the same mistake compat.fs made - shadowing base.fs with weaker
\ copies - and it has the same answer: ONE WORD, ONE DEFINITION. The words
\ below are the ones base.fs genuinely does not define.
\
\ (The old header claimed the file was "self-contained" so it could work on
\ a cartridge without COMPAT. base.fs defines all eleven unconditionally,
\ so there is nothing to be self-contained about.)

decimal

\ --- compiler helpers -------------------------------------------------------------
\ forward branch, close with THEN.  $4C c, = jmp opcode: base's jmp, is
\ SHADOWED by the assembler's jmp, ( addr -- ) once asm.fs is loaded!
\ w, not , : a jmp operand is 16 bits and THEN patches two bytes. A 4-byte
\ cell left two zeros in the code stream, which the CPU reads as BRK - the
\ same stage-B leftover base.fs's OF had.
: ahead ( -- orig ) $4c c, here 0 w, ; immediate
: ?comp  ( -- ) state @ 0= if -14 throw then ;  \ abort unless compiling
: ?stack ( -- ) depth 0< if -4 throw then ;     \ abort on stack underflow
: compile ( "name" -- ) postpone postpone ; immediate   \ legacy COMPILE
: [compile] ( "name" -- ) ' compile, ; immediate        \ force-compile
: ," ( "ccc<">" -- )                            \ counted string into HERE
  '"' parse dup c, here over allot swap move ;

\ --- forget ------------------------------------------------------------------------
\ Remove a word and everything defined after it.  Do not forget core words,
\ words below a module, or anything whose buffers something else still uses.
\ The stride is LEN + 4, not len + 3: an entry is the length byte, the name,
\ and a THREE-byte xt (interpreter.asm walks it with `adc #4`). Headers grow
\ DOWNWARD from $FEFF, so the entry before this one sits that far ABOVE it.
\ Off by one, LATEST lands inside the previous header, its length byte reads
\ as part of a name, and the whole chain is lost - FIND then fails for every
\ word in the system, which looks nothing like a FORGET bug.
: forget ( "name" -- )
  parse-name 2dup find-name ?dup 0= if notfound then nip nip
  dup >xt to here
  dup c@ $1f and + 4 + to latest ;
