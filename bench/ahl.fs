\ Ahl's benchmark (Creative Computing, 1983): for N = 1..100, take
\ SQR ten times and square ten times -- A should come back as N --
\ accumulating 2000 random numbers along the way. It reports SPEED
\ and then two ACCURACY figures, both 0 in a perfect world:
\
\   AHLA: ABS(1010 - S/5)  the arithmetic error the 2000 round trips
\                          left behind
\   AHLR: ABS(1000 - R)    how far the RND sum sits from its mean
\
\ The generator is rnd.fs scaled to [0,1) from its middle bits --
\ the low ones are the weak ones. Seed it so the run is repeatable.
fvariable fr  fvariable fs  fvariable fa2
: rnd01 ( F: -- r ) rnd 8 rshift $ffff and s>f  65536 s>f f/ ;
variable ahlms
: ahl
  1 seed !
  0 s>f fr f!  0 s>f fs f!
  ms@
  101 1 do
    i s>f fa2 f!
    11 1 do  fa2 f@ fsqrt fa2 f!  fr f@ rnd01 f+ fr f!  loop
    11 1 do  fa2 f@ 2 s>f f** fa2 f!  fr f@ rnd01 f+ fr f!  loop
    fs f@ fa2 f@ f+ fs f!
  loop
  ms@ swap - ahlms ! ;
ahl
.( AHL ) ahlms @ . cr
.( AHLA ) 1010 s>f  fs f@ 5 s>f f/  f- fabs f. cr
.( AHLR ) 1000 s>f  fr f@  f- fabs f. cr
