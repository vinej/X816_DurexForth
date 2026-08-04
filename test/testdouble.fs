\ Double-cell word tests (DOUBLE.TXT), written with the trailing-dot
\ double literals: 12. pushes 12 0. Requires tester.fs.

marker ---testdouble---

decimal

cr .( testdouble: literals ) cr
T{ 12. -> 12 0 }T
T{ -12. -> -12 -1 }T
T{ 0. -> 0 0 }T
T{ -1. -> -1 -1 }T
T{ 65536. -> 65536 0 }T                   \ fits the low cell now
T{ 4294967296. -> 0 1 }T                  \ 64-bit accumulation
T{ hex ff. decimal -> 255 0 }T            \ respects BASE
T{ hex -ff. decimal -> -255 -1 }T
: dl123 123. ;
T{ dl123 -> 123 0 }T                      \ compiled literal

cr .( testdouble: conversion ) cr
T{ 5 s>d -> 5. }T
T{ -5 s>d -> -5. }T
T{ 1000. d>s -> 1000 }T
T{ 10. 5 m+ -> 15. }T
T{ 10. -5 m+ -> 5. }T

cr .( testdouble: arithmetic ) cr
T{ 100000. 250000. d+ -> 350000. }T
T{ 250000. 100000. d- -> 150000. }T
T{ 100000. dnegate -> -100000. }T
T{ -100000. dabs -> 100000. }T
T{ 100000. dabs -> 100000. }T
T{ 100000. d2* -> 200000. }T
T{ -100000. d2* -> -200000. }T
T{ 200000. d2/ -> 100000. }T
T{ 65537. d2/ d2* -> 65536. }T            \ odd high cell: bit carries into lo
T{ -2. d2/ -> -1. }T

cr .( testdouble: comparisons ) cr
T{ 100000. 100000. d= -> true }T
T{ 100000. 100001. d= -> false }T
T{ 100000. 100001. d<> -> true }T
T{ 100000. 100001. d< -> true }T
T{ 100001. 100000. d> -> true }T
T{ -100000. 100000. d< -> true }T
T{ 1. -1. du< -> true }T                  \ -1. unsigned is the max double
T{ -1. 1. du> -> true }T
T{ 0. d0= -> true }T
T{ 1. d0= -> false }T
T{ 1. d0<> -> true }T
T{ -1. d0< -> true }T
T{ 1. d0> -> true }T
T{ -1. d0> -> false }T
T{ 100000. 200000. dmax -> 200000. }T
T{ 100000. 200000. dmin -> 100000. }T
T{ -100000. 200000. dmax -> 200000. }T
T{ -100000. 200000. dmin -> -100000. }T

cr .( testdouble: pictured output ) cr
T{ 100000. <# #s #> nip -> 6 }T           \ "100000" = 6 digits
T{ 100000. <# #s #> drop c@ -> 49 }T      \ leading '1'

cr .( testdouble ok ) cr

---testdouble---
