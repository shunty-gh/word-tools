# The lexicon loads from an ordered set of sources

The engine builds its lexicon from an ordered collection of `ILexiconSource`, not from a
single artefact — even though today that collection holds only the built-in artefact and
the user's personal additions. Sources are merged in order, and an entry's provenance is
retained, so later sources can add entries and override display forms without any source
knowing about the others.

This exists to keep one option open. If the app is ever used on more than a couple of
machines, personal words need to sync, and the intended answer is a small internet API
rather than a file-sync scheme. When that happens the API becomes another source in the
collection and nothing else changes. Collapsing the collection to a single artefact —
which will look like an obvious simplification, since it will only ever hold two entries
for a long time — would turn that into a rewrite of the loading path.

## Consequences

The composition root, not the engine, decides which sources exist. `Words.Core` never
constructs a source or touches the file system, and personal words are read and written
through an abstraction rather than direct file I/O, so a remote store can replace a local
file without changing `Words.Core`.

The artefact manifest — source names, versions, generator options and entry counts —
becomes part of the contract rather than a build log, because a remote source needs a
version to know when its cached copy is stale.

Deliberately not built now: no HTTP, no authentication, no transport DTOs, no API
project. The seam is the whole investment.

Note that queries returning `IAsyncEnumerable<Match>`
([ADR 0002](./0002-streaming-query-results.md)) also leave the door open to running
searches *server-side* later, since that is already the shape of a streamed response. That
is a consequence of the streaming decision, not a goal of this one.
