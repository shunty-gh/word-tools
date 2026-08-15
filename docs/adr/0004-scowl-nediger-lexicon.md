# ESDB (formerly SCOWL) as the spine, Nediger for crossword entries

Supersedes [ADR 0001](./0001-merged-ukacd-scowl-lexicon.md), which chose UKACD as the
crossword-shaped source. UKACD is dropped: last updated on 31 July 1999, no longer
maintained, and its distribution site was unreachable. The lexicon is the **English
Speller Database** merged with the **Nediger list**, built offline and committed.

ESDB is the same lineage as SCOWL — same maintainer, briefly called SCOWLv2, now at
`en-wl/wordlist`. Release **2026.02.25** is current and adds over 1,500 high-frequency
words from COCA plus 300 hand-picked modern terms, so it is genuinely up to date.

Nediger supplies what a speller database structurally cannot: **phrases and proper nouns**.
This is a correction to an earlier version of this ADR, which also credited Nediger with
supplying modern vocabulary — ESDB covers that itself. The division proved sharper than
expected once both were measured: **213,592 of Nediger's 350,291 entries contain a space**,
against **27 of ESDB's 250,543**.

## Licensing — resolved

Both sources are permissive, and the earlier "unresolved, blocks distribution" caveat no
longer applies.

- **Nediger is MIT licensed**, Copyright © 2026 bewilderingly. Verified by reading the
  LICENSE file in the repository, not inferred from the project's prose.
- **ESDB** grants permission to "use, copy, modify, distribute, and sell any part of the
  ESDB, **or word lists created from it**", provided the copyright notice appears in all
  copies — wording that covers a derived artefact explicitly.

What remains is an **attribution obligation, not a restriction**: both notices must ship
with anything distributed, which is what `words licence` and the eventual About screens
exist for. Both licence files are vendored under `data/sources/`.

Nediger also turned out to be **actively maintained**, with weekly releases since its
first upload in June 2026, and **dialect-agnostic** — it carries `colour` and `color`,
`realise` and `realize` alike.

## Source selection

ESDB lists are generated through the customisation tool at `app.aspell.net/create` and the
output vendored, rather than filtering the master file ourselves. That file's format changed
in the 2026.02.25 release and is explicitly still stabilising; the tool performs the dialect,
size and variant filtering we would otherwise reimplement, and each generated file opens with
a header recording the exact parameters used, so the artefact is self-documenting.

- **Both British dialects**, `-ise`/traditional *and* `-ize`/Oxford. Not cosmetic: `REALISE`
  and `REALIZE` are different letter multisets, so they yield different anagram results.
- **Size 80** of the available 35–85. For a solver a false positive costs a glance and a
  false negative costs the puzzle, and `Score` lets obscure entries rank low rather than vanish.
- **Spelling variant level 8** (uncommon). Chosen empirically rather than cautiously: level 8
  yields only 1.8% more entries than level 2, so the feared flood of junk does not exist,
  while the archaic and uncommon spellings it admits are exactly what crosswords lean on.
- **Roman numerals included, hacker terms excluded.** Roman numerals appear as crossword
  answers; `grepped` does not.
- **Diacritics retained**, and stripped by us into the search key — so `café` keeps its
  display form while still matching `CAFE`.

### Recovering a frequency signal

The inline word list carries **no per-entry score**, which the plan had assumed it would. The
size bands are cumulative supersets, so generating all five and recording the smallest band an
entry appears in reconstructs the gradient: 45,290 entries at size 35 through 250,543 at
size 80. All five are vendored, which is why `data/sources/` holds redundant-looking files.

## Merging

Entries are deduplicated on **display form**, not on search key. An earlier version of this
ADR said search key, which was wrong: `Polish` and `polish` share a key and are genuinely
different answers, so collapsing them silently loses one. Distinct display forms that share a
search key are all kept, and the indexes map one key to many entries. Identical display forms
merge their provenance, take the most generous score, and stay racy if any source said so.

## Considered options

- **Peter Broda's list** — around 427,000 scored entries. A fallback rather than the choice: an
  American construction list heavy with US abbreviations and partials, widely described as
  containing dubious entries, and its terms could not be verified at source because the site's
  TLS certificate has expired.
- **The clue databases** — not word lists at all, see [ADR 0005](./0005-clue-databases-deferred.md).
- **ESDB alone** — viable, but loses the phrases and proper nouns that are most of the point.

## Consequences

The merged lexicon holds **500,451 entries** — 286,839 single words, 213,612 phrases, 121,152
proper nouns, 1,257 racy. That is marginally past the 500,000 ceiling assumed when the
in-memory design was chosen, and Nediger grows weekly, so the mobile memory question arrives
sooner than expected rather than eventually.

Scores from the two sources measure different things — Nediger rates how readily a solver
would accept an entry as fill, ESDB's bands reflect roughly how common a word is. They
normalise onto one 0–100 `Score`, with provenance retained so the decision can be revisited
without rebuilding. Nediger's author notes the 51-versus-99 distinction is "sporadic and
unsystematic" for long entries, so the gap between them is kept deliberately modest.

Nediger's 49 band marks potentially racy entries, excluded by default.

ESDB's restructuring remains a live risk: the 2026.02.25 release warns that anyone generating
word lists from it directly will need to update their scripts. Going through the customisation
tool insulates us for now, but not permanently.
