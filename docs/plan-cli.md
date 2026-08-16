# Implementation plan — CLI first

Target the command-line app first. Everything the CLI needs lives in `Words.Core`, a
plain `net10.0` library with no console, file-system or UI dependencies, so the later
MAUI and Blazor front ends consume it unchanged.

See [CONTEXT.md](../CONTEXT.md) for the vocabulary used throughout, and
[docs/adr](./adr) for the decisions that shape this.

> ## Licensing — attribution required, no longer a blocker
>
> Both sources were verified during phase 1 and both are permissive. **Nediger is MIT**
> (Copyright © 2026 bewilderingly, read from its LICENSE file). **ESDB** explicitly permits
> distributing "word lists created from it" provided its copyright notice travels with them.
>
> What remains is an **attribution obligation**: both notices must ship with anything
> distributed, including an App Store submission. That is what `words licence` (phase 6) and
> the MAUI/web About screens exist for — treat them as release requirements, not niceties.
> Licence texts are vendored at `data/sources/nediger-LICENSE.txt` and
> `data/sources/esdb-COPYRIGHT.txt`. See [ADR 0004](./adr/0004-scowl-nediger-lexicon.md).

## Layout

```
src/Words.Core/               engine: model, lexicon loading, indexes, queries
src/Words.LexiconBuilding/   merges word lists in a directory into the artefact
src/Words.Cli/                `words` executable
tests/Words.Core.Tests/       xUnit, against a small hand-written lexicon
tests/Words.LexiconBuilding.Tests/  reader parsing and merge behaviour
tests/Words.Core.Benchmarks/  BenchmarkDotNet, holds the performance targets
data/sources/                 pinned ESDB + Nediger lists, with their licence texts
data/lexicon.gz               generated artefact, with its manifest alongside
```

`Directory.Build.props` sets nullable, implicit usings and warnings-as-errors;
`Directory.Packages.props` centralises package versions.

## Phases

### 0 — Skeleton ✅

Solution, projects, props files, `.gitignore`, CI running build + test, and `CLAUDE.md`.

*Done:* `dotnet build` and `dotnet test` both succeed; CI green on first run.

### 1 — Lexicon ✅

ESDB lists generated through `app.aspell.net/create` — both British dialects, size 80,
variant level 8, diacritics retained, roman numerals in, hacker terms out — and vendored
alongside the Nediger list under `data/sources/` with both licence texts. Each generated
file carries a header recording its own parameters, so the options are self-documenting
rather than needing to be restated.

All five ESDB size bands are vendored, not just size 80: the inline lists carry no
per-entry score, and since the bands are cumulative supersets, the smallest band an entry
appears in reconstructs the missing frequency signal.

`Words.LexiconBuilding` reads *every* list in a directory, identifying each file by
content rather than name, so licence and readme files are skipped without configuration.
Entries deduplicate on **display form** — not search key, which would collapse `Polish`
into `polish` — merging provenance and taking the most generous score.

*Done.* 500,451 entries: 286,839 single words, 213,612 phrases, 121,152 proper nouns,
1,257 racy, 5 discarded for having no letters. Artefact 2.1 MB compressed. Spot checks
pass: `red herring` keeps its space, `podcast`/`realise`/`realize` all present, `naïve`
displays its diaeresis while keying as `NAIVE`, `Polish` and `polish` both survive.
62 tests green.

### 2 — Model and indexes ✅

`Entry`, `EntryKinds`, `Sources`, `SearchKeys`, `LexiconArtefact`, `Lexicon`,
`ILexiconSource`, `IPersonalWordStore` and the CLI composition root are all in place. The
artefact is embedded in `Words.Core`, so every front end gets a working lexicon with no
deployment step.

The `IWordEngine` sketch below is unchanged and still belongs to phases 3–5; `Match`
does not exist yet.

```csharp
public sealed record Entry(
    string DisplayForm,
    string SearchKey,
    EntryKinds Kinds,
    int Score,
    Sources Sources,
    bool IsRacy);

[Flags]
public enum EntryKinds { None = 0, SingleWord = 1, Phrase = 2, ProperNoun = 4, All = SingleWord | Phrase | ProperNoun }

[Flags]
public enum Sources { None = 0, Esdb = 1, Nediger = 2, Personal = 4 }

public sealed record Match(IReadOnlyList<Entry> Components)
{
    public bool IsComposition => Components.Count > 1;
}

public interface ILexiconSource
{
    string Name { get; }
    ValueTask<IReadOnlyList<Entry>> LoadAsync(CancellationToken ct = default);
}

public interface IPersonalWordStore
{
    ValueTask<IReadOnlyList<string>> ReadAsync(CancellationToken ct = default);
    ValueTask AddAsync(string entry, CancellationToken ct = default);
}

public interface IWordEngine
{
    IAsyncEnumerable<Match> QueryAsync(PatternQuery query, CancellationToken ct = default);
    IAsyncEnumerable<Match> QueryAsync(AnagramQuery query, CancellationToken ct = default);
}
```

The lexicon is built from an **ordered collection** of sources, merged in order, later
sources overriding earlier display forms — see
[ADR 0006](./adr/0006-lexicon-loads-from-ordered-sources.md). Today that collection is the
built-in artefact followed by personal words. `Words.Core` never constructs a source or
touches the file system; the CLI's composition root does.

Two indexes, both **lazy**: entries bucketed by search-key length (patterns), and entries
keyed by canonical form — search key with letters sorted — for anagrams. Laziness was not
in the original design and was forced by measurement, below.

*Done.* Both sources load and merge, both indexes populate, 81 tests green.

**Cold start needed work to meet its budget.** The first honest measurement was **615 ms**,
against a 300 ms target. Four changes brought it down:

| Change | Why it mattered |
| --- | --- |
| ASCII fast path in `SearchKeys.From` | Unicode normalisation ran on all 500k display forms and allocates even when nothing decomposes; nearly all entries are pure ASCII |
| Buffer then parse synchronously | 500k `ReadLineAsync` calls cost more in state machines than the parsing itself |
| Skip the merge dictionary and the sort | With one contributing source they were 500k inserts and a half-million-element sort for no benefit |
| Build both indexes lazily | A pattern query never needs the anagram index, which is the expensive one |

Measured on the committed lexicon: **load 118 ms**, plus **7 ms** for the length index or
**149 ms** for the anagram index. So a pattern query pays ~125 ms and an anagram query
~267 ms, both inside budget. Process wall clock adds JIT warm-up on top, which is what
NativeAOT in phase 8 is for.

### 3 — Pattern queries ✅

`PatternMatcher` compiles the restricted language (literals, `?`, `[abc]`, `[^def]`) and
rejects anything else, naming the offending character and pointing at it. Every element
compiles to a 26-bit letter mask — a literal sets one bit, `?` sets all of them, a class
sets its own — so matching is one bit test per position with no branching on element type.

`WordEngine.QueryAsync` selects the single length bucket the pattern implies and scans it,
streaming `Match` values and yielding every 8,192 candidates so a WebAssembly host stays
responsive. Patterns are compiled *before* the iterator is returned, so a typo surfaces at
the call rather than at some later `await foreach`.

`EntryFilter` — kinds, sources, racy — is shared with the anagram queries to come, so
"exclude racy entries" means the same thing whichever question is asked.

A minimal `words pattern` was brought forward from phase 6 (bare lines, grep exit codes,
unquoted-glob detection) because the phase could not otherwise be verified against the real
lexicon. `--json`, `--limit`, `--sort`, `--source` and `--include-racy` remain phase 6.

*Done.* 126 tests green. Against the committed lexicon, `A?????R?E?T` returns
`autocorrect` and every result is exactly 11 letters; `RED?ERRING` returns `red herring`,
matching straight through the space; `AB*D` reports `'*' is not allowed` with a caret under
position 3. A query costs 140–160 ms end to end, process start included.

Worth recording: the plan's own example `A??D??R?E?T` has **no** matches in this lexicon —
the `D` in the fourth position rules everything out. It was always an illustration rather
than a real answer, and a future reader should not take an empty result for a defect.

### 4 — Anagram queries

Canonical-form lookup for the zero-blank case. For blanks, enumerate letter multisets of
size *k* (26, 351 and 3,276 for one, two and three blanks) and look each up. More than
three blanks is an error.

*Done when:* exact anagrams resolve, and blank counts up to three stay within the query
budget.

### 5 — Composition

Recursive enumeration over the remaining letter multiset, memoised on that multiset.
Components drawn only from single-word, non-proper-noun entries. Defaults: two components,
minimum component length 3, at most one blank. Three components and a minimum length of 2
are configurable; one-letter components never allowed.

Ranked by fewest components, then by the score of the weakest component, to decide which
survive the cap — then displayed alphabetically, so the cap selects meaningfully rather
than returning everything beginning with A.

*Done when:* a known split resolves, and a deliberately broad query cancels promptly
mid-enumeration.

### 6 — CLI

`words pattern` and `words anagram` on System.CommandLine 2.0, plus `words lexicon build
<dir>` for rebuilding from a directory of lists, `words add <entry>` for personal
additions, and `words licence` reproducing every bundled source's terms.

Options: `--json`, `--limit` (unlimited by default; 200 under `--compose`),
`--sort alpha|score|length` defaulting to alphabetical, `--source` to filter by provenance
including `personal`, and `--include-racy`.

Exit codes follow grep: `0` matches found, `1` none, `2` bad input. Truncation notices to
stderr so stdout stays pipeable.

Handle shell globbing explicitly: `?` and `[abc]` are glob characters, and zsh aborts the
command outright on an unmatched glob before the app starts. Accept the pattern as a bare
argument, via `--pattern`, and via stdin; when several bare arguments arrive looking like
filenames, say specifically that the pattern needs quoting.

*Done when:* the documented invocations work from a clean zsh shell, including the
unquoted-pattern case producing a useful error, and a word added via `words add` appears
in the next query.

### 7 — Test and benchmark layer

Property-based tests (CsCheck or FsCheck) for the three invariants: every anagram match is
a permutation of the supplied letters plus blanks; every pattern match has exactly the
pattern's length; every composition's components account for precisely the letters
supplied. BenchmarkDotNet covers cold start and warm query time.

*Done when:* properties pass over generated input and benchmarks record a baseline.

### 8 — Packaging

Publish as a `dotnet tool`, and as NativeAOT self-contained single-file binaries per
platform. AOT removes JIT warm-up from a process that runs once per query.

*Done when:* both artefacts run on a machine with no .NET SDK installed.

## After the CLI

MAUI and Blazor consume `Words.Core` unchanged. Both need an about screen carrying every
bundled source's licence text, and both are distribution events — see the warning above.
Blazor must enumerate results in chunks and yield between them,
[ADR 0002](./adr/0002-streaming-query-results.md).

Two deliberate future openings, neither built now:

- **A sync API.** Once personal words exist on more than one machine, a small internet API
  becomes another `ILexiconSource` and nothing else changes,
  [ADR 0006](./adr/0006-lexicon-loads-from-ordered-sources.md).
- **Clue search** over `cryptics.georgeho.org`, which is genuinely suited to SQLite and
  FTS5 — unlike the lexicon, which is not,
  [ADR 0005](./adr/0005-clue-databases-deferred.md).

Memory is the one open concern for mobile, and phase 1 made it more pressing rather than
less: the lexicon came in at 500,451 entries, marginally past the ceiling this design was
sized against, and Nediger ships weekly updates so it will keep growing. As C# objects that
is roughly 100MB with index overhead. The mitigation is a flat backing store with offsets
rather than per-entry objects, behind `ILexiconSource`. Measure on a real device before
building it.

## Decisions

| Area | Decision |
|---|---|
| Stack | C#, .NET 10 LTS; engine targets plain `net10.0` |
| Lexicon | ESDB 2026.02.25 (formerly SCOWL) as spine + Nediger, merged offline, committed |
| Dialect | Both British variants, `-ise`/traditional and `-ize`/Oxford |
| Size / variants | Size 80 of 35–85; variant level 8; roman numerals in, hacker terms out |
| ESDB bands | All five sizes vendored; smallest band an entry appears in gives its score |
| Diacritics | Retained in source and display form; stripped into the search key |
| Rejected sources | UKACD (frozen 1999), Broda (US, unverified — fallback), xd (no licence) |
| Licences | Nediger MIT; ESDB permits derived word lists. Attribution required |
| Entry model | Display form + search key; kinds and racy flag derived; source retained |
| Deduplication | On display form, not search key — `Polish` and `polish` both survive |
| Score | One 0–100 scale, normalised per source. Not frequency |
| Loading | Ordered collection of sources; personal words are a source |
| Storage | Compressed artefact + in-memory indexes. No SQLite for the lexicon |
| Personal words | Plain text, merged at load, added via `words add` |
| Pattern language | Literals, `?` or `.`, `[abc]`, `[^def]`. No `*`, no regex |
| Pattern length | Fixes answer length exactly |
| Phrases | Included by default in every query |
| Anagrams | Exact: answer length = letters + blanks. Up to 3 blanks |
| Composition | Opt-in `--compose`; 2 components default, 3 max, 1 blank max |
| Ordering | Alphabetical by default; `--sort score` available |
| Racy entries | Excluded by default, `--include-racy` to admit |
| Engine API | `IAsyncEnumerable<Match>` + `CancellationToken` |
| Scale | ≤ 500k entries, in memory, no database |
| Performance | Sub-10ms warm query, sub-300ms cold start |
| CLI | Single `words` binary, grep exit codes, stderr for notices |

## Risks

**~~Licence verification~~ — resolved in phase 1.** Both sources are permissive; what
remains is an attribution obligation, see the note at the top.

**Lexicon size exceeded its assumption.** 500,451 entries against a design sized for
"≤ 500k", and Nediger grows weekly. Not a problem on desktop; it moves the mobile memory
question from "later" to "before MAUI ships".

**ESDB is mid-restructure.** The 2026.02.25 release replaced separate text lists with a
master file plus SQLite and warns that existing scripts will break; a further release is
expected once the architecture settles. Generating through the customisation tool
insulates us for now, but not permanently — pin the vendored output and treat a
regeneration as a deliberate act.

**Derived classifications.** Entry kinds are inferred from spaces and capitalisation, and
the racy flag from a single Nediger score band, so some entries will be misfiled — 121,152
entries are currently classed as proper nouns on the strength of a leading capital alone.
Worth sampling before the results are trusted in anger.

**Nediger is young and hand-typed.** First uploaded June 2026, and its author explicitly
warns of remaining typos. Weekly updates mean regenerating is cheap; it also means the
artefact goes stale quietly.

**Composition result volume.** Search cost is fine; the number of valid splits is the
problem. The cap and ranking in phase 5 are the mitigation, and phase 7's benchmarks should
include a deliberately pathological input.
