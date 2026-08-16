namespace Words.Core;

/// <summary>
/// A compiled crossword pattern: literal letters, <c>?</c> or <c>.</c> for exactly one
/// unknown letter, and <c>[abc]</c> / <c>[^def]</c> character classes.
/// </summary>
/// <remarks>
/// Deliberately not a regular expression — see
/// <see href="../../docs/adr/0003-pattern-language-not-regex.md">ADR 0003</see>. Note in
/// particular that <c>?</c> means <em>exactly one letter</em>, the equivalent of regex
/// <c>.</c>, and not regex's "zero or one".
/// <para>
/// <c>.</c> is accepted as a synonym for <c>?</c> for a practical reason: it is not a shell
/// wildcard, so a pattern of letters and dots can be typed without quotes, whereas one
/// containing <c>?</c> cannot. It also happens to carry its regular-expression meaning
/// here, which <c>?</c> does not.
/// </para>
/// <para>
/// Every element compiles to a 26-bit mask of the letters it admits: a literal sets one
/// bit, <c>?</c> sets all of them, a class sets its own, and a negated class sets the rest.
/// Matching is then one bit test per position with no branching on element type, and the
/// pattern's element count fixes the answer's length exactly — which is why a pattern only
/// ever has to look at one length bucket of the lexicon.
/// </para>
/// </remarks>
public sealed class PatternMatcher
{
    private const uint AllLetters = (1u << 26) - 1;

    private readonly uint[] _masks;

    private PatternMatcher(uint[] masks) => _masks = masks;

    /// <summary>How many letters an answer must have to match.</summary>
    public int Length => _masks.Length;

    /// <summary>
    /// Compiles a pattern, case-insensitively.
    /// </summary>
    /// <exception cref="PatternSyntaxException">The pattern is empty or malformed.</exception>
    public static PatternMatcher Compile(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        if (pattern.Length == 0)
        {
            throw new PatternSyntaxException(pattern, 1, "The pattern is empty.");
        }

        var masks = new List<uint>(pattern.Length);
        var i = 0;

        while (i < pattern.Length)
        {
            var c = pattern[i];

            switch (c)
            {
                case '?':
                case '.':
                    masks.Add(AllLetters);
                    i++;
                    break;

                case '[':
                    masks.Add(CompileClass(pattern, ref i));
                    break;

                case ']':
                    throw new PatternSyntaxException(
                        pattern, i + 1, "']' has no matching '['.");

                default:
                    if (!IsLetter(c))
                    {
                        throw new PatternSyntaxException(
                            pattern,
                            i + 1,
                            $"'{c}' is not allowed in a pattern. Use letters, '?' or '.' for one unknown "
                            + "letter, or '[abc]' / '[^abc]' to choose between letters.");
                    }

                    masks.Add(BitFor(c));
                    i++;
                    break;
            }
        }

        return new PatternMatcher([.. masks]);
    }

    /// <summary>
    /// Compiles one <c>[...]</c> class, advancing <paramref name="i"/> past its closing
    /// bracket.
    /// </summary>
    private static uint CompileClass(string pattern, ref int i)
    {
        var open = i;
        var j = i + 1;
        var negated = false;

        if (j < pattern.Length && pattern[j] == '^')
        {
            negated = true;
            j++;
        }

        var mask = 0u;

        while (j < pattern.Length && pattern[j] != ']')
        {
            if (!IsLetter(pattern[j]))
            {
                throw new PatternSyntaxException(
                    pattern, j + 1, $"'{pattern[j]}' is not a letter, so it cannot appear inside '[...]'.");
            }

            mask |= BitFor(pattern[j]);
            j++;
        }

        if (j >= pattern.Length)
        {
            throw new PatternSyntaxException(pattern, open + 1, "'[' has no matching ']'.");
        }

        if (mask == 0)
        {
            throw new PatternSyntaxException(
                pattern, open + 1, "'[]' lists no letters, so nothing could match it.");
        }

        i = j + 1;
        return negated ? AllLetters & ~mask : mask;
    }

    /// <summary>
    /// Whether a search key matches. The key is assumed already normalised to uppercase
    /// A–Z, which <see cref="SearchKeys.From"/> guarantees.
    /// </summary>
    public bool Matches(string searchKey)
    {
        ArgumentNullException.ThrowIfNull(searchKey);

        if (searchKey.Length != _masks.Length)
        {
            return false;
        }

        for (var i = 0; i < _masks.Length; i++)
        {
            if ((_masks[i] & (1u << (searchKey[i] - 'A'))) == 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsLetter(char c) => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z');

    private static uint BitFor(char c) =>
        1u << ((c is >= 'a' and <= 'z' ? c - ('a' - 'A') : c) - 'A');
}
