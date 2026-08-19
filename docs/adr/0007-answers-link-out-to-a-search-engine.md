# Answers link out to a web search engine, rather than carrying definitions

A solver who has found `RED HERRING` often wants one of two things next: what it means, or
what else means the same. The lexicon holds neither. Each answer row therefore carries two
links — **Define** and **Synonyms** — which hand a search address to the platform's browser
and leave the app.

## Why not hold the data

Bundling definitions was the obvious alternative and was rejected on the same grounds that
shaped [ADR 0004](./0004-scowl-nediger-lexicon.md) and
[ADR 0005](./0005-clue-databases-deferred.md).

A dictionary is a different artefact from a word list, an order of magnitude larger, and the
freely licensed ones each bring their own attribution or share-alike terms to reason about.
The lexicon is 500,451 entries in 14 MB precisely because it holds only what matching needs —
a display form, a search key and a score. Definitions and thesaurus data would dwarf it, on a
target where memory is already the open question (see [plan-maui.md](../plan-maui.md)), to
serve a step that happens after the app's actual job is finished.

Calling a dictionary API was rejected too. It would make an offline app an online one, need a
key and a funded account, and turn a feature that costs nothing into one with a running bill
and an outage mode. The Android manifest asks for no permissions at all, and that stays true:
the browser does the network, under its own permission, and the app opens no socket.

## Why the user chooses the engine

The platforms do not expose the browser's own default search engine to an app, so an app that
wants to search the web must name one. Fixing it on Google would send every solver's answers
through a service some of them have deliberately left.

The engine is therefore a setting: Google by default, with Bing, Brave, DuckDuckGo, Ecosia,
Startpage and Yahoo alongside. It is stored by **name** rather than by position in the list,
so adding or reordering engines cannot silently change a saved choice, and a name we no
longer offer falls back to the default rather than leaving the answers without links.

This is the one kind of preference [UI.md](../../UI.md) permits without argument — harmless
personalisation of the user's own tools — rather than a decision the design declined to make.
It lives under Options, which is collapsed by default, because nobody needs to touch it to
solve a clue.

## Consequences

`WebSearchEngine` lives in `Words.Core`, next to `MatchOrdering` and for the same reason:
every front end that shows answers wants the same links, and the alternative is each of them
keeping its own copy of these URLs. It is pure string work — nothing in `Words.Core` opens a
connection, and the console, file-system and UI constraint on that project still holds.

A **composition** gets no links. It is several unrelated entries that happen to use the right
letters, so it has no definition and no synonyms; showing links that lead nowhere useful would
be worse than showing none.

Searches are phrased the way a person would type them — `define red herring`,
`red herring synonyms` — because that is what every one of these engines is tuned to answer
with a dictionary or thesaurus card. They use the **display form**, not the search key:
someone looking up `naïve` means that word, not `NAIVE`.

On Android 11 and later the manifest needs a `<queries>` element declaring an `ACTION_VIEW`
intent for `https`. It is not a permission and appears nowhere on the install screen, but
without it the app cannot see that a browser exists and the links quietly do nothing.

The CLI is unchanged. It writes answers to a stream that is as likely to be piped into
another program as read by a person, and a solver at a terminal already has a browser.
