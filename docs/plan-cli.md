# Implementation plan — CLI first

Target the command-line app first. Everything the CLI needs lives in `Words.Core`, a
plain `net10.0` library with no console, file-system or UI dependencies, so the later
MAUI and Blazor front ends consume it unchanged.

See [CONTEXT.md](../CONTEXT.md) for the vocabulary used throughout, and
[docs/adr](./adr) for the decisions that shape this.

> ## ⚠️ Licensing must be cleared before deployment
>
> The app is in-house only for now, and building against these word lists is fine on that
> basis. **Nediger's terms have not been verified** — its repository says the list is free
> to use, but the LICENSE file itself could not be read. ESDB's terms *are* clear
> (MIT-like, with a `Copyright` file of per-source attribution that must be reproduced).
>
> Resolve this **before any distribution**, and especially before an **Apple App Store**
> submission, where redistribution rights over bundled data are examined and a rejection
> costs a release cycle. Android and any public web deployment carry the same obligation.
> If Nediger's terms cannot be confirmed, ESDB alone still yields a working app — see
> [ADR 0004](./adr/0004-scowl-nediger-lexicon.md).

## Layout

```
src/Words.Core/               engine: model, lexicon loading, indexes, queries
src/Words.Lexicon.Building/   merges word lists in a directory into the artefact
src/Words.Cli/                `words` executable
tests/Words.Core.Tests/       xUnit, against a small hand-written lexicon
tests/Words.Core.Benchmarks/  BenchmarkDotNet, holds the performance targets
data/sources/                 pinned ESDB + Nediger lists, with their licence texts
data/lexicon.*                generated artefact + manifest, committed
```

`Directory.Build.props` sets nullable, implicit usings and warnings-as-errors;
`Directory.Packages.props` centralises package versions.

## Phases

### 0 — Skeleton

Solution, projects, props files, `.gitignore`, CI running build + test. Add a `CLAUDE.md`
at this point: the earlier attempt was declined because there was nothing in the repo to
describe, which is no longer true once this phase lands.

*Done when:* `dotnet build` and `dotnet test` both succeed on an empty test.

### 1 — Lexicon

Generate the ESDB lists through `app.aspell.net/create` with **both British dialects**
(`-ise`/traditional and `-ize`/Oxford), **size 80**, **spelling variants above the default
level 1**, and **diacritics retained**. Vendor those outputs alongside the Nediger list
under `data/sources/`, each with its licence text, and record the exact generator options
in the manifest — they are not reconstructable from the output.

`Words.Lexicon.Building` reads *every* list in a directory rather than two hard-coded
files. For each entry it derives the search key, kinds and racy flag; merges and
deduplicates on search key; normalises each source's scoring onto one 0–100 `Score`;
retains provenance; and emits a compressed artefact plus a manifest of source names,
versions, generator options and per-source entry counts.

Score normalisation is the judgement call. Nediger's four bands (99 / 51 / 49 / 25) and
ESDB's size tiers measure different things ([ADR 0004](./adr/0004-scowl-nediger-lexicon.md)),
so the mapping is a documented decision in the builder, not an incidental formula.

*Done when:* the artefact and manifest are committed, entry counts reported per source,
and spot checks pass — a phrase entry keeping its spaces in the display form, `PODCAST`
and `REALIZE` and `REALISE` all present, `NAÏVE` displayed with its diaeresis but keyed as
`NAIVE`, and racy-flagged entries excluded by default.

### 2 — Model and indexes

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

Two indexes built at load: entries bucketed by search-key length (patterns), and entries
keyed by canonical form — search key with letters sorted — for anagrams.

*Done when:* the artefact and a personal-words file load together as two sources, both
indexes are populated, and cold start is within budget.

### 3 — Pattern queries

Parse the restricted language (literals, `?`, `[abc]`, `[^def]`) into a compiled matcher.
Reject anything else, naming the offending character and its position. Select the length
bucket the pattern's length implies, then scan it. Phrase entries included by default.

*Done when:* `A??D??R?E?T` returns only 11-letter matches, and invalid patterns produce
positional errors.

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

Memory is the one open concern for mobile: ~500k entries as C# objects is roughly 100MB
with index overhead. The mitigation is a flat backing store with offsets rather than
per-entry objects, behind `ILexiconSource`. Measure on a real device before building it.

## Decisions

| Area | Decision |
|---|---|
| Stack | C#, .NET 10 LTS; engine targets plain `net10.0` |
| Lexicon | ESDB 2026.02.25 (formerly SCOWL) as spine + Nediger, merged offline, committed |
| Dialect | Both British variants, `-ise`/traditional and `-ize`/Oxford |
| Size / variants | Size 80 of 35–85; spelling variants above default level 1 |
| Diacritics | Retained in source and display form; stripped into the search key |
| Rejected sources | UKACD (frozen 1999), Broda (US, unverified — fallback), xd (no licence) |
| Entry model | Display form + search key; kinds and racy flag derived; source retained |
| Score | One 0–100 scale, normalised per source. Not frequency |
| Loading | Ordered collection of sources; personal words are a source |
| Storage | Compressed artefact + in-memory indexes. No SQLite for the lexicon |
| Personal words | Plain text, merged at load, added via `words add` |
| Pattern language | Literals, `?`, `[abc]`, `[^def]`. No `*`, no regex |
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

**Licence verification.** The headline risk, deferred rather than solved — see the warning
above. Blocks distribution, not development.

**ESDB is mid-restructure.** The 2026.02.25 release replaced separate text lists with a
master file plus SQLite and warns that existing scripts will break; a further release is
expected once the architecture settles. Generating through the customisation tool
insulates us for now, but not permanently — pin the vendored output and treat a
regeneration as a deliberate act.

**Acquiring the sources.** Broda's site has an expired certificate, and Nediger's entry
count and English variant were never established. Inspect what actually arrives before
phase 1 is considered done.

**Derived classifications.** Entry kinds are inferred from spaces and capitalisation, and
the racy flag from a single Nediger score band. Both will misfile some entries. Sample the
artefact once it exists rather than trusting the heuristics.

**Composition result volume.** Search cost is fine; the number of valid splits is the
problem. The cap and ranking in phase 5 are the mitigation, and phase 7's benchmarks should
include a deliberately pathological input.
