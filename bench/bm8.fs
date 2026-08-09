\ BM8 -- K^2, LN(K), SIN(K): the float library, 1000 times each.
\ F** goes through FLN and FEXP exactly as BASIC's ^ does, so the
\ same three transcendental paths are on the clock. MFLPT carries
\ about nine digits to IEEE single's seven; speed and accuracy trade
\ places here and Ahl below measures the accuracy half.
fvariable fa  fvariable fb  fvariable fc
: bm8 0 k !
  begin  k @ 1+ k !
    k @ s>f  2 s>f f**  fa f!
    k @ s>f  fln         fb f!
    k @ s>f  fsin        fc f!
  k @ 1000 < 0= until ;
ms@ bm8 ms@ swap - .( BM8 ) . cr
