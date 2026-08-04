\ X816 stage B: loop-control cells are 32 bits = TWO 16-bit words each on
\ the CPU return stack, low word pushed last (matching >R / R@ / core's
\ cell layout: i-lo at 3,s, i-hi at 5,s under a return address).
\ pla,/pha, run 16-bit (M=0), so the return-address juggling is one pull.
\ Inline branch operands after (do)/(loop)/(?do) stay 2 bytes -> w, / w!.

code (do) ( limit first -- )
pla, w sta,
msb 2+ lda,x pha, lsb 2+ lda,x pha,
msb lda,x pha, lsb lda,x pha,
inx, inx, inx, inx,
w lda, pha,
rts, end-code

\ leave stack (cells are 4 bytes)
variable lstk $28 allot
variable lsp lstk lsp !
: >l ( n -- ) lsp @ ! 4 lsp +! ;

: do 0
postpone (do) here dup >l ; immediate

: (?do)
2dup = if 2drop [ ' branch jmp, ] else
rw> 2+ w>r [ ' (do) jmp, ] then ;

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

\ X816: the CPU stack is touched only with pla/pha, which address the
\ real S in bank 0 wherever it is. Stack on entry, top down: ret(1 word,
\ inline 2-byte branch operand follows it), i(2 words), limit(2 words).
code (loop)
pla, w2 sta,                  \ return address
pla, w sta, pla, w 2+ sta,    \ i (low word, high word)
w inc, 2 @@ bne, w 2+ inc,    \ i++ (32-bit)
2 @:
pla, w3 sta, pla, w3 2+ sta,  \ limit
w 2+ lda, w3 2+ cmp, 1 @@ bne, \ high words differ: not done
w lda, w3 cmp, 1 @@ bne,       \ low words differ: not done
\ done: resume past the inline branch target
w2 lda, clc, 3 adc,# w2 sta,
w2 (jmp),
1 @:
\ not done: rebuild the frame and take the branch
w3 2+ lda, pha, w3 lda, pha,
w 2+ lda, pha, w lda, pha,
w2 lda, pha,
' branch jmp,

: loop
postpone (loop) dup w, resolve-leaves ; immediate

: (+loop) ( inc -- )
rw> swap r> 2dup +
rot 0< if tuck swap else tuck then
r@ 1- -rot within 0= if
>r w>r [ ' branch jmp, ] then
r> 2drop 2+ w>r ;

: +loop
postpone (+loop) dup w, resolve-leaves ; immediate

: i postpone r@ ; immediate
\ Stack on entry, top down: ret(1 word), inner i(2 words), inner
\ limit(2 words), outer i(2 words) - dig down with pulls, push it all
\ back, and deliver the outer index.
code j
pla, w sta,                    \ ret
pla, w2 sta, pla, w2 2+ sta,   \ inner i
pla, w3 sta, pla, w3 2+ sta,   \ inner limit
dex, dex,
pla, lsb sta,x pla, msb sta,x  \ outer i -> the data stack
msb lda,x pha, lsb lda,x pha,  \ outer i back (high word first)
w3 2+ lda, pha, w3 lda, pha,   \ inner limit back
w2 2+ lda, pha, w2 lda, pha,   \ inner i back
w lda, pha,                    \ ret back
rts, end-code

hide lstk
hide lsp
hide >l
hide resolve-leaves
