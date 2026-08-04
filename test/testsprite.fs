\ SPRITE group tests (X16 VERA sprite attribute RAM $1fc00). Requires tester.fs.
\ Attributes are read straight back out of VRAM to check each write.

marker ---testsprite---

decimal

cr .( testsprite: enable flag ) cr
\ SPRITES-ON/OFF toggle DC_VIDEO bit6 (DCSEL 0)
T{ sprites-on  $9f29 ioc@ $40 and -> $40 }T
T{ sprites-off $9f29 ioc@ $40 and -> 0 }T

cr .( testsprite: position ) cr
\ SPRITE-POS writes bytes 2..5; SPRITE-GET reads them back
T{ 100 50 5 sprite-pos 5 sprite-get -> 100 50 }T
T{ 300 200 5 sprite-pos 5 sprite-get -> 300 200 }T
\ SPRITE-MOV ( num x y -- ) is the same, argument order of BASIC MOVSPR
T{ 6 250 120 sprite-mov 6 sprite-get -> 250 120 }T

cr .( testsprite: image address ) cr
\ SPRITE-IMAGE stores graphaddr>>5 in bytes 0..1 of sprite 6 ($1fc30)
T{ $4000 6 sprite-image  1 $fc30 vaddr v@ v@ -> 0 2 }T
\ SPRITE-MEM ( num bank addr -- ): full 17-bit bank:addr, sprite 11 ($1fc58)
T{ 11 1 $8000 sprite-mem  1 $fc58 vaddr v@ v@ -> 0 $0c }T

cr .( testsprite: size / z-depth ) cr
\ SPRITE-SIZE ( width height sprite -- ) -> byte7 = h<<6 | w<<4, sprite 7 ($1fc3f)
T{ 3 2 7 sprite-size  1 $fc3f vaddr v@ -> $b0 }T
\ SPRITE-Z ( z sprite -- ) -> byte6 = z<<2, sprite 8 ($1fc46)
T{ 2 8 sprite-z  1 $fc46 vaddr v@ -> 8 }T
\ SPRITE ( num zdepth -- ) sets byte6 z<<2 and enables the layer, sprite 9 ($1fc4e)
T{ 9 3 sprite  1 $fc4e vaddr v@ -> $0c }T
T{ $9f29 ioc@ $40 and -> $40 }T

\ Leave the machine displayable: the tests above parked live sprites with
\ garbage images over the text layer, and a later suite's failure report
\ must not print underneath them.
sprites-off
T{ $9f29 ioc@ $40 and -> 0 }T

cr .( testsprite ok ) cr

---testsprite---
