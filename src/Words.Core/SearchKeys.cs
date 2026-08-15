using System.Globalization;
using System.Text;

namespace Words.Core;

/// <summary>
/// Operations on search keys. Named in the plural so it does not collide with
/// <see cref="Entry.SearchKey"/>, which is the more important use of the term.
/// </summary>
public static class SearchKeys
{
    /// <summary>
    /// Derives the search key for a display form: uppercase A–Z only, with spaces,
    /// hyphens, apostrophes, digits and diacritics removed. <c>Red Herring</c> becomes
    /// <c>REDHERRING</c>; <c>naïve</c> becomes <c>NAIVE</c>.
    /// </summary>
    /// <remarks>
    /// Decomposing to <see cref="NormalizationForm.FormD"/> splits an accented letter into
    /// its base letter plus a combining mark, so dropping the marks leaves the base letter
    /// behind. Every accented character in the bundled word lists decomposes this way, so
    /// no additional fold table is needed. Anything that survives decomposition without
    /// becoming an ASCII letter — were a source ever to contain æ or ß — is dropped rather
    /// than guessed at, which would show up as a missing entry rather than a wrong match.
    /// </remarks>
    public static string From(string displayForm)
    {
        ArgumentNullException.ThrowIfNull(displayForm);

        var decomposed = displayForm.Normalize(NormalizationForm.FormD);
        var key = new StringBuilder(decomposed.Length);

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (c is >= 'A' and <= 'Z')
            {
                key.Append(c);
            }
            else if (c is >= 'a' and <= 'z')
            {
                key.Append((char)(c - ('a' - 'A')));
            }
        }

        return key.ToString();
    }

    /// <summary>
    /// The canonical form of a search key: its letters sorted. Two entries are anagrams of
    /// each other exactly when their canonical forms are equal.
    /// </summary>
    public static string ToCanonical(string searchKey)
    {
        ArgumentNullException.ThrowIfNull(searchKey);

        var letters = searchKey.ToCharArray();
        Array.Sort(letters);
        return new string(letters);
    }
}
