\ STRING module tests. Requires tester.fs.
\ LINPUT / GETKEY block on the keyboard, so only their existence is checked.

marker ---teststring---

\ Standalone-safe: REQUIRE loads the Hayes tester only if it is not already
\ in, so `include teststring` works on its own at the prompt and costs nothing
\ inside the suite, where test.fs loaded it first.
require tester


include string

decimal

cr .( teststring: place / +place / count ) cr
create csb 64 allot
T{ s" HELLO" csb place csb count nip -> 5 }T
T{ csb count drop c@ -> 'H' }T
T{ s" AB" csb +place csb count nip -> 7 }T
T{ csb 6 + c@ csb 7 + c@ -> 'A' 'B' }T

cr .( teststring: compare ) cr
T{ s" ABC" s" ABC" compare -> 0 }T
T{ s" ABC" s" ABD" compare -> -1 }T
T{ s" ABD" s" ABC" compare -> 1 }T
T{ s" AB" s" ABC" compare -> -1 }T
T{ s" ABC" s" AB" compare -> 1 }T
T{ s" " s" " compare -> 0 }T

cr .( teststring: c-quote and sliteral ) cr
: tcq c" WORLD" ;
T{ tcq c@ -> 5 }T
T{ tcq count drop c@ -> 'W' }T
T{ c" XY" count nip -> 2 }T                   \ interpreted: counted in PAD
: tsl [ s" FORTH" ] sliteral ;
T{ tsl nip -> 5 }T
T{ tsl drop c@ -> 'F' }T

cr .( teststring: s-backslash-quote escapes ) cr
T{ s\" AB\nC" nip -> 4 }T
T{ s\" AB\nC" drop 2 + c@ -> 13 }T
T{ s\" \t" drop c@ -> 9 }T
T{ s\" \e[" drop c@ -> 27 }T
T{ s\" a\"b" nip -> 3 }T                      \ escaped quote does not end it
T{ s\" a\"b" drop 1+ c@ -> 34 }T
T{ s\" \\" drop c@ -> 92 }T
T{ s\" \x41\x62" drop dup c@ swap 1+ c@ -> 65 98 }T
T{ s\" \m" nip -> 2 }T                        \ \m = CR LF
: tsq s\" Q\tR" ;
T{ tsq nip -> 3 }T                            \ compiled form
T{ tsq drop 1+ c@ -> 9 }T

cr .( teststring: numbers <-> strings ) cr
T{ 255 nhex s" FF" compare -> 0 }T
T{ 5 nbin s" 101" compare -> 0 }T
T{ -12 str s" -12" compare -> 0 }T
T{ 42 str s" 42" compare -> 0 }T
T{ s" 42" val -> 42 }T
T{ s" -137" val -> -137 }T
T{ hex s" ff" val decimal -> 255 }T
T{ base @ -> 10 }T                            \ nhex/nbin restored BASE

cr .( teststring: slicing ) cr
T{ s" HELLO" asc -> 'H' }T
T{ 65 chr swap c@ -> 1 65 }T
T{ s" HELLO" len -> 5 }T
T{ s" HELLO" 3 left s" HEL" compare -> 0 }T
T{ s" HELLO" 9 left nip -> 5 }T
T{ s" HELLO" 3 right s" LLO" compare -> 0 }T
T{ s" HELLO" 2 3 mid s" ELL" compare -> 0 }T
T{ s" HELLO" 4 9 mid s" LO" compare -> 0 }T
T{ '*' 5 rpt s" *****" compare -> 0 }T

cr .( teststring: tib / existence ) cr
T{ #tib @ source nip = -> -1 }T               \ #tib cell = current line length
T{ ' linput 0<> ' getkey 0<> -> -1 -1 }T

cr .( teststring ok ) cr

---teststring---
