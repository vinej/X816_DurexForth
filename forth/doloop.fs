\ X816 stage C: loop-control cells are 32 bits = TWO 16-bit words each on
\ the CPU return stack, low word pushed last; a RETURN ADDRESS is THREE
\ bytes (jsl pushes the bank). The code words pull ret as PC-then-bank,
\ resume through [jmp], (jml [dp]), and re-push bank-then-PC so a final
\ rtl consumes exactly what jsl left. Inline branch operands stay 16-bit:
\ a definition never leaves its bank - but the transfer INTO the asm
\ words (branch, (do)) crosses banks, so those references are jml,.

code (do) ( limit first -- )
pla, w sta,
$20 sep, pla, w 2+ sta, $20 rep,
msb 2+ lda,x pha, lsb 2+ lda,x pha,
msb lda,x pha, lsb lda,x pha,
inx, inx, inx, inx,
$20 sep, w 2+ lda, pha, $20 rep,
w lda, pha,
rtl, end-code

\ leave stack (cells are 4 bytes)
variable lstk $28 allot
variable lsp lstk lsp !
: >l ( n -- ) lsp @ ! 4 lsp +! ;

: do 0
postpone (do) here dup >l ; immediate

: (?do)
2dup = if 2drop [ ' branch jml, ] else
rl> 2+ l>r [ ' (do) jml, ] then ;

: ?do
postpone (?do) here 0 w,
here dup >l ; immediate

: leave
postpone unloop
here 1+ >l 0 jmp, ; immediate

: resolve-leaves ( ?dopos dopos -- )
begin -4 lsp +!
dup lsp @ @ < while
here lsp @ @ w! repeat drop
\ ?do forward branch
?dup if here swap w! then ;

\ Stack on entry, top down: ret(3, inline 2-byte branch operand follows),
\ i(2 words), limit(2 words).
code (loop)
pla, w2 sta,
$20 sep, pla, w2 2+ sta, $20 rep, \ return address (PC, then bank)
pla, w sta, pla, w 2+ sta,        \ i (low word, high word)
w inc, 2 @@ bne, w 2+ inc,        \ i++ (32-bit)
2 @:
pla, w3 sta, pla, w3 2+ sta,      \ limit
w 2+ lda, w3 2+ cmp, 1 @@ bne,    \ high words differ: not done
w lda, w3 cmp, 1 @@ bne,          \ low words differ: not done
\ done: resume past the inline branch target
w2 lda, clc, 3 adc,# w2 sta,
w2 [jmp],
1 @:
\ not done: rebuild the frame and take the branch
w3 2+ lda, pha, w3 lda, pha,
w 2+ lda, pha, w lda, pha,
$20 sep, w2 2+ lda, pha, $20 rep,
w2 lda, pha,
' branch jml,

: loop
postpone (loop) dup w, resolve-leaves ; immediate

: (+loop) ( inc -- )
rl> swap r> 2dup +
rot 0< if tuck swap else tuck then
r@ 1- -rot within 0= if
>r l>r [ ' branch jml, ] then
r> 2drop 2+ l>r ;

: +loop
postpone (+loop) dup w, resolve-leaves ; immediate

: i postpone r@ ; immediate
\ Stack on entry, top down: ret(3), inner i(2 words), inner limit(2
\ words), outer i(2 words) - dig down with pulls, push it all back, and
\ deliver the outer index.
code j
pla, w sta,
$20 sep, pla, w 2+ sta, $20 rep,  \ ret
pla, w2 sta, pla, w2 2+ sta,      \ inner i
pla, w3 sta, pla, w3 2+ sta,      \ inner limit
dex, dex,
pla, lsb sta,x pla, msb sta,x     \ outer i -> the data stack
msb lda,x pha, lsb lda,x pha,     \ outer i back (high word first)
w3 2+ lda, pha, w3 lda, pha,      \ inner limit back
w2 2+ lda, pha, w2 lda, pha,      \ inner i back
$20 sep, w 2+ lda, pha, $20 rep,  \ ret back (bank, then PC)
w lda, pha,
rtl, end-code

hide lstk
hide lsp
hide >l
hide resolve-leaves
