\ FLOAT + FLOATX module tests. Requires tester.fs.
\ Exact checks use integer round-trips (F>S); transcendentals use F~ with a
\ small tolerance. The 'NOTFOUND hook is restored before the marker forgets
\ the float words (else later unknown words would jump into freed memory).

marker ---testfloat---

\ Standalone-safe: REQUIRE loads each only if it is not already in, so
\ `include testfloat` works on its own at the prompt and costs nothing inside
\ the suite, where test.fs loaded the tester first.
require tester

'notfound @ constant nf0          \ original not-found handler, restored below

include float
include floatx

decimal

\ tolerance for approximate compares: 1/10000
: ftol ( F: -- r ) 1 s>f 10000 s>f f/ ;

cr .( testfloat: quote literals coexist with the 'notfound float hook ) cr
T{ "xy" nip -> 2 }T

cr .( testfloat: trailing dot stays a double, dot+digits is a float ) cr
T{ 12. -> 12 0 }T
T{ 12.0 f>s -> 12 }T
T{ -3. d0< -> true }T

cr .( testfloat: int conversion + arithmetic ) cr
T{ 5 s>f f>s -> 5 }T
T{ 1000 s>f f>s -> 1000 }T
T{ 2 s>f 3 s>f f+ f>s -> 5 }T
T{ 7 s>f 3 s>f f- f>s -> 4 }T
T{ 2 s>f 3 s>f f* f>s -> 6 }T
T{ 6 s>f 2 s>f f/ f>s -> 3 }T
T{ fdepth -> 0 }T

cr .( testfloat: stack ops ) cr
T{ 1 s>f 2 s>f fswap f>s f>s -> 1 2 }T
T{ 1 s>f 2 s>f fover f>s f>s f>s -> 1 2 1 }T
T{ 1 s>f 2 s>f fdup f>s f>s f>s -> 2 2 1 }T
T{ 1 s>f 2 s>f fnip f>s fdepth -> 2 0 }T
T{ 1 s>f 2 s>f 3 s>f frot f>s f>s f>s -> 1 3 2 }T

cr .( testfloat: sign, compare ) cr
T{ 5 s>f fnegate f0< -> -1 }T
T{ 5 s>f f0< -> 0 }T
T{ 0 s>f f0= -> -1 }T
T{ 3 s>f f0= -> 0 }T
T{ 2 s>f 3 s>f f< -> -1 }T
T{ 3 s>f 2 s>f f< -> 0 }T
T{ 3 s>f 3 s>f f= -> -1 }T
T{ 2 s>f 3 s>f f= -> 0 }T
T{ 2 s>f 3 s>f f<> -> -1 }T
T{ 3 s>f 3 s>f f<> -> 0 }T
T{ 3 s>f 2 s>f f> -> -1 }T
T{ 2 s>f 3 s>f f> -> 0 }T
T{ 3 s>f 3 s>f f> -> 0 }T
T{ 3 s>f f0<> -> -1 }T
T{ 0 s>f f0<> -> 0 }T
T{ 3 s>f f0> -> -1 }T
T{ 0 s>f f0> -> 0 }T
T{ 3 s>f fnegate f0> -> 0 }T
T{ 0 s>f fnegate f0> -> 0 }T  \ negated zero is not positive
T{ -5 s>f fabs f>s -> 5 }T
T{ 2 s>f 3 s>f fmax f>s -> 3 }T
T{ 2 s>f 3 s>f fmin f>s -> 2 }T

cr .( testfloat: memory, defining words ) cr
fvariable fv1
T{ 42 s>f fv1 f! fv1 f@ f>s -> 42 }T
7 s>f fconstant fc7
T{ fc7 f>s -> 7 }T

cr .( testfloat: fvalue + extended to ) cr
3 s>f fvalue fvx
T{ fvx f>s -> 3 }T
T{ 7 s>f to fvx fvx f>s -> 7 }T           \ interpreted TO
: (fvset) s>f to fvx ;
T{ 9 (fvset) fvx f>s -> 9 }T              \ compiled TO
T{ fdepth -> 0 }T
5 value tv1
T{ 6 to tv1 tv1 -> 6 }T                   \ VALUE path still works
: (tvset) to tv1 ;
T{ 8 (tvset) tv1 -> 8 }T
11 22 2value tv2
T{ 33 44 to tv2 tv2 -> 33 44 }T           \ 2VALUE path still works

cr .( testfloat: >FLOAT parsing ) cr
T{ s" 3" >float -> -1 }T      T{ f>s -> 3 }T
T{ s" 2.5" >float -> -1 }T    T{ 4 s>f f* f>s -> 10 }T
T{ s" -2.5" >float -> -1 }T   T{ fnegate 4 s>f f* f>s -> 10 }T
T{ s" 25e-1" >float -> -1 }T  T{ 4 s>f f* f>s -> 10 }T
T{ s" 1e3" >float -> -1 }T    T{ f>s -> 1000 }T
T{ s" abc" >float -> 0 }T
T{ s" 1.5x" >float -> 0 }T
T{ s" " >float -> 0 }T
T{ fdepth -> 0 }T

cr .( testfloat: literals, interpreted + compiled ) cr
T{ 2.5 4 s>f f* f>s -> 10 }T
: fx15 1.5 f* ;
T{ 4 s>f fx15 f>s -> 6 }T
: fnop ;                          \ plain colon word (not immediate)
: f15 fnop 1.5 ;                  \ trailing float literal = the TCE trap
T{ f15 2 s>f f* f>s -> 3 }T
\ T{ }T compares the data stack only, so float results are reduced to a
\ flag: F= for binary-exact values (.5 .25 .75 2.5 ...), F~ otherwise.
T{ 12.12 12.12 f+ 24.24 ftol f~ -> -1 }T
T{ 0.5 0.25 f+ 0.75 f= -> -1 }T
T{ 12.5 12.5 f+ 25.0 f= -> -1 }T
T{ 100.5 0.5 f- 100.0 f= -> -1 }T
T{ 2.5 4.0 f* 10.0 f= -> -1 }T
T{ 10.0 2.5 f/ 4.0 f= -> -1 }T
T{ -2.5 fabs 2.5 f= -> -1 }T
T{ 1.5e2 150 s>f f= -> -1 }T      \ exponent forms
T{ -25e-1 fnegate 2.5 f= -> -1 }T
T{ .5 .5 f+ 1 s>f f= -> -1 }T     \ leading-dot literal
T{ 12.12 12.13 f< -> -1 }T
T{ 12.12 fdup f= -> -1 }T
T{ 3.5 3.25 fmax 3.5 f= -> -1 }T
: fsum25 12.5 12.5 f+ ;           \ compiled literals in arithmetic
T{ fsum25 25.0 f= -> -1 }T
T{ fdepth -> 0 }T

cr .( testfloat: transcendentals, tolerance 1e-4 ) cr
T{ 16 s>f fsqrt 4 s>f ftol f~ -> -1 }T
T{ 100 s>f flog 2 s>f ftol f~ -> -1 }T
T{ 2 s>f falog 100 s>f ftol f~ -> -1 }T
T{ 1 s>f fexp fln 1 s>f ftol f~ -> -1 }T
T{ 2 s>f 10 s>f fpow 1024 s>f ftol f~ -> -1 }T
T{ fpi 3.14159265 ftol f~ -> -1 }T
T{ 1 s>f fsincos fdup f* fswap fdup f* f+ 1 s>f ftol f~ -> -1 }T
T{ 0 s>f fsinh f0= -> -1 }T
T{ 0 s>f facos fpi2 ftol f~ -> -1 }T

cr .( testfloat: fatan2 quadrants ) cr
T{ 1 s>f 1 s>f fatan2 fpi 4 s>f f/ ftol f~ -> -1 }T
T{ 1 s>f 0 s>f fatan2 fpi2 ftol f~ -> -1 }T

cr .( testfloat: isqrt, sizing ) cr
T{ 16385 isqrt -> 128 }T
T{ 3 floats -> 15 }T
T{ 100 float+ -> 105 }T

nf0 'notfound !                   \ unhook BEFORE the marker forgets (fnum)
cr .( testfloat ok ) cr

---testfloat---
