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
> distributed, including an App Store submission. `words licence` satisfies this for the
> CLI — the texts are embedded in `Words.Core`, so they travel inside the binary — and the
> MAUI/web About screens must do the same. See
> [ADR 0004](./adr/0004-scowl-nediger-lexicon.md).
>
> **The project itself has no licence.** There is no `LICENSE` file, so the NuGet package
> declares no terms. That is fine while this is in-house; it needs deciding before anything
> is published, and it is a separate question from the word lists' terms above.

## Layout

```
src/Words.Core/               engine: model, lexicon loading, indexes, queries
src/Words.LexiconBuilding/   merges word lists in a directory into the artefact
src/Words.Cli/                `words` executable
tests/Words.Core.Tests/       xUnit, against a small hand-written lexicon
tests/Words.LexiconBuilding.Tests/  reader parsing and merge behaviour
tests/Words.Cli.Tests/        result ordering and limiting
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

### 4 — Anagram queries ✅

`AnagramLetters.Parse` reads user input into sorted letters plus a blank count, forgiving
case, accents, spaces, hyphens and apostrophes so a phrase can be pasted in as it stands.
`?` and `.` both mean a blank, for the same quoting reason patterns accept both.

`CanonicalForms()` yields what to look up: one form with no blanks, otherwise one per
combination *with repetition* of blank letters — 26, 351 and 3,276 for one, two and three.
Generating them in non-decreasing order means every form is distinct, so no answer can be
returned twice and no deduplication is needed. There is no scanning at all: an anagram
query is pure index lookup.

`QuerySyntaxException` is now shared by both query kinds — it was `PatternSyntaxException`
— so malformed letters and malformed patterns report identically and the CLI needs one
error path.

*Done.* 175 tests green. Against the committed lexicon, `listen` returns ten answers
including the phrase `lets in` and the possessive `inlet's`; `trisec.` returns 7-letter
answers only; `ab???` returns 896 answers in 0.29 s including process start, all five
letters — `blasé` among them, which confirms diacritic folding reaches the anagram index.
Errors point at the offending character:

```
words: At most 3 unknown letters are allowed, and this is number 4.
  cat????
        ^
```

### 5 — Composition ✅

`AnagramComposer` enumerates recursively over the remaining letter multiset. Each partition
is produced once, not once per ordering, by requiring that every component taken contains
the lowest letter still unused — without that rule `cat dog` and `dog cat` are both found,
and the duplication compounds with each component.

Components come only from single-word, non-proper-noun entries, so answers are not built
out of phrases. Defaults: two components, minimum length 3, at most one blank. Three
components and a minimum length of 2 are configurable; one-letter components never allowed.

**Memoisation turned out to be unnecessary.** The plan called for memoising on the remaining
multiset; measurement says the search is not where the time goes. A cache of eligible
entries per canonical form was worth having; nothing else was. Timings, all including
process start and the ~270 ms lexicon load:

| Query | Time |
| --- | ---: |
| `catdog --compose` | 0.32 s |
| `notaproblem --compose` | 0.29 s |
| `notaproblem --compose --components 3` | 0.30 s |
| `encyclopaedias --compose --components 3` (14 letters) | 0.41 s |
| `"encyclopaedias." --compose --components 3` (+1 blank) | 2.06 s |

Only the last is slow, and only because a blank multiplies the whole search by 26 — which
is exactly why composition allows one blank rather than three.

**Ctrl-C did not work and had to be wired up.** System.CommandLine only connects termination
signals to the cancellation token when `InvocationConfiguration.ProcessTerminationTimeout`
is set, and we were passing a configuration only on the help path. A broad composition
ignored the interrupt and ran to completion. Now an interrupt at 0.4 s exits at 0.42 s.

Interrupted queries exit **130**, not 1 — a search abandoned part-way has not established
that nothing matches, and a script must be able to tell those apart. Exit codes are now
named in `ExitCodes` rather than scattered as literals.

The CLI ranks by fewest components, then by the weakest component's score, keeps the best
200, and then sorts alphabetically for display, with a note to stderr saying how many were
suppressed.

*Done.* 195 tests green. `catdog --compose` returns `cat dog`, `act god` and eighteen more;
`notaproblem --compose` finds `amble pronto` and `aplomb tenor`;
`"encyclopaedias." --compose` finds `abs encyclopedia`.

### 6 — CLI ✅

Five commands: `pattern` (`pat`), `anagram` (`anag`), `add`, `lexicon` (`lex`) and
`licence` (`license`). Short aliases are functional and shown in the command list.

`--json`, `--limit`, `--sort`, `--source` and `--include-racy` live in a shared
`QueryOptions`, and both query commands hand off to a shared `QueryRunner`, so the two
cannot drift apart in how they filter or present answers.

`words licence` reproduces both bundled licences, which are **embedded in `Words.Core`**
rather than read from `data/` — a self-contained binary has to be able to show them, and
both sources require the notice to travel with anything distributed.

JSON is **source-generated** so it keeps working under NativeAOT in phase 8, and uses the
relaxed encoder: the default escapes anything HTML-unsafe, turning `inlet's` into
`inlet\u0027s` and `café` into `caf\u00e9`.

`Results.Arrange` is where the limit and the sort order interact, and is unit-tested in a
new `Words.Cli.Tests` project. When there are more answers than the limit allows, the
survivors are chosen by how likely they are — fewest words, then the weakest word — and
only then put into the requested display order. Truncating in display order would return
every answer beginning with A and nothing else.

Two notes on the option surface, cosmetic but worth recording:

- `--limit` is `int?` rather than carrying a sentinel default, because `[default: -1]` in
  the help reads as though it were a real limit.
- `--sort` declares no default value, because `Alpha` is the zero value and binding
  produces it anyway; declaring it printed `[default: Alpha]` against a lowercase list.

**`--sort length` is weaker than it sounds.** Every answer to a given query has the same
number of *letters* — a pattern fixes its length, and an anagram uses every letter — so it
only distinguishes composed answers, where the number of words varies. Implemented as
specified, but close to a no-op for single-word answers.

*Done.* 205 tests green. From a clean zsh shell, `words add zzzqqq` followed by
`words pattern ZZZQQQ` goes from exit 1 to exit 0 with the word returned, and an unquoted
`words pattern C?T` produces the quoting explanation.

### 7 — Test and benchmark layer ✅

**Property-based tests** (CsCheck — a new dependency, C#-native so it does not drag in
`FSharp.Core` the way FsCheck would). Six properties, each generating a small lexicon and
then deriving its query from an entry in it, so none can pass by finding nothing:

- every anagram answer is a permutation of the letters given
- a blank adds exactly one letter to the answer
- every pattern answer has exactly the pattern's length
- every pattern answer matches position by position
- every composition accounts for precisely the letters supplied
- composition only ever uses ordinary single words, all at or above the minimum length

Stable over 2,000 iterations as well as the default 100.

**Benchmarks.** Cold start, on the committed lexicon:

| | Mean | Allocated |
| --- | ---: | ---: |
| Load only | 101 ms | 137 MB |
| Load + length index (a pattern query) | 108 ms | 151 MB |
| Load + anagram index (an anagram query) | 240 ms | 257 MB |

Warm queries, both indexes already built:

| | Mean |
| --- | ---: |
| `anagram listen` (one lookup) | 211 ns |
| `anagram trisec.` (one blank, 26 lookups) | 1.2 µs |
| `pattern C.T` (3 letters) | 12 µs |
| `compose notaproblem` (2 words) | 63 µs |
| `anagram ab???` (three blanks, 3,276 lookups) | 146 µs |
| `pattern A.....R.E.T` (11 letters) | 289 µs |
| `compose encyclopaedias` (3 words) | 12.2 ms |

**A pattern's cost is the size of its length bucket, not how specific it looks.** The
11-letter pattern is 23× slower than the 3-letter one, because there are 50,440 eleven-letter
entries and only 3,416 three-letter ones. Buckets peak at nine letters (57,963), so the worst
pattern query scans about 58k entries — still a third of a millisecond, comfortably inside
the sub-10 ms target. Only the three-word composition exceeds it, and only just.

**Memory is the number to worry about.** A plain load allocates 137 MB and the anagram index
takes it to 257 MB. That is allocation rather than retained working set, but it is well above
the ~100 MB estimated earlier and it lands on the mobile concern already flagged. Worth
measuring on a real device before MAUI work starts, not after.

*Done.* 211 tests green; benchmark baselines recorded above.

### 8 — Packaging ✅

**NativeAOT.** `dotnet publish -r <rid>` produces a single 6.4 MB binary (the embedded
lexicon is 2.1 MB of that). On macOS it links only against system libraries — `libSystem`,
`CoreFoundation`, `libicucore` — with no .NET runtime dependency of any kind. It picks up
the OS's ICU, so diacritic folding still works: `words anagram "naïve"` finds `naive` and
`naïve` alike, which is the thing `InvariantGlobalization` would have broken.

**AOT publish caught a real defect.** `LexiconManifest.ToJson()` was still using the
reflection-based serialiser (IL2026/IL3050) while the CLI's output had been
source-generated since phase 6. It now uses a generated context, and the committed manifest
is byte-identical after the change.

**How much AOT actually buys, measured over 10 runs each:**

| | JIT | AOT |
| --- | ---: | ---: |
| `words licence` (no lexicon load) | 28 ms | **5 ms** |
| `words pattern C.T` | 146 ms | 103 ms |
| `words anagram listen` | 295 ms | 265 ms |

AOT removes a fixed ~25–45 ms of runtime and JIT start-up. That is most of the cost for
commands that do not touch the lexicon, and a third of a pattern query — but the lexicon
load dominates everything else, and AOT does nothing for it. **If queries need to get
faster, the load path is the lever, not AOT.**

**dotnet tool.** Packs as `Shunty.Words` with `ToolCommandName` of `words`, verified by
installing to a scratch tool path and running real queries. The package is 2.4 MB.

**Cross-compilation is not possible with AOT**, so `.github/workflows/release.yml` builds on
a matrix of `ubuntu-latest`, `macos-latest` and `windows-latest`. It attaches artefacts to
the workflow run rather than publishing them; making it a public release is a deliberate
step. `dotnet pack` was also added to CI so packaging cannot break unnoticed.

*Done, with two caveats.*

**The exit criterion is only half-satisfiable as written.** The AOT binary genuinely needs
nothing installed. A `dotnet tool` inherently requires the .NET runtime — that is what it is
for. The two artefacts serve different audiences rather than both clearing the same bar.

**The release workflow has never run.** It can only be validated on GitHub, and the AOT
publish is verified on `osx-arm64` only; `linux-x64` and `win-x64` are untried, and Linux in
particular needs the `clang`/`zlib1g-dev` step the workflow installs.

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
