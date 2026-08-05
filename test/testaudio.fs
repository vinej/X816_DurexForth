\ AUDIO - the parts of the machine that are hardware: VERA's PSG, VERA's
\ PCM FIFO, and the YM2151 port (base.fs).
\
\ The upstream version of this file opened with `include audio` and went
\ on to test FMINIT/FMNOTE/FMINST/PSGNOTE. That API is the X16 ROM's
\ audio driver - a note table and 163 instrument patches - and none of it
\ is on this machine. Porting it is a job, not a binding, so those words
\ do not exist here and this file does not pretend to test them. What it
\ does test is every register path that IS reachable.
\
\ Everything is verified by READING BACK: the PSG lives in VRAM, so its
\ registers can be peeked; AUDIO_CTRL and AUDIO_RATE read back; the
\ YM2151 answers writes only, so YM@ reports the shadow YM! keeps.

marker ---testaudio---

\ Standalone-safe: REQUIRE loads the Hayes tester only if it is not
\ already in, so `include testaudio` works on its own at the prompt and
\ costs nothing inside the suite, where test.fs loaded it first.
require tester

decimal

: pvp ( addr -- b ) 1 swap vpeek ;      \ VERA bank-1 peek, PSG at $F9C0

cr .( testaudio: PSG registers ) cr
psginit
T{ 0 psg@ 1 psg@ 2 psg@ 3 psg@ -> 0 0 0 0 }T
\ Voice 5's four bytes are at $F9D4..$F9D7. Checking through BOTH psg@
\ and a raw VPEEK is deliberate: psg@ agreeing with itself would prove
\ only that the offset arithmetic is self-consistent.
T{ $1234 5 psgfreq  $f9d4 pvp  $f9d5 pvp -> $34 $12 }T
T{ 20 psg@ 21 psg@ -> $34 $12 }T
T{ $2a 5 psgvol  $f9d6 pvp $3f and -> $2a }T
T{ 2 5 psgwav  $f9d7 pvp $c0 and -> $80 }T

cr .( testaudio: the read-modify-write words keep the other half ) cr
\ Volume and panning share one byte; waveform and pulse width share
\ another. PSGPAN and PSGWAV must keep what they do not own, which is
\ the whole reason they are not plain stores.
psginit
T{ 63 2 psgvol  1 2 psgpan  10 psg@ -> $7f }T   \ left only, volume 63 kept
T{ 3 2 psgpan  10 psg@ -> $ff }T                \ both channels, still 63
T{ 61 31 psg!  3 7 psgwav  31 psg@ -> 253 }T    \ pulse width 61 survives
\ ...and PSGPW is the other way round: it keeps the waveform. Without it
\ there was no way to set a pulse width at all, so every pulse voice was
\ stuck at the 1-in-64 duty cycle PSGINIT leaves - audible, but a thin
\ click rather than a tone, which is exactly how it was reported.
T{ 32 7 psgpw  31 psg@ -> $e0 }T                \ waveform 3 kept, width 32
T{ 0 7 psgpw  31 psg@ -> $c0 }T
\ PSGVOL is the deliberate exception: the page says "both channels", so
\ setting a volume sets them, and any earlier panning goes with it.
T{ 20 2 psgvol  10 psg@ -> $d4 }T

cr .( testaudio: PCM FIFO ) cr
$80 pcmctrl                             \ bit 7 on a WRITE resets the FIFO
T{ pcmfull? -> false }T
T{ pcmempty? -> true }T
T{ 15 pcmctrl $9f3b ioc@ $0f and -> 15 }T       \ volume nibble reads back
T{ 128 pcmrate $9f3c ioc@ -> 128 }T
0 pcmrate                               \ 0 = stopped, so nothing plays on
T{ 42 pcm! pcmempty? -> false }T        \ a byte went in
$80 pcmctrl
T{ pcmempty? -> true }T                 \ ...and the reset took it out again
create (pbuf) 8 allot
: (fillp) 8 0 do i (pbuf) i + c! loop ;
(fillp)
T{ (pbuf) 8 pcm-write pcmempty? -> false }T
$80 pcmctrl 0 pcmrate

cr .( testaudio: YM2151 shadow ) cr
\ The chip has no register readback at all, so YM@ reports what YM! last
\ sent. That is worth testing precisely because it is NOT the hardware:
\ a shadow that silently stopped tracking would look exactly like a chip
\ that stopped listening.
T{ $c7 $20 ym! $20 ym@ -> $c7 }T
T{ $3a $28 ym! $28 ym@ -> $3a }T
T{ $20 ym@ -> $c7 }T                    \ the first one is still there
T{ $ff 255 ym! 255 ym@ -> $ff }T        \ the top of the register file
T{ 511 ym@ -> $ff }T                    \ index is masked to 8 bits

cr .( testaudio ok ) cr

---testaudio---
