\ BM1 -- the empty loop, 1000 times.
\ Forth's loop counter lives on the return stack, already an integer:
\ compare with SuperBasic's FOR K%=1 TO 1000 (BM1I) for like against
\ like, and with FOR K=1 TO 1000 (BM1) to see what its float costs.
: bm1 1001 1 do loop ;
ms@ bm1 ms@ swap - .( BM1 ) . cr
