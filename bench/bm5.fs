\ BM5 -- BM4 plus a subroutine call each pass. In BASIC that is
\ GOSUB/RETURN through the return stack; here it is a call to an
\ empty word, which is what a Forth subroutine is.
: (sub) ;
: bm5 0 k !
  begin  k @ 1+ k !
    k @ 2 /  3 *  4 +  5 -  a !
    (sub)
  k @ 1000 < 0= until ;
ms@ bm5 ms@ swap - .( BM5 ) . cr
