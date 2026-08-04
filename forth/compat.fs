\ forth2012 compatibility stuff
\
\ EMPTY ON PURPOSE. Every word this file used to define now lives in
\ base.fs, so the machine has them at the PROMPT and not merely inside
\ the test suite: 2over 2swap d+ ?dnegate dabs and the double-cell
\ words moved first, and on 2026-08-04 the rest followed - true false
\ environment? cells cell+ char+ chars align aligned 0> >number sm/rem
\ >body defer defer! is, plus defer@ and action-of, which this file
\ never had at all.
\
\ Keeping copies here as well would not be harmless duplication, and
\ that is why this is a note rather than a deletion. Anything that
\ included this file got the LATER definition, so a base.fs fix was
\ invisible from the suite - which is exactly how lowercase hex went
\ missing: the pet# helper that used to live here mapped 'f' to 47,
\ so "ff" converted in HEX only when typed uppercase, and no test
\ could see that base.fs had
\ it right. Two definitions of one word means the tests are not
\ testing the machine.
\
\ The file stays because test/test.fs and BOTH card manifests include
\ it by name. Put nothing back: if a compatibility word is missing,
\ it belongs in base.fs with the rest.

\ ?negate ( n1 n2 -- n3 ) is the one word that was only ever a helper
\ here. base.fs's sm/rem inlines the same two branches, and nothing
\ else referenced it - but it is cheap, ANS-adjacent and someone's
\ old code may want it, so it survives as this file's whole content.
: ?negate 0< if negate then ;
