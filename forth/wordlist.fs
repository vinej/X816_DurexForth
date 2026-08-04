: hide ( "name" -- )
parse-name find-name ?dup if
dup latest - ( nt size )
>r c@ $1f and 4 + ( off: len byte + name + 3-byte xt )
latest swap over +  ( srca dsta )
dup to latest
r> move then ;
: defcode ( "name" -- )
parse-name 2dup find-name ?dup 0=
if notfound then nip nip
count $1f and + here 2dup swap w! split nip swap 2 + c! ;
: define defcode ] ;
