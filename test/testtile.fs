\ TILE group tests: VERA layer configuration. Requires tester.fs.
\ Config regs are read straight back. X816: the kernel console's text is
\ LAYER 0 (runtime/console.c), so the scratch layer for these tests is
\ layer 1 - the exact mirror of upstream, where layer 1 was the text.
\ (TILE/TDATA/TATTR are covered by testvideo.)

marker ---testtile---

decimal

cr .( testtile: layer enable ) cr
T{ 1 layer-on  $9f29 ioc@ $20 and -> $20 }T
T{ 1 layer-off $9f29 ioc@ $20 and -> 0 }T
T{ $9f29 ioc@ $10 and -> $10 }T               \ layer 0 (text) left enabled

cr .( testtile: layer-1 config registers ) cr
T{ 1 0 $2000 mapbase  $9f35 ioc@ -> $10 }T    \ $2000>>9 = $10
T{ 1 1 $2000 mapbase  $9f35 ioc@ -> $90 }T    \ bank bit -> reg bit7
T{ 1 0 $4000 tilebase $9f36 ioc@ -> $20 }T    \ ($4000>>11)<<2 = $20
T{ 1 $63 layer-mode   $9f34 ioc@ -> $63 }T

cr .( testtile ok ) cr

---testtile---
