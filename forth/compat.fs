\ forth2012 compatibility stuff
\ (2over 2swap d+ ?dnegate dabs and the double-cell words moved to base.fs core)

-1 constant true
0 constant false

: environment? 2drop 0 ;
: cells 4 * ; \ stage B: a cell is 32 bits
: cell+ 4 + ;
: char+ 1+ ;
: chars ; : align ; : aligned ;

: 0> 0 > ;

: accumulate ( +d0 addr digit - +d1 addr )
swap >r swap base @ um* drop
rot base @ um* d+ r> ;

: pet# ( char -- num )
$7f and dup \ lowercase
':' < if '0' else '7' then - ;

: >number ( ud addr u -- ud addr u )
begin over c@ pet# base @ u< over and
while >r dup c@ pet# accumulate
1+ r> 1- repeat ;

\ from FIG UK
: ?negate 0< if negate then ;
: sm/rem
2dup xor >r over >r abs >r dabs
r> um/mod swap r> ?negate
swap r> ?negate ;

: >body ( xt -- dataaddr ) 5 + ;
: defer create ['] abort ,
does> @ execute ;
: defer! >body ! ;
: is state @ if
postpone ['] postpone defer!
else ' defer! then ; immediate
