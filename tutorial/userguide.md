# durexForth X16 — User Guide

A tutorial for the Commander X16 port of durexForth. Every section below
matches one page of the built-in help (`HELP <topic>` on the machine), so you
can use this guide and the on-machine help interchangeably.

## Contents

- [1. Getting started](#1-getting-started)
- [2. STACK — data-stack juggling](#2-stack--data-stack-juggling)
- [3. ARITHMETIC](#3-arithmetic)
- [4. DOUBLE — 32-bit math](#4-double--32-bit-math)
- [5. LOGIC — comparisons and flags](#5-logic--comparisons-and-flags)
- [6. BITWISE](#6-bitwise)
- [7. CONSTANTS](#7-constants)
- [8. MEMORY](#8-memory)
- [9. NUMERIC — number output](#9-numeric--number-output)
- [10. NUMBER — parsing text](#10-number--parsing-text)
- [11. STRING](#11-string)
- [12. TERMINAL](#12-terminal)
- [13. INTERPRETER](#13-interpreter)
- [14. DEFINING — creating your own words](#14-defining--creating-your-own-words)
- [15. DICTIONARY — compiling and metaprogramming](#15-dictionary--compiling-and-metaprogramming)
- [16. FLOW — control structures](#16-flow--control-structures)
- [17. RETURN — the return stack](#17-return--the-return-stack)
- [18. STRUCTURE — records](#18-structure--records)
- [19. FILE — files, DOS, directories](#19-file--files-dos-directories)
- [20. SYSTEM](#20-system)
- [21. VIDEO — VERA, screen, cursor](#21-video--vera-screen-cursor)
- [22. TILE — tilemap cells and layers](#22-tile--tilemap-cells-and-layers)
- [23. PAL — palette](#23-pal--palette)
- [24. SPRITE](#24-sprite)
- [25. GRAPHIC — 320×240×256 bitmap (module)](#25-graphic--320240256-bitmap-module)
- [26. ADVGFX — clipping, flood fill, FX copy, rotozoom (module)](#26-advgfx--clipping-flood-fill-fx-copy-rotozoom-module)
- [27. ADVANCED — game math, buffers, compression (module)](#27-advanced--game-math-buffers-compression-module)
- [28. BMX — bitmap image files (module)](#28-bmx--bitmap-image-files-module)
- [29. AUDIOFM — PSG and FM notes](#29-audiofm--psg-and-fm-notes)
- [30. AUDIOYM — raw YM2151 registers](#30-audioym--raw-ym2151-registers)
- [31. AUDIOPCM — sampled sound](#31-audiopcm--sampled-sound)
- [32. ADVSND — envelopes, background PCM, ADPCM (module)](#32-advsnd--envelopes-background-pcm-adpcm-module)
- [33. VERAFX — the FX accelerator](#33-verafx--the-fx-accelerator)
- [34. INPUT — joysticks and mouse](#34-input--joysticks-and-mouse)
- [35. KEYBOARD](#35-keyboard)
- [36. LOADSAVE — PRG files, VRAM, verify](#36-loadsave--prg-files-vram-verify)
- [37. BANK — high-RAM banks](#37-bank--high-ram-banks)
- [38. KERNAL — calling the ROM](#38-kernal--calling-the-rom)
- [39. CONTROL — system control, clock, I2C](#39-control--system-control-clock-i2c)
- [40. FLOAT — floating point (module)](#40-float--floating-point-module)
- [41. BIT — bit and byte toolkit](#41-bit--bit-and-byte-toolkit)
- [42. ASSEMBLER — machine-code words](#42-assembler--machine-code-words)
- [43. Turnkey images — shipping a program](#43-turnkey-images--shipping-a-program)
- [Appendix A — module cheat sheet](#appendix-a--module-cheat-sheet)
- [Appendix B — things that bite](#appendix-b--things-that-bite)

---


## 1. Getting started

### Booting

There are three ways to run durexForth:

| How | What |
|---|---|
| `durexforth.crt` cartridge | The core system boots instantly from ROM. Extra features load on demand with `NEEDS`. |
| `durexforth_full.crt` cartridge | Same, plus the load/save and vramdisk libraries already resident. |
| `durexforth.prg` + SD card | `LOAD"DUREXFORTH.PRG"` from BASIC. Libraries and modules load from the card with `INCLUDE`. |

You land in the Forth interpreter (the *REPL*). Type words separated by
spaces, press RETURN, and they execute immediately:

```forth
2 3 + .          \ prints 5
ok
```

`ok` means the line was accepted. If you type an unknown word you get
`NAME?` and the line is abandoned — nothing is half-executed.

### The stack — the one idea you need

Forth passes everything on a **data stack**. Numbers push themselves;
words consume and produce stack items. `2 3 +` pushes 2, pushes 3, then `+`
replaces them with 5. `.` pops and prints.

Every word below is documented with a **stack effect** comment:

```
WORD ( before -- after )
```

`( n1 n2 -- n3 )` means: consumes two numbers, leaves one. Try `.S` at any
time to see the whole stack without disturbing it; if the stack gets into
a mess, `ABORT` clears everything and starts you fresh.

### Getting help on the machine

```forth
HELP             \ the topic index
HELP STACK       \ one topic page, paged with MORE
WORDS            \ every word currently defined
SEE DUP          \ decompile a word
```

### The edit / run loop

durexForth can launch the X816 resident editor and return to the Forth prompt:

```forth
EDIT                    \ open the resident editor
INCLUDE GAME.FS         \ compile a source file after it exists on the card
```

Named-file edit/save is still being ported to the X816 kernel filesystem API.
For now, compile files by name with `INCLUDE GAME.FS`. A file named `AUTORUN`
on the SD card is INCLUDEd automatically at every boot — put your startup
code there.

### On-demand modules: NEEDS and INCLUDE

Bigger word sets are **modules**. On a cartridge they live in ROM and cost
nothing until loaded:

```forth
NEEDS GRAPHIC        \ from cart ROM (cartridge boot)
INCLUDE GRAPHIC      \ same module from the SD card (prg boot)
```

Modules: `GRAPHIC FLOAT FLOATX FILE STRING SYSTEM EXTRAS ADVANCED ADVGFX
BMX ADVSND`. Each is ≤ 8 KB of source; only the compiled words cost RAM.

Note: `NEEDS` exists **only when booted from a cartridge** (it reads the
module out of cart ROM). On a `durexforth.prg` boot it is an unknown word
(`NEEDS?`) — use `INCLUDE <module>` there; the result is identical.

### Numbers and bases

`DECIMAL` and `HEX` switch the radix for input *and* output. A `$` prefix
is always hexadecimal regardless of base: `$FF .` prints `255`. A trailing
dot makes a 32-bit double: `1000000. D.`

### Word reference

- `HELP ( "topic" -- )` — show a help page. **topic**: a section name (STACK, VIDEO, ...); omitted = the index.
- `NEEDS ( "name" -- )` — load a module from cartridge ROM. **name**: module name, case-insensitive; already-loaded modules are recompiled, so guard with MARKER if needed.
- `INCLUDE ( "name" -- )` — compile a file from the SD card. **name**: filename as typed (no quotes).
- `EDIT ( -- )` — launch the X816 resident editor and return to Forth after exit. Named-file edit/save is still pending.
- `WORDS ( -- )`, `SEE ( "name" -- )`, `ABORT ( -- )` — list the dictionary / decompile a word / clear both stacks.

---

## 2. STACK — data-stack juggling

The daily drivers. Learn `DUP DROP SWAP OVER` first; the rest follow.

| Word | Effect | What it does | Example |
|---|---|---|---|
| `DUP` | `( x -- x x )` | duplicate the top | `5 DUP * .` → `25` |
| `DROP` | `( x -- )` | discard the top | `1 2 DROP .` → `1` |
| `SWAP` | `( a b -- b a )` | exchange the top two | `1 2 SWAP . .` → `1 2` |
| `OVER` | `( a b -- a b a )` | copy the second to top | `1 2 OVER .S` → `1 2 1` |
| `NIP` | `( a b -- b )` | drop the second | `1 2 NIP .` → `2` |
| `TUCK` | `( a b -- b a b )` | copy top under second | `1 2 TUCK .S` → `2 1 2` |
| `ROT` | `( a b c -- b c a )` | third to top | `1 2 3 ROT .` → `1` |
| `-ROT` | `( a b c -- c a b )` | top down to third | `1 2 3 -ROT .` → `2` |
| `PICK` | `( ... u -- ... x )` | copy the u-th item (0 = top) | `10 20 30 2 PICK .` → `10` |
| `ROLL` | `( ... u -- ... )` | move the u-th item to top | `1 2 3 2 ROLL .` → `1` |
| `DEPTH` | `( -- n )` | items on the stack | `1 2 DEPTH .` → `2` |
| `?DUP` | `( x -- x x \| 0 )` | dup only if non-zero | `0 ?DUP .` → `0` |
| `2DUP` | `( a b -- a b a b )` | dup the top pair | `1 2 2DUP D.` |
| `2DROP` | `( a b -- )` | drop a pair | |
| `2SWAP` | `( a b c d -- c d a b )` | swap pairs | |
| `2OVER` | `( a b c d -- a b c d a b )` | copy the 2nd pair | |
| `2ROT` | rotate the 3rd pair up | | |
| `.S` | `( -- )` | print the stack, unchanged | `1 2 .S` → `<2> 1 2` |

### Parameter legend

- **x, a, b, c, d** — any 16-bit cell (number, address, flag, xt).
- **u** (PICK/ROLL) — zero-based depth: `0 PICK` = DUP, `1 PICK` = OVER; `1 ROLL` = SWAP, `2 ROLL` = ROT.
- **n** (DEPTH) — number of cells currently on the stack, not counting n itself.
- Pair words (2DUP, 2SWAP, ...) treat two adjacent cells as one unit — a double or an addr/len string.

---

## 3. ARITHMETIC

16-bit signed integers (-32768..32767) unless stated. `.` pops and prints.

```forth
2 3 + .        \ 5        ( n1 n2 -- sum )
7 3 - .        \ 4        ( n1 n2 -- diff )
4 5 * .        \ 20       ( n1 n2 -- prod )
17 5 / .       \ 3        ( n1 n2 -- quot )   signed divide
17 5 MOD .     \ 2        ( n1 n2 -- rem )
17 5 /MOD . .  \ 3 2      ( n1 n2 -- rem quot )
```

The scaled and mixed-precision operations keep a 32-bit intermediate, so
they never overflow halfway:

- `*/ ( n1 n2 n3 -- n )` — n1×n2÷n3. `10 3 4 */ .` → `7`. **The** Forth idiom
  for fractions: `x 100 320 */` scales screen coordinates to percent.
- `*/MOD ( n1 n2 n3 -- rem quot )` — same with remainder.
- `UM* ( u1 u2 -- ud )` — unsigned 16×16→32. `1000 1000 UM* D.` → `1000000`
- `M* ( n1 n2 -- d )` — signed 16×16→32.
- `UM/MOD ( ud u -- urem uquot )` — 32÷16 unsigned.
- `FM/MOD ( d n -- rem quot )` — floored division of a double.
- `SM/REM ( d n -- rem quot )` — symmetric (truncating) division.
- `UD/MOD ( ud u -- urem udquot )` — 32÷16 with a 32-bit quotient.
- `UD* ( ud u -- ud )` — 32×16 unsigned.
- `M*/ ( d n1 n2 -- d )` — d×n1÷n2 via a 48-bit intermediate.

Small helpers:

```forth
-5 ABS .       \ 5         5 NEGATE .   \ -5
9 1+ .         \ 10        9 1- .       \ 8
9 2+ .         \ 11        9 2- .       \ 7
5 2* .         \ 10        -10 2/ .     \ -5  (keeps sign)
3 9 MAX .      \ 9         3 9 MIN .    \ 3
-7 SGN .       \ -1  (sign: -1, 0 or 1, like BASIC SGN)
```

### Word reference

- `+ - * ( n1 n2 -- n3 )` — **n1, n2**: signed 16-bit operands; result wraps modulo 65536.
- `/ MOD /MOD ( n1 n2 -- ... )` — **n1**: dividend; **n2**: divisor (must be non-zero). `/MOD` leaves remainder under quotient.
- `*/ */MOD ( n1 n2 n3 -- ... )` — **n1 × n2** kept in 32 bits, then divided by **n3**; no halfway overflow.
- `FM/MOD ( d n -- rem quot )` — **d**: 32-bit signed dividend (two cells); **n**: 16-bit divisor. Floored: remainder has the divisor's sign.
- `SM/REM ( d n -- rem quot )` — same operands, symmetric: quotient truncates toward zero, remainder has the dividend's sign.
- `UM/MOD ( ud u -- urem uquot )` — **ud**: unsigned 32-bit dividend; **u**: unsigned divisor; both results 16-bit.
- `UD/MOD ( ud u -- urem udquot )` — like UM/MOD but the quotient stays 32-bit (**udquot** = two cells).
- `UM* ( u1 u2 -- ud )` / `M* ( n1 n2 -- d )` — full 16×16→32 multiply, unsigned / signed.
- `UD* ( ud u -- ud' )` — 32-bit × 16-bit, low 32 bits kept.
- `M*/ ( d n1 n2 -- d' )` — d × n1 / n2 through a 48-bit intermediate. **n1, n2**: signed singles (|n| ≤ 32767); truncates toward zero.
- `ABS ( n -- u )`, `NEGATE ( n -- -n )`, `SGN ( n -- -1|0|1 )` — magnitude, sign flip, sign extraction.
- `1+ 1- 2+ 2- ( n -- n' )` — fast add/subtract of 1 or 2.
- `2* ( n -- n*2 )` / `2/ ( n -- n/2 )` — arithmetic shifts; 2/ keeps the sign bit.
- `MAX MIN ( n1 n2 -- n )` — signed larger / smaller.

---

## 4. DOUBLE — 32-bit math

A *double* is two stack cells (low, then high). Type one with a trailing
dot: `100000.` — and print with `D.`.

```forth
100000. 250000. D+ D.    \ 350000
5 S>D D.                 \ 5      ( n -- d ) sign-extend
1000000. D>S             \ drop the high cell (must fit 16 bits!)
```

| Word | Effect |
|---|---|
| `S>D` / `D>S` | single ↔ double |
| `D+` `D-` | add / subtract doubles |
| `DNEGATE` `DABS` | negate / absolute value |
| `M+ ( d n -- d )` | add a single to a double |
| `D2*` `D2/` | double / halve |
| `D=` `D<>` `D<` `D>` `DU<` `DU>` `D0=` `D0<>` `D0<` `D0>` | comparisons (returns a flag) |
| `DMAX` `DMIN` | larger / smaller |
| `D.` `D.R` | print (D.R right-justified in a width) |

Example — average two big numbers: `: DAVG ( d1 d2 -- d ) D+ D2/ ;`

### Word reference

A double **d** is two cells: low half pushed first, high half on top. **ud** = unsigned double.

- `S>D ( n -- d )` — sign-extend a single. `D>S ( d -- n )` — drop the high cell (value must fit 16 bits).
- `D+ D- ( d1 d2 -- d3 )` — 32-bit add / subtract.
- `M+ ( d n -- d' )` — add a signed single to a double.
- `DNEGATE ( d -- -d )` / `DABS ( d -- ud )` — negate / absolute value.
- `D2* ( d -- d*2 )` / `D2/ ( d -- d/2 )` — shift; D2/ is arithmetic.
- `D= D<> D< D> ( d1 d2 -- flag )` — equality / signed compare. `DU< DU> ( ud1 ud2 -- flag )` — unsigned compare.
- `D0= D0<> D0< D0> ( d -- flag )` — test against zero.
- `DMAX DMIN ( d1 d2 -- d )` — larger / smaller.
- `D. ( d -- )` / `D.R ( d width -- )` — print; **width**: minimum field width, right-justified, space-padded.

---

## 5. LOGIC — comparisons and flags

Comparisons leave **TRUE (-1)** or **FALSE (0)**; `IF` treats any non-zero
as true.

```forth
5 5 =  .        \ -1          3 9 <  .    \ -1  (signed)
5 0<> .         \ -1          0 0=   .    \ -1  (also logical NOT)
-3 0< .         \ -1          4 0>   .    \ -1
3 9 U< .        \ -1  (unsigned — use for addresses!)
5 0 10 WITHIN . \ -1  (lo <= n < hi)
```

`<>` is not-equal, `U>` unsigned greater. Remember: comparing *addresses*
with signed `<` breaks above $7FFF — always `U<`.

### Word reference

All comparisons consume their operands and leave **flag**: TRUE (-1) or FALSE (0).

- `0= 0< 0> 0<> ( n -- flag )` — compare one value against zero. `0=` doubles as logical NOT on flags.
- `= <> ( a b -- flag )` — equal / not equal (any cells).
- `< > ( n1 n2 -- flag )` — **signed** compare: `n1 < n2` etc.
- `U< U> ( u1 u2 -- flag )` — **unsigned** compare — required for addresses.
- `WITHIN ( n lo hi -- flag )` — true when lo ≤ n < hi (half-open; works signed or unsigned as long as all three are the same kind).

---

## 6. BITWISE

```forth
HEX
0F 33 AND .     \ 3        ( x1 x2 -- x3 )
0F 30 OR  .     \ 3F
FF 0F XOR .     \ F0
0 INVERT .      \ FFFF (= -1)  flip all bits
1 4 LSHIFT .    \ 10       logical shift left
100 4 RSHIFT .  \ 10       logical shift right
DECIMAL
```

Typical register work: `$9F26 C@ 8 OR $9F26 C!` sets bit 3 of an I/O byte.

### Word reference

- `AND OR XOR ( x1 x2 -- x3 )` — bitwise combine two cells.
- `INVERT ( x -- ~x )` — flip all 16 bits (one's complement).
- `LSHIFT RSHIFT ( x u -- x' )` — **u**: shift count 0–15; RSHIFT is logical (zero fill), unlike the arithmetic `2/`.

---

## 7. CONSTANTS

`0 1 2 -1` are fast single-byte words; `TRUE` = -1, `FALSE` = 0,
`BL` = 32 (the space character): `BL EMIT` prints a space.

### Word reference

- `0 1 2 -1 ( -- n )` — the constant itself; single-byte fast words.
- `TRUE ( -- -1 )` / `FALSE ( -- 0 )` — canonical flags.
- `BL ( -- 32 )` — the space character code.

---

## 8. MEMORY

Cells are 16-bit, addresses are byte addresses. The dictionary grows from
`HERE`; `ALLOT` reserves space.

```forth
VARIABLE X
42 X !          \ store a cell        ( x addr -- )
X @ .           \ fetch → 42          ( addr -- x )
1 X +!          \ add to the cell     ( n addr -- )
X ?             \ fetch-and-print → 43

$9F61 C@ .      \ fetch a byte        ( addr -- c )
7 $0400 C!      \ store a byte        ( c addr -- )
```

Building data structures:

```forth
CREATE TABLE  1 , 2 , 3 ,     \ , compiles a cell at HERE
CREATE BUF  100 ALLOT          \ reserve 100 bytes
TABLE 2 CELLS + @ .            \ 3   (CELLS: n cells → bytes)
BUF 100 ERASE                  \ zero it        ( addr u -- )
BUF 100 BL FILL                \ fill with spaces ( addr u c -- )
```

- `MOVE ( src dst u -- )` — copy, overlap-safe. `CMOVE`/`CMOVE>` force
  low→high / high→low order.
- `2@ ( addr -- lo hi )` / `2! ( lo hi addr -- )` — a double in memory.
- `PAD ( -- addr )` — scratch buffer, valid until the next PAD user.
- `HERE UNUSED` — next free byte / bytes remaining.
- `CELL+ CHAR+ CHARS ALIGN ALIGNED` — portable address arithmetic
  (ALIGN words are no-ops on the 65C02).
- `DUMP ( addr -- )` — hex dump 8 lines; `N` continues where it stopped.

### Word reference

**addr** is always a byte address; **u** a byte count; **c** a byte value 0–255.

- `@ ( addr -- x )` / `! ( x addr -- )` — fetch / store one 16-bit cell (little-endian).
- `C@ ( addr -- c )` / `C! ( c addr -- )` — fetch / store one byte.
- `2@ ( addr -- lo hi )` / `2! ( lo hi addr -- )` — fetch / store a double (two cells).
- `+! ( n addr -- )` — add n into the cell at addr.
- `, ( x -- )` / `C, ( c -- )` — compile a cell / byte at HERE, advancing it.
- `HERE ( -- addr )` — next free dictionary byte. `UNUSED ( -- u )` — bytes left.
- `ALLOT ( n -- )` — reserve **n** bytes of dictionary (negative gives space back — dangerous).
- `PAD ( -- addr )` — scratch area; contents survive only until the next PAD user.
- `ERASE ( addr u -- )` / `BLANK ( addr u -- )` / `FILL ( addr u c -- )` — set u bytes to 0 / spaces / **c**.
- `MOVE ( src dst u -- )` — copy u bytes, safe for overlap. `CMOVE` forces ascending, `CMOVE>` descending order (matters only when regions overlap).
- `CELL+ ( addr -- addr+2 )` / `CELLS ( n -- n*2 )` — cell address math. `CHAR+ CHARS ALIGN ALIGNED` — byte equivalents / no-ops here.
- `DUMP ( addr -- )` — 8-line hex dump from **addr**; `N ( -- )` dumps the next 8 lines.

---

## 9. NUMERIC — number output

```forth
42 .            \ signed, current base
42 U.           \ unsigned
5 4 .R          \ right-justified in 4 columns:    5
1000000. D.     \ print a double
X ?             \ print the cell at an address
HEX 255 . DECIMAL   \ FF
BASE @ .        \ the radix variable itself
```

**Pictured output** builds a string digit by digit (right to left) —
this is how you get thousands separators, decimal points, zero padding:

```forth
: .## ( n -- )  0 <# # # [CHAR] . HOLD #S #> TYPE ;
1234 .##        \ 12.34
```

`<#` starts, `#` converts one digit, `#S` the rest, `HOLD` inserts a
character, `SIGN` a minus, `#>` finishes leaving `( addr len )` for `TYPE`.
`HOLDS ( addr len -- )` inserts a whole string.

### Word reference

- `. ( n -- )` / `U. ( u -- )` — print signed / unsigned in the current BASE, then a space.
- `.R ( n width -- )` / `U.R ( u width -- )` / `D.R ( d width -- )` — right-justified. **width**: minimum columns; longer numbers just overflow it.
- `? ( addr -- )` — shorthand for `@ .`.
- `<# ( -- )` — start pictured output (works on an **ud** on the stack).
- `# ( ud -- ud' )` — extract one digit (current BASE) into the picture, right to left.
- `#S ( ud -- 0 0 )` — extract all remaining digits (at least one).
- `HOLD ( c -- )` — insert character **c** into the picture. `HOLDS ( addr len -- )` — insert a string.
- `SIGN ( n -- )` — insert `-` if **n** (usually the original number) is negative.
- `#> ( ud -- addr len )` — finish; leaves the string for TYPE.
- `DECIMAL HEX ( -- )` — set BASE to 10 / 16. `BASE ( -- addr )` — the radix variable itself (2–36 make sense).

---

## 10. NUMBER — parsing text

- `>NUMBER ( ud addr len -- ud' addr' len' )` — accumulate digits.
- `PARSE-NAME ( -- addr len )` — next space-delimited word from the input.
- `PARSE ( c -- addr len )` — up to delimiter c.
- `WORD ( c -- c-addr )` — like PARSE but a counted string.
- `FIND ( c-addr -- c-addr 0 | xt 1 | xt -1 )` — dictionary lookup.

```forth
: ECHO-NAME  PARSE-NAME TYPE ;   ECHO-NAME HELLO   \ prints HELLO
S" 123" VAL .                     \ 123 (VAL is in the STRING module)
```

### Word reference

- `>NUMBER ( ud addr len -- ud' addr' len' )` — accumulate digits of the current BASE into **ud** until a non-digit; leaves the unconsumed rest of the string.
- `PARSE-NAME ( -- addr len )` — next whitespace-delimited word from the input line.
- `PARSE ( c -- addr len )` — everything up to delimiter **c** (no leading-space skip).
- `WORD ( c -- c-addr )` — like PARSE, skipping leading delimiters, result as a counted string at a transient buffer.
- `FIND ( c-addr -- c-addr 0 | xt 1 | xt -1 )` — look up a counted name. 1 = immediate word, -1 = ordinary, 0 = not found (name left).

---

## 11. STRING

`S" ." COUNT TYPE /STRING CHAR [CHAR]` are always there; the full set is
the STRING module (`NEEDS STRING` / `INCLUDE STRING`). Run-time built
strings (interpreted `S\"`, `C"`, `CHR`, `RPT`) live in PAD — copy them
if you need them to survive (max 189 chars).

```forth
S" hello" TYPE            \ hello     ( -- addr len )
: HI ." hello" ;  HI      \ print inside a definition
CHAR A .                  \ 65
S\" line1\nline2" TYPE    \ escapes: \n \t \" \\ \e ...
```

With the module:

```forth
S" HELLO" 2 3 MID TYPE    \ ELL   ( start is 1-based )
S" HELLO" 2 LEFT TYPE     \ HE
S" HELLO" 2 RIGHT TYPE    \ LO
S" 42" VAL .              \ 42
-12 STR TYPE              \ -12
255 NHEX TYPE             \ FF
5 NBIN TYPE               \ 101
[CHAR] * 5 RPT TYPE       \ *****
S" ABC" S" ABD" COMPARE . \ -1  (-1/0/1 like strcmp)
```

- `COUNT ( c-addr -- addr len )` — unpack a counted string.
- `PLACE / +PLACE ( addr len dst -- )` — store / append as counted string.
- `/STRING ( addr len n -- addr+n len-n )` — chop n chars off the front.
- `ASC LEN CHR` — first char code / length / one-char string.
- `LINPUT ( addr n -- n2 )` — read a keyboard line into a buffer.
- `SLITERAL ( addr len -- )` — compile a string literal.
- `TIB #TIB` — the terminal input buffer and its count.

### Word reference

A string is **( c-addr u )** — address + length. A *counted* string is one byte of length followed by the text.

- `S" ( "text" -- addr len )` — string literal. Interpreted: transient buffer; compiled: stored in the definition.
- `"text"` — the same without the space after the quote: `"hello world" TYPE`. Defined words always win over this syntax; an unterminated string errors like an unknown word.
- `S\" ( "text" -- addr len )` — same with escapes: `\n \t \" \\ \e \xAB \m` (CR+LF).
- `." ( "text" -- )` — compile-time: print the text when the word runs.
- `C" ( "text" -- c-addr )` — counted-string literal.
- `COUNT ( c-addr -- addr len )` — unpack a counted string.
- `TYPE ( addr len -- )` — print it.
- `COMPARE ( a1 u1 a2 u2 -- n )` — lexicographic: -1 / 0 / 1.
- `/STRING ( addr len n -- addr+n len-n )` — drop **n** chars from the front (n may exceed len — clamp yourself).
- `PLACE ( addr len dst -- )` / `+PLACE` — store / append as a counted string at **dst** (your buffer, sized len+1).
- `CHAR ( "c" -- n )` / `[CHAR] ( "c" -- )` — next char's code, interpret / compile version.
- `LINPUT ( c-addr +n -- +n2 )` — keyboard line into your buffer of size **+n**; returns count typed.
- `STR ( n -- addr u )` / `VAL ( addr u -- n )` — number ↔ string in the current BASE.
- `NHEX NBIN ( u -- addr u )` — unsigned as hex / binary digits.
- `ASC ( addr u -- code )` — first character's code. `LEN ( addr u -- u )` — the count. `CHR ( code -- addr 1 )` — one-char string in PAD.
- `LEFT ( a u n -- a n' )` / `RIGHT ( a u n -- a' n' )` — first / last **n** chars. `MID ( a u start len -- a' len' )` — substring, **start is 1-based**.
- `RPT ( char n -- addr u )` — **char** repeated **n** times (PAD, ≤ 189).
- `SLITERAL ( addr len -- )` — compile the string into the current definition.
- `TIB ( -- addr )` / `#TIB ( -- addr )` — terminal input buffer and its count variable.
- `GETKEY ( -- char )` — wait for a key, PETSCII, no echo.

---

## 12. TERMINAL

```forth
65 EMIT          \ A
CR               \ newline
3 SPACES         \ three blanks
KEY EMIT         \ wait for a key, echo it
PAGE             \ clear screen + home
RVS ." bar" RVS  \ reverse-video text
.( compiling...) \ print immediately, even mid-compile
PAD 40 ACCEPT    \ read a line (max 40) → count
```

### Word reference

- `EMIT ( c -- )` — output one character code (PETSCII).
- `CR ( -- )` — newline. `SPACE ( -- )` — one blank. `SPACES ( n -- )` — **n** blanks (n ≤ 0 prints none).
- `KEY ( -- c )` — wait for a keypress, return its code.
- `ACCEPT ( addr n -- len )` — edit a line into **addr** (capacity **n**); returns chars actually taken.
- `.( text) ( -- )` — print immediately, even while compiling (progress messages in source files).
- `PAGE ( -- )` — clear screen, home cursor. `RVS ( -- )` — toggle reverse video.

---

## 13. INTERPRETER

- `EVALUATE ( addr len -- )` — interpret a string: `S" 2 3 +" EVALUATE .` → 5
- `SOURCE ( -- addr len )` / `>IN` — current input line and parse offset.
- `SOURCE-ID` — 0 = keyboard, -1 = EVALUATE, else a fileid.
- `REFILL ( -- flag )` — pull the next input line.
- `STATE` — non-zero while compiling.
- `( comment )` and `\ line comment`.

Fun trick — a word that re-reads its own line: `>IN @` / `>IN !` rewinds
the parser.

### Word reference

- `EVALUATE ( addr len -- )` — run a string through the interpreter/compiler.
- `SOURCE ( -- addr len )` — the current input line. `>IN ( -- addr )` — variable: parse offset into it (set it to re-parse).
- `SOURCE-ID ( -- id )` — 0 keyboard, -1 EVALUATE string, otherwise the fileid being INCLUDEd.
- `REFILL ( -- flag )` — fetch the next line from the current source; false at end.
- `STATE ( -- addr )` — variable, non-zero while compiling. Read it; don't write it (use `[` and `]`).
- `( comment )` / `\ comment` — inline / to-end-of-line comments.

---

## 14. DEFINING — creating your own words

```forth
: SQUARE ( n -- n² ) DUP * ;     \ colon ... semicolon
5 SQUARE .                        \ 25

VARIABLE SCORE     10 CONSTANT LIVES
5 VALUE SPEED      7 TO SPEED     \ VALUEs read bare, written with TO
2VARIABLE BIGNUM   100000. 2CONSTANT MILLION-TENTH
80 BUFFER: LINEBUF                \ n-byte buffer word
```

### VARIABLE vs VALUE vs CONSTANT

All three hold one cell — they differ in how you read and write it:

| | `VARIABLE X` | `10 VALUE X` | `10 CONSTANT X` |
|---|---|---|---|
| read | `X @` | `X` | `X` |
| write | `42 X !` | `42 TO X` | never |
| initialised? | **no** — junk until you store | yes, at creation | yes |
| its address | `X` pushes it | not meant for that | not meant for that |

A **VARIABLE**'s name pushes the *address* of its cell; you do the memory
access yourself with `@` and `!` (and friends like `+!` and `?`). A
**VALUE**'s name pushes the *contents* directly — no `@` — and is written
through `TO`. A **CONSTANT** is a VALUE you can never change.

```forth
VARIABLE SCORE  0 SCORE !     \ address-style: read X @, write n X !
1 SCORE +!  SCORE ?           \ 1

25 VALUE SPEED                \ contents-style: read bare, write with TO
SPEED 2* TO SPEED  SPEED .    \ 50   (variable spelling: SPEED @ 2* SPEED !)
```

Rule of thumb: **CONSTANT** if it never changes; **VALUE** for something
read often and written rarely (a speed, a mode, a device number — the
reads stay clean); **VARIABLE** when it changes constantly or you need the
address itself (`+!` counters, buffers, anything other code pokes at).

Two traps: a fresh **VARIABLE is not zeroed** in durexForth — after a
MARKER/FORGET cycle it holds old dictionary bytes, so always initialise
(`VARIABLE X 0 X !`). And `TO` writes into whatever name *follows* it in
the source, so it only works with names, not computed xts.

`CREATE` + `DOES>` is Forth's defining-word factory — the created word
pushes its data field, `DOES>` adds behaviour:

```forth
: ARRAY ( n "name" -- ) CREATE CELLS ALLOT DOES> SWAP CELLS + ;
10 ARRAY SCORES          \ SCORES ( i -- addr )
42 3 SCORES !   3 SCORES @ .    \ 42
```

Deferred words are runtime-switchable:

```forth
DEFER GREET
: HI ." hi" ;   ' HI IS GREET   GREET      \ hi
ACTION-OF GREET  ( -- xt )                 \ read it back
```

`:NONAME ... ;` leaves an xt instead of naming the word — handy for
callbacks: `:NONAME 1 SCORE +! ; IRQ`.

### Word reference

`"name"` means the word parses a name from the input line right after it.

- `: ( "name" -- )` / `; ( -- )` — begin / end a definition. `:NONAME ( -- xt )` — anonymous; `;` leaves the xt.
- `CREATE ( "name" -- )` — name pushes its data-field address (build data after it with `,` / ALLOT).
- `VARIABLE ( "name" -- )` — one uninitialised cell (store before first use!). `2VARIABLE` — two cells.
- `CONSTANT ( n "name" -- )` / `2CONSTANT ( d "name" -- )` — name pushes the value.
- `VALUE ( n "name" -- )` / `2VALUE ( d "name" -- )` — like CONSTANT but writable with TO.
- `TO ( n "name" -- )` — store into a VALUE/2VALUE (picks the size automatically).
- `BUFFER: ( n "name" -- )` — name pushes an **n**-byte uninitialised buffer.
- `DOES> ( -- )` — ends the building part of a defining word; what follows runs when the *created* word executes, with its data address on the stack.
- `DEFER ( "name" -- )` — a word whose action is assigned later. `IS ( xt "name" -- )` — assign it. `ACTION-OF ( "name" -- xt )` — read it.
- `DEFER@ ( xt1 -- xt2 )` / `DEFER! ( xt2 xt1 -- )` — the same via execution tokens. **xt1**: the deferred word; **xt2**: its action.
- `SYNONYM ( "new" "old" -- )` — make **new** an alias of **old**: same xt, zero overhead, immediacy preserved. `SYNONYM PRINT .`

---

## 15. DICTIONARY — compiling and metaprogramming

```forth
' DUP EXECUTE        \ tick: get a word's xt, run it later
: RUN2 ( xt -- ) DUP EXECUTE EXECUTE ;
MARKER SANDBOX       \ ...experiment... SANDBOX forgets it all
FORGET SQUARE        \ remove SQUARE and everything after it
SEE SQUARE           \ decompile
SIZE SQUARE          \ byte cost of a definition
```

- `[']` — compile-time tick (inside definitions).
- `LITERAL` — compile a computed value: `: K [ 2 3 + ] LITERAL ;`
- `POSTPONE` — compile another word's compile-time action (for writing
  your own IMMEDIATE control words).
- `IMMEDIATE` — the latest definition runs even while compiling.
- `COMPILE, ( xt -- )` — append a call.
- `[` `]` — drop to interpret state and back mid-definition.
- `>BODY ( xt -- addr )` — a CREATEd word's data field.
- `," ( "str" -- )` — compile a counted string inline.
- Introspection: `LATEST NAME>STRING FIND-NAME >XT DOWORDS HIDE`.

Example — walk the dictionary printing every name:

```forth
:NONAME ( nt -- flag ) NAME>STRING TYPE SPACE TRUE ; DOWORDS
```

### Word reference

**xt** = execution token (what EXECUTE runs); **nt** = name token (a dictionary header).

- `' ( "name" -- xt )` — look up at run time. `['] ( "name" -- )` — compile the xt as a literal (inside definitions).
- `EXECUTE ( xt -- )` — run it (documented under FLOW).
- `COMPILE, ( xt -- )` — append a call to the definition being compiled.
- `LITERAL ( x -- )` — immediate: compile the value on the stack now as an inline constant. `2LITERAL ( d -- )` — double version.
- `POSTPONE ( "name" -- )` — compile the *compilation* behaviour of name (the standard way to build new control words). `[COMPILE]`, `COMPILE` — legacy variants.
- `[ ( -- )` / `] ( -- )` — leave / re-enter compile state mid-definition (compute a value at compile time).
- `IMMEDIATE ( -- )` — mark the latest definition to run during compilation.
- `MARKER ( "name" -- )` — running name later forgets everything defined after the marker (including itself).
- `FORGET ( "name" -- )` — remove name and everything after it.
- `>BODY ( xt -- addr )` — data field of a CREATEd word.
- `?COMP ( -- )` — abort unless compiling. `?STACK ( -- )` — abort on stack under/overflow.
- `," ( "text" -- )` — compile the text as a counted string at HERE.
- `LATEST ( -- nt )` — most recent definition. `NAME>STRING ( nt -- addr u )` — its name. `>XT ( nt -- xt )` — its code. `FIND-NAME ( addr u -- nt|0 )` — lookup.
- `DOWORDS ( xt -- )` — call your **xt ( nt -- flag )** for every word; return false to stop the walk.
- `HIDE ( "name" -- )` — remove a word from the search order (it still runs if referenced).
- `SEE ( "name" -- )` / `SIZE ( "name" -- )` — decompile / print byte cost.

---

## 16. FLOW — control structures

All of these live **inside colon definitions**.

```forth
: SIGN. ( n -- ) DUP 0< IF ." neg" ELSE 0> IF ." pos" ELSE ." zero" THEN THEN ;

: COUNTDOWN ( n -- ) BEGIN DUP . 1- DUP 0= UNTIL DROP ;
: FOREVER   BEGIN ." tick " AGAIN ;
: SUM       ( n -- sum ) 0 SWAP BEGIN DUP 0> WHILE TUCK + SWAP 1- REPEAT DROP ;

: TABLE.    10 0 DO I . LOOP ;          \ 0..9  (limit start DO)
: EVENS     10 0 DO I . 2 +LOOP ;       \ 0 2 4 6 8
: MAYBE     ( n -- ) 0 ?DO I . LOOP ;   \ skips entirely when n = 0
```

`I` and `J` read the inner / outer loop index; `LEAVE` exits the loop,
`UNLOOP EXIT` leaves loop *and* word early.

```forth
: DIGIT? ( c -- )
  CASE
    [CHAR] 0 OF ." zero" ENDOF
    [CHAR] 1 OF ." one"  ENDOF
    ." other"
  ENDCASE ;
```

Errors — `ABORT" message"` for fatal, CATCH/THROW for recoverable:

```forth
: RISKY ( n -- ) 0= IF -99 THROW THEN ." fine" ;
: TRY   ( n -- ) ['] RISKY CATCH IF ." caught!" THEN ;
1 TRY    \ fine        0 TRY    \ caught!
```

`RECURSE` calls the word being defined; `EXECUTE ( xt -- )` calls an xt;
`QUIT` restarts the interpreter, `ABORT` clears the stacks first.

### Word reference

All compile-only (use inside `:` definitions). **flag**: any non-zero = true.

- `IF ( flag -- ) ... ELSE ... THEN` — two-way branch; ELSE optional.
- `BEGIN ... UNTIL ( flag -- )` — loop until flag true (test at the bottom).
- `BEGIN ... AGAIN` — endless. `BEGIN ... WHILE ( flag -- ) ... REPEAT` — test in the middle; false exits past REPEAT.
- `DO ( limit start -- ) ... LOOP` — counted loop from **start** while index < **limit** (runs at least once — see ?DO).
- `?DO ( limit start -- )` — skips the whole loop when limit = start.
- `+LOOP ( n -- )` — add **n** (may be negative) to the index; crossing the limit boundary exits.
- `I ( -- n )` / `J ( -- n )` — innermost / next-outer index.
- `LEAVE ( -- )` — exit the loop now. `UNLOOP ( -- )` — discard loop bookkeeping before an early EXIT.
- `CASE ( -- ) x OF ... ENDOF ... ENDCASE` — `OF ( sel x -- sel )` runs its clause when sel = **x** (consuming both); `ENDCASE ( sel -- )` drops the selector on fall-through.
- `RECURSE ( -- )` — call the word being defined.
- `EXIT ( -- )` — return from the word immediately (inside a DO loop: UNLOOP first).
- `EXECUTE ( i*x xt -- j*x )` — run **xt** with whatever stack contract it has.
- `ABORT ( -- )` — clear both stacks, back to the prompt. `ABORT" ( flag "msg" -- )` — do that with a message when flag is true.
- `CATCH ( i*x xt -- j*x 0 | i*x n )` — run xt; 0 = completed, else the THROW code **n** with the stack depth restored (values may be garbage — refetch).
- `THROW ( n -- )` — non-zero **n** unwinds to the nearest CATCH; `0 THROW` is a no-op. Uncaught = ABORT with a message.
- `QUIT ( -- )` — reset the return stack and interpreter, keep the data stack.
- `AHEAD ( -- )` — unconditional forward branch, resolved by THEN (for control-word authors).

---

## 17. RETURN — the return stack

A second stack for temporaries and loop bookkeeping. **Balance it before
your word ends** — its top is your return address.

```forth
: THIRD ( a b c -- a b c a ) >R OVER R> SWAP ;
```

`>R R> R@ RDROP 2>R 2R> 2R@` — move / copy singles and pairs. `I` and `J`
read loop indices kept there. Never `>R` in interpreted text (outside a
definition) — the interpreter needs its own return stack.

### Word reference

- `>R ( x -- ) ( R: -- x )` — move the top cell to the return stack.
- `R> ( -- x ) ( R: x -- )` — move it back. Must balance within the same word!
- `R@ ( -- x )` — copy the top of the return stack.
- `RDROP ( R: x -- )` — discard it.
- `2>R 2R> 2R@` — pair versions (order preserved: `2>R` then `2R>` round-trips).
- `I J ( -- n )` — loop indices live here; that is why `>R` inside a DO loop shifts what I sees — balance before LOOP.

---

## 18. STRUCTURE — records

```forth
BEGIN-STRUCTURE POINT
  FIELD:  P.X          \ one cell
  FIELD:  P.Y
  CFIELD: P.TAG        \ one byte
END-STRUCTURE

CREATE P1 POINT ALLOT
10 P1 P.X !   20 P1 P.Y !
P1 P.X @ .           \ 10
```

`+FIELD ( u n "name" -- u' )` makes a field of any size (e.g.
`8 +FIELD P.NAME`).

### Word reference

- `BEGIN-STRUCTURE ( "name" -- addr 0 )` — start; leaves bookkeeping (address to patch + running offset 0).
- `FIELD: ( u "name" -- u' )` — a one-cell field at offset **u**; the new word is `( addr -- addr+u )`.
- `CFIELD: ( u "name" -- u' )` — a one-byte field.
- `+FIELD ( u n "name" -- u' )` — an **n**-byte field (arrays, strings inside records).
- `END-STRUCTURE ( addr u -- )` — finish; the structure name now pushes the total size **u** (use with ALLOT).

---

## 19. FILE — files, DOS, directories

`INCLUDE`, `REQUIRE` and the DOS words are always resident; the ANS file
set is the FILE module (`NEEDS FILE`). fileids are KERNAL logical files
2..7 — up to 6 open at once.

```forth
INCLUDE GAME.FS              \ compile a file
REQUIRE LIB.FS               \ only once
S" GAME.FS" INCLUDED         \ same, from a string

NEEDS FILE
S" DATA.TXT" R/O OPEN-FILE ABORT" open?"  VALUE FD
PAD 80 FD READ-LINE ABORT" read?" ( len flag ) DROP PAD SWAP TYPE
FD CLOSE-FILE DROP

S" LOG.TXT" W/O CREATE-FILE DROP VALUE LOG
S" hello" LOG WRITE-LINE DROP
LOG CLOSE-FILE DROP

S" OLD.TXT" DELETE-FILE DROP
S" OLD.TXT" S" NEW.TXT" RENAME-FILE DROP  \ ( old-name new-name -- ior )
S" DATA.TXT" FILE-STATUS NIP 0= .         \ -1 if it exists
```

Positioning (FAT32 card): `FILE-POSITION FILE-SIZE REPOSITION-FILE`
work through the CBDOS T/P commands. `R/O W/O R/W` are the access modes
(`R/W` = CBDOS modify — the file must exist), `BIN` is accepted and a
no-op.

Directory and DOS:

```forth
DIR                      \ list the directory
S" SUBDIR" CD            \ change dir;  S" .." CD up;  S" /" CD root
LS *.FS                  \ list with a pattern
DOS S:OLDFILE            \ raw DOS command (scratch)
RDERR                    \ read the drive error channel
8 DEVICE                 \ pick the disk device (default 8)
```

### Word reference

**fileid**: KERNAL logical file number 2–7 (max 6 open). **ior**: 0 = success, non-zero = error. **fam**: access method from `R/O W/O R/W` (+ `BIN`, a no-op).

- `INCLUDE ( "name" -- )` / `INCLUDED ( addr u -- )` — compile a file (nestable, ≈4 deep).
- `REQUIRE ( "name" -- )` / `REQUIRED ( addr u -- )` — compile only if not already included.
- `INCLUDE-FILE ( fileid -- )` — interpret the rest of an open file (≤ 8 KB; do not nest).
- `OPEN-FILE ( addr u fam -- fileid ior )` — open. `CLOSE-FILE ( fileid -- ior )`.
- `CREATE-FILE ( addr u fam -- fileid ior )` — create/overwrite, then open.
- `DELETE-FILE ( addr u -- ior )` — DOS scratch. `RENAME-FILE ( aold uold anew unew -- ior )`.
- `READ-FILE ( addr u fileid -- u2 ior )` — read up to **u** bytes; **u2** actually read.
- `READ-LINE ( addr u fileid -- u2 flag ior )` — one line (CR or LF); **flag** false only at end of file.
- `WRITE-FILE ( addr u fileid -- ior )` / `WRITE-LINE` — write u bytes (LINE adds CR).
- `FILE-POSITION ( fileid -- ud ior )` / `FILE-SIZE ( fileid -- ud ior )` — 32-bit position / size.
- `REPOSITION-FILE ( ud fileid -- ior )` — seek to byte **ud** (FAT32 card only).
- `FILE-STATUS ( addr u -- x ior )` — ior 0 = the named file exists.
- `RESIZE-FILE` — always ior -1 (unsupported). `FLUSH-FILE` — no-op (CBDOS flushes on close).
- `CD ( addr u -- )` — change directory (`..` up, `/` root). `DIR ( -- )` — list.
- `DEVICE ( n -- )` — select the disk unit, default 8.
- `DOS ( "cmd" -- )` — raw command to the drive; prints the reply. `SEND-CMD ( addr u -- )` — same from a string. `RDERR ( -- )` — read the error channel.
- `LS ( "pattern" -- )` — directory listing, optional wildcard.
- `OPEN ( addr u fam -- fileid ior )` / `CLOSE ( fileid -- ior )` — low-level channel open/close.

---

## 20. SYSTEM

- `HELP ( "topic" )` — the on-machine version of this guide.
- `AUTORUN` — a card file named AUTORUN is INCLUDEd at every boot.
- `BYE` — leave Forth (cart build: cold restart). `VER .` — version.
- `X16 C64 F256` — platform flags; `X16 .` → -1 here.
- `RANDOM ( -- n )` — 16-bit hardware entropy. `RND ( u -- n )` — 0..u-1:
  `6 RND 1+` rolls a die.
- `USR ( i*x addr -- j*x )` — call machine code with the stack.
- `FREE` / `UNUSED` — dictionary space left.
- `WORDS` — list the dictionary.
- `IOABORT ( ior -- )` — throw -37 if an I/O result is non-zero.
- `TI STOP` — a jiffy stopwatch: `TI ... STOP` prints elapsed time (TI as in BASIC; START is the turnkey boot cell, section 43).
- `NOTFOUND` — the unknown-word hook (FLOAT redirects it for literals).

### Interrupt words (the IRQ dispatcher)

Arm a Forth word on a VERA interrupt source; `0` in place of the xt
disarms. Armed words run *inside* the interrupt on a private stack — keep
them short, stack-balanced, no disk I/O, and disarm before FORGETting.

```forth
VARIABLE FRAMES
: TICK 1 FRAMES +! ;
' TICK IRQ               \ run TICK every frame (60 Hz VSYNC)
0 IRQ                    \ disarm

' HBLANK 100 LINE-IRQ    \ run HBLANK at scanline 100 ( xt line -- )
0 0 LINE-IRQ             \ off

' BOOM SPRCOL-IRQ        \ on sprite collision...
COLLISIONS .             \ ...groups seen since last read (then cleared)

' REFILL-FIFO AFLOW-IRQ  \ PCM FIFO low - the word MUST refill the FIFO
                         \ (use ADVSND's PCM-PLAY instead of rolling your own)
```

The dispatcher saves W/W2/W3, the RAM bank, VERA CTRL and both data-port
addresses around every armed word, so handlers may use VPOKE & friends
freely.

### Word reference

- `BYE ( -- )` — leave Forth. `VER ( -- n )` — version, major*256+minor.
- `X16 C64 F256 ( -- flag )` — platform flags for portable source.
- `RANDOM ( -- n )` — hardware entropy. `RND ( u -- n )` — uniform 0..u-1 (**u** ≥ 1).
- `USR ( i*x addr -- j*x )` — jump to machine code at **addr**; it sees and may edit the Forth stack.
- `FREE ( -- )` / `UNUSED ( -- u )` — print / return dictionary space left.
- `ENVIRONMENT? ( addr u -- false | value true )` — query a standard attribute string.
- `IOABORT ( ior -- )` — non-zero **ior** prints a message and THROWs -37.
- `TI ( -- clk )` / `STOP ( clk -- )` — jiffy stopwatch (TI as in BASIC); STOP prints the elapsed time.
- `NOTFOUND ( -- addr )` — the unknown-word hook cell (advanced; FLOAT uses it for literals).

IRQ dispatcher — **xt**: from `'` or `[']`; 0 = disarm. Armed words run inside the interrupt on a private 16-cell stack: short, balanced, no disk I/O, disarm before FORGET.

- `IRQ ( xt -- )` — every frame (60 Hz VSYNC).
- `LINE-IRQ ( xt line -- )` — at scanline **line** 0–511 each frame; `0 0 LINE-IRQ` disarms.
- `SPRCOL-IRQ ( xt -- )` — on sprite collision.
- `COLLISIONS ( -- mask )` — collision groups seen since the last call (reading clears; bits = VERA collision mask groups).
- `AFLOW-IRQ ( xt -- )` — PCM FIFO below 1/4. The word MUST push samples (refilling is the only acknowledge) — use ADVSND's PCM-PLAY rather than arming this yourself.

---

## 21. VIDEO — VERA, screen, cursor

Single random access with VPOKE/VPEEK; bulk streaming with VADDR + V!/V@
(the address auto-increments):

```forth
0 $1000 65 VPOKE         \ write one VRAM byte  ( bank addr value -- )
0 $1000 VPEEK .          \ 65
1 $FA00 VADDR            \ point the data port ( bank addr -- )
10 0 DO I V! LOOP        \ stream 10 bytes
$1234 V!W                \ 16-bit write, low byte first
```

Text screen:

```forth
0 SCREEN                 \ 0=80x60 1=80x30 2=40x60 3=40x30 128=320x240
1 6 COLOR                \ fg bg 0-15
2 BORDER                 \ border colour
CLS                      \ clear
10 5 LOCATE ." here"     \ row col
CURSOR . .               \ read back row col
POS .                    \ column only
100 SCROLLX  0 SCROLLY   \ hardware scroll 0-4095
```

### Word reference

**bank**: VRAM bit 16 (0 or 1 — VRAM is 128 KB). **addr**: 16-bit VRAM address within the bank.

- `VPOKE ( bank addr value -- )` / `VPEEK ( bank addr -- value )` — one random byte access.
- `VADDR ( bank addr -- )` — aim data port 0 with auto-increment +1.
- `V! ( byte -- )` / `V@ ( -- byte )` — stream through the port (address advances).
- `V!W ( w -- )` — 16-bit store, low byte first.
- `SCREEN ( mode -- )` — 0: 80×60 text, 1: 80×30, 2: 40×60, 3: 40×30, 128: 320×240×256 bitmap.
- `COLOR ( fg bg -- )` — text colours 0–15. `BORDER ( color -- )` — frame colour 0–15.
- `CLS ( -- )` — clear text screen.
- `LOCATE ( row col -- )` — move the cursor. `CURSOR ( -- row col )` / `POS ( -- col )` — read it.
- `SCROLLX SCROLLY ( n -- )` — layer-1 hardware scroll, 0–4095 (wraps).

---

## 22. TILE — tilemap cells and layers

The text screen is VERA layer 1's tilemap; TILE pokes cells directly:

```forth
0 0 65 $61 TILE          \ x y code attr: an 'A', white on blue
0 0 TDATA .              \ 65
0 0 TATTR .              \ $61 (fg | bg<<4)
```

Layer control (layer 0 or 1):

```forth
0 LAYER-ON   0 LAYER-OFF
0 1 $B000 MAPBASE        \ layer bank addr (512-aligned map)
0 1 $A000 TILEBASE       \ layer bank addr (2K-aligned tiles)
0 $60 LAYER-MODE         \ raw config byte: map size, depth...
```

Save/restore: `TMAPSAVE TMAPLOAD ( c-addr u )` for the whole layer-1 map,
`TILESAVE ( c-addr u vaddr len )` / `TILELOAD ( c-addr u vaddr )` for
bank-1 tilesets.

### Word reference

- `TILE ( x y code attr -- )` — write one map cell. **x, y**: column/row in the current mode; **code**: tile/screen code 0–255; **attr**: colour byte = fg | bg×16.
- `TDATA ( x y -- code )` / `TATTR ( x y -- attr )` — read a cell back.
- `LAYER-ON / LAYER-OFF ( layer -- )` — **layer**: 0 or 1 (text lives on 1).
- `MAPBASE ( layer bank addr -- )` — tile-map base, 512-byte aligned VRAM.
- `TILEBASE ( layer bank addr -- )` — tile-graphics base, 2 KB aligned.
- `LAYER-MODE ( layer cfg -- )` — raw config byte: bits 7:6 map height, 5:4 map width (0-3 = 32/64/128/256), bit 3 T256C, bit 2 bitmap mode, bits 1:0 colour depth (0-3 = 1/2/4/8 bpp).
- `TMAPSAVE / TMAPLOAD ( c-addr u -- )` — whole layer-1 map to/from a file named by the string.
- `TILESAVE ( c-addr u vaddr len -- )` / `TILELOAD ( c-addr u vaddr -- )` — **len** bytes of bank-1 tile graphics at **vaddr**.

---

## 23. PAL — palette

256 entries, 12-bit `$0RGB`. Entry 0 is the background/transparent colour;
0-15 boot as the classic 16-colour set.

```forth
$0F00 1 PAL!             \ colour 1 = pure red   ( rgb index -- )
$0FF0 2 PAL!             \ colour 2 = yellow
```

### Word reference

- `PAL! ( rgb index -- )` — **rgb**: 12-bit $0RGB (each nibble 0–15, e.g. $0F00 red, $00F0 green, $000F blue, $0FFF white); **index**: palette entry 0–255. Entry 0 is background/transparent; sprites/tiles use 16-colour slices starting at index 16×n.

---

## 24. SPRITE

128 hardware sprites. Point a sprite at pixel data in VRAM, position it,
give it a Z-depth:

```forth
$4000 0 SPRITE-IMAGE     \ sprite 0's pixels at VRAM $4000 (4bpp, 32-aligned)
1 1 0 SPRITE-SIZE        \ 16x16 (codes 0-3 = 8/16/32/64)
160 120 0 SPRITE-POS     \ x y sprite
0 3 SPRITE               \ sprite 0 in front + sprites on
0 SPRITE-GET . .         \ read position back
SPRITES-OFF              \ hide the layer
```

BASIC-style aliases: `SPRITE-MOV ( num x y )`, `SPRITE-MEM ( num bank addr )`.
Disk: `SPRITE-SAVE / SPRITE-LOAD ( c-addr u sprite )` for the image data.
Collisions fire through `SPRCOL-IRQ` + `COLLISIONS` (see SYSTEM).

### Word reference

**sprite / num**: 0–127. Image data is read from VRAM.

- `SPRITES-ON / SPRITES-OFF ( -- )` — enable / disable the whole sprite layer.
- `SPRITE-IMAGE ( graphaddr sprite -- )` — pixel data address (**32-byte aligned**; 4 bpp).
- `SPRITE-MEM ( num bank addr -- )` — same with an explicit VRAM bank.
- `SPRITE-POS ( x y sprite -- )` — position, 12-bit signed-ish (0–4095 wraps; negative x/y park it off-screen).
- `SPRITE-GET ( sprite -- x y )` — read the position back.
- `SPRITE-SIZE ( width height sprite -- )` — codes 0–3 = 8/16/32/64 pixels each way.
- `SPRITE-Z ( z sprite -- )` — 0 off, 1 behind layer 0, 2 between layers, 3 in front.
- `SPRITE ( num zdepth -- )` — set Z and switch the layer on (BASIC style).
- `SPRITE-MOV ( num x y -- )` — BASIC MOVSPR argument order.
- `SPRITE-SAVE / SPRITE-LOAD ( c-addr u sprite -- )` — the sprite's pixel block to/from a file.

---

## 25. GRAPHIC — 320×240×256 bitmap (module)

`NEEDS GRAPHIC` (cart) or `INCLUDE GRAPHIC` (card).

```forth
NEEDS GRAPHIC
GINIT                    \ enter bitmap mode
GCLS                     \ clear
160 120 5 PSET           \ x y color
0 0 319 239 3 LINE       \ x1 y1 x2 y2 color
50 50 100 80 7 FRAME     \ outline rectangle
60 60 90 70 2 RECT       \ filled (VERA-cache fast)
160 120 40 1 CIRCLE      \ x y r color outline
160 120 20 4 FCIRCLE     \ filled
20 20 300 220 6 RING     \ ellipse outline in a bounding box
20 20 300 220 8 OVAL     \ filled ellipse
10 10 1 S" HI" GTEXT     \ text into the bitmap
0 SCREEN PAGE            \ back to text mode
```

Pen API — set the colour once, then draw without repeating it:

```forth
5 GCOLOR  10 10 PLOT  100 100 DRAW  20 20 60 60 BOX
```

(`PLOT DRAW BOX FBOX ELL FELL CIRC DISC SAY` mirror the words above.)
Low-level bitmap primitives: `BPSET BHLINE BVLINE BLINE BFILL BRECT BCLS`.

### Word reference

Coordinates: **x** 0–319, **y** 0–239; **color** 0–255 (palette index). Shapes clip to the screen.

- `GINIT ( -- )` — enter 320×240×256 bitmap mode. `GCLS ( -- )` — clear to colour 0.
- `PSET ( x y color -- )` — one pixel.
- `LINE ( x1 y1 x2 y2 color -- )` — line between two points.
- `FRAME ( x1 y1 x2 y2 color -- )` / `RECT` — rectangle outline / filled (RECT uses the VERA cache — fast).
- `RING ( x1 y1 x2 y2 color -- )` / `OVAL` — ellipse outline / filled inside the bounding box.
- `CIRCLE ( x y r color -- )` / `FCIRCLE` — outline / filled circle, centre x,y radius **r**.
- `GTEXT ( x y color c-addr u -- )` — draw the string's characters into the bitmap.
- `GCOLOR ( color -- )` — set the pen for the pen API: `PLOT ( x y )`, `DRAW ( x y )` (line from the last point), `BOX FBOX ELL FELL ( x1 y1 x2 y2 )`, `CIRC DISC ( x y r )`, `SAY ( x y c-addr u )`.
- Low level, no clipping: `BPSET ( x y c )`, `BHLINE ( x y w c )`, `BLINE`, `BFILL`, `BRECT`, `BCLS` — straight to VRAM.
- `ISQRT ( u -- n )` — integer floor square root (used by the ellipse code; handy on its own).

---

## 26. ADVGFX — clipping, flood fill, FX copy, rotozoom (module)

`NEEDS GRAPHIC NEEDS ADVGFX`.

```forth
\ Cohen-Sutherland clipping
10 20 300 230 CLIP-RECT            \ set the clip window (default full screen)
-50 100 400 100 CLIP-LINE          \ ( x1 y1 x2 y2 -- x1' y1' x2' y2' flag )
. . . . .                          \ flag=-1: clipped endpoints
-50 100 400 100 5 CLINE            \ clip + draw in one word

40 40 7 FLOOD                      \ flood fill from a seed point ( x y color )

0 32000 0 64000 320 FX-COPY        \ VRAM→VRAM, 4 bytes/flush
                                   \ ( sbank saddr dbank daddr u )
                                   \ destination must be 4-byte aligned
```

### Rotozoom / mode-7 (FX affine)

An 8-bpp tile set + tile map form a square texture; a fixed-point ray
samples it. One ray + one line per scanline = rotation and zoom:

```forth
1 $3000 1 $3800 1 0 AFFINE-ON      \ tiles@1:$3000 map@1:$3800, 8x8 tiles, wrap
0 0 512 0 AFFINE-RAY               \ from texel (0,0), one texel per read
0 61440 320 AFFINE-LINE            \ sample 320 texels to VRAM row 192
AFFINE-OFF
```

- `AFFINE-ON ( tbank taddr mbank maddr size clip )` — size 0-3 = 2/8/32/128
  tiles square; clip 0 = wrap.
- `AFFINE-RAY ( x y dx dy )` — start texel, signed step in 1/512 texels
  (512 = 1:1; feed `COS8`/`SIN8` × zoom for rotation).
- `AFFINE-SPAN ( n )` — DATA1→DATA0 texel pump (aim port 0 yourself).
- `AFFINE-LINE ( bank addr n )` — aim + span in one word.
- `AFFINE-OFF` — normal reads again.

### Word reference

- `CLIP-RECT ( xmin ymin xmax ymax -- )` — set the clip window (inclusive). Default and reset: `0 0 319 239 CLIP-RECT`.
- `CLIP-LINE ( x1 y1 x2 y2 -- x1' y1' x2' y2' flag )` — Cohen–Sutherland. **flag** true: the clipped segment is visible; false: fully outside (coordinates then meaningless zeros).
- `CLINE ( x1 y1 x2 y2 color -- )` — clip then draw (needs GRAPHIC's LINE).
- `FLOOD ( x y color -- )` — fill the 4-connected region of the colour under the seed **x,y** with **color**. 128-span seed stack; monstrous shapes may fill partially.
- `FX-COPY ( sbank saddr dbank daddr u -- )` — VRAM→VRAM, 4 bytes per cache flush. **dbank:daddr must be 4-byte aligned**; source anywhere; **u**: byte count (tail handled).
- `AFFINE-ON ( tbank taddr mbank maddr size clip -- )` — **tbank:taddr**: 8-bpp tile pixels (64 B/tile, 2 KB aligned); **mbank:maddr**: the tile map, one byte per tile (2 KB aligned); **size**: 0–3 = 2×2/8×8/32×32/128×128 tiles (16–1024 texels square); **clip**: 0 wrap at the edges, 1 clip (outside reads tile 0).
- `AFFINE-RAY ( x y dx dy -- )` — **x, y**: start texel 0–1023; **dx, dy**: signed step per read in 1/512-texel units (512 = 1 texel; 256 = zoom ×2 in; 1024 = zoom ×2 out).
- `AFFINE-SPAN ( n -- )` — copy **n** texels DATA1→DATA0; aim port 0 (e.g. with VADDR) first.
- `AFFINE-LINE ( bank addr n -- )` — VADDR + span in one word; destination auto-increments +1.
- `AFFINE-OFF ( -- )` — restore normal port-1 reads.

---

## 27. ADVANCED — game math, buffers, compression (module)

`NEEDS ADVANCED`.

```forth
1234 RND-SEED   RND16 .   RND8 .   \ seeded 16-bit xorshift PRNG
64 SIN8 .        \ 127   ( angle 0-255 -- -127..127 )
0 COS8 .         \ 127   SIN8U/COS8U give 0..255
10 0 ATAN2 .     \ 0     ( dx dy -- angle; 0=east 64=down )
0 100 128 LERP . \ 50    ( a b t -- a+(b-a)*t/255 )
```

Ring buffers (size must be a power of 2):

```forth
16 RING KEYS
65 KEYS >RING    KEYS RING> .    \ 65
KEYS RING# .                     \ 0 bytes queued
```

Decompression:

```forth
SRC DST ZX0-DECOMPRESS .   \ pure-Forth ZX0 (salvador) → end address
SRC DST MEM-DECOMPRESS .   \ KERNAL LZSA2 → end address
```

### Word reference

- `RND-SEED ( n -- )` — seed the xorshift generator (0 is nudged to 1 — zero is its fixed point).
- `RND16 ( -- u )` — next 16-bit pseudo-random value. `RND8 ( -- c )` — low 8 bits of it.
- `SIN8 COS8 ( angle -- n )` — **angle**: 0–255 = a full circle (wraps); result -127..127.
- `SIN8U COS8U ( angle -- u )` — same, offset to 0..255 (for volumes/scales).
- `ATAN2 ( dx dy -- angle )` — the angle of vector (dx, dy) in 0–255 units; 0 = east (+x), 64 = down (+y). Inverse of SIN8/COS8.
- `LERP ( a b t -- n )` — linear interpolation a→b; **t**: 0–255 where 0 = a, 255 = b exactly.
- `RING ( size "name" -- )` — create a byte ring buffer; **size**: power of 2.
- `>RING ( c rng -- )` — push a byte (overwrites the oldest when full).
- `RING> ( rng -- c )` — pop the oldest byte (empty = garbage: check RING# first).
- `RING# ( rng -- n )` — bytes queued.
- `ZX0-DECOMPRESS ( src dst -- end )` — unpack ZX0 (salvador) data at **src** to **dst**; returns the first address after the output.
- `MEM-DECOMPRESS ( src dst -- end )` — same for LZSA2 via the KERNAL.

---

## 28. BMX — bitmap image files (module)

`NEEDS BMX`. Loads/saves the community BMX format (what Prog8 and the X16
tools write): header + palette + 8-bpp rows.

```forth
NEEDS BMX
S" TITLE.BMX" 8 0 0 BMX-LOAD .   \ ( name u dev vbank vaddr -- ior ) 0 = ok
BMX-WIDTH @ . BMX-HEIGHT @ .     \ header vars filled by the load

320 BMX-STRIDE !                 \ rows this many bytes apart (default 320)
8 BMX-WIDTH ! 2 BMX-HEIGHT !     \ describe, then save a region:
S" STAMP" 8 0 $8000 BMX-SAVE .
```

ior codes: 0 ok, 1 I/O error, 2 not BMX v1, 3 compressed (unsupported).
Vars: `BMX-WIDTH BMX-HEIGHT BMX-BPP BMX-PALSTART BMX-PALCOUNT BMX-BORDER
BMX-STRIDE`. Widths under the stride load as stamps into a larger screen.

### Word reference

- `BMX-LOAD ( c-addr u dev vbank vaddr -- ior )` — load the named image: palette to its slot, pixels to **vbank:vaddr**, rows BMX-STRIDE bytes apart. Fills the header vars.
- `BMX-SAVE ( c-addr u dev vbank vaddr -- ior )` — write an image from VRAM using the current header vars (set BMX-WIDTH/HEIGHT etc. first).
- **ior**: 0 ok, 1 I/O error, 2 not a BMX v1 file, 3 compressed (unsupported).
- Header variables: `BMX-WIDTH BMX-HEIGHT` (pixels), `BMX-BPP` (8 supported), `BMX-PALSTART` (first palette entry), `BMX-PALCOUNT` (entries, 256 = all), `BMX-BORDER` (suggested border colour), `BMX-STRIDE` (bytes between row starts; 320 = full-screen rows, smaller widths load as stamps).

---

## 29. AUDIOFM — PSG and FM notes

Two synths: VERA PSG (16 voices) and the YM2151 (8 channels). Volumes
0-63. A packed note is `(octave<<4) | 1..12`, 0 = release.

```forth
FMINIT                    \ init + default patches
0 0 FMINST                \ instrument 0 on channel 0
63 0 FMVOL
$4A 0 FMNOTE              \ octave 4, note 10 (A) — concert A
440 0 FMFREQ              \ or by frequency in Hz (17-4434)
30 0 FMDRUM               \ drum sounds 25-87
S" CDEFGAB" 0 FMPLAY      \ letters, blocking, ~8 jiffies each
S" CEG" 0 FMCHORD         \ chord across channels 0,1,2

PSGINIT
63 0 PSGVOL   0 0 PSGWAV        \ wave 0-3 pulse/saw/tri/noise
$4A 0 PSGNOTE                    \ same packed-note format
1000 0 PSGFREQ                   \ raw frequency
3 0 PSGPAN                       \ 1=left 2=right 3=both
S" CDE" 0 PSGPLAY
```

`FMVIB ( speed depth )` — global vibrato. `FMPAN` — per channel pan.

### Word reference

**voice**: PSG 0–15. **channel**: YM 0–7. **vol**: 0–63. **note**: (octave×16) + 1..12 (C=1 … B=12), 0 = release. Play strings: letters A–G at octave 4, ~8 jiffies each, blocking.

- `PSGINIT ( -- )` — silence and reset all 16 voices.
- `PSGFREQ ( freq voice -- )` — raw VERA frequency word (freq ≈ Hz × 2.68).
- `PSGNOTE ( note voice -- )` — packed note; sets the frequency only — set volume/wave first.
- `PSGVOL ( vol voice -- )` — volume, both channels. `PSGPAN ( pan voice -- )` — 1 left, 2 right, 3 both.
- `PSGWAV ( wave voice -- )` — 0 pulse, 1 saw, 2 triangle, 3 noise.
- `PSGPLAY / PSGCHORD ( c-addr u voice -- )` — melody on one voice / one note per successive voice.
- `FMINIT ( -- )` — reset the YM2151 and load default patches.
- `FMINST ( inst channel -- )` — patch 0–162 from the ROM bank.
- `FMVOL ( vol channel -- )` — 0–63. `FMPAN ( pan channel -- )` — 1/2/3.
- `FMNOTE ( note channel -- )` — packed note, 0 = key off.
- `FMFREQ ( freq channel -- )` — by frequency in Hz, 17–4434; 0 releases.
- `FMDRUM ( drum channel -- )` — General-MIDI-ish drum numbers 25–87.
- `FMVIB ( speed depth -- )` — global LFO vibrato, both 0–127.
- `FMPLAY / FMCHORD ( c-addr u channel -- )` — melody / chord.

---

## 30. AUDIOYM — raw YM2151 registers

```forth
$C7 $20 YM!        \ write a register directly ( value reg -- )
$20 YM@ .          \ read back (via the ROM driver's shadow)
$11 $20 FMPOKE     \ write THROUGH the ROM API (keeps FMVOL shadows in sync)
```

Use `YM!` for effects the note API doesn't reach (LFO, per-operator
envelopes — see the YM2151 datasheet).

### Word reference

- `YM! ( value reg -- )` — raw write to YM2151 register **reg** 0–255 (see the datasheet: $08 key-on, $20+ channel, $60+ operator TL, $18/$19 LFO...).
- `FMPOKE ( value reg -- )` — the same write routed through the ROM audio driver so its volume shadows stay in sync — use this when mixing with FMVOL/FMNOTE.
- `YM@ ( reg -- value )` — read the driver's shadow of a register.

---

## 31. AUDIOPCM — sampled sound

VERA has a 4 KB sample FIFO. Signed 8/16-bit, mono or stereo.

```forth
16 128 + PCMCTRL       \ volume 15 + reset FIFO (bit4 stereo, bit5 16-bit)
PAD 1000 PCM-WRITE     \ prime up to 4 KB, unthrottled
128 PCMRATE            \ start the DAC: 0=stop .. 128=48828 Hz
PCMFULL? .             \ pace longer streams yourself:
BEGIN NEXT-BYTE PCMFULL? 0= WHILE PCM! REPEAT
```

For hands-off streaming (interrupt-refilled, looping) use ADVSND below.

### Word reference

- `PCMCTRL ( n -- )` — AUDIO_CTRL byte: bits 3:0 volume 0–15, bit 4 stereo, bit 5 16-bit samples, bit 7 (write) resets/empties the FIFO. `$8F` = reset + mono 8-bit, full volume.
- `PCMRATE ( n -- )` — sample rate: 0 stops the DAC, 128 = 48828 Hz, linear in between (n × 381.5 Hz).
- `PCM! ( byte -- )` — push one sample byte; silently dropped if the FIFO is full. Samples are signed two's-complement.
- `PCMFULL? ( -- flag )` — true when the 4 KB FIFO can take no more.
- `PCM-WRITE ( addr count -- )` — blast **count** bytes from RAM; no throttling — for priming an empty FIFO with ≤ 4 KB.

---

## 32. ADVSND — envelopes, background PCM, ADPCM (module)

`NEEDS ADVSND`.

### PSG envelopes (attack / sustain / release)

```forth
NEEDS ADVSND
1000 0 PSGFREQ  0 0 PSGWAV  3 0 PSGPAN     \ voice setup first
63 4 30 2 0 ENV-START    \ peak astep sus rstep voice
' ENV-TICK IRQ           \ tick every frame — envelopes now run themselves
0 ENV-RELEASE            \ enter release now (sus=255 holds until this)
0 ENV-STOP               \ hard silence
```

Per tick: volume += astep until peak (astep 0 = jump), hold sus ticks
(255 = forever), then -= rstep to silence (rstep 0 = hold).

### Background PCM streaming from banked RAM

```forth
$83 PCMCTRL                    \ 8-bit mono, volume 3, reset
S" SOUND.RAW" 8 2 BANKLOAD     \ sample into RAM banks 2+
0 PCM-LOOP !                   \ 1 = repeat forever
2 $A000 12000. 128 PCM-PLAY    \ bank addr length(double) rate
PCM-PLAYING? .                 \ -1 while data remains
PCM-STOP                       \ disarm + silence + flush
```

PCM-PLAY primes the FIFO, then refills from the AFLOW interrupt — your
program keeps running. It auto-disarms at the end of the data; lengths are
32-bit so whole songs across many banks work; the $BFFF→$A000 bank seam is
handled.

### ADPCM (4:1 compressed samples)

```forth
ADPCM-INIT                     \ predictor 0, step 0
SRC DST 1000 ADPCM>PCM .       \ 1000 bytes in → 2000 8-bit samples out
-3103 64 ADPCM!                \ restore state from a WAV block header
```

IMA ADPCM, low nibble first, signed 8-bit out — VERA-ready. Decode is
offline (Forth is too slow for live decode): unpack whole banks up front,
then `PCM-PLAY` the result.

### Word reference

Envelopes — **voice**: 0–15; volumes 0–63; one tick = one ENV-TICK call (a frame when armed on IRQ).

- `ENV-START ( peak astep sus rstep voice -- )` — **peak**: target volume; **astep**: volume added per tick (0 = jump straight to peak); **sus**: ticks held at peak (255 = hold until ENV-RELEASE); **rstep**: volume subtracted per tick after sustain (0 = hold until ENV-STOP). Set frequency/wave/pan first.
- `ENV-RELEASE ( voice -- )` — jump to the release phase now.
- `ENV-STOP ( voice -- )` — silence and disarm the voice immediately.
- `ENV-TICK ( -- )` — advance every armed envelope one step; call once per frame: `' ENV-TICK IRQ`.
- `PCM-PLAY ( bank addr ud rate -- )` — stream from banked RAM. **bank**: first RAM bank; **addr**: $A000–$BFFF window address of the first byte; **ud**: byte count as a double (write `12000.` or `lo hi`); **rate**: 1–128 as PCMRATE. Primes the FIFO, then refills from the AFLOW interrupt; crosses bank seams; auto-disarms at the end.
- `PCM-LOOP ( -- addr )` — variable: store 1 before PCM-PLAY to loop forever, 0 for one-shot.
- `PCM-PLAYING? ( -- flag )` — true while data remains to hand to the FIFO.
- `PCM-STOP ( -- )` — disarm the refiller, stop the DAC, flush the FIFO.
- `ADPCM-INIT ( -- )` — reset the decoder (predictor 0, step index 0).
- `ADPCM! ( pred index -- )` — load decoder state from an IMA WAV block header. **pred**: signed 16-bit predictor; **index**: step index 0–88.
- `ADPCM>PCM ( src dst u -- dst' )` — decode **u** input bytes (2 samples each, low nibble first) to signed 8-bit samples at **dst**; returns the address after the output. Offline use only — decode first, then PCM-PLAY.

---

## 33. VERAFX — the FX accelerator

```forth
2 DCSEL                    \ select an FX register bank (regs at $9F29-2C)
1000 1000 FX-MULT D.       \ 1000000 — hardware 16x16 signed multiply
1000 1000 FX* D.           \ same, double result
$55 0 $8000 1000 FX-FILL   \ fill VRAM fast via the 32-bit cache
0 $8000 1000 FX-CLEAR      \ zero a region
FX-OFF                     \ back to plain VPOKE behaviour
```

`FX-FILL` handles any count — the unaligned 1-3 lead/tail bytes are
written plain. The affine (rotozoom) words are documented under ADVGFX.

### Word reference

- `DCSEL ( n -- )` — select VERA register window 0–63; the four registers $9F29–$9F2C change meaning per window (2 = FX control, 6 = cache...). Leave it at 0 when done.
- `FX-MULT ( a b -- lo hi )` / `FX* ( n1 n2 -- d )` — hardware signed 16×16→32 multiply; result is a double.
- `FX-FILL ( byte vbank vaddr count -- )` — fill VRAM with **byte** using 32-bit cache writes (≈4× VPOKE speed); any alignment and count.
- `FX-CLEAR ( vbank vaddr count -- )` — `0 FX-FILL`.
- `FX-OFF ( -- )` — reset FX control so plain V!/VPOKE behave normally again (FX-FILL/FX-MULT already clean up after themselves).

---

## 34. INPUT — joysticks and mouse

```forth
0 JOY .          \ keyboard joystick; 1-4 = SNES pads; bits active-high
1 MOUSE          \ pointer on (0 off, -1 auto-scale)
MX . MY . MB .   \ position + buttons (bit0 L, bit1 R, bit2 M)
MWHEEL .         \ signed wheel movement since last read
```

Game loop pattern:

```forth
: INPUT? 0 JOY DUP 1 AND IF ." right " THEN DROP ;
```

### Word reference

- `JOY ( n -- buttons )` — **n**: 0 = keyboard joystick, 1–4 = SNES pads. Result: button bits active-high (SNES layout: B Y Select Start Up Down Left Right A X L R), 0 when the pad is absent.
- `MOUSE ( mode -- )` — 0 hide, 1 show, -1 show scaled to the current screen mode.
- `MX ( -- x )` / `MY ( -- y )` — pointer position in screen pixels.
- `MB ( -- buttons )` — bit 0 left, bit 1 right, bit 2 middle.
- `MWHEEL ( -- delta )` — signed wheel steps since the last call (reading clears).

---

## 35. KEYBOARD

```forth
KEY               \ wait + echo (standard Forth)
GETKEY            \ wait, no echo, PETSCII code
KEY? IF GETKEY . THEN      \ poll without blocking
S" DE-DE" KEYMAP  \ switch layout (28 ROM layouts; see HELP CONTROL)
1 CHARSET         \ 1=ISO 2=PET upper/gfx 3=upper/lower ... 12=Katakana
```

### Word reference

- `KEY ( -- c )` — wait for a key (standard Forth, echoes).
- `KEY? ( -- flag )` — true if a key is waiting; does not consume it.
- `GETKEY ( -- char )` — wait, return the PETSCII code, no echo.
- `KEYMAP ( c-addr u -- )` — layout by name (case-insensitive; unknown aborts): ABC/X16 EN-US/INT EN-GB DE-DE FR-FR SV-SE ES-ES IT-IT ... (28 ROM layouts, see HELP CONTROL).
- `CHARSET ( n -- )` — 8×8 ROM charset: 1 ISO, 2 PET upper/graphics, 3 PET upper/lower, ... 12 Katakana.

---

## 36. LOADSAVE — PRG files, VRAM, verify

Device is usually 8 (the SD card). Names are `( c-addr u )` strings.

```forth
S" SPRITES.BIN" 8 $4000 BLOAD     \ load to an address ( .. dev addr -- )
S" DATA.PRG" 8 LOAD                \ load to the address in its PRG header
S" IMG.BIN" 8 1 $0000 VLOAD        \ into VRAM, header skipped
S" IMG.RAW" 8 1 $0000 BVLOAD       \ headerless, straight into VRAM
S" SHEET" 8 1 $4000 2048 VSAVE     \ save VRAM out ( .. bank vaddr len )
S" DATA" 8 $A000 $B000 SAVE        \ save memory start..end
S" DATA" 8 $A000 BVERIFY .         \ -1 = file matches memory
```

Raw binary (no PRG header handling):
`LOADB ( name u dst -- endaddr|0 )`, `SAVEB ( start end name u -- )`.
Tile helpers `TILESAVE TILELOAD TMAPSAVE TMAPLOAD` are under TILE.

### Word reference

**c-addr u**: the filename. **dev**: device number, usually 8. PRG files carry a 2-byte load-address header.

- `LOAD ( c-addr u dev -- )` — load a PRG to the address in its header.
- `BLOAD ( c-addr u dev addr -- )` — load a PRG relocated to **addr** (header skipped).
- `SAVE ( c-addr u dev start end -- )` — save memory **start..end** (end exclusive) as a PRG.
- `VLOAD ( c-addr u dev bank vaddr -- )` — PRG into VRAM at **bank:vaddr**, header skipped.
- `BVLOAD ( c-addr u dev bank vaddr -- )` — headerless file straight into VRAM.
- `VSAVE ( c-addr u dev bank vaddr len -- )` — save **len** bytes of VRAM as a PRG (VLOAD reads it back).
- `BVERIFY ( c-addr u dev addr -- flag )` — compare a headerless file to memory at **addr**; -1 = identical.
- `LOADB ( nameptr namelen dst -- endaddr )` — raw binary load to **dst**; returns the address after the last byte, or 0 on failure (no error text).
- `SAVEB ( start end nameptr namelen -- )` — raw binary save, **end** exclusive.

---

## 37. BANK — high-RAM banks

512 KB of RAM hides behind the $A000-$BFFF window, 8 KB per bank. Perfect
for levels, music and samples. All these preserve the bank register.

```forth
DATABANK .                     \ highest bank present (bank 0 = KERNAL's)
S" LEVELS.BIN" 8 2 BANKLOAD    \ file → banks 2, 3, ... ( name u dev bank )
2 0 $6000 4096 BANK>MEM        \ bank:off → low RAM ( bank boff addr u )
$6000 2 0 4096 MEM>BANK        \ low RAM → bank:off
S" SLICE" 8 2 100 500 BANKSAVE \ save 500 bytes from bank 2 offset 100

3 SETBANK                      \ or raw: pick the window bank
2 100 B@ .                     \ read one byte ( bank off -- b )
7 2 100 B!                     \ write one ( b bank off -- )
```

Loads and copies auto-advance across bank boundaries.

### Word reference

**bank**: RAM bank 1..DATABANK (bank 0 is the KERNAL's). **off/boff**: offset 0–8191 into the $A000 window. All words preserve the $00 bank register.

- `DATABANK ( -- bank )` — highest usable bank (from KERNAL MEMTOP).
- `BANKLOAD ( c-addr u dev bank -- )` — file into RAM at bank:$A000; bigger than 8 KB spills into bank+1, bank+2...
- `BANKSAVE ( c-addr u dev bank off len -- )` — save **len** bytes starting at bank:off. Writes a PRG with an $A000 header, so BANKLOAD restores it.
- `BANK>MEM ( bank boff addr u -- )` — banked → low RAM, fast, crosses bank seams.
- `MEM>BANK ( addr bank boff u -- )` — low RAM → banked.
- `SETBANK ( bank -- )` — raw: select the window bank (remember to restore).
- `B@ ( bank off -- byte )` / `B! ( byte bank off -- )` — single-byte access without touching the current bank.

---

## 38. KERNAL — calling the ROM

```forth
65 0 0 $FFD2 SYSCALL 2DROP DROP    \ CHROUT: prints A ( a x y addr -- a' x' y' )
0 0 0 10 $C83F BCALL 2DROP DROP    \ any ROM bank: ( a x y bank addr -- ... )
```

Common entries: CHROUT `$FFD2`, CHRIN `$FFCF`, GETIN `$FFE4`, PLOT `$FFF0`.
Channel I/O for your own file plumbing: `CHKIN CHKOUT CHRIN CLRCHN READST`.

### Word reference

- `SYSCALL ( a x y addr -- a' x' y' )` — call **addr** in the KERNAL bank. **a, x, y**: values loaded into the 6502 registers .A/.X/.Y; the routine's exit registers come back in the same order. Flags are not returned.
- `BCALL ( a x y bank addr -- a' x' y' )` — the same for any ROM/RAM **bank** (audio ROM = 10, math = 4...).
- `CHKIN / CHKOUT ( file# -- ior )` — route input / output through an open logical file.
- `CHRIN ( -- char )` — read one byte from the current input channel.
- `CLRCHN ( -- )` — back to keyboard/screen. After CHKIN inside an INCLUDEd file, restore the file with `SOURCE-ID CHKIN`.
- `READST ( -- status )` — KERNAL status: bit 6 ($40) = EOF, bit 1/0 = timeouts, $80 = device not present.

---

## 39. CONTROL — system control, clock, I2C

```forth
EDIT                    \ resident editor; returns to Forth after exit
60 SLEEP                \ ~1 s (jiffies)     500 MS   \ ~0.5 s
TICKS D.                \ the 24-bit jiffy counter as a double
TIME@ . . .             \ h m s        DATE@ . . .   \ y mo d
2026 7 10 12 0 0 SETTIME
$6F $20 I2CPEEK .       \ read an I2C register (RTC NVRAM here)
$6F $20 7 I2CPOKE       \ write one
REBOOT                  \ soft reset        RESET   \ hard (SMC)
POWEROFF                \ power down via the SMC
MONITOR                 \ ML monitor (X exits; reset afterwards)
```

### Word reference

- `EDIT ( -- )` — launch the X816 resident editor and return to Forth after exit. Named-file edit/save is still pending.
- `SLEEP ( jiffies -- )` — wait in 1/60 s ticks. `MS ( u -- )` — wait ≈**u** milliseconds (busy loop, ≥ u).
- `TICKS ( -- ud )` — the 24-bit jiffy clock as an unsigned double.
- `TIME@ ( -- h m s )` / `DATE@ ( -- y mo d )` — read the RTC (y = 4-digit year).
- `SETTIME ( y mo d h m s -- )` — set it.
- `I2CPEEK ( dev reg -- byte )` / `I2CPOKE ( dev reg val -- )` — I2C bus access. **dev**: 7-bit device address ($42 SMC, $6F RTC); **reg**: register/offset.
- `KEYMAP ( c-addr u -- )` — keyboard layout (see KEYBOARD).
- `REBOOT ( -- )` — soft reset. `RESET ( -- )` — SMC hardware reset. `POWEROFF ( -- )` — SMC power-down.
- `MONITOR ( -- )` — the ROM machine-language monitor; does not return cleanly (X, then reset).

---

## 40. FLOAT — floating point (module)

`NEEDS FLOAT` (+ `NEEDS FLOATX` for the extended set). Floats live on
their own stack, 5-byte MFLPT (~9 digits), computed by the ROM math
library. After loading, float literals just work: `3.14`, `1E6`, `-2.5E-2`.

```forth
NEEDS FLOAT
2 S>F FSQRT F.          \ 1.41421356
3.14 FDUP F* F.         \ 9.8596
S" 2.5" >FLOAT . F.     \ -1 2.5
10 S>F FLN F.           \ 2.30258509
FVARIABLE TEMP   98.6 TEMP F!   TEMP F@ F.
: AREA ( F: r -- a ) FDUP F* 3.14159 F* ;
```

Word set: `F+ F- F* F/ FSQRT FNEGATE FABS FPOW FMAX FMIN FSIN FCOS FTAN
FATAN FLN FEXP F= F<> F< F> F0= F0<> F0< F0> FDROP FDUP FSWAP FOVER FNIP FDEPTH FCLEAR F.
F>S FVARIABLE FCONSTANT FLITERAL ISQRT`.
FLOATX adds `FPI FROT FSINCOS FLOG FALOG FASIN FACOS FATAN2 FSINH FCOSH
FTANH F~` and the BASIC aliases `SQR SIN COS TAN ATN LOG EXP`.

Notes: domain errors (divide by zero, FLN of a negative) are **not**
trapped. If you FORGET the float words, restore the `'NOTFOUND` hook
first (`'NOTFOUND @` before, `'NOTFOUND !` after).

### Word reference

Floats use their own stack, shown as `( F: ... )`. Values are 5-byte MFLPT, ≈9 significant digits, range ≈ ±1.7E38.

- `S>F ( n -- ) ( F: -- r )` — integer to float. `F>S ( -- n ) ( F: r -- )` — float to integer (non-negative).
- `>FLOAT ( c-addr u -- flag )` — parse text; true = ok with the value pushed ( F: -- r ).
- `F@ / F! ( f-addr -- )` — load / store 5 bytes at **f-addr** ( F: stack ↔ memory ).
- `F+ F- F* F/ ( F: a b -- c )` — arithmetic. Note F/ by zero is not trapped.
- `FSQRT FNEGATE FABS ( F: r -- r' )` — root (r ≥ 0), sign flip, magnitude.
- `FPOW F** ( F: x y -- x^y )` — via exp(y·ln x); **x** must be > 0.
- `FMAX FMIN ( F: a b -- r )` — pick one.
- `FSIN FCOS FTAN FATAN ( F: r -- r' )` — radians.
- `FLN FEXP ( F: r -- r' )` — natural log (r > 0) / e^r.
- `F0= F0<> F0< F0> ( -- flag ) ( F: r -- )` / `F= F<> F< F> ( -- flag ) ( F: a b -- )` — comparisons (consume floats, flag on the data stack; equality is exact — prefer F~ for computed values).
- `FDROP FDUP FSWAP FOVER FNIP` — float-stack shuffles. `FDEPTH ( -- n )` / `FCLEAR ( -- )`.
- `F. ( F: r -- )` — print. `FVARIABLE FCONSTANT ( "name" )` — 5-byte variable / constant. `FLITERAL` — compile the top float inline.
- `ISQRT ( u -- n )` — integer square root, no float stack involved.
- FLOATX adds: `FPI FPI2 FLN10 FROT FSINCOS FLOG FALOG FLNP1 FEXPM1 FSINH FCOSH FTANH FASIN FACOS FATAN2 F~ FVALUE FLOAT+ FLOATS FALIGN FALIGNED` and BASIC aliases `SQR SIN COS TAN ATN LOG EXP`. `FVALUE ( F: r "name" -- )` is a float VALUE — read by name, write with `TO` (which keeps working for VALUE/2VALUE).

---

## 41. BIT — bit and byte toolkit

```forth
$1234 SPLIT . .        \ $12 $34  ( n -- high low )
$A $5 CATNIB .         \ $A5     ( nh nl -- byte )
X 4 SBIT               \ set bit 2 of the byte at X   ( addr mask -- )
X 4 CBIT               \ clear it
FLAG @ X 4 FBIT        \ set-or-clear by flag ( flag addr mask -- )
```

### Word reference

- `SPLIT ( n -- bh bl )` — high byte under low byte: `$1234 SPLIT` → `$12 $34`.
- `CATNIB ( nh nl -- byte )` — join two nibbles 0–15 into (nh×16)|nl.
- `SBIT ( addr mask -- )` — OR **mask** into the byte at **addr** (set bits).
- `CBIT ( addr mask -- )` — AND the complement (clear bits).
- `FBIT ( flag addr mask -- )` — set the mask bits when **flag** is non-zero, clear them when 0.

---

## 42. ASSEMBLER — machine-code words

durexForth's inline assembler is part of the core (asm.fs — always
present). The syntax is *operand first*, then a mnemonic ending in a
comma; the suffix after the comma is the addressing mode:

```forth
5 lda,#          \ LDA #5        immediate
$9f20 lda,       \ LDA $9F20     absolute or zeropage (size picked for you)
lsb lda,x        \ LDA LSB,x     indexed by X (,y likewise)
w lda,(y)        \ LDA (W),y     indirect indexed (,(x) likewise)
asl,a            \ ASL A         accumulator: asl,a lsr,a rol,a ror,a
w (jmp),         \ JMP (W)       indirect jump
inx, pha, sei, rts,              \ single-byte ops
```

Only the plain-6502 mnemonic set exists — **no 65C02 extras** (`bra,`,
`stz,`, `phx,` are not words; emit raw bytes with `C,` if you truly need
one).

### Anatomy of a code word

The X register is the **data-stack pointer**. A cell's low byte lives at
`LSB,x`, its high byte at `MSB,x`. `inx,` drops a cell, `dex,` makes room
to push one (always store both halves!).

```forth
code 2* ( n -- n*2 )
lsb asl,x        \ shift the low byte, carry out...
msb rol,x        \ ...into the high byte
rts,
end-code

code sum2 ( a b -- a+b )         \ pop one cell, rewrite the next
clc,
lsb lda,x  lsb 1+ adc,x  lsb 1+ sta,x    \ LSB,x = top, LSB+1,x = second
msb lda,x  msb 1+ adc,x  msb 1+ sta,x
inx,                                      \ drop the old top
rts, end-code
```

`W W2 W3` are three 16-bit zeropage scratch cells reserved for code words
(the IRQ dispatcher saves them, so they are interrupt-safe).

### Using Forth VARIABLEs from code

A `VARIABLE` name pushes its address *while the assembler is reading your
code*, so it drops straight in as an operand — the natural way to keep
state or loop counters that survive between calls:

```forth
variable ticks
code tick+ ( -- )                \ ticks 1+! in machine code
ticks inc,
+branch bne,                     \ low byte wrapped? bump the high byte
ticks 1+ inc,
:+
rts, end-code
```

The same works for `CREATE`d tables: `mytable lda,y` indexes a table by Y.

### Flow: IF/THEN and BEGIN/UNTIL in code

Two label words give you structured flow. `:-` marks a backward target
that `-branch <branch>` jumps to; `+branch <branch>` opens a forward
branch that `:+` lands. Remember the branch takes the **opposite**
condition of the Forth word you are imitating:

```forth
\ IF..THEN:  skip when the condition is FALSE
lsb lda,x  msb ora,x             \ top cell zero?
+branch beq,                     \ IF ( skip forward when Z set )
  ...                            \ the "true" part
:+                               \ THEN

\ BEGIN..UNTIL:  a countdown loop ( n -- ), n >= 1
code stars ( n -- )              \ print n asterisks (n = 1..255)
lsb lda,x w sta,                 \ borrow W as the counter
inx,
:-                               \ BEGIN
txa, pha,  '*' lda,#  $ffd2 jsr,  pla, tax,   \ CHROUT (see below)
w dec,
-branch bne,                     \ UNTIL the counter hits 0
rts, end-code
```

Nest forward branches strictly **LIFO** — the innermost `+branch` must
meet its `:+` before an outer one is resolved. (Backward `:-` targets are
plain values on the stack during assembly; if you interleave them with
pending forwards, keep the bookkeeping straight or restructure — a
post-tested loop with the zero-case handled in Forth is usually simpler.)

### Calling the KERNAL

ROM routines clobber X — your stack pointer! — and much of zeropage. Save
X on the hardware stack around every `jsr,` into the ROM:

```forth
code emit* ( -- )                \ CHROUT a star
txa, pha,
'*' lda,#  $ffd2 jsr,
pla, tax,
rts, end-code
```

If the routine needs values in registers, load them from `LSB,x`/`MSB,x`
*before* the `txa, pha,` (or park them in `W`).

### Word reference

- `CODE ( "name" -- )` / `END-CODE ( -- )` — bracket a machine-code word; finish the code with `rts,`.
- `LSB / MSB ( -- addr )` — zeropage bases of the split data stack; index with the X register (`lsb lda,x` = the top's low byte, `lsb 1+ lda,x` = the second cell's). `dex,` pushes a cell slot, `inx,` pops.
- `W W2 W3 ( -- addr )` — three 16-bit zeropage scratch cells, saved by the IRQ dispatcher.
- Mnemonics end in a comma, operand first: `5 lda,#` (immediate), `$9F20 lda,` (absolute/zp), `lsb lda,x` / `lda,y` (indexed), `w lda,(y)` / `lda,(x)` (indirect), `asl,a` (accumulator), `w (jmp),` (indirect jump), `iny, rts, php,` … NMOS set only — no `bra,`/`stz,`/`phx,`.
- Branch labels: `:- ( -- addr )` marks a backward target consumed by `-branch <bcc,>`; `+branch <bcc,>` opens a forward branch resolved by `:+`. Nest forwards LIFO (innermost `:+` first).
- A VARIABLE / CREATEd name used as an operand assembles its data address: `ticks inc,`.
- KERNAL calls: `txa, pha, … $ffd2 jsr, … pla, tax,` — ROM clobbers X and zeropage.

---

## 43. Turnkey images — shipping a program

Compile your program, point `START` at its entry word, save a bootable
image:

```forth
INCLUDE GAME.FS
' MAIN START !
SAVE-PACK GAME          \ packed image; LOAD"GAME",8 runs MAIN at boot
```

`SAVE-PRG` saves a plain runnable PRG; `SAVE-FORTH` the raw memory image.
`TOP` / `TOP!` manage the dictionary ceiling for packing. On the
cartridge, the same mechanism plus the AUTORUN file gives you an instant
boot-to-game machine.

### Word reference

- `START ( -- addr )` — the boot-word cell: `' MAIN START !` makes MAIN run at load/boot.
- `SAVE-PACK ( "name" -- )` — pack the dictionary against the kernel and save a turnkey PRG that runs START.
- `SAVE-PRG ( "name" -- )` — plain runnable PRG, no packing.
- `SAVE-FORTH ( "name" -- )` — raw memory image $0801..TOP.
- `TOP ( -- addr )` / `TOP! ( addr -- )` — read / move the dictionary ceiling (packing).

---

## Appendix A — module cheat sheet

| Module | Cartridge boot | PRG boot | Gives you |
|---|---|---|---|
| GRAPHIC | `NEEDS GRAPHIC` | `INCLUDE GRAPHIC` | 320×240 bitmap drawing, pen API |
| ADVGFX | `NEEDS ADVGFX` | `INCLUDE ADVGFX` | clipping, flood fill, FX-COPY, rotozoom |
| ADVANCED | `NEEDS ADVANCED` | `INCLUDE ADVANCED` | PRNG, sin/cos/atan2/lerp, rings, ZX0/LZSA |
| ADVSND | `NEEDS ADVSND` | `INCLUDE ADVSND` | PSG envelopes, background PCM, ADPCM |
| BMX | `NEEDS BMX` | `INCLUDE BMX` | BMX image load/save |
| FLOAT / FLOATX | `NEEDS FLOAT` | `INCLUDE FLOAT` | floating point (+extended) |
| FILE | `NEEDS FILE` | `INCLUDE FILE` | ANS file words, CD/DIR |
| STRING | `NEEDS STRING` | `INCLUDE STRING` | full string toolkit |
| SYSTEM | `NEEDS SYSTEM` | `INCLUDE SYSTEM` | SYSCALL, USR, RANDOM, FREE... |
| EXTRAS | `NEEDS EXTRAS` | `INCLUDE EXTRAS` | structures, DEFER, FORGET, POSTPONE aids |

`NEEDS` reads the module out of cartridge ROM, so it **only exists on
cartridge boots** — on a `durexforth.prg` boot it is an unknown word
(`NEEDS?`). There, `INCLUDE <module>` compiles the same source from the
SD card. Everything the module defines is identical either way.

## Appendix B — things that bite

- **VARIABLEs are not zeroed.** After a MARKER/FORGET cycle a fresh
  VARIABLE holds junk — initialise explicitly: `VARIABLE X 0 X !`
- **`DO`, `BEGIN`, `>R` are compile-only.** Wrap them in a `:` definition;
  they cannot run from the keyboard or an interpreted file line.
- **Compare addresses with `U<`,** not `<` — addresses above $7FFF are
  negative as signed numbers.
- **PAD is transient.** Interpreted `S\"`/`CHR`/`RPT` strings live there;
  copy them out before the next PAD user runs.
- **IRQ words:** short, balanced, no disk I/O, `0 IRQ` before FORGET.
- **CBDOS file modes are case-sensitive** in raw DOS strings: `,S,W` works,
  `,s,w` wedges the drive (the FILE module handles this for you).
- The X16 boots with **RAM bank 1** selected, not 0. Bank 0 belongs to the
  KERNAL.
