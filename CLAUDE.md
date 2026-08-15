# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A crossword and anagram solver. A shared engine answers letter-shaped questions about a
fixed body of English words; the CLI is the first front end, with MAUI (macOS, Windows,
Android, iOS) and a web app planned against the same engine.

Work is phased and the plan is [docs/plan-cli.md](docs/plan-cli.md). **Phase 0 (skeleton)
is complete; phase 1 onwards is not started** — `Words.Core` is currently empty and the
CLI is a stub. Check the plan before assuming a type exists.

## Commands

```bash
dotnet build
dotnet test
dotnet test --filter "FullyQualifiedName~PatternMatcherTests"   # one class
dotnet test --filter "DisplayName~matches phrase entries"       # one test

dotnet run --project src/Words.Cli -- pattern "A??D??R?E?T"     # note the quotes
dotnet run -c Release --project tests/Words.Core.Benchmarks     # phase 7 onwards
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
"zero or one". The pattern language is literals, `?`, and `[abc]` / `[^def]`, deliberately
not a regular expression ([ADR 0003](docs/adr/0003-pattern-language-not-regex.md)). Never
describe it as regex-like in help text or errors.

**`?` means something different in each query kind.** A *cell* in a pattern (unknown
letter, known position); a *blank* in an anagram (unknown letter, no position). See
[CONTEXT.md](CONTEXT.md).

**`?` and `[abc]` are shell glob characters.** zsh aborts an unmatched glob before the app
starts, while bash passes it through — so unquoted patterns fail on macOS and work on
Linux. Always quote patterns in examples and docs.

**Scores are not word frequency.** A crossword list rates how good an entry is as fill,
which is a different judgement from how often a word is written. The field is `Score` for
that reason; don't reintroduce `Frequency`.

**Lexicon licensing is unresolved.** The app is in-house only. Nediger's terms are
unverified and must be cleared before any distribution, particularly an App Store
submission ([ADR 0004](docs/adr/0004-scowl-nediger-lexicon.md)). Don't add distribution
tooling that implies the question is settled.

## Conventions

[CONTEXT.md](CONTEXT.md) is the glossary and is authoritative for naming — `Entry` not
`Word`, `Lexicon` not `Dictionary`, `Match` not `Result`. Update it when a term is settled
rather than batching it up. ADRs live in `docs/adr/`; supersede rather than edit when a
decision changes, so the reasoning trail survives.
