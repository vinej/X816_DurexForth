\ BM4 -- the classic one: constants instead of the variable.
\ K/2*3+4-5 in integers truncates where BASIC's floats carry
\ fractions; the WORK -- four operations on a fetched value -- is the
\ same, and the operations are what the benchmark counts.
: bm4 0 k !
  begin  k @ 1+ k !
    k @ 2 /  3 *  4 +  5 -  a !
  k @ 1000 < 0= until ;
ms@ bm4 ms@ swap - .( BM4 ) . cr
