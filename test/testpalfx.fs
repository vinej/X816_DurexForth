\ PAL + VERAFX(DCSEL) tests. Requires tester.fs.

marker ---testpalfx---

decimal

cr .( testpalfx: palette ) cr
\ Use a high palette index (200) so the default 16-colour text palette is
\ untouched. Entry = 2 bytes: low = G<<4|B, high = R. $0abc at $1fa00+200*2.
T{ $0abc 200 pal!  1 $fb90 vaddr v@ v@ -> $bc $0a }T
T{ $0f00 201 pal!  1 $fb92 vaddr v@ v@ -> $00 $0f }T

cr .( testpalfx: DCSEL ) cr
T{ 5 dcsel $9f25 ioc@ -> 10 }T
0 dcsel

cr .( testpalfx: FX-MULT ) cr
\ Stage B: the 32-bit product is ONE cell.
T{ 1000 1000 fx-mult -> 1000000 }T
T{ 3 4 fx-mult -> 12 }T
T{ -1 2 fx-mult -> -2 }T                 \ signed
T{ 3 4 fx* -> 12 }T                       \ fx* alias

cr .( testpalfx: FX-FILL / FX-CLEAR ) cr
\ scratch VRAM at bank0 $8000 (unused in 80x60 text mode)
$ab 0 $8000 8 fx-fill
T{ 0 $8000 vpeek  0 $8007 vpeek -> $ab $ab }T
0 $8000 8 fx-clear
T{ 0 $8000 vpeek  0 $8007 vpeek -> 0 0 }T
\ remainder: fill 6 of 8 bytes, last 2 stay clear
$cc 0 $8000 6 fx-fill
T{ 0 $8005 vpeek -> $cc }T
T{ 0 $8006 vpeek  0 $8007 vpeek -> 0 0 }T
fx-off
T{ 0 $8010 $77 vpoke  0 $8010 vpeek -> $77 }T   \ plain VPOKE still works after FX

cr .( testpalfx ok ) cr

---testpalfx---
