\ acceptance: conversions past 2^24, printing, round trips
include float
cr .( PROBE) cr
.( a1 ) 20000000 s>f f>s . cr
.( a2 ) 200000000 s>f f>s . cr
.( a3 ) -20000000 s>f f>s . cr
.( a4 ) 2 s>f f. cr
.( a5 ) 2 s>f fsqrt f. cr
.( a6 ) 1234567 s>f f. cr
fvariable fa2
: rt ( n -- )
  s>f fa2 f!
  11 1 do fa2 f@ fsqrt fa2 f! loop
  fa2 f@ f. cr
  11 1 do fa2 f@ 2 s>f f** fa2 f! loop
  fa2 f@ f. cr ;
.( r2 ) cr 2 rt
.( r16 ) cr 16 rt
.( r100 ) cr 100 rt
.( t1 ) 0.5 f. cr
.( t2 ) 3.14159265 f. cr
.( t3 ) 1 s>f fexp f. cr
.( t4 ) fdepth . depth . cr
.( PROBEEND) cr
