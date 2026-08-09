\ probe.fs -- the single-session scratch bench, run by bench-probe.sh.
\
\ NOT a test: bench.fs and run-bench.sh are the measurement, test/ is
\ the correctness suite. This file is the 90-second loop the float
\ engine port was debugged in -- edit it freely, it is meant to be
\ overwritten. What it holds now is the acceptance run that closed
\ that work, kept because it is the shape a bring-up probe should
\ have: one operation per line, each printing a number a reader can
\ check by hand, and a marker at the end so a harness can tell
\ "finished" from "died halfway".
include float
cr .( PROBE) cr
.( a1 ) 20000000 s>f f>s . cr          \ past 2^24: FTOI's restored branch
.( a2 ) -20000000 s>f f>s . cr
.( a3 ) 2 s>f fsqrt f. cr              \ 1.41421...
.( a4 ) 1 s>f fexp f. cr               \ 2.71828...
.( a5 ) 2 s>f 10 s>f f** f>s . cr      \ 1024, by the integral fast path
.( a6 ) 4 s>f 0.5 f** f. cr            \ 2, by exp(y ln x)
fvariable fa2
: rt ( n -- )                          \ Ahl's round trip: sqrt x10, square x10
  s>f fa2 f!
  11 1 do fa2 f@ fsqrt fa2 f! loop
  11 1 do fa2 f@ 2 s>f f** fa2 f! loop
  fa2 f@ f. cr ;
.( a7 ) 100 rt                         \ ~100, not 1.5e+38
.( a8 ) fdepth . depth . cr            \ both stacks clean
.( PROBEEND) cr
