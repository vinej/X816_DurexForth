\ ANS structures and the rest of STRING.TXT (base.fs). Pure Forth over
\ words that already existed; the two pages were 0/5 and 9/29.
\
\ Also guards PAD, which used to be a FIXED address the dictionary had
\ grown past - every word here that builds a string builds it there.

marker ---teststruct---

\ Standalone-safe: REQUIRE loads the Hayes tester only if it is not
\ already in, so `include teststruct` works on its own at the prompt and
\ costs nothing inside the suite, where test.fs loaded it first.
require tester

decimal

cr .( teststruct: PAD is above HERE, and writing it is safe ) cr
\ PAD was $10500, chosen when the dictionary was small, and HERE passed
\ it long ago - so `65 pad c!` wrote into compiled code. Nothing in the
\ boot chain wrote there, so it waited for a user. These two assertions
\ are the whole fix: PAD is past HERE, and a word defined BEFORE a pad
\ write still works after it.
: (canary) 1234 ;
T{ pad here u> -> true }T
T{ 65 pad c! pad c@ -> 65 }T
T{ (canary) -> 1234 }T
\ ...and it tracks HERE rather than sitting still.
0 value (p1)
pad to (p1)
create (spacer) 40 allot
T{ pad (p1) u> -> true }T

cr .( teststruct: structures ) cr
begin-structure point
  field: p.x
  field: p.y
  cfield: p.flag
end-structure
T{ point -> 9 }T                        \ 4 + 4 + 1
T{ 0 p.x -> 0 }T
T{ 0 p.y -> 4 }T
T{ 0 p.flag -> 8 }T
\ A field is an OFFSET applied to whatever base it is given, so the same
\ names work on any buffer - that is the property worth pinning down.
create pt point allot
T{ 11 pt p.x !  22 pt p.y !  1 pt p.flag c!  pt p.x @ pt p.y @ pt p.flag c@
   -> 11 22 1 }T
T{ 100 p.x -> 100 }T                    \ no hidden base address
\ ...including a far one, since a field is just arithmetic on a cell.
point far-buffer: fpt
T{ 77 fpt p.y !  fpt p.y @ -> 77 }T
T{ fpt $50000 u< -> false }T
\ +FIELD takes any size, which is how you embed an array.
begin-structure rec
  field: r.id
  20 +field r.name
end-structure
T{ rec -> 24 }T
T{ 0 r.name -> 4 }T

cr .( teststruct: counted strings ) cr
create cs1 40 allot
T{ s" hello" cs1 place cs1 count s" hello" compare -> 0 }T
T{ cs1 c@ -> 5 }T
T{ s" -world" cs1 +place cs1 count s" hello-world" compare -> 0 }T
T{ cs1 c@ -> 11 }T
T{ s" " cs1 +place cs1 c@ -> 11 }T      \ appending nothing changes nothing

cr .( teststruct: the BASIC-flavoured string words ) cr
T{ s" abc" len -> 3 }T
T{ s" Abc" asc -> 65 }T
T{ 65 chr s" A" compare -> 0 }T
T{ s" abcdef" 3 left s" abc" compare -> 0 }T
T{ s" abcdef" 2 right s" ef" compare -> 0 }T
T{ s" abcdef" 2 3 mid s" bcd" compare -> 0 }T   \ MID counts from 1
T{ s" abcdef" 1 2 mid s" ab" compare -> 0 }T
\ Asking for more than there is gives what there is, rather than reading
\ off the end - the case that turns a display bug into a crash.
T{ s" abc" 10 left s" abc" compare -> 0 }T
T{ s" abc" 10 right s" abc" compare -> 0 }T
T{ s" abc" 2 10 mid s" bc" compare -> 0 }T
T{ 42 5 rpt s" *****" compare -> 0 }T
T{ 42 0 rpt nip -> 0 }T

cr .( teststruct: numbers to strings and back ) cr
T{ -12 str s" -12" compare -> 0 }T
T{ 0 str s" 0" compare -> 0 }T
T{ 255 nhex s" FF" compare -> 0 }T
T{ 5 nbin s" 101" compare -> 0 }T
\ NHEX and NBIN must leave BASE exactly as they found it.
T{ base @ 255 nhex 2drop 5 nbin 2drop base @ = -> true }T
T{ s" 42" val -> 42 }T
T{ s" -7" val -> -7 }T
T{ s" 0" val -> 0 }T
hex
\ Both sides in HEX: the literal on the right is parsed in the current
\ base too, so writing 255 here would mean $255 and the test would fail
\ while VAL was perfectly correct.
T{ s" ff" val -> ff }T                  \ VAL honours the current base
T{ s" 10" val -> 10 }T                  \ ...16, not 10
decimal
T{ s" 10" val -> 10 }T                  \ and 10 again once back in decimal

cr .( teststruct: SLITERAL compiles a string ) cr
: (sl) [ s" inline" ] sliteral ;
T{ (sl) s" inline" compare -> 0 }T

cr .( teststruct ok ) cr

---teststruct---
