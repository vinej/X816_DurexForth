#!/usr/bin/env python3
"""Catch the trap that has cost five build cycles.

durexForth's `(` comment does NOT nest and it ends at the FIRST `)`. So a
comment whose prose contains a bracket - "(see kfs.c)", "not(a>b)", or a
stack effect quoted inside the prose - silently ENDS THERE, and the rest
of the sentence is executed as code. It surfaces as an undefined word
that appears nowhere in the source ("for?", "this?"), or as a stack
underflow a long way from its cause.

Rule enforced: between a comment's opening "( " and its first ")", there
must be no further "(". That is the shape every real instance had.

    python build/parencheck.py forth/*.fs test/*.fs

Exit 1 and a file:line for each hit. run-tests.sh runs it before it
builds a card, because finding this in a text file beats finding it in a
screenshot of a crashed machine.
"""
import re
import sys

def check(path):
    """Walk the source the way the interpreter does.

    State matters, and getting it wrong makes this tool lie. INSIDE a "("
    comment a "\\" is ORDINARY TEXT - only ")" ends the comment - so a
    scanner that strips "\\ ..." first will eat a closing bracket that
    happens to sit on such a line and then report the next definition's
    stack effect as a nested comment. That false positive is exactly what
    an unstated state machine buys you.
    """
    hits = []
    lines = open(path, encoding="utf-8", errors="replace").read().split("\n")

    in_comment = False
    open_line = 0
    body = []
    for line_no, line in enumerate(lines, 1):
        # TESTING (tester.fs) moves >IN past the end of the line, so
        # nothing after it is interpreted - including the trailing "("
        # testcoreplus.fs writes on purpose while testing how "(" parses.
        if not in_comment and line.strip().upper().startswith("TESTING"):
            continue

        i = 0
        while i < len(line):
            if in_comment:
                j = line.find(")", i)
                if j < 0:
                    body.append(line[i:])
                    break
                body.append(line[i:j])
                text = " ".join(body)
                if "(" in text:
                    hits.append((open_line, text.strip()[:70]))
                in_comment = False
                body = []
                i = j + 1
            else:
                rest = line[i:]
                # A \ comment runs to end of line; nothing after it counts.
                mb = re.search(r"(?:^|\s)\\(?:\s|$)", rest)
                mp = re.search(r"(?:^|\s)\(\s", rest)
                if mp and (not mb or mp.start() < mb.start()):
                    in_comment = True
                    open_line = line_no
                    body = []
                    i += mp.end()
                else:
                    break
    if in_comment:
        hits.append((open_line, "comment never closes"))
    return hits


# The loop words compile branches and have no interpreted meaning. Typed
# at the interpreter they build a structure into HERE that nothing ever
# runs - so the loop silently does not loop, and its operands stay on the
# stack to surface as a WRONG NUMBER somewhere later. Cost two cycles.
#
# IF/ELSE/THEN are deliberately NOT here: durexForth resolves those
# through the same immediate words, and flagging them would fire on the
# `[ ... ]` idiom that base.fs uses legitimately.
COMPILE_ONLY = {"do", "?do", "loop", "+loop", "begin", "while", "repeat",
                "until", "again", "leave"}


def strip_comments(lines):
    """Blank every comment, keeping the line count so numbers stay true.

    This has to be shared with the check above rather than approximated,
    because a word like `until` appears in ordinary PROSE constantly -
    scanning raw lines produced fifty false positives and buried the two
    real ones. A checker that cries wolf is worse than no checker.
    """
    out = []
    in_comment = False
    for line in lines:
        # base.fs DEFINES "(" and ".(" themselves. On those two lines the
        # bracket is a name being given, not a comment being opened, and
        # treating it as one swallows the ':' and makes everything after
        # look like loose code.
        if re.search(r"(?:^|\s):\s+\.?\(\s", line):
            out.append("")
            continue
        buf = []
        i = 0
        while i < len(line):
            if in_comment:
                j = line.find(")", i)
                if j < 0:
                    break
                in_comment = False
                i = j + 1
                continue
            rest = line[i:]
            mb = re.search(r"(?:^|\s)\\(?:\s|$)", rest)
            # ".(" prints its text; it is not code either, and its prose
            # is as full of words like "loop" as any other sentence.
            mp = re.search(r"(?:^|\s)\.?\(\s", rest)
            if mp and (not mb or mp.start() < mb.start()):
                buf.append(rest[:mp.start()])
                in_comment = True
                i += mp.end()
            elif mb:
                buf.append(rest[:mb.start()])
                break
            else:
                buf.append(rest)
                break
        out.append("".join(buf))
    return out


def check_compile_only(path):
    """Flag loop words used outside a colon definition.

    Only lines OUTSIDE ':' ... ';' are considered, and CODE ... END-CODE
    is skipped whole: that is the assembler's vocabulary, where the same
    spellings mean something else.
    """
    hits = []
    in_def = False
    in_code = False
    raw = open(path, encoding="utf-8", errors="replace").read().split("\n")
    # TESTING consumes the rest of its line, so it can never reach a word.
    raw = ["" if l.strip().upper().startswith("TESTING") else l for l in raw]
    stripped = strip_comments(raw)
    for line_no, line in enumerate(stripped, 1):
        # The ":" that opens the definition of "(" itself was blanked by
        # strip_comments, so note it here or the whole body of that word
        # reads as loose code - which is how base.fs's own REFILL loop
        # got reported three times.
        if re.search(r"(?:^|\s):\s+\.?\(\s", raw[line_no - 1]):
            in_def = True
            continue
        words = line.split()
        for w in words:
            lw = w.lower()
            if lw == "code" or lw == ":noname":
                in_code = in_code or lw == "code"
                if lw == ":noname":
                    in_def = True
                continue
            if lw == "end-code":
                in_code = False
                continue
            if in_code:
                continue
            if lw == ":":
                in_def = True
                continue
            if lw == ";":
                in_def = False
                continue
            if not in_def and lw in COMPILE_ONLY:
                hits.append((line_no, w, line.strip()[:60]))
    return hits


def main(argv):
    bad = 0
    for path in argv:
        for line_no, body in check(path):
            print("%s:%d: comment ends early at an inner ')': %s"
                  % (path, line_no, body))
            bad += 1
        for line_no, word, body in check_compile_only(path):
            print("%s:%d: '%s' is compile-only, used outside a definition: %s"
                  % (path, line_no, word, body))
            bad += 1
    if bad:
        print("%d problem(s). Prose with brackets wants \\ ; loops want a "
              "colon definition." % bad)
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
