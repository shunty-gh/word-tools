# A restricted pattern language instead of regular expressions

Pattern queries use a deliberately small language — literal letters, `?` or `.` for exactly
one unknown letter, and `[abc]` / `[^def]` character classes — matched by hand-written
code rather than `System.Text.RegularExpressions`. The grammar is small enough that a
matcher is straightforward, it cannot backtrack catastrophically on hostile input, and
because a pattern's length fixes the answer's length exactly, matching can be narrowed
to entries of that length before any character is compared.

Full regular expressions were rejected: they would forfeit the length-bucketing
optimisation, admit expressions with no sensible meaning for a crossword grid, and
require a syntax error surface far larger than the problem deserves.

## `.` as a synonym for `?`

Added after using the solver in anger. Both spell "exactly one unknown letter", and the
reason for two is entirely practical: **`?` is a shell wildcard and `.` is not.** In zsh an
unquoted pattern containing `?` aborts the command before the program starts, so `?`
effectively requires quotes; a pattern of letters and dots can be typed bare. Since most
patterns are just letters and gaps, that removes quoting from the common case.

It costs one extra `case` label in the compiler, which was the bar set for accepting it.

## Consequences

`?` means something different here from its regular-expression meaning of "zero or
one" — it matches exactly one letter, the equivalent of regex `.`. This will surprise
anyone who assumes the pattern is a regex, so the language must never be described as
"regex-like" in help text, errors or documentation.

`.` cuts the other way: it *does* carry its regular-expression meaning, which makes the
language look more regex-like than it is. Expect users to try `.*`. That is rejected with a
message naming `*` as the problem, which is the right place for them to find out.

There is deliberately no `*` or other variable-length construct. One would directly
contradict the rule that pattern length fixes answer length, which is the property the
whole design leans on.
