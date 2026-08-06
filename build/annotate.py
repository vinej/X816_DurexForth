#!/usr/bin/env python3
"""Annotate help/helpdoc/*.txt: prefix every word-definition line with
[x] (word exists in durexForth) or [ ] (not implemented yet). Report
durexForth words that appear in no help file.

The third state, [-], is NOT derived and never rewritten: it means the word
belonged to the X16 or the C64 and is not coming - a device number, a CBM
DOS channel, a ROM call, a PRG header. This script leaves those lines
exactly as it found them, so the judgement stays with whoever made it."""
import re, glob, os, subprocess, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
os.chdir(ROOT)

# --- durexForth word set: ground truth captured from a running build
# (base + compat/io/dos/rnd/timer/see) via `dowords`, see build/have.txt ---
have = set(w.strip().lower() for w in open('build/have.txt', encoding='latin1')
           if w.strip())

# private / internal / runtime words that are not public API and the inline
# assembler mnemonics (documented collectively in ASSEMBLER.TXT, not per-word).
HIDDEN = set("""1mi 23mi 2mi 3mi >l addr dodoes hash last-dump latestxt locp
locs lsp lstk newstart oldstart oldtop refp refs resolve-leaves restore-forth
+branch -branch .begin .then :+ :- @: @@ ,branch branch! branchptr lit litc
lits pet# type! print accumulate scan scan-0branch scan-jmp scan-jsr scan-loop
print-0branch print-jmp print-jsr print-lits print-of print-to-branch
print-unloop print-xt skip-lits remove-then reached-end while? my-xt xt>nt
defcode define #again #else #if #leave #repeat #until #while seed
(+loop) (?do) (do) (loop) (of) (to) (includes) (jmp), 0branch branch header
pushya ?dnegate ?negate ---modules--- ---see--- ---turnkey--- more""".split())

def internal(w):
    if w in HIDDEN: return True
    # inline-assembler mnemonics: any comma-word except the real , and c,
    if ',' in w and w not in (',', 'c,'): return True
    return False

def leading_word(line):
    # A word-definition line is "NAME ( stack ) ..." or "NAME / NAME2 ...".
    # It may be indented; continuation/prose lines won't have '(' or '/' as
    # their second token. Section headers start with '==='.
    if not line.strip() or line.lstrip().startswith('==='):
        return None
    toks = line.split()
    if len(toks) >= 2 and toks[1] in ('(', '/'):
        return toks[0]
    return None

matched = set()
for path in sorted(glob.glob('help/helpdoc/*.txt') + glob.glob('help/helpdoc/*.TXT')):
    if os.path.basename(path).upper() == 'INDEX.TXT':
        continue
    out = []
    for line in open(path, encoding='latin1').read().split('\n'):
        # [-] is a decision, not a measurement: pass it through untouched
        if re.match(r'^\s*\[-\] ', line):
            out.append(line)
            w = leading_word(re.sub(r'^(\s*)\[-\] ', r'\1', line))
            if w: matched.add(w.lower())
            continue
        # strip any prior annotation so the script is idempotent
        stripped = re.sub(r'^\[[ x]\] ', '', line)
        w = leading_word(stripped)
        if w is not None:
            exists = w.lower() in have
            if exists: matched.add(w.lower())
            out.append(('[x] ' if exists else '[ ] ') + stripped)
        else:
            out.append(stripped)
    open(path, 'w', encoding='latin1', newline='\n').write('\n'.join(out))

# durexForth public words not documented anywhere
undoc = sorted(w for w in have if w not in matched and not internal(w))
print("# durexForth words NOT found in any help file (%d):" % len(undoc))
for w in undoc:
    print(w)
