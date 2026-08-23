# Apache 2.0 for the code, and only the code

The project is released under the **Apache License, Version 2.0**. `LICENSE` at the repo
root is a verbatim copy of the canonical text; `NOTICE` beside it records the copyright and
the boundary this decision draws.

Apache over MIT for three things MIT does not give. An **express patent grant with
termination** (§3), which matters for something published to NuGet and heading for app
stores, where MIT offers an implicit grant at best. **Inbound = outbound** (§5), so
contributions arrive under the project's own terms without a contributor licence agreement.
And a **NOTICE mechanism** (§4(d)) that already had a job here: both word lists oblige their
notices to travel with anything distributed, and Apache gives that a conventional home
rather than an invented one.

Apache over MPL 2.0 because MPL's file-level copyleft is an obligation on other people —
publish your modifications to these files — bought for no benefit this project can name.
Nobody closing a fork of a crossword solver harms it. MPL would also complicate the two
distribution routes actually planned: an app store binary owes a source-availability notice
for the Executable Form, and `Words.Core` is meant to be referenced by front ends, which
copyleft makes a question rather than a given.

## The licence stops at the code

The lexicon is **not ours to license**. ESDB and the Nediger list are third-party works,
permissively licensed but still theirs, and no licence this project chooses can relicense
them ([ADR 0004](./0004-scowl-nediger-lexicon.md)).

That includes the generated artefact. `data/lexicon.gz`, and the copy embedded in
`Words.Core`, are word lists *derived* from those sources — derivation both sources permit,
on the condition that their notices come along. A bare `LICENSE` at the root, read as
covering everything in the repository, would therefore be a claim the project cannot make,
which is why the scope is stated in `NOTICE` rather than left to inference.

`LICENSE` itself is kept byte-for-byte canonical, placeholder appendix and all, so GitHub,
NuGet and SPDX tooling detect it; the project's actual copyright lives in `NOTICE`, in the
assembly metadata via `Copyright` in `Directory.Build.props`, and in
`PackageLicenseExpression` on the CLI package. The file is spelled `LICENSE` against the
project's British usage everywhere else because that is the spelling those tools look for.

## Consequences

Three licence texts now have to reach whoever runs the program, not merely sit in the
repository: Apache §4(a) requires a copy of the licence to accompany what is distributed,
and both word lists require their notices to. A self-contained NativeAOT binary and an app
store package have nothing beside them, so all three are **embedded in `Words.Core`** and
surfaced by `words licence` and the About screen. Those were already release requirements
for the word lists; they now carry the program's own terms too, which is why the licence
text lives in the engine rather than in any one front end — every front end owes the same
obligation.

`LexiconLicence` and `LexiconLicences` were renamed to `Licence` and `Licences` at the same
time. The record was never lexicon-specific, and a type called `LexiconLicences` holding the
program's own terms would have been actively misleading. The word lists stay together under
`Licences.WordLists`; the program's own terms are `Licences.Program`.
