# Clue databases are out of scope for now

Two large cryptic crossword clue databases were evaluated alongside the word lists and
deliberately not adopted. They are not lexicons — they map clues to answers — so they
would add a new feature (look up how a clue has been used before, and eventually
wordplay hints) rather than improve the solver we are building.

`cryptics.georgeho.org` is the attractive one: over 500,000 clues, weighted towards
British publications — Telegraph, FT, Guardian, Independent, Times — with wordplay
indicators and charades alongside. It is also the only data source considered here with
an unambiguous licence.

That licence is the reason to decide deliberately rather than drift into it. The data is
**ODbL 1.0**, which is share-alike: a database derived from it inherits obligations. Mixing
it into the lexicon by accident would entangle the whole artefact, so if clue search is
built, its data stays a separate database with its own terms.

`xd.saul.pw` is rejected outright rather than deferred — its corpus is pre-1965 New York
Times puzzles, it states no licence, and the underlying puzzles are copyrighted works.
