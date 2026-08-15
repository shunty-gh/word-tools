# A merged UKACD + SCOWL lexicon, built offline

> **Status: superseded by [ADR 0004](./0004-scowl-nediger-lexicon.md).** Better-maintained
> crossword word lists were found shortly after this was written, and UKACD was dropped.
> The reasoning below still explains why a *merged* lexicon is necessary at all, which
> ADR 0004 assumes rather than restates.

The solver needs a lexicon that is strong on the phrases, proper nouns and literary
vocabulary that cryptic crosswords draw on, but that also knows words coined after
1999 and carries some notion of how common a word is. No single free list does both,
so we merge two: UKACD supplies the crossword-shaped entries, SCOWL (British, bands up
to 70) supplies modern vocabulary and frequency bands. The merge runs offline in
`tools/`, and its output is committed to the repository.

## Considered options

- **UKACD alone.** Purpose-built for exactly this problem and unmatched on phrases and
  proper nouns, but frozen at version 1.6 on 31 July 1999 — no *podcast*, *broadband*,
  *smartphone* — and a flat list with no frequency data, leaving nothing to rank by.
- **SCOWL alone.** Maintained and frequency-banded, but thin on the phrases, proper
  nouns and quotations that make a cryptic solver useful.
- **Merged (chosen).** Deduplicated on search key. On a conflicting display form,
  UKACD's spelling wins, as it is the crossword-canonical one.

## Consequences

Two licences must be honoured rather than one. UKACD's terms specifically require its
copyright notice to be displayed prominently and its licence text reproduced verbatim,
which makes an about/licence surface a product requirement in every front end, not an
optional extra.

Because the merge is a committed artefact rather than a build step, the build needs no
network access and is deterministic, and any change to the lexicon shows up as a
reviewable diff. The cost is that regenerating it is a deliberate manual act, so the
tool must record its source versions in a manifest alongside the output.

Both source lists are pinned and vendored. UKACD is no longer maintained and its
distribution site was unreachable when this decision was taken, so depending on
fetching it at build time would be a liability.
