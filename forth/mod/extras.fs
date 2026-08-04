\ EXTRAS - structures, deferred-word helpers, legacy dictionary words
\ (STRUCTURE.TXT, DEFINING.TXT, DICTIONARY.TXT, FLOW.TXT AHEAD).
\ Cart: NEEDS EXTRAS      SD card: INCLUDE EXTRAS
\ Self-contained: carries its own >BODY / DEFER family so it also works on the
\ core cartridge without COMPAT (redefinitions there are identical + harmless).

decimal

\ --- structures (Forth-2012) ---------------------------------------------------
\   begin-structure point  field: p.x  field: p.y  end-structure
\   point -> 4 ;  <addr> p.y -> addr+2
: begin-structure ( "name" -- addr 0 ) create here 0 , 0 does> @ ;
: end-structure ( addr size -- ) swap ! ;
: +field  ( off n "name" -- off' ) create over , + does> @ + ;
: field:  ( off "name" -- off' ) 2 +field ;
: cfield: ( off "name" -- off' ) 1 +field ;

\ --- deferred words --------------------------------------------------------------
: >body ( xt -- dataaddr ) 5 + ;
: defer ( "name" -- ) create ['] abort , does> @ execute ;
: defer! ( xt2 xt1 -- ) >body ! ;
: defer@ ( xt1 -- xt2 ) >body @ ;
: is ( xt "name" -- ) state @ if
  postpone ['] postpone defer! else ' defer! then ; immediate
: action-of ( "name" -- xt ) state @ if
  postpone ['] postpone defer@ else ' defer@ then ; immediate

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
: forget ( "name" -- )
  parse-name 2dup find-name ?dup 0= if notfound then nip nip
  dup >xt to here
  dup c@ $1f and + 3 + to latest ;
