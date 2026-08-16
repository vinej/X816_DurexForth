0 value addr
: accept ( addr avail -- len )
swap to addr 0 ( avail len )
begin key case
\ return and delete ($08: the X816 console backspace, not PETSCII $14).
\ BACKSPACE IS THREE CHARACTERS, not one: the X816 console's $08 only steps
\ the cursor left - console.c's con_putc decrements con_curx and returns,
\ leaving the glyph in VRAM. So rub it out and step back over it, which is
\ exactly what the kernel's own sh_readline does. Emitting the bare $08 left
\ every deleted character on screen while the line buffer shortened
\ underneath it, so the display and the buffer disagreed from the first
\ correction onwards.
$0d of nip space exit endof
$08 of dup if 1- $08 emit bl emit $08 emit then endof
\ ( avail len char ) add to buffer?
>r 2dup > r@ $7f and $1f > and if
 r@ over addr + c! 1+ r@ emit then r>
endcase again ;
hide addr
