\ Directories over the kernel's DIR_* / FS_CHDIR / FS_MKDIR calls
\ (asm/file.asm + base.fs). The other half of the filesystem story from
\ testfile.fs, and it WRITES TO THE CARD the same way: it makes a
\ directory of its own, works inside it, and removes it again.
\
\ The X16 words this replaces sent DOS command strings to a CBM device
\ over the IEC bus. There is no IEC bus here and no device number; a path
\ is ASCII with / separators and the kernel resolves it.

marker ---testdir---

\ Standalone-safe: REQUIRE loads the Hayes tester only if it is not
\ already in, so `include testdir` works on its own at the prompt and
\ costs nothing inside the suite, where test.fs loaded it first.
require tester

decimal

\ CWD returns ( c-addr u ior ) - three items - so it cannot be compared
\ directly by the tester, which records the WHOLE stack at -> . This
\ folds it to one flag, which is what every check below actually wants.
: cwd= ( c-addr u -- flag ) cwd if 2drop 2drop 0 exit then compare 0= ;

cr .( testdir: where we start ) cr
\ The suite runs from the card root. Asserting it means every relative
\ path below is anchored, and a leftover CD from an earlier failed run
\ shows up here rather than as a confusing failure three tests later.
T{ s" /" cwd= -> true }T

cr .( testdir: make one, enter it, come back ) cr
T{ s" TDIR" mkdir -> 0 }T
T{ s" TDIR" mkdir 0<> -> true }T        \ making it twice is refused
T{ s" TDIR" cd -> 0 }T
T{ s" /TDIR" cwd= -> true }T
T{ s" .." cd -> 0 }T
T{ s" /" cwd= -> true }T

cr .( testdir: a file made inside it is really inside it ) cr
\ This is what proves CD moved the KERNEL's idea of the directory and not
\ just a string: the file is created by a RELATIVE name while inside TDIR,
\ then found by an ABSOLUTE one from the root.
T{ s" TDIR" cd -> 0 }T
variable fd
T{ s" INNER.TXT" r/o create-file swap fd ! -> 0 }T
T{ s" hi" fd @ write-file -> 0 }T
T{ fd @ close-file -> 0 }T
T{ s" .." cd -> 0 }T
T{ s" /TDIR/INNER.TXT" file-status nip -> 0 }T
T{ s" INNER.TXT" file-status nip 0<> -> true }T   \ not in the root

cr .( testdir: listing finds it, and knows it is not a directory ) cr
0 value (h)
0 value (seen)
0 value (sawdir)
0 value (err)
T{ s" /TDIR" dir-open swap to (h) -> 0 }T
\ The walk is a COLON DEFINITION, not loose lines: BEGIN/WHILE/REPEAT are
\ compile-only, and typing them at the interpreter compiles branches into
\ HERE that nothing ever executes - the loop silently does not loop.
\ Any ior mid-listing is recorded and ends the walk rather than being
\ asserted inside it: a T{ }T in there would compare two items against
\ one and report WRONG NUMBER instead of the error it found.
: (walk) ( -- )
  begin (h) dir-next ( flag ior )
    ?dup if to (err) drop 0 then
  while
    dirent-name s" INNER.TXT" compare 0= if
      dirent-size to (seen)
      dirent-dir? if -1 to (sawdir) then
    then
  repeat ;
(walk)
T{ (h) dir-close -> 0 }T
T{ (err) -> 0 }T                        \ no error during the walk
T{ (seen) -> 2 }T                       \ the two bytes we wrote
T{ (sawdir) -> 0 }T                     \ a file, not a directory

cr .( testdir: a non-empty directory will not be removed ) cr
\ FAT32 would happily orphan the contents; the kernel refuses instead, and
\ that refusal is the only thing standing between a typo and lost files.
T{ s" TDIR" rmdir 0<> -> true }T
T{ s" /TDIR/INNER.TXT" file-status nip -> 0 }T    \ survived the attempt
T{ s" /TDIR/INNER.TXT" delete-file -> 0 }T
T{ s" TDIR" rmdir -> 0 }T
T{ s" TDIR" cd 0<> -> true }T           \ and it is really gone

cr .( testdir: opening something that is not a directory ) cr
T{ s" NOSUCHDIR" dir-open nip 0<> -> true }T

cr .( testdir: we finish where we started ) cr
T{ s" /" cwd= -> true }T

cr .( testdir ok ) cr

---testdir---
