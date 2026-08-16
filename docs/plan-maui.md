# Implementation plan — MAUI app

The desktop and mobile front end, over the same `Words.Core` the CLI uses. See
[plan-cli.md](./plan-cli.md) for the engine, and [CONTEXT.md](../CONTEXT.md) for vocabulary.

## Decisions

**Mac Catalyst first.** The project targets `net10.0-maccatalyst` and nothing else. Every
additional platform needs its SDK present merely to *build* — Android wants the Android SDK,
iOS wants Xcode, Windows wants Windows — so listing a platform nobody is working on breaks
`dotnet build` for everyone and takes CI with it. Add a platform when work on it starts, and
give it a CI runner at the same time.

**The storage rework is deferred, not forgotten.** The lexicon retains **223 MB** with both
indexes built (140 MB entries, 7 MB length index, 77 MB anagram index), and the working set
is 225 MB. That is fine on desktop and questionable on a phone. Deferred because the honest
next step is a measurement on a real device, which needs an app to exist first — and because
the fix can stay internal: `Entry` can keep its current property surface while slicing
shared `char[]` buffers behind it. See "Memory" below.

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

The number that decides whether the current design survives on iOS:

| Stage | Retained |
| --- | ---: |
| Entries loaded | 140 MB |
| + length index (pattern queries) | 147 MB |
| + anagram index (anagram queries) | 223 MB |

140 MB across 500,451 entries is 293 bytes each, for an average of 11 characters of display
form and 11 of search key. The cost is object overhead: each entry is a heap object holding
two further heap objects. Flattened into shared `char[]` buffers with offsets, the same data
is about **27 MB**.

The lazy anagram index already helps — a session that only ever runs pattern queries holds
147 MB, not 223 MB.

**Next step is a measurement on a real device, not a rewrite.** Desktop tolerates this
comfortably; the question is what iOS does under memory pressure.

## Platforms

Mac Catalyst is the only one in `TargetFrameworks`. What each of the others needs:

| Platform | State |
| --- | --- |
| Mac Catalyst | Working, and the only one in `TargetFrameworks`. No workload install was needed. |
| iOS | **Verified building.** Xcode with the iOS 26.5 SDK. One line to enable. |
| Android | **Verified building**, once API 36 was installed — Android Studio's SDK had only `android-36.1`, and .NET looks for `android-36`. |
| Windows | Cannot be built on macOS at all. Needs a Windows machine or a CI runner. |

Only Mac Catalyst is enabled, because every extra target is built on each full solution
build and the UI loop is tight. Re-enabling either is a one-line change; add its CI runner
at the same time.

## No tab bar

Shell's `TabBar` was removed. Its labels rendered unreadably small on Mac Catalyst and
**no UIKit appearance API would touch them** — `UITabBarItem.Appearance` (ignored since
iOS 15 whenever an appearance object is in use), `UITabBarAppearance`, and
`UISegmentedControl.Appearance` were each tried against a clean build and each did nothing.
MAUI draws that control itself on Catalyst.

For a two-page app the tab bar was heavyweight regardless, so About is now reached from an
ordinary `Button` on the page, which we style like anything else. Don't reintroduce Shell
tabs expecting to control their type size.

The nav bar is hidden too (`Shell.NavBarIsVisible="False"`): it repeated the window title.

The template's default font size is 14 throughout `Resources/Styles/Styles.xaml`, which
reads small on a Mac; raised to 16, with the search field and results larger still.

## Text substitution

macOS replaces a typed `...` with a single `…` (U+2026) as you type, which reached the
parser as an unknown character and produced an error where the user had done nothing wrong.

Fixed in two places, deliberately:

- `PatternMatcher` and `AnagramLetters` read `…` as three unknown letters. This is the
  robust fix: it also covers pasted text and the CLI, where the same substitution can arrive
  from anywhere.
- The MAUI `Entry` sets `IsSpellCheckEnabled` and `IsTextPredictionEnabled` to false, so the
  field shows what was actually typed rather than silently rewriting it.

## Status

**The app works end to end**, driven and checked on screen: `c.t` in crossword mode returns
the same 9 answers as the CLI in 101 ms; `listen` in anagram mode returns the same 10,
including the phrase `lets in` and the possessive `inlet's`; the mode switch dims the
inactive side; About renders both licences and navigates back.

The first anagram of a session took 738 ms against the CLI's ~270 ms, because the lazy
anagram index is built on first use in a Debug build. Worth re-checking in Release before
treating it as a problem.

Next, roughly in order:

1. Personal words: an "add" affordance, matching `words add`.
2. Options — sources, racy, limit, sort, compose depth — only compose is exposed so far.
3. Visual design. It is functional and plain.
4. Measure on an iOS device, then decide on the flattened store.
5. Add platforms one at a time, each with its CI runner.
