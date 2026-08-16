# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A crossword and anagram solver. A shared engine answers letter-shaped questions about a
fixed body of English words; the CLI is the first front end, with MAUI (macOS, Windows,
Android, iOS) and a web app planned against the same engine.

Work is phased and the plan is [docs/plan-cli.md](docs/plan-cli.md). **Phases 0–7 are
complete** — the CLI is feature-complete (`pattern`, `anagram`, `add`, `lexicon`,
`licence`), with property-based tests and benchmark baselines. The merged lexicon (500,451
entries) is committed at `data/lexicon.gz` and embedded into `Words.Core`. **Phase 8 is not
started** — no packaging as a `dotnet tool` or NativeAOT binary.

## Commands

```bash
dotnet build
dotnet test
dotnet test tests/Words.Core.Tests                              # one project
dotnet test --filter "FullyQualifiedName~SearchKeysTests"       # one class
dotnet test --filter "DisplayName~folds diacritics"             # one test

# Rebuild the lexicon after changing anything in data/sources/. The artefact and its
# manifest are committed, so review the diff before committing a rebuild.
dotnet run --project src/Words.Cli -- lexicon build data/sources -o data/lexicon.gz

# What the lexicon holds, and what loading it costs. Use this after touching the load
# path — it reports load time and each index separately.
dotnet run -c Release --project src/Words.Cli -- lexicon info

dotnet run --project src/Words.Cli -- pattern "A?????R?E?T"     # note the quotes
# Benchmarks. Baselines are recorded in docs/plan-cli.md — compare against them.
dotnet run -c Release --project tests/Words.Core.Benchmarks -- --filter "*QueryBenchmarks*"
dotnet run -c Release --project tests/Words.Core.Benchmarks -- --filter "*LoadBenchmarks*"

# Property tests run 100 cases by default; raise it when changing the engine.
CsCheck_Iter=2000 dotnet test tests/Words.Core.Tests --filter "EngineProperties"
```

The SDK is pinned in `global.json`; the target framework, nullable and
warnings-as-errors live in `Directory.Build.props`; package versions are centralised in
`Directory.Packages.props`, so `PackageReference` entries carry no `Version` attribute.

## Architecture

`Words.Core` is the engine and **must not acquire console, file-system or UI
dependencies** — that constraint is the whole reason MAUI and Blazor can reuse it
unchanged. The CLI's composition root decides where data comes from; the engine never
constructs a source or opens a file.

The lexicon is built from an **ordered collection** of `ILexiconSource`, merged in order,
not from a single artefact. Today that is the built-in word list followed by the user's
personal additions. This looks over-engineered for two entries and is deliberate — see
[ADR 0006](docs/adr/0006-lexicon-loads-from-ordered-sources.md) before simplifying it.

Every entry carries a **display form** (what a person sees: `Red Herring`, `naïve`) and a
**search key** (uppercase A–Z only, spaces, hyphens and accents removed: `REDHERRING`,
`NAIVE`). All matching happens against the search key. The display form is never searched.

Two query kinds, both returning `IAsyncEnumerable<Match>` with a `CancellationToken`:
pattern queries match by position, anagram queries by letter multiset. Streaming is not
incidental — it is what lets a Blazor front end yield to the browser event loop mid-search
([ADR 0002](docs/adr/0002-streaming-query-results.md)).

## Things that will catch you out

**`?` is not the regex `?`.** In a pattern it means *exactly one letter* — regex `.`, not
"zero or one". The pattern language is literals, `?` or `.`, and `[abc]` / `[^def]`,
deliberately not a regular expression
([ADR 0003](docs/adr/0003-pattern-language-not-regex.md)). Never describe it as regex-like
in help text or errors, even though `.` happens to agree with its regex meaning.

**`.` and `?` are the same thing.** `.` exists solely because it is not a shell wildcard,
so `words pattern A..D` works unquoted where `A??D` does not. Don't remove it as redundant.

**`?` means something different in each query kind.** A *cell* in a pattern (unknown
letter, known position); a *blank* in an anagram (unknown letter, no position). See
[CONTEXT.md](CONTEXT.md).

**`?` and `[abc]` are shell glob characters.** zsh aborts an unmatched glob before the app
starts (`zsh: no matches found`), while bash passes it through — so unquoted patterns fail
on macOS and work on Linux. Quote patterns containing `?` or `[abc]` in examples and docs;
`.` needs no quotes. Note that patterns never contain spaces (a space is a syntax error),
so quoting is only ever about globbing.

`words pattern` has a **hidden `expanded` argument** with `ZeroOrMore` arity that exists
solely to absorb the filenames a shell substitutes for an unquoted pattern, so the command
can explain what happened instead of emitting "unrecognized argument". It also keeps the
usage line honest about accepting exactly one pattern. Don't delete it as unused.

**A command's `Description` must stay one line.** System.CommandLine uses it both in the
command's own help and in the parent's command list, so a multi-paragraph description makes
`words --help` unreadable. Examples and notes go in the command's `ExtendedHelp` constant,
which `Program.cs` appends below the options of that command's own help.

**Help output is rewritten on its way out**, in `HelpText` and `Program.cs`. The argument
is *named* `"pattern"` (quotes included) because that is the only way to get quotes into
System.CommandLine's usage line, which always wraps a name in `<>`; the rewrite then moves
them outside, so `<"pattern">` reads as `"<pattern>"` — literal quotes around a placeholder,
rather than quotes that look like part of the name. Only help and parse-error output is
buffered for this; query results stream untouched.

**Exit codes are grep's, named in `ExitCodes`: 0 found, 1 nothing found, 2 bad request,
130 interrupted.** `Program.cs` overrides System.CommandLine's parse-failure code, which is
1 by default — that would tell a script "no matches" when the command was actually
malformed. An interrupted query is 130 for the same reason.

**`InvocationConfiguration.ProcessTerminationTimeout` is what makes Ctrl-C work.** Without
it System.CommandLine never connects the signal to the cancellation token, and a long
composition runs to completion after the user has given up. It is set in `Program.cs` and
must be passed on every `InvokeAsync` path, not just the help one.

**Composition produces each partition once** because every component taken must contain the
lowest letter still unused (`AnagramComposer`). Removing that rule silently doubles or
triples the results with reorderings of answers already found.

**Limiting happens before sorting, not after** (`Results.Arrange`). Survivors are chosen by
likelihood — fewest words, then the weakest word — and only then put into the requested
display order. Applying `--limit` after an alphabetical sort would return every answer
beginning with A and nothing else.

**JSON output is source-generated** (`JsonResultsContext`) so it survives NativeAOT in
phase 8, and uses `UnsafeRelaxedJsonEscaping` — the default encoder escapes apostrophes and
accents, which mangles answers like `inlet's` and `café`.

**A pattern's cost is its length bucket, not how specific it looks.** Buckets peak at nine
letters (~58k entries) and fall away at both ends, so an 11-letter pattern is 23× the work
of a 3-letter one. Don't assume a long, mostly-literal pattern is the cheap case.

**Queries compile their pattern before returning the iterator.** `QueryAsync` is not itself
an iterator method — it validates, then returns a private one. Merging them would defer
every syntax error to the first `await foreach`, far from the call that caused it.

**Scores are not word frequency.** A crossword list rates how good an entry is as fill,
which is a different judgement from how often a word is written. The field is `Score` for
that reason; don't reintroduce `Frequency`.

**The lexicon carries an attribution obligation.** Both sources are permissively licensed
(Nediger MIT; ESDB permits derived word lists), but both notices must ship with anything
distributed. `words licence` and the MAUI/web About screens are release requirements, not
niceties ([ADR 0004](docs/adr/0004-scowl-nediger-lexicon.md)).

**`data/lexicon.gz` is generated but committed**, and embedded into `Words.Core` at build
time. Don't hand-edit it, and don't regenerate it casually — a rebuild from unchanged
inputs is byte-identical by design, so any diff means an input actually changed. The
manifest records each source's SHA-256.

**The load path is performance-sensitive and was tuned deliberately.** Cold start started
at 615ms against a 300ms budget. The ASCII fast path in `SearchKeys.From`, the buffer-then-
parse-synchronously shape of `LexiconArtefact.ReadAsync`, the lazily-built indexes, and the
absence of a sort in `Lexicon.LoadAsync` are all load-bearing. Re-measure with
`lexicon info` after touching any of them; tidying them into more obvious-looking code will
cost hundreds of milliseconds on every CLI invocation.

**The builder project is `Words.LexiconBuilding`, not `Words.Lexicon.Building`.** The
latter creates a `Words.Lexicon` namespace that shadows the `Lexicon` *type* from anywhere
inside `Words.*`, which breaks every CLI file that needs both. Don't "tidy" the name back.

## Conventions

[CONTEXT.md](CONTEXT.md) is the glossary and is authoritative for naming — `Entry` not
`Word`, `Lexicon` not `Dictionary`, `Match` not `Result`. Update it when a term is settled
rather than batching it up. ADRs live in `docs/adr/`; supersede rather than edit when a
decision changes, so the reasoning trail survives.
