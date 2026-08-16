\ SYSCTL[2] TURBO and the timer-based MS. The machine boots at an exact
\ 8 MHz average; writing bit 2 of $9F80 releases the domain's full 14 MHz.
\ Reads return the EFFECTIVE speed - the MiSTer OSD's CPU Turbo option ORs
\ over the software bit - so on hardware with the OSD forcing fast, the bit
\ can truthfully read 1 after FALSE TURBO. Every assertion here holds under
\ either OSD setting. Requires tester.fs.

marker ---testturbo---

\ Standalone-safe: REQUIRE loads the Hayes tester only if it is not
\ already in, so `include test/turbo` works on its own at the prompt and
\ costs nothing inside the suite, where test.fs loaded it first.
require test/tester

decimal

cr .( testturbo: sysctl sanity - overlay down, really native ) cr
T{ $9f80 ioc@ 1 and -> 0 }T      \ bit 0: boot ROM overlay long dropped
T{ $9f80 ioc@ 2 and -> 0 }T      \ bit 1: the live E flag, 0 = native mode

cr .( testturbo: true turbo reads back effective-fast ) cr
T{ true turbo turbo? -> true }T
T{ cpu-mhz -> 14 }T

\ The low 24 bits of the free-running ms timer. $9F90 must be read FIRST:
\ it latches bits 31:8, and $9F91-$9F93 return that latch.
: (ms@) ( -- u )
  $9f90 ioc@ $9f91 ioc@ 8 lshift or $9f92 ioc@ 16 lshift or ;

cr .( testturbo: ms waits on the timer at 14 MHz ) cr
T{ 0 ms -> }T
T{ (ms@) 30 ms (ms@) swap - 30 < -> false }T

\ Software bit back down - boot never sets it, so FALSE is the true prior.
\ Effective speed is now whatever the OSD says; assert only consistency.
false turbo

cr .( testturbo: ms waits on the timer at the boot speed too ) cr
T{ (ms@) 30 ms (ms@) swap - 30 < -> false }T
T{ cpu-mhz 8 = cpu-mhz 14 = or -> true }T

cr .( testturbo ok ) cr

---testturbo---
