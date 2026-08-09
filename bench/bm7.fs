\ BM7 -- BM6 with an array store inside the inner loop: M(L)=A for
\ L = 1..5. The BASIC original DIMs M(5); allot the same five cells
\ (plus the unused 0th, as BASIC does).
create m 6 cells allot
: bm7 0 k !
  begin  k @ 1+ k !
    k @ 2 /  3 *  4 +  5 -  a !
    (sub)
    6 1 do  a @  m i cells +  !  loop
  k @ 1000 < 0= until ;
ms@ bm7 ms@ swap - .( BM7 ) . cr
