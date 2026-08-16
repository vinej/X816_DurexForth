\ durexForth X816 stage-A test runner.
\ Included by an AUTORUN of "include test/test" (run-tests.sh writes one
\ onto the card) or by hand from the prompt. On the first failed assertion
\ the Hayes tester prints "INCORRECT RESULT" / "WRONG NUMBER" and QUITs, so
\ reaching the banner below means every test passed.
\
\ EVERY NAME BELOW CARRIES A "test/" PREFIX, and the modules do not. The
\ suite's forty-odd files live in a TEST subdirectory of the Forth
\ directory so that /FORTH is the language and not a heap; the kernel
\ resolves a relative name against the working directory (kfs.c,
\ kfs_abspath), and the working directory stays /FORTH -- that is where
\ base and the modules are, and where durexForth's own boot chain reads
\ from. So a test is "test/testcore" and a module is still "advanced".
\ The names after the prefix are CARD names, 8.3 and truncated: the
\ kernel's FAT32 reader skips long filenames, so testcoreplus travels as
\ coreplus. ../X816_core/tools/mksdcard.py places them and run-tests.sh
\ builds a matching card.
\ Stage A scope (16-bit cells, kernel console, kernel FS_*): the ANS core
\ suites, exceptions, doubles, and the VERA words. Omitted until their
\ words return: testbank/testromdisk/testvramdisk (no banking, no romdisk),
\ testfile/testloadsave (fs words beyond INCLUDED), the remaining
\ mod/*.fs suites (modules load via require from disk), testx16 (charset
\ and friends are parked), testinput (no joystick/mouse on the core),
\ testaudio (audio module), testsee (C64 screen scraping).

marker ---test---

page cr .( >> compat) cr parse-name compat included
cr .( >> tester) cr parse-name test/tester included
\ Upstream bracketed the core suites in their own marker because their
\ helper constants shadow the assembler's (MSB especially), breaking any
\ CODE word compiled afterwards. None of the remaining suites compiles a
\ CODE word, and the X816 dictionary has 24 KB the C64 never did, so the
\ definitions simply stay until ---test--- unwinds everything at the end.
cr .( >> testcore) cr parse-name test/testcore included
cr .( >> testcoreplus) cr parse-name test/coreplus included
cr .( >> testcoreext) cr parse-name test/coreext included
cr .( >> testexception) cr parse-name test/testexc included
cr .( >> testdouble) cr parse-name test/testdbl included
cr .( >> testvideo) cr parse-name test/testvid included
cr .( >> testsprite) cr parse-name test/testspr included
cr .( >> testtile) cr parse-name test/testtile included
cr .( >> testpalfx) cr parse-name test/testpal included
cr .( >> testcoreadd) cr parse-name test/coreadd included
cr .( >> testfar) cr parse-name test/testfar included
cr .( >> testbrk) cr parse-name test/testbrk included
cr .( >> testturbo) cr parse-name test/turbo included
cr .( >> testnmi) cr parse-name test/testnmi included
cr .( >> testfont) cr parse-name test/testfont included
cr .( >> testfile) cr parse-name test/testfile included
cr .( >> testdir) cr parse-name test/testdir included
cr .( >> testhelp) cr parse-name test/testhelp included
cr .( >> testload) cr parse-name test/testload included
cr .( >> teststruct) cr parse-name test/struct included
cr .( >> testaudio) cr parse-name test/testaud included
cr .( >> testfm) cr parse-name test/testfm included
cr .( >> testfloat) cr parse-name test/testfloa included
cr .( >> testinput) cr parse-name test/testinp included
cr .( >> teststring) cr parse-name test/teststri included
cr .( >> testextras) cr parse-name test/testextr included
cr .( >> testadv) cr parse-name test/testadv included
cr .( >> testirq) cr parse-name test/testirq included
cr .( >> testadvsnd) cr parse-name test/testadvs included
cr .( >> testgraphic) cr parse-name test/testgrap included
cr .( >> testadvgfx) cr parse-name test/testadvg included
cr .( >> testsystem) cr parse-name test/testsyst included
cr .( >> testbmx) cr parse-name test/testbmx included

\ include-mechanism smoke test (loads the file "1")
:noname s" include test/1 2" evaluate
2 <> abort" include failed"
1 <> abort" include failed" ; execute

---test---

decimal cr cr
.( ============================) cr
.( +++ ALL TESTS PASSED +++) cr
.( ============================) cr

\ In the emulator, exit right away so the harness gets its verdict; on
\ hardware that write is open bus and the word simply returns, so fall
\ through to the prompt - the banner stays in the scrollback and the
\ machine stays usable after a green run.
0 emu-exit
