# Implementation plan — MAUI app

The desktop and mobile front end, over the same `Words.Core` the CLI uses. See
[plan-cli.md](./plan-cli.md) for the engine, and [CONTEXT.md](../CONTEXT.md) for vocabulary.

**[UI.md](../UI.md) governs the interface.** Usability first: platform conventions, standard
controls, obvious affordances, colour that carries no meaning on its own. Read it before
designing a screen.

## Decisions

**Mac Catalyst, iOS and Android.** Every platform listed in `TargetFrameworks` needs its
workload present merely to *build*, so one nobody is working on breaks `dotnet build` for
everyone and takes CI with it. Add a platform only alongside its CI job. Windows cannot be
built on macOS at all, which is the only reason it is absent.

Android shares the macOS runner rather than earning a Linux one: restore spans every
framework the project lists whichever one is being built, so each leg installs the same
workloads regardless. Splitting Android onto Linux would buy nothing until the project stops
multi-targeting.

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
  Services/LookupSettings.cs            the chosen web search engine, remembered
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

## Visual design

Governed by [UI.md](../UI.md), which outranks any general advice about being distinctive.

**Ink on newsprint.** A crossword is a printed thing filled in by hand, and the single accent
is the blue of the pen you fill it in with. The palette is otherwise monochrome, so nothing
depends on hue: the primary action is the only *filled* button, and answer tags are words
rather than colours. The interface reads in black and white, as UI.md requires.

**The cell strip is the one bold idea.** The query is drawn beneath the input as squares of
the grid — letters you have filled, gaps you do not empty. It earns its place by doing a job:
it shows how long the answer will be, which otherwise has to be counted by eye. It is
read-only and flat, because static things must not look clickable. Everything around it is
deliberately quiet, and there is no animation anywhere.

**Answer tags.** A row carries a short word — `yours`, `name`, `phrase` — when there is
something worth knowing at a glance while filling a grid. Untagged answers are ordinary
single words, which is most of them.

**Looking an answer up.** A grid answer is often a word the solver has never met, so each row
carries two links — `Define` and `Synonyms` — that hand the word to the platform's default
browser as a web search. They are small bordered buttons rather than coloured text: a link
that looks like prose is a link nobody presses, and colour must not be the only signal
(UI.md). They are 36 points tall rather than the page's 44, because at 44 every row of a
five-hundred-row list would be a paragraph high.

The engine is chosen once, under Options, and remembered: Google by default, with Bing,
DuckDuckGo, Brave, Ecosia and Yahoo offered. That list is short on purpose — each extra entry
is a decision asked of someone who came to solve a crossword. The URL building lives in
`Words.Core.WordLookup`, for the same reason `MatchOrdering` does: the web front end will need
it too, and two copies of a list of URLs is how one of them quietly rots.

Nothing here makes the app networked. It composes a URL and hands it over; the browser fetches
under its own permissions, and the Android manifest still asks for no permission at all. It
does now declare `<queries>` for `VIEW`/`https`, which is package *visibility* rather than a
permission — since Android 11 an app cannot even ask whether a browser exists without it, and
that check is what `Launcher` makes before giving up.

UI.md changed three things that were already built: Search is now the only filled button
where all four had been identical; every button is at least 44 points tall; and the empty
message was cut back, because the placeholder already teaches the syntax and each element
should do one job.

Colours live in `Resources/Styles/Colors.xaml` under role names; the styling is in
`Design.xaml`, merged after the template's `Styles.xaml` so it wins.

**The icon is Pattern Lens** — a search lens holding a three-by-three grid with one cell
filled in the accent blue, its handle resolving into a fountain-pen nib. Grid, search, and
the pen you fill it in with, which is the same idea the palette rests on. Concept boards and
the reasoning are in [docs/icon-concepts](./icon-concepts/README.md); the production sources
are clean SVG in `Resources/AppIcon` and `Resources/Splash`, using only the primitives MAUI's
resizetizer supports. Checked on a phone home screen: nothing is clipped by the icon mask,
and it reads at that size against Apple's own icons.

**The app's identity was still the template's** and is now settled: `ApplicationTitle` was
`Words.Maui`, which is what the home screen displayed, and `ApplicationId` was
`com.companyname.words.maui`. They are now `Words` and `uk.co.shunty.words`. Worth doing
before distribution rather than after — the identifier is how the OS and any store know the
app, and it also determines where per-app data lives, so changing it later orphans a saved
personal word list.

**No display typeface is bundled** — that is a repo asset with licensing implications, and
this project has been careful about exactly that. Say if you want one and I will check terms
first.

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

**Android is enabled and verified on screen**, on a Pixel 10 emulator: `c.t` returns the same
9 answers in 169 ms and `listen` the same 10, `lets in` and `inlet's` included; the cell strip,
the mode switch, the tags and About all render, and the system back gesture leaves About.
The first anagram of a session cost 2,124 ms, then 1,150 ms on a warmer run — the same lazy
index build seen on Mac Catalyst, exaggerated by an emulator and a Debug build.

Two things Android surfaced, both now fixed:

- **The placeholder was cut off** at phone width — `Your letters, with . for each one you
  don't know` lost `know` entirely, with no ellipsis, so the one element meant to teach the
  syntax trailed off mid-sentence. Both placeholders are now short enough to fit, which cost
  the `RED.ERRING` example. They are shared copy, so the Apple targets changed too.
- **The template asked for `INTERNET` and `ACCESS_NETWORK_STATE`**, which an entirely offline
  app cannot justify on an install screen. Both are gone; the Release manifest now requests no
  permissions at all. Debug still shows `INTERNET`, injected by the debug build for the
  debugger, not by the manifest.

**The lookup links have not been seen on screen.** They were written in an environment with no
.NET SDK, so nothing in the app was compiled or run. The URL building is covered by unit tests
in `Words.Core.Tests`; the app side — the two buttons per row, their ancestor-bound commands,
the engine picker and the saved preference — needs a build and a look on Mac Catalyst and
Android before it counts as done. Worth checking there specifically: that a row still reads at
phone width with two buttons on it, and that `Define` on `inlet's` and on `naïve` reaches the
browser with the word intact.

Next, roughly in order:

1. Build and check the lookup links on Mac Catalyst and Android, per the note above.
2. Measure on an iOS device, then decide on the flattened store. Android now has the same
   question, and an emulator cannot answer it either — it runs on the Mac's RAM.
3. Windows, if it is wanted, which needs a Windows runner and cannot be built on macOS.
4. Possibly: merge a personal word into the loaded lexicon instead of reloading.
