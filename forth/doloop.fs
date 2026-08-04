code (do) ( limit first -- )
pla, w sta,
pla, tay,

msb 1+ lda,x pha, lsb 1+ lda,x pha,
msb lda,x pha, lsb lda,x pha,
inx, inx,

tya, pha,
w lda, pha,
rts, end-code

\ leave stack
variable lstk $14 allot
variable lsp lstk lsp !
: >l ( n -- ) lsp @ ! 2 lsp +! ;

: do 0
postpone (do) here dup >l ; immediate

: (?do)
2dup = if 2drop [ ' branch jmp, ] else
r> 2+ >r [ ' (do) jmp, ] then ;

: ?do
postpone (?do) here 0 ,
here dup >l ; immediate

: leave
postpone unloop
here 1+ >l 0 jmp, ; immediate

: resolve-leaves ( ?dopos dopos -- )
begin -2 lsp +!
dup lsp @ @ < while
here lsp @ @ ! repeat drop
\ ?do forward branch
?dup if here swap ! then ;

\ X816: the C64 (loop) indexed the stack page directly - tsx then inc
\ $103,x and an 8-bit txs. Neither survives a native 65816 whose S lives
\ outside page 1: absolute $01xx,x reads the wrong page THROUGH the data
\ bank, and an 8-bit txs zeroes SH, dropping the return stack into the
\ direct page. This version touches the CPU stack only with pla/pha,
\ which address the real S in bank 0 wherever it is.
\ Stack on entry, top down: ret(2, inline branch word follows it),
\ i(2), limit(2).
code (loop)
pla, w2 sta, pla, w2 1+ sta, \ return address
pla, tay, pla, w sta,        \ i: y = lsb, w = msb
iny, 2 bne, w inc,           \ i++
pla, w3 sta, pla, w3 1+ sta, \ limit
w lda, w3 1+ cmp, 1 @@ bne,  \ msb differs: not done
tya, w3 cmp, 1 @@ bne,       \ lsb differs: not done
\ done: resume past the inline branch target
w2 lda, clc, 3 adc,# w2 sta,
w2 1+ lda, 0 adc,# w2 1+ sta,
w2 (jmp),
1 @:
\ not done: rebuild the frame and take the branch
w3 1+ lda, pha, w3 lda, pha,
w lda, pha, tya, pha,
w2 1+ lda, pha, w2 lda, pha,
' branch jmp,

: loop
postpone (loop) dup , resolve-leaves ; immediate

: (+loop) ( inc -- )
r> swap r> 2dup +
rot 0< if tuck swap else tuck then
r@ 1- -rot within 0= if
>r >r [ ' branch jmp, ] then
r> 2drop 2+ >r ;

: +loop
postpone (+loop) dup , resolve-leaves ; immediate

: i postpone r@ ; immediate
\ X816: same page-1 rewrite as (loop). Stack on entry, top down: ret(2),
\ inner i(2), inner limit(2), outer i(2) - dig down with pulls, push it
\ all back, and deliver the outer index.
code j
pla, w sta, pla, w 1+ sta,     \ ret
pla, w2 sta, pla, w2 1+ sta,   \ inner i
pla, w3 sta, pla, w3 1+ sta,   \ inner limit
pla, tay, pla,                 \ outer i: y = lsb, a = msb
dex, msb sta,x lsb sty,x       \ push it on the data stack
pha, tya, pha,                 \ outer i back
w3 1+ lda, pha, w3 lda, pha,   \ inner limit back
w2 1+ lda, pha, w2 lda, pha,   \ inner i back
w 1+ lda, pha, w lda, pha,     \ ret back
rts, end-code

hide lstk
hide lsp
hide >l
hide resolve-leaves
