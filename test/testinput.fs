\ INPUT - SNES pads on VIA1 port A, and the SMC mouse over the I2C bus on
\ the same port (base.fs).
\
\ Headless: there is no pad plugged in and no hand on a mouse, so this
\ asserts the shapes rather than the values. An ABSENT pad is a real,
\ checkable state - its data line floats high, so all 24 bits read as ones
\ and the third byte is $FF - and that is the assertion with teeth here.
\
\ MX/MY deliberately are NOT compared against a fixed number: the emulator
\ delivers a pointer of its own, so a run may legitimately start with a
\ packet already waiting. Bounds and behaviour hold either way.

marker ---testinput---

\ Standalone-safe: REQUIRE loads the Hayes tester only if it is not already
\ in, so `include test/testinp` works on its own at the prompt.
require test/tester

decimal

cr .( testinput: an absent pad reads as absent, not as every button ) cr
T{ 1 joy? -> false }T
T{ 1 joy -> 0 }T
T{ 2 joy -> 0 }T
T{ 3 joy -> 0 }T
T{ 4 joy -> 0 }T
\ Joystick 0 is the X16's keyboard-as-joystick, which has nothing behind it
\ here. It must read as no buttons - the bug worth guarding is the opposite,
\ an empty slot decoding to all twelve buttons at once, because the wire is
\ active low and an empty slot is the one case that reads back as zero.
T{ 0 joy -> 0 }T
\ Out of range gives the same answer, not an index off the end of anything.
T{ 9 joy -> 0 }T

cr .( testinput: the raw scan sees floating lines as all ones ) cr
joy-scan
T{ joy1 @ -> 16777215 }T                \ 24 bits, every one of them high
T{ joy1 @ 255 and -> 255 }T             \ byte 2 = $FF = no pad answered
T{ joy2 @ joy1 @ = -> true }T           \ every line floats the same way

cr .( testinput: scanning leaves the I2C pins alone ) cr
\ PA0 and PA1 are SDA and SCL. JOY-SCAN read-modify-writes the direction
\ register precisely so a scan between two I2C bytes cannot wreck the bus,
\ and the mouse below shares that bus - so this is not hypothetical.
via1-ddr ioc@ 3 and constant (ddr-i2c-before)
joy-scan
T{ via1-ddr ioc@ 3 and -> (ddr-i2c-before) }T
\ ...and the pad lines are inputs while latch and clock are outputs.
T{ via1-ddr ioc@ 240 and -> 0 }T
T{ via1-ddr ioc@ 12 and -> 12 }T

cr .( testinput: the mouse answers over I2C ) cr
\ The SMC's mouse device ID is a fixed, non-zero register, so reading it
\ back proves the bit-banged I2C read works end to end - which nothing else
\ here can prove while the mouse itself is sitting still.
: (smc@) ( cmd -- b )
  (i2c-start) $84 (i2c>) (i2c>) (i2c-stop)
  (i2c-start) $85 (i2c>) false (i2c<) (i2c-stop) ;
T{ $22 (smc@) 0<> -> true }T

1 mouse
T{ mb 7 invert and -> 0 }T              \ only the three button bits
T{ mx 0 640 within -> true }T
T{ my 0 480 within -> true }T
\ MWHEEL is a delta and clears itself, so a second read with no wheel
\ movement in between is zero whatever the first one said.
mwheel drop
T{ mwheel -> 0 }T
0 mouse
\ Switched off nothing moves: the poll is the only thing that writes these.
mx constant (mx-off)
T{ mx -> (mx-off) }T

cr .( testinput ok ) cr

---testinput---
