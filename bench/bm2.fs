\ BM2 -- an explicit counter in a VARIABLE and a conditional branch,
\ which is what the BASIC original's K=K+1 / IF K<1000 THEN 300 is.
\ The variable is the point: BM2 minus BM1 is what naming the counter
\ costs each language.
variable k
: bm2 0 k !  begin  k @ 1+ k !  k @ 1000 < 0= until ;
ms@ bm2 ms@ swap - .( BM2 ) . cr
