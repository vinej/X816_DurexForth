\ ADVSND module tests. Requires tester.fs.

marker ---testadvsnd---

\ Standalone-safe: REQUIRE loads the Hayes tester only if it is not already
\ in, so `include test/testadvs` works on its own at the prompt and costs nothing
\ inside the suite, where test.fs loaded it first.
require test/tester


\ NOT `include audio`: there is no audio module here. That file was the X16
\ ROM's audio driver, and the register-level words it wrapped - PSG! PSG@
\ PSGFREQ PSGVOL PCMCTRL YM! YM@ and the rest - are in base.fs, always
\ present. The note and patch API is `include fm`.
include advsnd

decimal

: vvol ( voice -- n ) 4 * $f9c2 + 1 swap vpeek ;
: ticks ( n -- ) 0 do env-tick loop ;

cr .( testadvsnd: envelopes ) cr
0 1 psgvol                                 \ pan both, volume 0
40 0 255 0 1 env-start                     \ instant attack, hold forever
T{ 1 vvol -> $e8 }T                        \ $c0 pan | 40
1 env-stop
T{ 1 vvol -> $c0 }T

0 2 psgvol
60 25 2 10 2 env-start                     \ ramp 25/tick, sustain 2, fall 10
T{ 2 vvol $3f and -> 0 }T
env-tick env-tick env-tick                 \ 25, 50, clamp at the peak
T{ 2 vvol $3f and -> 60 }T
env-tick env-tick env-tick env-tick        \ 2 sustain ticks, turn, 1 release
T{ 2 vvol $3f and -> 50 }T
20 ticks                                   \ ride the release out
T{ 2 vvol $3f and -> 0 }T

0 3 psgvol                                 \ ticked from the VSYNC slot
40 2 255 5 3 env-start
' env-tick irq
100 ms
0 irq
T{ 3 vvol $3f and 10 > -> -1 }T
3 env-stop

cr .( testadvsnd: adpcm ) cr
create adv1 $77 c, $77 c, $77 c, $ff c,
create adbuf 24 allot
adpcm-init
T{ adv1 adbuf 4 adpcm>pcm adbuf - -> 8 }T
T{ adbuf c@  adbuf 3 + c@  adbuf 4 + c@  adbuf 5 + c@ -> 0 0 2 4 }T
T{ adbuf 6 + c@  adbuf 7 + c@ -> 255 243 }T
T{ ad-x @ -> 64 }T
T{ -3103 64 adpcm! ad-p @ -> 29665 }T      \ WAV block header restore
create adv2 10 allot  adv2 10 $77 fill
adpcm-init
adv2 adbuf 10 adpcm>pcm drop               \ pump until it saturates
T{ adbuf 18 + c@  adbuf 19 + c@ -> 127 127 }T
T{ ad-p @  ad-x @ -> $ffff 88 }T

\ THE PCM STREAMING SECTION IS GONE with the words it tested. It drove the
\ X16's banked RAM window by writing address 0 - the direct page here - and
\ waited on an AFLOW interrupt whose enable bit this hardware will not hold.
\ See advsnd.fs for the whole story and what to use instead.

cr .( testadvsnd ok ) cr

---testadvsnd---
