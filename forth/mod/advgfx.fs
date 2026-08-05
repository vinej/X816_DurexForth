\ ADVGFX - clipping, flood fill, fast VRAM copy (ADVANCED.TXT).
\ Load GRAPHIC first:  cart NEEDS GRAPHIC NEEDS ADVGFX,
\ card INCLUDE GRAPHIC INCLUDE ADVGFX (uses its BXY>BA / BPSET / BHLINE).
\ Ported from C:\quartus\projects\x16_library (util/clip.asm, gfx/bitmap.asm,
\ gfx/verafx.asm).

decimal

\ --- Cohen-Sutherland line clipping ------------------------------------------
variable clxmin  variable clymin  variable clxmax  variable clymax
0 clxmin !  0 clymin !  319 clxmax !  239 clymax !
: clip-rect ( xmin ymin xmax ymax -- )  \ inclusive; default 0 0 319 239
  clymax ! clxmax ! clymin ! clxmin ! ;

variable ca-x  variable ca-y  variable cb-x  variable cb-y
: (ocx) ( x -- c ) dup clxmin @ < 1 and swap clxmax @ > 2 and or ;
: (ocy) ( y -- c ) dup clymin @ < 4 and swap clymax @ > 8 and or ;
: (oc)  ( x y -- c ) (ocy) swap (ocx) or ;
: (yat) ( xc -- y )                     \ y where the segment crosses x = xc
  >r cb-y @ ca-y @ -  r> ca-x @ -  cb-x @ ca-x @ -  */  ca-y @ + ;
: (xat) ( yc -- x )
  >r cb-x @ ca-x @ -  r> ca-y @ -  cb-y @ ca-y @ -  */  ca-x @ + ;
\ rtl, not rts,. Every word in this Forth is entered with jsl and must
\ leave with rtl: an rts ($60 against $6b) pops two bytes where three went
\ on, so the bank byte stays and the NEXT rtl returns into nowhere - at a
\ distance that depends on what ran in between. These were all rts.
: (cpt) ( code -- x y )                 \ intersection point for one outcode bit
  dup 1 and if drop clxmin @ dup (yat) exit then
  dup 2 and if drop clxmax @ dup (yat) exit then
  4 and if clymin @ dup (xat) swap exit then
  clymax @ dup (xat) swap ;

: clip-line ( x1 y1 x2 y2 -- x1' y1' x2' y2' flag )  \ flag: any part visible?
  cb-y ! cb-x ! ca-y ! ca-x !
  begin
    ca-x @ ca-y @ (oc)  cb-x @ cb-y @ (oc)
    2dup and if 2drop 0 0 0 0 0 exit then
    2dup or 0= if
      2drop ca-x @ ca-y @ cb-x @ cb-y @ -1 exit then
    swap ?dup if
      nip (cpt) ca-y ! ca-x !           \ clip endpoint A by its own code
    else
      (cpt) cb-y ! cb-x !               \ A inside: clip endpoint B
    then
  again ;

: cline ( x1 y1 x2 y2 color -- )        \ clipped line (needs GRAPHIC's LINE)
  >r clip-line if r> line else 2drop 2drop r> drop then ;

\ --- scanline flood fill --------------------------------------------------------
\ FLOOD ( x y color -- ) fills the 4-connected region of the seed's colour on
\ the 320x240 bitmap. Uses a 128-span seed stack; pathological shapes beyond
\ that are filled incompletely.
\ 128 seeds of TWO CELLS each. The slot was 4 bytes with the pair at +0
\ and +2, which is two 16-bit cells - and `!` writes FOUR here, so the
\ second store landed on top of the first and every seed popped back as
\ garbage. FLOOD then filled nothing at all, silently.
create (fsk) 1024 allot   variable (fsp)
variable ftgt  variable fcol  variable ffx  variable ffy
variable fxl   variable fxr   variable fs-y  variable fs-in
: (pix@) ( x y -- c ) bxy>ba vpeek ;
: (fpush) ( x y -- )
  (fsp) @ 128 < if
    (fsp) @ 8 * (fsk) + tuck 4 + ! !  1 (fsp) +! else 2drop then ;
: (fpop) ( -- x y ) -1 (fsp) +!  (fsp) @ 8 * (fsk) + dup @ swap 4 + @ ;
: (frun?) ( y x -- flag )               \ target-coloured pixel at x,y?
  dup 0< over 319 > or if 2drop 0 exit then
  swap (pix@) ftgt @ = ;
: (fscan) ( y xl xr -- )                \ push one seed per target run on row y
  rot fs-y !  0 fs-in !
  fs-y @ 0 240 within 0= if 2drop exit then
  1+ swap ?do
    fs-y @ i (frun?) if
      fs-in @ 0= if i fs-y @ (fpush)  1 fs-in ! then
    else 0 fs-in ! then
  loop ;
: flood ( x y color -- )
  fcol !
  2dup (pix@) dup fcol @ = if drop 2drop exit then ftgt !
  0 (fsp) !  (fpush)
  begin (fsp) @ while
    (fpop) ffy ! ffx !
    ffy @ ffx @ (frun?) if
      ffx @ begin dup 1- ffy @ swap (frun?) while 1- repeat fxl !
      ffx @ begin dup 1+ ffy @ swap (frun?) while 1+ repeat fxr !
      fxl @ ffy @  fxr @ fxl @ - 1+  fcol @ bhline
      ffy @ 1- fxl @ fxr @ (fscan)
      ffy @ 1+ fxl @ fxr @ (fscan)
    then
  repeat ;

\ --- VERA FX cached copy ----------------------------------------------------------
\ FX-COPY ( sbank saddr dbank daddr u -- ) VRAM copy, 4 bytes per flush.
\ The DESTINATION must be 4-byte aligned; the source may be anything.
\ Colon, not CODE, and that is a correctness choice rather than taste. The
\ assembly version here was 6502-shaped in a Forth whose words run with a
\ SIXTEEN-bit accumulator: it popped its argument with one `inx,` where a
\ cell is two bytes of X, decremented a 16-bit counter as though it were
\ two 8-bit halves, and read VERA's data port with a 16-bit load, taking
\ two registers at a time. Each read below is a byte, and the loop counts
\ in Forth. Slower per quad, and it copies what it was asked to.
: (fxq) ( n -- )                        \ n >= 1 cache quads: 4 reads, 1 flush
  0 ?do
    $9f24 ioc@ drop  $9f24 ioc@ drop
    $9f24 ioc@ drop  $9f24 ioc@ drop
    0 $9f23 ioc!
  loop ;

: fx-copy ( sbank saddr dbank daddr u -- )
  >r
  2 dcsel 0 $9f29 c!                    \ FX_CTRL = 0 while the ports are aimed
  0 $9f2c c!                            \ FX_MULT off + cache index reset
  2swap                                 \ ( db da sb sa )
  $9f25 c@ 1 or $9f25 c!                \ port 1 = source, +1
  dup 255 and $9f20 c!  8 rshift $9f21 c!
  1 and $10 or $9f22 c!
  $9f25 c@ 254 and $9f25 c!             \ port 0 = destination, +4
  dup 255 and $9f20 c!  8 rshift $9f21 c!
  1 and $30 or $9f22 c!
  2 dcsel $60 $9f29 c!                  \ CACHE_FILL | CACHE_WRITE
  r@ 2 rshift ?dup if (fxq) then
  2 dcsel 0 $9f29 c!  0 dcsel           \ FX off; DCSEL/ADDRSEL back to 0
  $9f22 c@ 15 and 16 or $9f22 c!        \ port 0 increment back to +1
  r> 3 and 0 ?do $9f24 c@ $9f23 c! loop ;

\ --- VERA FX affine (rotozoom / mode-7) ---------------------------------------------
\ Port 1 becomes a texture sampler: an 8x8-texel tile set + a tile map define
\ a square texture, AFFINE-RAY aims a fixed-point sampling ray, and every
\ DATA1 read returns the texel under the ray and steps it.  A rotated, scaled
\ scanline is one AFFINE-RAY + one AFFINE-LINE; feed dx/dy from SIN8/COS8
\ (NEEDS ADVANCED) times the zoom.  Tiles are 8 bpp, 64 bytes each.
: (fx>base) ( bank addr -- bits )       \ VRAM addr 16:11 packed into bits 7:2
  11 rshift swap 1 and 5 lshift or 2* 2* ;
: affine-on ( tbank taddr mbank maddr size clip -- )
\ tile data + tile map VRAM addresses (both 2 KB aligned);
\ size: 0=2x2 1=8x8 2=32x32 3=128x128 tiles; clip: 0 = wrap at the edges
  2 dcsel  3 $9f29 c!                   \ FX_CTRL: ADDR1 mode 3 = affine
  >r >r
  (fx>base) r> 3 and or $9f2b c!        \ FX_MAPBASE | map size
  (fx>base) r> 1 and 2* or $9f2a c!     \ FX_TILEBASE | clip enable
  0 dcsel ;
: (incr!) ( n lo-reg -- )               \ 15-bit signed 1/512-texel increment
  >r $7fff and dup 255 and r@ c! 8 rshift r> 1+ c! ;
: affine-ray ( x y dx dy -- )
\ x/y: starting texel 0-1023; dx/dy: signed step per read, 512 = one texel
  3 dcsel
  $9f2b (incr!)  $9f29 (incr!)
  5 dcsel  $80 $9f29 c!  $80 $9f2a c!   \ subpixel 0.5: sample texel centres
  4 dcsel  swap
  dup 255 and $9f29 c!  8 rshift 7 and $9f2a c!
  dup 255 and $9f2b c!  8 rshift 7 and $9f2c c!  \ positions last: prefetch
  0 dcsel ;
: (aspan) ( n -- )                      \ n >= 1 texels: DATA1 -> DATA0
  0 ?do $9f24 ioc@ $9f23 ioc! loop ;
: affine-span ( n -- ) ?dup if (aspan) then ;  \ port 0 aimed by the caller
: affine-line ( bank addr n -- )        \ sample n texels to VRAM dst, +1 step
  >r vaddr r> affine-span ;
: affine-off ( -- ) 2 dcsel 0 $9f29 c! 0 dcsel ;
