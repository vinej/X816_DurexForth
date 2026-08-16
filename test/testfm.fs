\ FM - the YM2151 note API in mod/fm.fs, which AUDIOFM.TXT listed as
\ absent for the whole port: FMINIT, FMINST, FMNOTE, FMDRUM, FMVOL,
\ FMPAN, FMVIB and PSGNOTE.
\
\ Every assertion here reads the chip back through YM@, which is the
\ SHADOW that base.fs's YM! keeps - the YM2151 answers no reads at all,
\ so there is nothing else to check against. That is enough to catch the
\ things that actually went wrong while porting this: a patch byte
\ written to the wrong register, attenuation applied to a modulator, a
\ pan that quietly wiped the instrument's feedback.
\
\ The pitches themselves were verified outside Forth, by capturing the
\ emulator's audio: BASIC notes $3a/$4a/$5a measure 220/440/881 Hz.

marker ---testfm---

\ Standalone-safe: REQUIRE loads each only if it is not already in, so
\ `include test/testfm` works on its own at the prompt.
require test/tester
require fm

decimal

cr .( testfm: packed notes to MIDI, and MIDI to key codes ) cr
T{ $4a bas>midi -> 69 }T                \ octave 4, note 10 = A4
T{ $41 bas>midi -> 60 }T                \ note 1 is C, so this is middle C
T{ $5a bas>midi -> 81 }T
T{ $40 bas>midi -> -1 }T                \ note field 0 is not a note
T{ $4d bas>midi -> -1 }T                \ ...and neither is 13
T{ 69 midi>kc -> $4a }T
T{ 81 midi>kc -> $5a }T
\ Middle C is key code $3E, not $4E: the chip's note nibble runs C#..C,
\ so a C sits in the octave BELOW the one its name suggests. This is
\ exactly why the ROM's table is carried here rather than computed.
T{ 60 midi>kc -> $3e }T
T{ 108 midi>kc -> $7e }T
T{ 0 midi>kc -> -1 }T                   \ below anything the chip can play

cr .( testfm: FMINST writes every byte of the patch ) cr
fminit
48 0 fminst                             \ String Ensemble 1
\ Byte 0 is RL/FB/CON: the feedback and algorithm come from the patch...
T{ $20 ym@ 63 and -> 44 }T
\ ...and the stereo bits that FMINIT set are still there, because the
\ two share one register and a patch load must not re-pan a channel.
T{ $20 ym@ 192 and -> 192 }T
\ Byte 1 is PMS/AMS at $38+channel. The first draft of this driver called
\ it a spare and skipped it, which costs every instrument its vibrato
\ and tremolo depth without making a sound anyone would call wrong.
T{ $38 ym@ -> $30 }T
T{ $40 ym@ -> $31 }T                    \ byte 2, the first of the 24

cr .( testfm: FMVOL attenuates carriers and leaves modulators alone ) cr
fminit
0 0 fminst                              \ Acoustic Piano, algorithm 4
63 0 fmvol                              \ loudest: no attenuation at all
T{ $70 ym@ -> $17 }T                    \ carrier, at the patch's own level
T{ $78 ym@ -> 0 }T                      \ the other carrier
T{ $60 ym@ -> $35 }T                    \ a modulator
0 0 fmvol                               \ quietest
T{ $70 ym@ -> 127 }T                    \ pinned at silence, not wrapped
T{ $78 ym@ -> 126 }T
\ The modulator is untouched, and that is the point: total level on a
\ modulator is how hard it drives the operator below it, so turning it
\ down changes the timbre and leaves the loudness roughly alone.
T{ $60 ym@ -> $35 }T
32 0 fmvol
T{ $78 ym@ -> 62 }T                     \ 63-32 = 31 steps, doubled

cr .( testfm: FMPAN keeps the feedback it shares a register with ) cr
fminit
0 0 fminst
1 0 fmpan
T{ $20 ym@ 192 and -> 64 }T
T{ $20 ym@ 63 and -> 4 }T               \ patch 0's FB/CON, still there
2 0 fmpan
T{ $20 ym@ 192 and -> 128 }T
3 0 fmpan
T{ $20 ym@ 192 and -> 192 }T
T{ $20 ym@ 63 and -> 4 }T

cr .( testfm: FMNOTE sets the pitch and keys on ) cr
fminit
0 0 fminst
$4a 0 fmnote
T{ $28 ym@ -> $4a }T
T{ $30 ym@ -> 0 }T                      \ dead on the note, no bend
T{ $08 ym@ -> $78 }T                    \ channel 0, all four operators
$5a 1 fmnote
T{ $29 ym@ -> $5a }T
T{ $08 ym@ -> $79 }T                    \ channel 1 this time
\ Note 0 releases rather than cutting: the key-off lets the envelope's
\ release rate finish the sound.
0 0 fmnote
T{ $08 ym@ -> 0 }T
\ A note the chip cannot reach leaves the channel exactly as it was.
fminit
0 0 fminst
$4a 0 fmnote
$01 0 fmnote                            \ octave 0, below the range
T{ $28 ym@ -> $4a }T

cr .( testfm: FMMIDI is the same word in the other numbering ) cr
fminit
0 0 fminst
69 0 fmmidi
T{ $28 ym@ -> $4a }T
T{ $08 ym@ -> $78 }T

cr .( testfm: FMDRUM loads a percussion patch and its fixed pitch ) cr
fminit
36 0 fmdrum                             \ the GM kick
T{ $28 ym@ -> $3e }T
T{ $20 ym@ 63 and -> 61 }T              \ patch 134's FB/CON
T{ $08 ym@ -> $78 }T
42 0 fmdrum                             \ closed hi-hat: a different patch
T{ $28 ym@ -> $5e }T

cr .( testfm: FMVIB drives the one LFO the chip has ) cr
fminit
0 0 fminst
100 40 fmvib
T{ $19 ym@ -> 168 }T                    \ 40 with bit 7 set: phase depth
T{ $18 ym@ -> 100 }T
\ Depth also has to open each channel's PMS or the LFO runs inaudibly,
\ so every channel gets maximum sensitivity - and its AMS is left alone.
T{ $38 ym@ 112 and -> 112 }T
T{ $3f ym@ 112 and -> 112 }T
100 0 fmvib
T{ $38 ym@ 112 and -> 0 }T

cr .( testfm: PSGNOTE, the same packed note on a VERA voice ) cr
psginit
$4a 0 psgnote
\ 1181 is the frequency word base.fs's own PSG example calls "~440 Hz",
\ arrived at here from the ROM's table instead of by hand.
T{ 0 psg@ 1 psg@ 8 lshift or -> 1181 }T
$5a 0 psgnote
T{ 0 psg@ 1 psg@ 8 lshift or -> 2362 }T \ an octave up is twice the word
63 0 psgvol
0 0 psgnote                             \ note 0 releases: volume to zero
T{ 2 psg@ 63 and -> 0 }T

cr .( testfm: PSGMIDI, the same voice in MIDI numbering ) cr
psginit
69 0 psgmidi                            \ A4 = 440 Hz
T{ 0 psg@ 1 psg@ 8 lshift or -> 1181 }T
81 0 psgmidi                            \ an octave up is twice the word
T{ 0 psg@ 1 psg@ 8 lshift or -> 2362 }T

cr .( testfm: the play-string parser ) cr
\ THE STRING IS COPIED FIRST, and that is not fussiness: S" at the
\ interpreter hands back a pointer into the input buffer, which the next
\ line overwrites. A parser tested across several lines would be reading
\ the test file itself - which is exactly what the first run of this did,
\ and it reported every note as 64.
create sbuf 40 allot
: >sbuf ( c-addr u -- addr u ) dup >r sbuf swap move sbuf r> ;
: 1st ( c-addr u -- x code ) >sbuf (ps-start) (ps-next) ;
: nth ( n -- x code ) 0 ?do (ps-next) 2drop loop (ps-next) ;

\ Middle C is 60, and the scale walks the white notes.
T{ s" C" 1st -> 60 1 }T
ps-reset
T{ s" CDEFGAB" >sbuf (ps-start) 6 nth -> 71 1 }T   \ B4
ps-reset
\ Accidentals stack, and they are read before the length.
T{ s" C#" 1st -> 61 1 }T
T{ s" C-" 1st -> 59 1 }T
T{ s" C##" 1st -> 62 1 }T                          \ C double sharp is D
\ Octaves: O sets, < and > step, and both clamp rather than wrapping.
T{ s" O2C" 1st -> 36 1 }T
ps-reset
T{ s" >C" 1st -> 72 1 }T
ps-reset
T{ s" <C" 1st -> 48 1 }T
ps-reset
T{ s" O0<<<C" 1st -> 12 1 }T                       \ clamped at octave 0
ps-reset
T{ s" O7>>>C" 1st -> 96 1 }T                       \ ...and at 7
ps-reset

\ Lengths are 240/denominator, and a dot adds half of the last addition.
s" C4" 1st 2drop   T{ ps-len @ -> 60 }T
s" C8" 1st 2drop   T{ ps-len @ -> 30 }T
s" C4." 1st 2drop  T{ ps-len @ -> 90 }T
s" C4.." 1st 2drop T{ ps-len @ -> 105 }T
\ L sets the default for the notes that give none.
s" L8C" >sbuf (ps-start) (ps-next) 2drop
T{ ps-len @ ps-deflen @ -> 30 30 }T
ps-reset

\ The settings that are not notes, and the codes for the ones that are.
T{ s" V12" 1st -> 12 3 }T
T{ s" P2" 1st -> 2 4 }T
T{ s" I5" 1st -> 5 5 }T
T{ s" R8" 1st -> 0 2 }T
s" T90" >sbuf (ps-start) (ps-next) 2drop  T{ ps-tempo @ -> 90 }T
s" S4" >sbuf (ps-start) (ps-next) 2drop   T{ ps-art @ -> 4 }T
ps-reset
\ A bare V or I with no digits is ignored rather than read as a zero -
\ the ROM's parser does the same, and the alternative is a silent
\ instrument change nobody typed.
T{ s" VC" 1st -> 60 1 }T
\ Lower case is the same language.
T{ s" c" 1st -> 60 1 }T
ps-reset

cr .( testfm: FMPLAY and PSGPLAY reach the chips ) cr
\ Short notes on purpose: these BLOCK for real frames, and the suite has
\ a whole machine to test after this one. L64 at tempo 240 is under a
\ frame a note, so the string costs what the parse costs.
fminit
s" T240L64O4CDE" >sbuf 0 fmplay
T{ $28 ym@ -> 68 }T                     \ E4's key code, still in the shadow
T{ ps-octave @ -> 4 }T
ps-reset
psginit
63 0 psgvol
s" T240L64O4A" >sbuf 0 psgplay
T{ 0 psg@ 1 psg@ 8 lshift or -> 1181 }T \ A4 was the last frequency written
T{ 2 psg@ 63 and -> 0 }T                \ ...and the note released after it
ps-reset

cr .( testfm: the chord forms sound at once and return ) cr
\ One note per channel, no waiting: after a three-note chord from
\ channel 0 the parser is pointing at channel 3, and all three key codes
\ are in the shadow.
fminit
s" O4CEG" >sbuf 0 fmchord
T{ $28 ym@ -> 62 }T                     \ ch0 C4
T{ $29 ym@ -> 68 }T                     \ ch1 E4
T{ $2a ym@ -> 72 }T                     \ ch2 G4
T{ ps-ch @ -> 3 }T
ps-reset

cr .( testfm: FMFREQ, a frequency the chip has to be told as a note ) cr
\ The proof is that FMFREQ and FMMIDI agree: 440 Hz and MIDI 69 are the
\ same pitch, so they must produce the same key code and no fraction.
fminit
69 0 fmmidi
T{ $28 ym@ -> 74 }T                     \ MIDI 69 is A4...
440 0 fmfreq
T{ $28 ym@ $30 ym@ -> 74 0 }T           \ ...and so is 440 Hz, to the 64th
880 0 fmfreq
T{ $28 ym@ -> 90 }T                     \ an octave up is +16: KC is octave<<4
262 0 fmfreq
T{ $28 ym@ -> 62 }T                     \ C4, and a shade sharp of 261.63 Hz
T{ $30 ym@ 0> -> true }T
\ Half a semitone above A4 lands near half of 64: the fraction is real
\ arithmetic, not a rounded note.
453 0 fmfreq
T{ $28 ym@ -> 74 }T
T{ $30 ym@ 2 rshift 26 38 within -> true }T
0 0 fmfreq
T{ $08 ym@ -> 0 }T                      \ 0 Hz is a key-off on channel 0

cr .( testfm ok ) cr

---testfm---
