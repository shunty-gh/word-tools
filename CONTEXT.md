# Words

A solver for crossword and anagram puzzles. A shared engine answers letter-shaped
questions about a fixed body of English words; the CLI, and later the desktop, mobile
and web front ends, are presentation over that one engine.

## The lexicon

**Lexicon**:
The complete body of words and phrases the solver can return. Fixed at build time, not
user-editable.
_Avoid_: Dictionary (implies definitions, which we do not hold; also collides with the
`Dictionary<K,V>` type), word list, corpus

**Entry**:
One member of the lexicon — a single thing that could be a puzzle answer. May be a
single word, a hyphenated word, or a multi-word phrase.
_Avoid_: Word (a word is only one kind of entry), term, item

**Display form**:
The entry as it is shown to a person, retaining spaces, hyphens, apostrophes,
accents and capitalisation. `Red Herring`, `well-known`, `naïve`.
_Avoid_: Original, raw, text

**Search key**:
The entry reduced to the letters a solver actually types — uppercase A–Z only, with
spaces, hyphens, apostrophes and accents removed and diacritics folded. `Red Herring`
→ `REDHERRING`. All matching happens against the search key; the display form is only
ever shown, never searched.
_Avoid_: Normalised form, canonical form, slug

**Score**:
How readily a solver would accept an entry as an answer, from 0 to 100. Normalised
from whichever source the entry came from. Deliberately *not* word frequency — a
crossword list rates entries by how good they are as fill, which is a different
judgement from how often a word is written.
_Avoid_: Frequency, rank, weight, quality

**Source**:
The word list an entry came from, retained on every entry so results can be filtered
by provenance — British-only, say — without rebuilding the lexicon.
_Avoid_: Origin, provider, dataset

## Queries

**Query**:
A question put to the engine. Exactly two kinds exist: a pattern query and an anagram
query.

**Pattern**:
A description of an answer by *position* — which letters sit in which cells. Its length
fixes the answer's length exactly.
_Avoid_: Regex, expression, mask (the language is deliberately not a regular
expression; see the note on `?` below)

**Cell**:
One position in a pattern, written `?` or `.` when its letter is unknown. Matches exactly
one letter — never zero, never more than one. The two spellings are identical in meaning;
`.` exists because it is not a shell wildcard and so needs no quoting.
_Avoid_: Wildcard, any-char

**Blank**:
An unknown letter in an anagram query, also written `?` or `.`, standing for one letter
whose identity *and* position are both unknown. Every blank is consumed by the answer, so
an answer's length is always the number of letters supplied plus the number of blanks.
_Avoid_: Wildcard, joker, unknown

> `?` means something different in each query kind — a *cell* in a pattern (unknown
> letter, known position), a *blank* in an anagram (unknown letter, no position). The
> character is shared because that is what solvers already type; the concepts are not.
> Note also that `?` here is **not** its regular-expression meaning of "zero or one".

## Answers

**Match**:
One result returned for a query — either a single entry, or a composition.
_Avoid_: Result, hit, answer (an answer is the puzzle's, a match is ours)

**Phrase entry**:
An entry whose display form is more than one word, such as `RED HERRING`. Matched
exactly like any other entry, through its search key, because a puzzle grid has no
spaces to distinguish it. Eligible for every query by default.
_Avoid_: Multi-word (ambiguous — see composition), compound

**Composition**:
A match assembled from two or more separate entries whose letters together account for
exactly the letters supplied. Only anagram queries compose, and only on request; a
pattern never composes, because the lexicon already holds the phrases a grid would ask
for.
_Avoid_: Multi-word anagram, split, decomposition

**Component**:
One entry used inside a composition.
_Avoid_: Part, fragment, piece, word

**Entry kind**:
A classification of an entry as a single word, a phrase, or a proper noun, used to
include or exclude entries per query. Derived from the entry's own text rather than
supplied by the lexicon, so it is a good guess and not an authority.
_Avoid_: Type, category, class, tag

## Terms

**Licence**:
The terms something shipped with the app is used under — either the program's own
(Apache 2.0) or a bundled word list's. Not lexicon-specific: `Licences.Program` and
`Licences.WordLists` are the same shape and are displayed the same way. British spelling
throughout, including the `words licence` command; the root file is `LICENSE` only
because that is the spelling GitHub, NuGet and SPDX tooling look for.
_Avoid_: License (in code and prose), terms, legal, EULA

**Notice**:
The attribution that must travel with anything distributed. Each word list requires one,
and Apache 2.0 requires this project's to be passed on by redistributors, which is what
the root `NOTICE` file is for. Reproducing them is an obligation, not a courtesy — see
[ADR 0008](docs/adr/0008-apache-2-licence.md).
_Avoid_: Credit, acknowledgement, attribution blurb
## Looking an answer up

**Lookup**:
Leaving the app to ask the web about an answer you already have. Exactly two kinds
exist: a **definition** — what the answer means — and **synonyms** — what else means
the same. The lexicon holds neither and is not going to, so a lookup answers the question
by handing an address to the browser instead
([ADR 0007](docs/adr/0007-answers-link-out-to-a-search-engine.md)).
_Avoid_: Search (a search is what the solver asks *this* app), define, thesaurus

**Web search engine**:
The search service a lookup is addressed to — Google, Bing, DuckDuckGo and the rest —
chosen once by the user and remembered. Named `WebSearchEngine` in full, never
`SearchEngine`, because `WordEngine` is already the thing that answers queries and the
two must not be read as relatives.
_Avoid_: Search engine (unqualified), provider, browser (the browser is what opens the
address, not what answers it)
