\ BM6 -- BM5 plus an empty inner loop of five.
: bm6 0 k !
  begin  k @ 1+ k !
    k @ 2 /  3 *  4 +  5 -  a !
    (sub)
    6 1 do loop
  k @ 1000 < 0= until ;
ms@ bm6 ms@ swap - .( BM6 ) . cr
