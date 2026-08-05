\ ANS file access over the kernel's FS_* calls (asm/file.asm + base.fs).
\
\ Replaces the upstream CBDOS version, which opened channels through a
\ `file` module that has no X816 equivalent. What it covered and this does
\ not is listed at the bottom, with reasons - a test quietly dropped is
\ indistinguishable from one that never existed.
\
\ These WRITE TO THE CARD, like the kernel's own KFSTEST does, because a
\ filesystem test that only reads proves the reader and nothing else. Every
\ file is made under a name of our own and deleted at the end; a failed run
\ can leave FTEST*.TXT behind, which is harmless and is the trail you want.
\ Card names are bare 8.3 - the kernel uppercases them itself.
\
\ Every T{ ... }T must leave EXACTLY the items it compares: the tester
\ records the whole stack at -> , so a stray handle left under the ior is
\ a WRONG NUMBER OF RESULTS, not a passing test. Hence `swap fd !` on
\ every open. Requires tester.fs.

marker ---testfile---

decimal

variable fd
create (fb) 64 allot            \ scratch for reads

cr .( testfile: create, write, close ) cr
T{ s" FTEST.TXT" r/o create-file swap fd ! -> 0 }T
T{ fd @ 0<> -> true }T                  \ handles start at 1, never 0
T{ s" hello" fd @ write-file -> 0 }T
T{ s" -world" fd @ write-file -> 0 }T   \ a second write appends
T{ fd @ close-file -> 0 }T

cr .( testfile: it is on the card, at the right size ) cr
T{ s" FTEST.TXT" r/o open-file swap fd ! -> 0 }T
T{ fd @ file-size -> 11 0 0 }T          \ "hello-world" as a double, + ior

cr .( testfile: read it back, bytes and all ) cr
T{ (fb) 64 fd @ read-file -> 11 0 }T
T{ (fb) 11 s" hello-world" compare -> 0 }T
\ Reading at the end gives 0 bytes and NO error: that is how end of file
\ arrives here, and a test expecting an ior would hide a broken short read.
T{ (fb) 64 fd @ read-file -> 0 0 }T

cr .( testfile: seek and tell ) cr
T{ fd @ file-position -> 11 0 0 }T      \ the read left us at the end
T{ 0 0 fd @ reposition-file -> 0 }T
T{ fd @ file-position -> 0 0 0 }T
T{ (fb) 5 fd @ read-file -> 5 0 }T
T{ (fb) 5 s" hello" compare -> 0 }T
T{ fd @ file-position -> 5 0 0 }T
T{ 6 0 fd @ reposition-file -> 0 }T     \ forward, past the dash
T{ (fb) 5 fd @ read-file -> 5 0 }T
T{ (fb) 5 s" world" compare -> 0 }T
\ ...and BACKWARDS, which is the path worth testing: kfs.c can only walk a
\ cluster chain forwards, so a backward seek restarts it from the first
\ cluster. A file this small fits one cluster, but the code is the same.
T{ 0 0 fd @ reposition-file -> 0 }T
T{ (fb) 5 fd @ read-file -> 5 0 }T
T{ (fb) 5 s" hello" compare -> 0 }T
T{ fd @ close-file -> 0 }T

cr .( testfile: OPEN-FILE refuses rather than truncating ) cr
\ This kernel has no open-for-writing-that-keeps-the-contents, so W/O and
\ R/W are refused. Proving the file SURVIVES the refusal is the point: a
\ nonzero ior with an emptied file behind it would be the data loss the
\ rule exists to prevent.
T{ s" FTEST.TXT" w/o open-file -> 0 1 }T
T{ s" FTEST.TXT" r/w open-file -> 0 1 }T
T{ s" FTEST.TXT" r/o open-file swap fd ! -> 0 }T
T{ fd @ file-size -> 11 0 0 }T          \ still all eleven bytes
T{ fd @ close-file -> 0 }T

cr .( testfile: a missing file reports, and asking does not create it ) cr
T{ s" NOSUCH.TXT" r/o open-file nip 0<> -> true }T
T{ s" NOSUCH.TXT" file-status nip 0<> -> true }T
T{ s" FTEST.TXT" file-status nip -> 0 }T

cr .( testfile: read-line, including a last line with no newline ) cr
T{ s" FTEST2.TXT" r/o create-file swap fd ! -> 0 }T
T{ s" one" fd @ write-line -> 0 }T
T{ s" two" fd @ write-line -> 0 }T
T{ s" three" fd @ write-file -> 0 }T    \ deliberately unterminated
T{ fd @ close-file -> 0 }T
T{ s" FTEST2.TXT" r/o open-file swap fd ! -> 0 }T
T{ (fb) 64 fd @ read-line -> 3 -1 0 }T
T{ (fb) 3 s" one" compare -> 0 }T
T{ (fb) 64 fd @ read-line -> 3 -1 0 }T
T{ (fb) 3 s" two" compare -> 0 }T
\ No newline ends the file, so this is still A LINE - flag true - and only
\ the NEXT call reports end of file. Getting that boundary wrong silently
\ eats the last line of every file that lacks a trailing newline.
T{ (fb) 64 fd @ read-line -> 5 -1 0 }T
T{ (fb) 5 s" three" compare -> 0 }T
T{ (fb) 64 fd @ read-line -> 0 0 0 }T
T{ fd @ close-file -> 0 }T

cr .( testfile: rename, then delete both ) cr
T{ s" FTEST2.TXT" s" FTEST3.TXT" rename-file -> 0 }T
T{ s" FTEST2.TXT" file-status nip 0<> -> true }T   \ the old name is gone
T{ s" FTEST3.TXT" file-status nip -> 0 }T
T{ s" FTEST3.TXT" delete-file -> 0 }T
T{ s" FTEST.TXT" delete-file -> 0 }T
T{ s" FTEST.TXT" file-status nip 0<> -> true }T    \ really deleted
T{ s" FTEST3.TXT" file-status nip 0<> -> true }T

cr .( testfile: the two words that report they cannot ) cr
\ Neither has a kernel call behind it. Returning 0 would tell a caller
\ their data was safe on the card when nothing had been asked to put it
\ there - CLOSE-FILE is the sync point on this machine.
T{ 1 flush-file -> 1 }T
T{ 0 0 1 resize-file -> 1 }T

\ NOT TESTED HERE, and not silently: INCLUDE-FILE ( fileid -- ) is not
\ implemented - the interpreter's source stack (io.asm) is entered by
\ INCLUDED, which opens the file itself, and handing it an already-open
\ handle means opening those internals up. R/W modify mode is not
\ implemented because the kernel has no such open mode. Both are [ ] in
\ FILE.TXT for exactly these reasons.

cr .( testfile ok ) cr

---testfile---
