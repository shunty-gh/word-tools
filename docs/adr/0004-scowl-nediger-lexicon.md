# ESDB (formerly SCOWL) as the spine, Nediger for crossword entries

Supersedes [ADR 0001](./0001-merged-ukacd-scowl-lexicon.md), which chose UKACD as the
crossword-shaped source. UKACD is dropped: last updated on 31 July 1999, no longer
maintained, and its distribution site was unreachable. The lexicon is now the **English
Speller Database** merged with the **Nediger list**, built offline and committed.

ESDB is the same lineage as SCOWL — same maintainer, briefly called SCOWLv2, now at
`en-wl/wordlist` — but renamed and restructured. Release **2026.02.25** is current and
adds over 1,500 high-frequency words from COCA plus 300 hand-picked modern terms, so it
is genuinely up to date rather than merely stable.

It is the spine because it is the only source considered here whose licence survives
scrutiny: derived from many sources under BSD-compatible terms, combined under an
**MIT-like licence**, with a `Copyright` file of per-source attribution that must be
reproduced.

Nediger supplies what a speller database structurally cannot: **phrases, proper nouns and
multi-word entries**. This is a correction to an earlier version of this ADR, which also
credited Nediger with supplying modern vocabulary — ESDB 2026.02.25 covers that itself.
Nediger's remaining justification is narrower but still sound, and its scoring scheme is
legible (99 easy or inferrable, 51 general, 49 potentially racy, 25 rare).

## Source selection

The list is generated through the customisation tool at `app.aspell.net/create` and the
output vendored, rather than filtering the master file ourselves. That file's format
changed in the 2026.02.25 release and is explicitly still stabilising; the tool already
performs the dialect, size and variant filtering we would otherwise reimplement against
it. Committing the output makes the tool's non-determinism irrelevant, provided the exact
option set is recorded in the manifest.

- **Both British dialects**, `-ise`/traditional *and* `-ize`/Oxford. Not cosmetic:
  `REALISE` and `REALIZE` are different letter multisets, so they yield different anagram
  results and match different patterns. Picking one produces confidently wrong answers.
- **Size 80** of the available 35–85. For a solver a false positive costs a glance and a
  false negative costs the puzzle, and `Score` lets obscure entries rank low rather than
  vanish. Not 85, which is where the genuine junk lives.
- **Spelling variants above the default level of 1**, because crosswords lean on archaic
  and uncommon spellings precisely because they make useful fill.
- **Diacritics retained**, and stripped by us into the search key — so `CAFÉ` keeps its
  display form while still matching `CAFE`.

## Considered options

- **Peter Broda's list** — around 427,000 scored entries, far larger. A fallback rather
  than the choice: an American construction list heavy with US abbreviations, brand names
  and partials, widely described as containing dubious and objectionable entries, and its
  terms could not be verified at source because the site's TLS certificate has expired.
- **The clue databases** — not word lists at all, see [ADR 0005](./0005-clue-databases-deferred.md).
- **ESDB alone** — viable, and the fallback if Nediger cannot be licensed. Loses phrases
  and proper nouns, which is most of the point.

## Consequences

**Nediger's licence is unresolved and must be settled before distribution.** Its
repository says the list is free to use, but the LICENSE file itself could not be read.
Acceptable while the app is in-house; a blocker the moment it ships, particularly for an
App Store submission where redistribution rights over bundled data are examined. ESDB's
terms are clear, so falling back to ESDB alone always yields a shippable app.

Scores from different sources measure different things — Nediger rates how readily a
solver would accept an entry as fill, ESDB's sizes reflect roughly how common a word is.
They normalise onto one 0–100 `Score`, with each entry retaining its source so the
decision can be revisited without rebuilding.

Nediger's 49 band marks potentially racy entries, excluded by default. The roadmap ends at
iOS and Android, making this a store-review matter rather than one of taste.

ESDB's restructuring is a live risk: the 2026.02.25 release warns that anyone generating
word lists from it directly will need to update their scripts, and a further release is
expected once the architecture settles. Going through the customisation tool insulates us
from that, but not permanently.
