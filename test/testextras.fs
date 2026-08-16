\ EXTRAS module tests. Requires tester.fs.

marker ---testextras---

\ Standalone-safe: REQUIRE loads the Hayes tester only if it is not already
\ in, so `include test/testextr` works on its own at the prompt and costs nothing
\ inside the suite, where test.fs loaded it first.
require test/tester


include extras

decimal

\ STRUCTURES AND THE DEFER FAMILY MOVED OUT of this file, because they
\ moved out of the module. extras.fs used to define eleven words base.fs
\ already had, in their pre-stage-B forms -- >BODY as xt+5 when the CREATE
\ shape here puts the body at xt+7, FIELD: as two bytes when a cell is
\ four. Those copies shadowed the working ones, and DEFER! wrote an
\ execution token over a DOES> pointer: the first call to a deferred word
\ hung the machine. This file caught it the first time it was run.
\ base.fs owns them now, and teststruct.fs covers the structure words with
\ the sizes a 32-bit cell actually gives. What is left below is what
\ extras.fs genuinely provides.

cr .( testextras: ahead / ?comp / ?stack ) cr
: tah ahead 99 . then 42 ;
T{ tah -> 42 }T
T{ ' ?comp catch -> -14 }T                 \ interpreting -> throws
: tqi ?comp 5 postpone literal ; immediate \ guard passes while compiling
: tqc tqi ;
T{ tqc -> 5 }T
T{ ?stack depth -> 0 }T

cr .( testextras: compile / [compile] / comma-quote ) cr
: t1 compile dup ; immediate
: t2 t1 ;
T{ 5 t2 -> 5 5 }T
: imm5 5 postpone literal ; immediate
: t3 [compile] imm5 ; immediate
: t4 t3 ;
T{ t4 -> 5 }T
create cs1 ," HI"
T{ cs1 count nip -> 2 }T
T{ cs1 1+ c@ -> 'H' }T

cr .( testextras: forget ) cr
: fg1 11 ;
: fg2 22 ;
forget fg1
T{ s" fg1" find-name s" fg2" find-name or -> 0 }T
: fg3 33 ;
T{ fg3 -> 33 }T

cr .( testextras ok ) cr

---testextras---
