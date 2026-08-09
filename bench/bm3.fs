\ BM3 -- BM2 plus A=K/K*K+K-K: five reads of the variable and four
\ operations, left to right exactly as BASIC evaluates them. Integer
\ division where BASIC divides floats -- the value of A is the same
\ K either way.
variable a
: bm3 0 k !
  begin  k @ 1+ k !
    k @ k @ /  k @ *  k @ +  k @ -  a !
  k @ 1000 < 0= until ;
ms@ bm3 ms@ swap - .( BM3 ) . cr
