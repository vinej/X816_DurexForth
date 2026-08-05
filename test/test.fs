\ durexForth X816 stage-A test runner.
\ Included by an AUTORUN of "include test" (run-tests.sh writes one onto the
\ card) or by hand from the prompt. On the first failed assertion the Hayes
\ tester prints "INCORRECT RESULT" / "WRONG NUMBER" and QUITs, so reaching
\ the banner below means every test passed.
\ Stage A scope (16-bit cells, kernel console, kernel FS_*): the ANS core
\ suites, exceptions, doubles, and the VERA words. Omitted until their
\ words return: testbank/testromdisk/testvramdisk (no banking, no romdisk),
\ testfile/testloadsave (fs words beyond INCLUDED), testfloat and the
\ mod/*.fs suites (modules load via require from disk), testx16 (charset
\ and friends are parked), testinput (no joystick/mouse on the core),
\ testaudio (audio module), testsee (C64 screen scraping).

marker ---test---

page cr .( >> compat) cr parse-name compat included
cr .( >> tester) cr parse-name tester included
\ Upstream bracketed the core suites in their own marker because their
\ helper constants shadow the assembler's (MSB especially), breaking any
\ CODE word compiled afterwards. None of the remaining suites compiles a
\ CODE word, and the X816 dictionary has 24 KB the C64 never did, so the
\ definitions simply stay until ---test--- unwinds everything at the end.
cr .( >> testcore) cr parse-name testcore included
cr .( >> testcoreplus) cr parse-name coreplus included
cr .( >> testcoreext) cr parse-name coreext included
cr .( >> testexception) cr parse-name testexc included
cr .( >> testdouble) cr parse-name testdbl included
cr .( >> testvideo) cr parse-name testvid included
cr .( >> testsprite) cr parse-name testspr included
cr .( >> testtile) cr parse-name testtile included
cr .( >> testpalfx) cr parse-name testpal included
cr .( >> testcoreadd) cr parse-name coreadd included
cr .( >> testfar) cr parse-name testfar included
cr .( >> testbrk) cr parse-name testbrk included
cr .( >> testturbo) cr parse-name turbo included
cr .( >> testnmi) cr parse-name testnmi included
cr .( >> testfont) cr parse-name testfont included
cr .( >> testfile) cr parse-name testfile included
cr .( >> testdir) cr parse-name testdir included
cr .( >> testhelp) cr parse-name testhelp included
cr .( >> testload) cr parse-name testload included
cr .( >> teststruct) cr parse-name struct included
cr .( >> testaudio) cr parse-name testaud included
cr .( >> testfm) cr parse-name testfm included

\ include-mechanism smoke test (loads the file "1")
:noname s" include 1 2" evaluate
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
