\ HELP - the manual, read off the card (base.fs, /HELP on the card).
\
\ NOTHING HERE CALLS `help` ON A REAL PAGE, deliberately. HELP pauses
\ every 22 lines and waits for a key, so a page longer than that would
\ block the suite for ever - and pages grow. The display loop is covered
\ end to end against a file this test writes itself, whose length it
\ therefore controls.

marker ---testhelp---

\ Standalone-safe: REQUIRE loads the Hayes tester only if it is not
\ already in, so `include testhelp` works on its own at the prompt and
\ costs nothing inside the suite, where test.fs loaded it first.
require tester

decimal

cr .( testhelp: topic -> card path ) cr
\ Card names are the topic truncated to EIGHT characters and uppercased,
\ because the kernel's FAT32 reader skips long filenames. Both card
\ builders apply that rule (run-tests.sh and X816_core mksdcard.py); if
\ this and they ever disagree, HELP looks for a file that is not there.
T{ s" PAL" (hpath!) s" /HELP/PAL.TXT" compare -> 0 }T
T{ s" ARITHMETIC" (hpath!) s" /HELP/ARITHMET.TXT" compare -> 0 }T
T{ s" INTERPRETER" (hpath!) s" /HELP/INTERPRE.TXT" compare -> 0 }T
T{ s" video" (hpath!) s" /HELP/VIDEO.TXT" compare -> 0 }T

cr .( testhelp: the pages are actually on the card ) cr
\ One short name, and every name that had to be truncated - those are the
\ ones a card builder can get wrong while the rest still work.
T{ s" INDEX" (hpath!) file-status nip -> 0 }T
T{ s" PAL" (hpath!) file-status nip -> 0 }T
T{ s" ARITHMETIC" (hpath!) file-status nip -> 0 }T
T{ s" ASSEMBLER" (hpath!) file-status nip -> 0 }T
T{ s" CONSTANTS" (hpath!) file-status nip -> 0 }T
T{ s" DICTIONARY" (hpath!) file-status nip -> 0 }T
T{ s" INTERPRETER" (hpath!) file-status nip -> 0 }T
T{ s" STRUCTURE" (hpath!) file-status nip -> 0 }T
T{ s" NOSUCHTOPIC" (hpath!) file-status nip 0<> -> true }T

cr .( testhelp: the display loop, on a file of known length ) cr
\ Three lines, one of them EMPTY - which is the case that broke TYPE and
\ sprayed the screen with 2^32 bytes. A blank line in the middle of a
\ page is completely ordinary, so this stays here as the end-to-end guard
\ even though testcoreadd now checks TYPE directly.
variable fd
T{ s" HTEST.TXT" r/o create-file swap fd ! -> 0 }T
T{ s" one" fd @ write-line -> 0 }T
T{ s" " fd @ write-line -> 0 }T
T{ s" three" fd @ write-line -> 0 }T
T{ fd @ close-file -> 0 }T

T{ s" HTEST.TXT" r/o open-file swap to (hfd) -> 0 }T
cr (hshow)                              \ prints the three lines
T{ (hfd) close-file -> 0 }T
T{ depth -> 0 }T                        \ and left nothing behind
T{ s" HTEST.TXT" delete-file -> 0 }T

cr .( testhelp: an unknown topic is refused, not fatal ) cr
\ HELP prints its own message and returns; the machine keeps working and
\ the stack is clean. Safe to run for real because it never opens a page.
help nosuchtopic
T{ depth -> 0 }T
T{ 2 3 + -> 5 }T

cr .( testhelp ok ) cr

---testhelp---
