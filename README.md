# words

A crossword and anagram solver for British puzzles.

One engine answers letter-shaped questions about a fixed body of English words. The
command line is the first front end; MAUI (macOS, Windows, Android, iOS) and a web app are
planned against the same engine.

> **Status: the command line is complete.** Next are the MAUI app and the web app, both
> built on the same engine. See [docs/plan-cli.md](docs/plan-cli.md).

## What it does

**Crossword solver.** Give it the letters you have and the gaps you don't:

```console
$ words pattern A.....R.E.T
autocorrect

$ words pattern RED.ERRING
red herring
```

A pattern's length fixes the answer's length exactly. **`.` and `?` both mean exactly one
letter** — note that this is **not** a regular expression, where `?` would mean "zero or
one". Character classes work too: `[aeiou]` for a vowel, `[^s]` for anything but an `s`.

Prefer `.`: it is not a shell wildcard, so it needs no quotes. `?` and `[abc]` are, so
quote those — in zsh an unquoted `A??D` aborts before the program even starts.

```console
$ words pattern "C[aeiou]T"
cat
cit
cot
cut
```

Grids have no spaces, so a phrase answer matches straight through it — which is why
`RED.ERRING` finds `red herring`.

**Anagram solver.** Give it your letters, using `.` or `?` for one you don't know yet:

```console
$ words anagram listen
elints
enlist
inlet's
inlets
Intel's
lets in
listen
silent
tinsel

$ words anagram trisec.
atresic
cistern
credits
cretins
...
```

Here `.` is a blank: an unknown letter with no fixed position. It is always used, so an
answer's length is the letters you gave plus the blanks — up to three. Case, accents,
spaces, hyphens and apostrophes in your input are all forgiven, so a phrase can be pasted
straight in.

**Multi-word answers** with `--compose`:

```console
$ words anagram notaproblem --compose
amble pronto
aplomb tenor
...
```

Composed answers are built from ordinary single words, never phrases or proper nouns, and
use two words by default (`--components 3` for three). Only the most likely 200 are shown,
unless you pass `--limit`.

## Options

Both solvers accept `--json`, `--limit n` (`0` for all), `--sort alpha|score|length`,
`--source esdb|nediger|personal` and `--include-racy`. Commands have short aliases: `pat`,
`anag`, `lex`.

Exit codes follow grep, so the solver scripts cleanly: **0** answers found, **1** none,
**2** something wrong with the request, **130** interrupted.

## Your own words

```console
$ words add "bletchley park"
$ words add jabberwock --score 40
```

Personal entries are merged into the lexicon on every query, and `--source personal` limits
results to them. The file is plain text you can edit by hand — one entry per line with an
optional `;score` — at `~/Library/Application Support/words/personal.txt` on macOS,
`~/.config/words/personal.txt` on Linux, `%APPDATA%\words\personal.txt` on Windows.

The same `?` therefore means two different things — a *cell* in a pattern, a *blank* in an
anagram. That is what solvers already type, so the character is shared even though the
concepts are not. [CONTEXT.md](CONTEXT.md) is the glossary.

> **Quoting.** `?` and `[abc]` are shell glob characters: in zsh an unquoted pattern fails
> before the program even starts, while in bash it silently works, so the same command
> behaves differently on different machines. Quote those, or use `.` and skip the quotes.

## Installing

**As a standalone binary** — needs nothing installed, not even .NET. Build it for your own
platform (NativeAOT cannot cross-compile, so this must run on the machine you want it for):

```bash
dotnet publish src/Words.Cli -c Release -r osx-arm64 -o out   # or linux-x64, win-x64
./out/words pattern RED.ERRING
```

**As a .NET tool** — needs the .NET SDK, but installs onto your PATH:

```bash
dotnet pack src/Words.Cli -c Release -o nupkg
dotnet tool install -g --add-source ./nupkg Shunty.Words
words pattern RED.ERRING
```

## Building

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/shunty-gh/word-tools.git
cd word-tools
dotnet build
dotnet test

dotnet run --project src/Words.Cli -- lexicon info
```

`lexicon info` reports what the lexicon contains and how long it takes to load.

## The lexicon

500,451 entries, merged from two word lists and committed to the repository, so builds need
no network and are reproducible.

| Source | Entries | Contributes |
| --- | ---: | --- |
| [ESDB](https://github.com/en-wl/wordlist) (formerly SCOWL), 2026.02.25 | 250,543 | systematic vocabulary, inflections, a frequency signal |
| [Nediger](https://codeberg.org/bewilderingly/Nediger-list), 2026-08-13 | 350,291 | phrases, proper nouns, crossword-shaped entries |

ESDB is generated for **both British spellings** — `-ise`/traditional and `-ize`/Oxford —
because `REALISE` and `REALIZE` are different letter multisets and a solver that knows only
one is simply wrong. 213,612 entries are phrases; 1,257 are flagged as potentially racy and
excluded by default.

To rebuild after changing anything in `data/sources/`:

```bash
dotnet run --project src/Words.Cli -- lexicon build data/sources -o data/lexicon.gz
```

A rebuild from unchanged inputs is byte-identical by design, so any diff means an input
really changed.

### Attribution

Both word lists are permissively licensed, and both notices must travel with anything
distributed. `words licence` prints them; the texts are embedded in the binary so it works
wherever the program does.

- **Nediger** — MIT, © 2026 bewilderingly. See `data/sources/nediger-LICENSE.txt`.
- **ESDB** — © 2000–2026 Kevin Atkinson, which permits distributing word lists created from
  it provided the notice is included. See `data/sources/esdb-COPYRIGHT.txt`.

## Layout

```
src/Words.Core/              the engine — model, lexicon, indexes, queries
src/Words.LexiconBuilding/   merges word lists into the artefact
src/Words.Cli/               the `words` executable
tests/                       xUnit, plus a BenchmarkDotNet project
data/sources/                the pinned word lists and their licences
data/lexicon.gz              the built artefact, committed
docs/adr/                    why things are the way they are
```

`Words.Core` has no console, file-system or UI dependencies, which is what lets the CLI,
MAUI and a web app share it unchanged.

## Design notes

The decisions worth knowing about are recorded as ADRs:

- [0002](docs/adr/0002-streaming-query-results.md) — queries stream results rather than
  returning a list
- [0003](docs/adr/0003-pattern-language-not-regex.md) — a restricted pattern language, not
  regular expressions
- [0004](docs/adr/0004-scowl-nediger-lexicon.md) — why these two word lists
- [0005](docs/adr/0005-clue-databases-deferred.md) — why clue databases are out of scope
- [0006](docs/adr/0006-lexicon-loads-from-ordered-sources.md) — why the lexicon loads from
  an ordered set of sources
