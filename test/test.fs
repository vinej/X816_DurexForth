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
\ The ANS core tests define helper constants without a marker - including MSB,
\ which SHADOWS the assembler's stack-page constant and silently breaks every
\ CODE word compiled afterwards. Bracket them so all that leaks away before
\ the later suites run.
marker ---coretests---
cr .( >> testcore) cr parse-name testcore included
cr .( >> testcoreplus) cr parse-name coreplus included
cr .( >> testcoreext) cr parse-name coreext included
---coretests---
cr .( >> testexception) cr parse-name testexc included
cr .( >> testdouble) cr parse-name testdbl included
cr .( >> testvideo) cr parse-name testvid included
cr .( >> testsprite) cr parse-name testspr included
cr .( >> testtile) cr parse-name testtile included
cr .( >> testpalfx) cr parse-name testpal included
cr .( >> testcoreadd) cr parse-name coreadd included

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
\ hardware that write is open bus, so fall through to a halt loop that
\ keeps the banner on screen for the person at the monitor.
0 emu-exit
: ---halt--- begin again ;
---halt---
