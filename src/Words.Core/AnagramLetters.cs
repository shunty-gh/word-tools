using System.Globalization;
using System.Text;

namespace Words.Core;

/// <summary>
/// The letters supplied for an anagram, plus however many are still unknown.
/// </summary>
/// <remarks>
/// An answer uses every letter given and every blank, so its length is always
/// <see cref="Length"/> — there is no "subset of these letters" reading.
/// </remarks>
public sealed class AnagramLetters
{
    /// <summary>
    /// The most blanks a query may have. Three costs 3,276 index lookups, which is still
    /// well under a millisecond; the limit exists to keep the result list readable, not
    /// because more would be slow.
    /// </summary>
    public const int MaxBlanks = 3;

    private AnagramLetters(string letters, int blanks)
    {
        Letters = letters;
        Blanks = blanks;
    }

    /// <summary>The known letters, uppercase and sorted.</summary>
    public string Letters { get; }

    /// <summary>How many letters are unknown.</summary>
    public int Blanks { get; }

    /// <summary>The length every answer must have.</summary>
    public int Length => Letters.Length + Blanks;

    /// <summary>
    /// Reads user input into letters and blanks.
    /// </summary>
    /// <remarks>
    /// Forgiving about what people paste: case is folded, accents are reduced to their base
    /// letter, and spaces, hyphens and apostrophes are ignored so a phrase can be pasted
    /// straight in. <c>?</c> and <c>.</c> both mean a blank — <c>.</c> because it is not a
    /// shell wildcard and so needs no quoting.
    /// </remarks>
    /// <exception cref="QuerySyntaxException">
    /// The input holds a character that is neither a letter, a blank nor ignorable
    /// punctuation; or it has more than <see cref="MaxBlanks"/> blanks; or it is empty.
    /// </exception>
    public static AnagramLetters Parse(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var letters = new List<char>(input.Length);
        var blanks = 0;

        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];

            switch (c)
            {
                case '?' or '.':
                    if (++blanks > MaxBlanks)
                    {
                        throw new QuerySyntaxException(
                            input,
                            i + 1,
                            $"At most {MaxBlanks} unknown letters are allowed, and this is number {blanks}.");
                    }

                    break;

                // Ignored so a phrase or a hyphenated word can be pasted in as it stands.
                case ' ' or '-' or '\'' or '’':
                    break;

                default:
                    letters.Add(ToLetter(input, i, c));
                    break;
            }
        }

        if (letters.Count == 0 && blanks == 0)
        {
            throw new QuerySyntaxException(input, 1, "No letters were given.");
        }

        var sorted = letters.ToArray();
        Array.Sort(sorted);

        return new AnagramLetters(new string(sorted), blanks);
    }

    /// <summary>
    /// Every canonical form an answer could have: one when there are no blanks, and one per
    /// combination of blank letters otherwise — 26, 351 and 3,276 for one, two and three.
    /// </summary>
    /// <remarks>
    /// Blank letters are enumerated as combinations *with repetition* in non-decreasing
    /// order, so <c>AB</c> and <c>BA</c> are not both produced. Every combination yields a
    /// distinct canonical form, so no entry can be returned twice.
    /// </remarks>
    public IEnumerable<string> CanonicalForms()
    {
        if (Blanks == 0)
        {
            yield return Letters;
            yield break;
        }

        var chosen = new int[Blanks];
        var key = new char[Length];

        while (true)
        {
            Letters.CopyTo(0, key, 0, Letters.Length);

            for (var i = 0; i < Blanks; i++)
            {
                key[Letters.Length + i] = (char)('A' + chosen[i]);
            }

            Array.Sort(key);
            yield return new string(key);

            // Advance to the next non-decreasing combination.
            var position = Blanks - 1;
            while (position >= 0 && chosen[position] == 25)
            {
                position--;
            }

            if (position < 0)
            {
                yield break;
            }

            chosen[position]++;

            for (var rest = position + 1; rest < Blanks; rest++)
            {
                chosen[rest] = chosen[position];
            }
        }
    }

    /// <summary>Folds one input character to an uppercase A–Z letter, or rejects it.</summary>
    private static char ToLetter(string input, int index, char c)
    {
        if (c is >= 'A' and <= 'Z')
        {
            return c;
        }

        if (c is >= 'a' and <= 'z')
        {
            return (char)(c - ('a' - 'A'));
        }

        // Decompose just this character so an accented letter keeps its position in the
        // input, which the error message below depends on.
        if (char.IsLetter(c))
        {
            foreach (var part in c.ToString().Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(part) is UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (part is >= 'A' and <= 'Z')
                {
                    return part;
                }

                if (part is >= 'a' and <= 'z')
                {
                    return (char)(part - ('a' - 'A'));
                }
            }
        }

        throw new QuerySyntaxException(
            input,
            index + 1,
            $"'{c}' is not a letter. Use letters, and '?' or '.' for a letter you do not know.");
    }
}
