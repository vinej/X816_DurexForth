\ The period's BASIC benchmarks, in Forth, for one comparison:
\ SuperBasic and durexForth on the SAME machine, the SAME emulator,
\ the SAME millisecond counter. ../X816_SuperBasic/bench/ holds the
\ BASIC originals; run-bench.sh here and there print the same table.
\
\ WHAT IS BEING COMPARED, said plainly. Rugg & Feldman (Kilobaud 1977)
\ and Ahl (Creative Computing 1983) measured INTERPRETERS. Forth
\ COMPILES its definitions, so this is interpreted BASIC against
\ compiled Forth on identical hardware -- that difference is the
\ point, not a distortion. Two more differences matter when reading
\ the numbers:
\
\   * BM1-BM7 here are INTEGER, because a Forth loop counter and
\     variable are integers. BASIC's K is a float only because
\     Microsoft BASIC had no other kind. SuperBasic's BM1I is the
\     like-for-like integer loop.
\   * BM8 and AHL use the FLOAT module: 5-byte MFLPT, about nine
\     digits, against SuperBasic's IEEE-754 single of about seven.
\     Ahl's accuracy figure reflects the FORMAT as much as the code.
\
\ AUTORUN includes this file; it includes the rest. Each benchmark
\ times itself with MS@ and prints one row of the table.

include float
include rnd

cr .( durexForth on the X816, 8 MHz -- Rugg/Feldman and Ahl) cr

include bm1
include bm2
include bm3
include bm4
include bm5
include bm6
include bm7
include bm8
include ahl

cr .( BENCHEND) cr
