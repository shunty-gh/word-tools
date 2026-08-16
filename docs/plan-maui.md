# Implementation plan — MAUI app

The desktop and mobile front end, over the same `Words.Core` the CLI uses. See
[plan-cli.md](./plan-cli.md) for the engine, and [CONTEXT.md](../CONTEXT.md) for vocabulary.

## Decisions

**Mac Catalyst and iOS.** Every platform listed in `TargetFrameworks` needs its SDK present
merely to *build*, so one nobody is working on breaks `dotnet build` for everyone and takes
CI with it. Add a platform only alongside its CI job. Android builds but is not enabled;
Windows cannot be built on macOS at all.

**The anagram index has been reworked; the entries have not.** Retained memory is **160 MB**
with both indexes built, down from 223 MB. The remaining 140 MB is the entries themselves.
See "Memory" below for what that would take and what it would buy.

**The web app will be API-backed**, not WebAssembly, so the engine runs on a server there.
That leaves mobile as the only memory-constrained target, and weakens the WebAssembly
rationale in [ADR 0002](./adr/0002-streaming-query-results.md) — though streaming still earns
its place through early exit and cancellation.

**No workload install was needed.** Mac Catalyst builds with the .NET 10 SDK as it stands.

## Structure

```
src/Words.Maui/
  Services/LexiconService.cs            loads the lexicon once, off the UI thread
  Services/AppDataPersonalWordStore.cs  personal words, same text format as the CLI
  ViewModels/SearchViewModel.cs         query, results, status
  MainPage.xaml                         the solver
  AboutPage.xaml                        licences — an obligation, not a courtesy
```

`MauiProgram` is the composition root, exactly as `Composition` is for the CLI: the engine
is handed its sources and never decides where entries come from.

Loading is started at startup rather than on first search, so it overlaps the first frame.
`LexiconService` hands out a single shared `Task<WordEngine>`, so a second caller arriving
mid-load waits rather than starting a second load.

The result list keeps the CLI's rule: when there are more answers than it will show, rank by
likelihood first and only then sort alphabetically, so the cap does not simply keep
everything beginning with A.

Two new dependencies, both standard for MAUI and source-generated rather than reflective:
`CommunityToolkit.Mvvm` for `[ObservableProperty]`/`[RelayCommand]`, and the MAUI packages
themselves. The MAUI project opts out of central package management, because its versions
come from `$(MauiVersion)`, which `Directory.Packages.props` cannot resolve.

## Memory

| Stage | Retained | Before the anagram rework |
| --- | ---: | ---: |
| Entries loaded | 140 MB | 140 MB |
| + length index (pattern queries) | 147 MB | 147 MB |
| + anagram index (anagram queries) | **160 MB** | 223 MB |

**The anagram index went from 77 MB to 14 MB.** Most of that 77 MB was never the keys: it
was 437,476 separate `Entry[]` arrays, the majority holding a single entry and each carrying
an object header, plus the `List<Entry>` used to build every one. It is now one array of
entries sorted by canonical form, with the canonical forms concatenated into a single `char`
buffer and found by binary search — see `AnagramIndex`.

The cost is roughly **double the per-lookup time**: 19 span comparisons instead of one hash.
Warm queries went from 211 ns to 256 ns for a single lookup, and from 146 µs to 251 µs for
the 3,276 lookups of three blanks. Against a 10 ms target that is not worth defending.

A packed letter-count key would be smaller again, but the lexicon contains an entry with
**sixteen of one letter** — "Buffalo buffalo Buffalo buffalo buffalo buffalo Buffalo
buffalo" — so no fixed bit width is obviously safe, and a collision would mean wrong answers
rather than slow ones.

### What remains

The entries themselves are still **140 MB**, which is 293 bytes each for an average of 11
characters of display form and 11 of search key. The cost is object overhead: each entry is
a heap object holding two more. Flattened into shared `char[]` buffers with offsets, the
same data is about **27 MB**, which would put the whole app near 60 MB.

That change is contained but not free. `Words.Cli` and `Words.Maui` never name `Entry` — the
only front-end contact is three member names read off `Match.Components` — and `SearchKey`
never escapes the engine. So the work lands in `Words.Core`, in the tests that construct
entries, and awkwardly on `ILexiconSource`, which hands over `IReadOnlyList<Entry>`: copying
those into flat arrays would make *peak* load memory worse unless that seam changes too, and
that seam exists to keep a remote source possible ([ADR 0006](./adr/0006-lexicon-loads-from-ordered-sources.md)).

**Mobile is the only memory-constrained target.** The web app is planned as an API-backed
service rather than WebAssembly, so `Words.Core` will run on a server where 160 MB is
unremarkable. An earlier version of this plan claimed the web app was the strongest argument
for flattening; that is no longer true, and the case now rests on iOS and Android alone.

**It cannot be settled on a simulator**, which runs on the Mac's RAM with no jetsam limits,
and there is no physical device to test on.

## Status

**The app works end to end**, driven and checked on screen: `c.t` in crossword mode returns
the same 9 answers as the CLI in 101 ms; `listen` in anagram mode returns the same 10,
including the phrase `lets in` and the possessive `inlet's`; the mode switch dims the
inactive side; About renders both licences and navigates back.

The first anagram of a session took 738 ms against the CLI's ~270 ms, because the lazy
anagram index is built on first use in a Debug build. Worth re-checking in Release before
treating it as a problem.

**Personal words and query options are done**, and verified on screen: adding
`quzzlewump` through the prompt, then searching for it, returns it. Options are behind a
self-rolled disclosure — order, only-my-words, include-rude, and the compose bounds, which
appear only while composing.

Adding a word calls `LexiconService.Invalidate()`, so the next search reloads and picks it
up. That search cost **1,593 ms** in Debug, being a full reload plus an anagram index
rebuild. Acceptable for an occasional action, but if adding words becomes routine it is the
obvious thing to make incremental — a single entry could be merged into the loaded lexicon
rather than discarding it.

Next, roughly in order:

1. Visual design. It is functional and plain.
2. Measure on an iOS device, then decide on the flattened store.
3. Add platforms one at a time, each with its CI runner.
4. Possibly: merge a personal word into the loaded lexicon instead of reloading.
