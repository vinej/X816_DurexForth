\ Core-additions tests. Requires tester.fs.

marker ---testcoreadd---

decimal

cr .( testcoreadd: arithmetic ) cr
T{ 9 2- -> 7 }T
T{ 0 2- -> -2 }T
T{ 7 sgn -> 1 }T
T{ -7 sgn -> -1 }T
T{ 0 sgn -> 0 }T
T{ $0a $05 catnib -> $a5 }T
T{ $0f $0f catnib -> $ff }T

cr .( testcoreadd: bit ops ) cr
variable bt
T{ 0   bt !  bt $0f sbit  bt @ -> $0f }T
T{ $ff bt !  bt $0f cbit  bt @ -> $f0 }T
T{ 0   bt !  -1 bt $03 fbit  bt @ -> $03 }T
T{ $ff bt !   0 bt $03 fbit  bt @ -> $fc }T

cr .( testcoreadd: stack ) cr
T{ 1 2 3 4 5 6 2rot -> 3 4 5 6 1 2 }T
T{ 1 2 3 0 roll -> 1 2 3 }T
T{ 1 2 3 1 roll -> 1 3 2 }T
T{ 1 2 3 2 roll -> 2 3 1 }T
T{ 1 2 3 4 5 6 5 roll -> 2 3 4 5 6 1 }T

cr .( testcoreadd: timing runs ) cr
1 sleep
1 ms
ticks 2drop

\ X816: no real-time clock - settime/time@/date@ went with the parked
\ clock.asm (there is no RTC on the machine; see X816_core doc/KERNEL.md
\ on the two clocks it has instead). sleep/ticks above now count VSYNC
\ frames via the kernel's IRQ_FRAMES, which these smoke lines still cover.

cr .( testcoreadd: 2 / unused / blank ) cr
T{ 2 -> 2 }T
T{ unused 0<> -> -1 }T \ unused is unsigned (ANS: u) and exceeds $7FFF here
create bbuf 8 allot
T{ bbuf 8 blank   bbuf c@  bbuf 7 + c@ -> 32 32 }T

cr .( testcoreadd: return stack 2>r 2r> 2r@ rdrop ) cr
: t2rat 2>r 2r@ 2r> ;
T{ 11 22 t2rat -> 11 22 11 22 }T
: t2rt 1 2 2>r 2r> ;
T{ t2rt -> 1 2 }T
: trdt 5 >r 6 >r rdrop r> ;
T{ trdt -> 5 }T

cr .( testcoreadd: cmove / cmove> ) cr
create cs 5 allot   create cd 5 allot
: initcs 5 0 do i 10 + cs i + c! loop ;
initcs   cs cd 5 cmove
T{ cd c@  cd 4 + c@ -> 10 14 }T
create ov 6 allot
: initov 6 0 do i ov i + c! loop ;
initov   ov  ov 1+  5 cmove>              \ overlap shift-up, high-first
T{ ov c@  ov 1+ c@  ov 5 + c@ -> 0 0 4 }T

cr .( testcoreadd: double words ) cr
T{ 5 s>d -> 5 0 }T
T{ 1234 0 d>s -> 1234 }T
T{ 10 0  20 0 d+ -> 30 0 }T
T{ 100 0  30 0 d- -> 70 0 }T
T{ 21 0 d2* -> 42 0 }T
T{ 84 0 d2/ -> 42 0 }T
T{ 0 0 d0= -> -1 }T    T{ 1 0 d0= -> 0 }T
T{ 0 0 d0< -> 0 }T     T{ -1 -1 d0< -> -1 }T
T{ 5 0  5 0 d= -> -1 }T    T{ 5 0  6 0 d= -> 0 }T
T{ 5 0  6 0 d< -> -1 }T    T{ 6 0  5 0 d< -> 0 }T
T{ -1 -1  1 0 d< -> -1 }T                 \ -1. < 1.
T{ 5 0  6 0 du< -> -1 }T
T{ 5 0  6 0 dmax -> 6 0 }T
T{ 5 0  6 0 dmin -> 5 0 }T
1234 0 d.                                 \ exercise pictured double output

cr .( testcoreadd: ud* / m*/ ) cr
T{ 100 0 7 ud* -> 700 0 }T
T{ 0 1 3 ud* -> 0 3 }T                     \ 65536*3 = 196608 = $30000
T{ 100 s>d 7 2 m*/ -> 350 s>d }T           \ 100*7/2
T{ 1000 s>d 3 4 m*/ -> 750 s>d }T          \ 3000/4
T{ -100 s>d 7 2 m*/ -> -350 s>d }T         \ sign of d
T{ 100 s>d -7 2 m*/ -> -350 s>d }T         \ sign of n1
T{ 100 s>d 7 -2 m*/ -> -350 s>d }T         \ sign of n2
T{ -100 s>d -7 2 m*/ -> 350 s>d }T         \ two negatives cancel
T{ 30000 s>d 20000 3 m*/ -> $c200 $0beb }T \ 600M/3 = 200,000,000 (double result)

\ X816: the i2c (SMC/RTC) and charset tests are parked with their words -
\ no SMC on the core, and the kernel console owns the character set.

cr .( testcoreadd: number output .r u.r d.r holds ? ) cr
T{ 0 0 <# s" xy" holds #> drop c@ -> 120 }T   \ 'x' first char
T{ 0 0 <# s" xy" holds #> nip -> 2 }T          \ length
cr T{ 5 3 .r -> }T                             \ these print; assert empty stack
T{ -5 3 .r -> }T
T{ 100 4 u.r -> }T
T{ 1000 0 6 d.r -> }T
create qv 0 ,
T{ 42 qv ! qv ? -> }T                          \ ? prints the stored cell
cr

cr .( testcoreadd: double/buffer defining words ) cr
3 4 2constant dcon
T{ dcon -> 3 4 }T
2variable dvar
T{ 5 6 dvar 2!  dvar 2@ -> 5 6 }T
80 buffer: dbuf
T{ dbuf dbuf = -> -1 }T                         \ same address each time
T{ 11 dbuf c!  dbuf c@ -> 11 }T
7 8 2value dval
T{ dval -> 7 8 }T
9 10 to dval                                    \ TO on a 2value (interpret)
T{ dval -> 9 10 }T
: setdv 100 200 to dval ;                       \ TO on a 2value (compile)
setdv
T{ dval -> 100 200 }T
: cdlit [ 21 22 ] 2literal ;
T{ cdlit -> 21 22 }T

cr .( testcoreadd ok ) cr

---testcoreadd---
