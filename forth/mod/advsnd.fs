\ rtl, not rts,. Every word in this Forth is entered with jsl and must
\ leave with rtl: an rts ($60 against $6b) pops two bytes where three went
\ on, so the bank byte stays and the NEXT rtl returns into nowhere - at a
\ distance that depends on what ran in between. These were all rts.
\ ADVSND - PSG envelopes, PCM streaming, ADPCM decode (ADVANCED.TXT).
\ Cart: NEEDS ADVSND      SD card: INCLUDE ADVSND
\ Ported from C:\quartus\projects\x16_library (audio/psg.asm ASR envelopes,
\ audio/pcm.asm AFLOW streaming, audio/adpcm.asm IMA decoder).

decimal

\ --- PSG ASR envelopes -----------------------------------------------------------
\ Per voice: attack ramps the volume to a peak, sustain holds it for a tick
\ count, release ramps it back to silence.  Drive ENV-TICK once per frame --
\ typically armed on the VSYNC callback:  ' env-tick irq
create env-stg 16 allot  env-stg 16 0 fill   \ 0 idle / 1 attack / 2 sus / 3 rel
create env-vol 16 allot  create env-pk  16 allot
create env-ast 16 allot  create env-rst 16 allot
create env-sus 16 allot  create env-sct 16 allot

: (envw) ( voice -- )              \ write vol to the PSG, keeping the pan bits
  dup 4 * $f9c2 +
  1 over vpeek $c0 and             ( v reg pan )
  rot env-vol + c@ or              ( reg val )
  1 -rot vpoke ;

: env-start ( peak astep sus rstep voice -- )
\ peak 0-63; astep/tick (0 = jump straight to the peak); sustain ticks at the
\ peak (255 = hold until ENV-RELEASE); rstep/tick (0 = hold until ENV-STOP).
\ Set the voice's frequency, wave and pan first (PSGFREQ PSGWAV PSGPAN).
  15 and >r
  env-rst r@ + c!
  dup env-sus r@ + c!  env-sct r@ + c!
  dup env-ast r@ + c!
  swap 63 and env-pk r@ + c!       ( astep )
  0= if env-pk r@ + c@ env-vol r@ + c!  2
  else  0 env-vol r@ + c!  1  then
  env-stg r@ + c!
  r> (envw) ;

: env-release ( voice -- )         \ enter the release phase now
  15 and dup env-stg + c@ if 3 swap env-stg + c! else drop then ;
: env-stop ( voice -- )            \ silence and disarm immediately
  15 and 0 over env-stg + c!  0 over env-vol + c!  (envw) ;

: env-tick ( -- )                  \ advance every armed envelope one step
  16 0 do
    env-stg i + c@
    dup 1 = if drop                                   \ --- attack
      env-vol i + c@ env-ast i + c@ +
      env-pk i + c@ min dup env-vol i + c!
      env-pk i + c@ = if
        2 env-stg i + c!  env-sus i + c@ env-sct i + c! then
      i (envw)
    else dup 2 = if drop                              \ --- sustain
      env-sus i + c@ 255 <> if
        env-sct i + c@ ?dup
        if 1- env-sct i + c! else 3 env-stg i + c! then
      then
    else 3 = if                                       \ --- release
      env-rst i + c@ ?dup if
        env-vol i + c@ swap - 0 max dup env-vol i + c!
        0= if 0 env-stg i + c! then
        i (envw)
      then
    then then then
  loop ;

\ --- PCM streaming from banked RAM -------------------------------------------------
\ PCM-PLAY hands the sample to the FIFO in the background: it primes the 4 KB
\ FIFO, then refills from the AFLOW interrupt (armed on the AFLOW-IRQ slot,
\ auto-disarmed when the data runs out).  Set the format and volume first with
\ PCMCTRL; samples are two's-complement signed.  PCM-LOOP 1 = repeat forever.
variable pn
code (pcm>f) ( addr n -- )         \ n = 1..256 bytes, current RAM bank -> FIFO
lsb lda,x pn sta,                  \ low byte only: 0 means 256
inx,
lsb lda,x w sta,
msb lda,x w 1+ sta,
inx,
0 ldy,#
:-
w lda,(y)
$9f3d sta,
iny,
pn dec,
-branch bne,
rtl, end-code

\ --- PCM streaming: NOT HERE, and it cannot be ------------------------------
\ This module used to carry an interrupt-fed PCM streamer - PCM-PLAY,
\ PCM-STOP, PCM-PLAYING?, PCM-LOOP - and both halves of it are X16 shapes
\ that this machine does not have:
\   * It walked the X16's BANKED RAM window, $A000-$BFFF, switching banks
\     by writing address 0. The X816 is flat 16 MB with no window and no
\     bank register, and address 0 is the direct page - the test for it
\     wrote there and took a BRK, which is exactly what it deserved.
\   * It refilled the FIFO from VERA's audio-FIFO-low interrupt. AFLOW's
\     enable bit does not stick on this hardware, so that interrupt never
\     arrives; the kernel says the same thing from its side and excludes
\     AFLOW from its acknowledge.
\ Feeding the FIFO from a flat address is base.fs's PCM-WRITE, and pacing
\ it needs either the AFLOW enable to start working or a polled loop on
\ PCMFULL?. Rewriting the streamer around a bank register that does not
\ exist would have been busywork with a plausible shape.

\ --- IMA ADPCM decode (4-bit -> 8-bit signed, offline) ------------------------------
\ The canonical IMA/DVI algorithm (WAV flavour, LOW nibble of each byte first).
\ Too slow to feed the DAC live from Forth: decode whole banks up front, then
\ PCM-PLAY the result.  ADPCM! loads a WAV block header's predictor and index.
\ Both tables are built with `,` and a cell is FOUR bytes, so the index is
\ scaled by 4. It was 2* - the stride from when a cell was two - which read
\ the halves of neighbouring entries as step sizes and made the decoder
\ produce plausible-looking noise instead of audio.
create ad-st                       \ the 89-entry step table
7 , 8 , 9 , 10 , 11 , 12 , 13 , 14 , 16 , 17 , 19 , 21 , 23 , 25 , 28 , 31 ,
34 , 37 , 41 , 45 , 50 , 55 , 60 , 66 , 73 , 80 , 88 , 97 , 107 , 118 ,
130 , 143 , 157 , 173 , 190 , 209 , 230 , 253 , 279 , 307 , 337 , 371 ,
408 , 449 , 494 , 544 , 598 , 658 , 724 , 796 , 876 , 963 , 1060 , 1166 ,
1282 , 1411 , 1552 , 1707 , 1878 , 2066 , 2272 , 2499 , 2749 , 3024 ,
3327 , 3660 , 4026 , 4428 , 4871 , 5358 , 5894 , 6484 , 7132 , 7845 ,
8630 , 9493 , 10442 , 11487 , 12635 , 13899 , 15289 , 16818 , 18500 ,
20350 , 22385 , 24623 , 27086 , 29794 , 32767 ,
create ad-it -1 , -1 , -1 , -1 , 2 , 4 , 6 , 8 ,
variable ad-p                      \ predictor + $8000 (kept unsigned-offset)
variable ad-x                      \ step index 0-88
variable ad-d                      \ output pointer

: adpcm-init ( -- )  $8000 ad-p !  0 ad-x ! ;
: adpcm! ( pred index -- )         \ state from an IMA WAV block header
\ The predictor is a SIGNED 16-bit field in the block header and is kept
\ here as unsigned 0..$FFFF. Masking is not decoration: with 32-bit cells
\ a negative predictor sign-extends, and `$8000 xor` then leaves the top
\ sixteen bits set instead of wrapping - the decoder read that as a huge
\ positive sample and saturated on the spot.
  0 max 88 min ad-x !  65535 and $8000 xor ad-p ! ;

: (ad-nib) ( n -- )                \ advance the decoder by one 4-bit code
  ad-x @ 4 * ad-st + @ >r          ( n ) ( r: step )
  r@ 3 rshift                      \ diff = step>>3 (+s>>2 if b0)(+s>>1)(+s)
  over 1 and if r@ 2 rshift + then
  over 2 and if r@ 1 rshift + then
  over 4 and if r@ + then
  r> drop                          ( n diff )
  over 8 and if                    \ predictor +/- diff, saturated
    ad-p @ over u< if drop 0 else ad-p @ swap - then
  else
    \ Clamped, not wrapped: this detected overflow by testing whether the
    \ sum came out SMALLER than the predictor, which only happens when the
    \ addition wraps - and with 32-bit cells it never does. The predictor
    \ walked past $FFFF and the samples went wherever it went.
    ad-p @ +  65535 min
  then ad-p !
  7 and 4 * ad-it + @  ad-x @ +  0 max 88 min  ad-x ! ;
: (ad8) ( -- b )  ad-p @ 8 rshift $80 xor ;   \ current sample, signed 8-bit

: adpcm>pcm ( src dst u -- dst' )  \ u input bytes -> 2u signed 8-bit samples
  swap ad-d !                      \ (set the RAM bank yourself; no bank wrap)
  over + swap ?do
    i c@ dup 15 and (ad-nib) (ad8) ad-d @ c!  1 ad-d +!
    4 rshift      (ad-nib) (ad8) ad-d @ c!  1 ad-d +!
  loop ad-d @ ;
