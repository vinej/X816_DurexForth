0 value addr
: accept ( addr avail -- len )
swap to addr 0 ( avail len )
begin key case
\ return and delete ($08: the X816 console backspace, not PETSCII $14):
$0d of nip space exit endof
$08 of dup if 1- $08 emit then endof
\ ( avail len char ) add to buffer?
>r 2dup > r@ $7f and $1f > and if
 r@ over addr + c! 1+ r@ emit then r>
endcase again ;
hide addr
