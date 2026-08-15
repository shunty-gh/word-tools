# A restricted pattern language instead of regular expressions

Pattern queries use a deliberately small language — literal letters, `?` for exactly
one unknown letter, and `[abc]` / `[^def]` character classes — matched by hand-written
code rather than `System.Text.RegularExpressions`. The grammar is small enough that a
matcher is straightforward, it cannot backtrack catastrophically on hostile input, and
because a pattern's length fixes the answer's length exactly, matching can be narrowed
to entries of that length before any character is compared.

Full regular expressions were rejected: they would forfeit the length-bucketing
optimisation, admit expressions with no sensible meaning for a crossword grid, and
require a syntax error surface far larger than the problem deserves.

## Consequences

`?` means something different here from its regular-expression meaning of "zero or
one" — it matches exactly one letter, the equivalent of regex `.`. This will surprise
anyone who assumes the pattern is a regex, so the language must never be described as
"regex-like" in help text, errors or documentation.

There is deliberately no `*` or other variable-length construct. One would directly
contradict the rule that pattern length fixes answer length, which is the property the
whole design leans on.
